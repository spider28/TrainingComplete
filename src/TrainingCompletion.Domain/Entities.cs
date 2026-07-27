namespace TrainingCompletion.Domain;

public sealed class Learner
{
    public required string Id { get; set; }
    public required string DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Course
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    public uint RowVersion { get; set; }
}

public sealed class Enrollment
{
    public Guid Id { get; set; }
    public required string CourseId { get; set; }
    public required string LearnerId { get; set; }
    public EnrollmentStatus Status { get; set; }
    public DateTimeOffset EnrolledAt { get; set; }
}

public sealed class CourseCompletion
{
    public Guid Id { get; set; }
    public required string CourseId { get; set; }
    public required string LearnerId { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public CertificateStatus CertificateStatus { get; set; }
    public WorkflowStatus ReportingStatus { get; set; }
    public WorkflowStatus NotificationStatus { get; set; }
    public string? CertificateBlobName { get; set; }
    public int Version { get; set; } = 1;
    public string? IdempotencyKey { get; set; }
    public string? RequestHash { get; set; }
}

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public required string EventType { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int PublishAttempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
}

public sealed class ProcessedMessage
{
    public Guid EventId { get; set; }
    public required string ConsumerName { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}

public sealed class CourseCompletionSummary
{
    public required string CourseId { get; set; }
    public int CompletionCount { get; set; }
    public DateTimeOffset LastCompletedAt { get; set; }
}

public sealed class NotificationLog
{
    public Guid Id { get; set; }
    public Guid CompletionId { get; set; }
    public required string LearnerId { get; set; }
    public required string Type { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ConsumerFailure
{
    public Guid EventId { get; set; }
    public Guid CompletionId { get; set; }
    public required string ConsumerName { get; set; }
    public int AttemptCount { get; set; }
    public required string LastError { get; set; }
    public DateTimeOffset LastFailedAt { get; set; }
    public bool IsResolved { get; set; }
}
