using Npgsql;

namespace TrainingCompletion.IntegrationTests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Skip = "Set POSTGRES_TEST_CONNECTION to a dedicated training_test PostgreSQL database.";
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, "training_test", StringComparison.Ordinal))
        {
            Skip = "Safety guard: integration tests only run against a database named training_test.";
        }
    }
}

