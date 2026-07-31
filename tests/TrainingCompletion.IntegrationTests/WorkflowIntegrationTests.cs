using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TrainingCompletion.Application;
using TrainingCompletion.Domain;
using TrainingCompletion.Infrastructure.Consumers;
using TrainingCompletion.Infrastructure.Services;

namespace TrainingCompletion.IntegrationTests;

public sealed class WorkflowIntegrationTests
{
    [Fact]
    public void DatabaseGuard_RejectsDevelopmentDatabase()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TestDatabase.EnsureSafe(
                "Host=localhost;Database=training_dev;Username=test;Password=test"));
    }

    [PostgresFact]
    public async Task CannotEnrollTwice()
    {
        await using var dbContext = await TestDatabase.CreateResetAsync();
        var service = new CourseService(dbContext, new FixedClock());
        await service.EnrollAsync("course-2001", "learner-1001", default);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.EnrollAsync("course-2001", "learner-1001", default));

        Assert.Equal("duplicate_enrollment", exception.ErrorCode);
    }

    [PostgresFact]
    public async Task CourseListDoesNotTreatMissingEnrollmentAsEnrolled()
    {
        await using var dbContext = await TestDatabase.CreateResetAsync();
        var courses = await new CourseService(dbContext, new FixedClock())
            .GetCoursesAsync("learner-1001", default);

        Assert.All(courses, course => Assert.Null(course.EnrollmentStatus));
    }

    [PostgresFact]
    public async Task CannotExceedCourseCapacity()
    {
        await using var dbContext = await TestDatabase.CreateResetAsync();
        dbContext.Learners.Add(new Learner
        {
            Id = "learner-1002",
            DisplayName = "Second Learner",
            IsActive = true
        });
        var course = await dbContext.Courses.SingleAsync(x => x.Id == "course-2001");
        course.Capacity = 1;
        await dbContext.SaveChangesAsync();
        var service = new CourseService(dbContext, new FixedClock());
        await service.EnrollAsync("course-2001", "learner-1001", default);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.EnrollAsync("course-2001", "learner-1002", default));

        Assert.Equal("course_full", exception.ErrorCode);
    }

    [PostgresFact]
    public async Task CannotCompleteWithoutEnrollment()
    {
        await using var dbContext = await TestDatabase.CreateResetAsync();
        var service = new CompletionService(dbContext, new FixedClock());

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(
                "learner-1001",
                "course-2001",
                "missing-enrollment",
                "correlation-test",
                default));

        Assert.Equal("enrollment_required", exception.ErrorCode);
        Assert.Empty(await dbContext.OutboxMessages.ToListAsync());
    }

    [PostgresFact]
    public async Task CompletionAndOutboxRollbackTogether()
    {
        await using var dbContext = await TestDatabase.CreateResetAsync();
        await using (var transaction = await dbContext.Database.BeginTransactionAsync())
        {
            var completionId = Guid.CreateVersion7();
            dbContext.CourseCompletions.Add(new CourseCompletion
            {
                Id = completionId,
                LearnerId = "learner-1001",
                CourseId = "course-2001",
                CompletedAt = new FixedClock().UtcNow,
                CertificateStatus = CertificateStatus.Pending,
                ReportingStatus = WorkflowStatus.Pending,
                NotificationStatus = WorkflowStatus.Pending
            });
            dbContext.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.CreateVersion7(),
                EventType = nameof(CourseCompletedEvent),
                Payload = "{}",
                CreatedAt = new FixedClock().UtcNow
            });
            await dbContext.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        dbContext.ChangeTracker.Clear();
        Assert.Empty(await dbContext.CourseCompletions.ToListAsync());
        Assert.Empty(await dbContext.OutboxMessages.ToListAsync());
    }

    [PostgresFact]
    public async Task CompletionAndOutboxAreCreatedOnceForIdempotentReplay()
    {
        await using var dbContext = await TestDatabase.CreateResetAsync();
        var clock = new FixedClock();
        await new CourseService(dbContext, clock)
            .EnrollAsync("course-2001", "learner-1001", default);
        var service = new CompletionService(dbContext, clock);

        var created = await service.CreateAsync(
            "learner-1001",
            "course-2001",
            "stable-key",
            "correlation-test",
            default);
        var replayed = await service.CreateAsync(
            "learner-1001",
            "course-2001",
            "stable-key",
            "correlation-test",
            default);

        Assert.False(created.Replayed);
        Assert.True(replayed.Replayed);
        Assert.Equal(created.Completion.CompletionId, replayed.Completion.CompletionId);
        Assert.Equal(1, await dbContext.CourseCompletions.CountAsync());
        Assert.Equal(1, await dbContext.OutboxMessages.CountAsync());
    }

    [PostgresFact]
    public async Task StatusReturnsCurrentWorkflowAndCertificateIsNotReady()
    {
        await using var dbContext = await TestDatabase.CreateResetAsync();
        var clock = new FixedClock();
        await new CourseService(dbContext, clock)
            .EnrollAsync("course-2001", "learner-1001", default);
        var created = await new CompletionService(dbContext, clock)
            .CreateAsync(
                "learner-1001",
                "course-2001",
                "status-key",
                "correlation-test",
                default);

        var status = await new CompletionService(dbContext, clock)
            .GetAsync(created.Completion.CompletionId, default);
        var downloadService = new CertificateDownloadService(dbContext, new NeverCalledStore());
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            downloadService.DownloadAsync(created.Completion.CompletionId, default));

        Assert.Equal(CertificateStatus.Pending, status.CertificateStatus);
        Assert.Equal("certificate_not_ready", exception.ErrorCode);
    }

    [PostgresFact]
    public async Task DuplicateConsumersDoNotRepeatBusinessEffects()
    {
        await using var dbContext = await TestDatabase.CreateResetAsync();
        var clock = new FixedClock();
        await new CourseService(dbContext, clock)
            .EnrollAsync("course-2001", "learner-1001", default);
        await new CompletionService(dbContext, clock)
            .CreateAsync(
                "learner-1001",
                "course-2001",
                "handler-key",
                "correlation-test",
                default);
        var outbox = await dbContext.OutboxMessages.AsNoTracking().SingleAsync();
        var message = JsonSerializer.Deserialize<CourseCompletedEvent>(outbox.Payload)!;
        var notification = new NotificationHandler(
            dbContext,
            clock,
            NullLogger<NotificationHandler>.Instance);

        await notification.HandleAsync(message, default);
        dbContext.ChangeTracker.Clear();
        await notification.HandleAsync(message, default);

        Assert.Equal(1, await dbContext.NotificationLogs.CountAsync());
        Assert.Equal(
            1,
            await dbContext.ProcessedMessages.CountAsync(
                x => x.ConsumerName == "notification"));
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } =
            new(2026, 7, 27, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class NeverCalledStore : ICertificateStore
    {
        public Task UploadAsync(
            string blobName,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The store should not be called.");

        public Task<CertificateDownload?> DownloadAsync(
            string blobName,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The store should not be called.");
    }
}
