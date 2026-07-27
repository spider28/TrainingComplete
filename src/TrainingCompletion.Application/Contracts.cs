using TrainingCompletion.Domain;

namespace TrainingCompletion.Application;

public sealed record CourseCompletedEvent(
    Guid EventId,
    Guid CompletionId,
    string LearnerId,
    string CourseId,
    DateTimeOffset CompletedAt,
    string CorrelationId);

public sealed record CourseDto(
    string Id,
    string Title,
    string Description,
    int Capacity,
    int RemainingCapacity,
    bool IsActive,
    EnrollmentStatus? EnrollmentStatus,
    Guid? CompletionId);

public sealed record EnrollmentDto(
    Guid Id,
    string CourseId,
    string LearnerId,
    EnrollmentStatus Status,
    DateTimeOffset EnrolledAt);

public sealed record CompletionStatusDto(
    Guid CompletionId,
    string CourseId,
    string LearnerId,
    string CoreStatus,
    CertificateStatus CertificateStatus,
    WorkflowStatus ReportingStatus,
    WorkflowStatus NotificationStatus,
    int Version,
    bool CertificateAvailable);

public sealed record CompletionCreationResult(
    CompletionStatusDto Completion,
    bool Replayed);

public sealed record DiagnosticsDto(
    int PendingOutboxCount,
    IReadOnlyList<OutboxFailureDto> RecentOutboxFailures,
    IReadOnlyList<ConsumerFailureDto> RecentConsumerFailures);

public sealed record OutboxFailureDto(
    Guid EventId,
    int Attempts,
    string Error,
    DateTimeOffset? NextAttemptAt);

public sealed record ConsumerFailureDto(
    Guid EventId,
    Guid CompletionId,
    string Consumer,
    int Attempts,
    string Error,
    DateTimeOffset LastFailedAt);

public interface ICertificateDocumentGenerator
{
    byte[] Generate(string learnerName, string courseTitle, DateTimeOffset completedAt);
}

public interface ICertificateStore
{
    Task UploadAsync(string blobName, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);
    Task<CertificateDownload?> DownloadAsync(string blobName, CancellationToken cancellationToken);
}

public sealed record CertificateDownload(Stream Content, string ContentType, long Length);

public interface IEventPublisher
{
    Task PublishAsync(CourseCompletedEvent message, CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
