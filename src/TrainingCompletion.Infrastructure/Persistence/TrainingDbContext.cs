using Microsoft.EntityFrameworkCore;
using TrainingCompletion.Domain;

namespace TrainingCompletion.Infrastructure.Persistence;

public sealed class TrainingDbContext(DbContextOptions<TrainingDbContext> options) : DbContext(options)
{
    public DbSet<Learner> Learners => Set<Learner>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<CourseCompletion> CourseCompletions => Set<CourseCompletion>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<CourseCompletionSummary> CourseCompletionSummaries => Set<CourseCompletionSummary>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<ConsumerFailure> ConsumerFailures => Set<ConsumerFailure>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Learner>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(100);
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.HasData(new Learner
            {
                Id = "learner-1001",
                DisplayName = "Demo Learner",
                IsActive = true
            });
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(100);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasData(
                new Course
                {
                    Id = "course-2001",
                    Title = "Event-Driven Architecture Fundamentals",
                    Description = "Learn reliable messaging, outbox delivery, and idempotent consumers.",
                    Capacity = 20,
                    IsActive = true
                },
                new Course
                {
                    Id = "course-2002",
                    Title = "ASP.NET Core Web API",
                    Description = "Build maintainable HTTP APIs with ASP.NET Core.",
                    Capacity = 15,
                    IsActive = true
                },
                new Course
                {
                    Id = "course-2003",
                    Title = "Infrastructure with Terraform",
                    Description = "Create and review Azure infrastructure as code.",
                    Capacity = 10,
                    IsActive = true
                });
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CourseId).HasMaxLength(100);
            entity.Property(x => x.LearnerId).HasMaxLength(100);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.CourseId, x.LearnerId }).IsUnique();
            entity.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Learner>().WithMany().HasForeignKey(x => x.LearnerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CourseCompletion>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CourseId).HasMaxLength(100);
            entity.Property(x => x.LearnerId).HasMaxLength(100);
            entity.Property(x => x.CertificateStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ReportingStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.NotificationStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.CertificateBlobName).HasMaxLength(300);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(128);
            entity.Property(x => x.RequestHash).HasMaxLength(64);
            entity.HasIndex(x => new { x.CourseId, x.LearnerId }).IsUnique();
            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(200);
            entity.Property(x => x.Payload).HasColumnType("jsonb");
            entity.Property(x => x.LastError).HasMaxLength(500);
            entity.HasIndex(x => new { x.PublishedAt, x.NextAttemptAt });
        });

        modelBuilder.Entity<ProcessedMessage>(entity =>
        {
            entity.HasKey(x => new { x.EventId, x.ConsumerName });
            entity.Property(x => x.ConsumerName).HasMaxLength(64);
        });

        modelBuilder.Entity<CourseCompletionSummary>(entity =>
        {
            entity.HasKey(x => x.CourseId);
            entity.Property(x => x.CourseId).HasMaxLength(100);
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LearnerId).HasMaxLength(100);
            entity.Property(x => x.Type).HasMaxLength(64);
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.HasIndex(x => new { x.CompletionId, x.Type }).IsUnique();
        });

        modelBuilder.Entity<ConsumerFailure>(entity =>
        {
            entity.HasKey(x => new { x.EventId, x.ConsumerName });
            entity.Property(x => x.ConsumerName).HasMaxLength(64);
            entity.Property(x => x.LastError).HasMaxLength(500);
            entity.HasIndex(x => new { x.IsResolved, x.LastFailedAt });
        });
    }
}

