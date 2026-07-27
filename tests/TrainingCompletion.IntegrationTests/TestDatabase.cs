using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrainingCompletion.Infrastructure.Persistence;

namespace TrainingCompletion.IntegrationTests;

internal static class TestDatabase
{
    public static async Task<TrainingDbContext> CreateResetAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION")
            ?? throw new InvalidOperationException("POSTGRES_TEST_CONNECTION is required.");
        EnsureSafe(connectionString);
        var options = new DbContextOptionsBuilder<TrainingDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var dbContext = new TrainingDbContext(options);
        await dbContext.Database.MigrateAsync();
        await dbContext.Database.ExecuteSqlRawAsync("""
            TRUNCATE TABLE
                "ConsumerFailures",
                "NotificationLogs",
                "CourseCompletionSummaries",
                "ProcessedMessages",
                "OutboxMessages",
                "CourseCompletions",
                "Enrollments"
            RESTART IDENTITY CASCADE;
            DELETE FROM "Learners" WHERE "Id" <> 'learner-1001';
            UPDATE "Courses" SET "Capacity" = CASE "Id"
                WHEN 'course-2001' THEN 20
                WHEN 'course-2002' THEN 15
                WHEN 'course-2003' THEN 10
                ELSE "Capacity"
            END, "IsActive" = TRUE;
            """);
        return dbContext;
    }

    public static void EnsureSafe(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, "training_test", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Destructive integration-test cleanup is only allowed for training_test.");
        }
    }
}
