using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TrainingCompletion.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TrainingDbContext))]
[Migration("202607270001_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "Learners" (
                "Id" varchar(100) PRIMARY KEY,
                "DisplayName" varchar(200) NOT NULL,
                "IsActive" boolean NOT NULL
            );

            CREATE TABLE "Courses" (
                "Id" varchar(100) PRIMARY KEY,
                "Title" varchar(200) NOT NULL,
                "Description" text NOT NULL,
                "Capacity" integer NOT NULL,
                "IsActive" boolean NOT NULL
            );

            CREATE TABLE "Enrollments" (
                "Id" uuid PRIMARY KEY,
                "CourseId" varchar(100) NOT NULL REFERENCES "Courses" ("Id") ON DELETE RESTRICT,
                "LearnerId" varchar(100) NOT NULL REFERENCES "Learners" ("Id") ON DELETE RESTRICT,
                "Status" varchar(32) NOT NULL,
                "EnrolledAt" timestamptz NOT NULL,
                CONSTRAINT "UX_Enrollments_Course_Learner" UNIQUE ("CourseId", "LearnerId")
            );

            CREATE TABLE "CourseCompletions" (
                "Id" uuid PRIMARY KEY,
                "CourseId" varchar(100) NOT NULL REFERENCES "Courses" ("Id") ON DELETE RESTRICT,
                "LearnerId" varchar(100) NOT NULL REFERENCES "Learners" ("Id") ON DELETE RESTRICT,
                "CompletedAt" timestamptz NOT NULL,
                "CertificateStatus" varchar(32) NOT NULL,
                "ReportingStatus" varchar(32) NOT NULL,
                "NotificationStatus" varchar(32) NOT NULL,
                "CertificateBlobName" varchar(300) NULL,
                "Version" integer NOT NULL,
                "IdempotencyKey" varchar(128) NULL,
                "RequestHash" varchar(64) NULL,
                CONSTRAINT "UX_Completions_Course_Learner" UNIQUE ("CourseId", "LearnerId")
            );
            CREATE UNIQUE INDEX "IX_CourseCompletions_IdempotencyKey"
                ON "CourseCompletions" ("IdempotencyKey")
                WHERE "IdempotencyKey" IS NOT NULL;

            CREATE TABLE "OutboxMessages" (
                "Id" uuid PRIMARY KEY,
                "EventType" varchar(200) NOT NULL,
                "Payload" jsonb NOT NULL,
                "CreatedAt" timestamptz NOT NULL,
                "PublishedAt" timestamptz NULL,
                "PublishAttempts" integer NOT NULL DEFAULT 0,
                "LastError" varchar(500) NULL,
                "NextAttemptAt" timestamptz NULL
            );
            CREATE INDEX "IX_OutboxMessages_Pending"
                ON "OutboxMessages" ("PublishedAt", "NextAttemptAt");

            CREATE TABLE "ProcessedMessages" (
                "EventId" uuid NOT NULL,
                "ConsumerName" varchar(64) NOT NULL,
                "ProcessedAt" timestamptz NOT NULL,
                PRIMARY KEY ("EventId", "ConsumerName")
            );

            CREATE TABLE "CourseCompletionSummaries" (
                "CourseId" varchar(100) PRIMARY KEY REFERENCES "Courses" ("Id") ON DELETE RESTRICT,
                "CompletionCount" integer NOT NULL,
                "LastCompletedAt" timestamptz NOT NULL
            );

            CREATE TABLE "NotificationLogs" (
                "Id" uuid PRIMARY KEY,
                "CompletionId" uuid NOT NULL REFERENCES "CourseCompletions" ("Id") ON DELETE CASCADE,
                "LearnerId" varchar(100) NOT NULL,
                "Type" varchar(64) NOT NULL,
                "Status" varchar(32) NOT NULL,
                "CreatedAt" timestamptz NOT NULL,
                CONSTRAINT "UX_NotificationLogs_Completion_Type" UNIQUE ("CompletionId", "Type")
            );

            CREATE TABLE "ConsumerFailures" (
                "EventId" uuid NOT NULL,
                "CompletionId" uuid NOT NULL,
                "ConsumerName" varchar(64) NOT NULL,
                "AttemptCount" integer NOT NULL,
                "LastError" varchar(500) NOT NULL,
                "LastFailedAt" timestamptz NOT NULL,
                "IsResolved" boolean NOT NULL,
                PRIMARY KEY ("EventId", "ConsumerName")
            );
            CREATE INDEX "IX_ConsumerFailures_Resolved_FailedAt"
                ON "ConsumerFailures" ("IsResolved", "LastFailedAt");

            INSERT INTO "Learners" ("Id", "DisplayName", "IsActive")
            VALUES ('learner-1001', 'Demo Learner', TRUE);

            INSERT INTO "Courses" ("Id", "Title", "Description", "Capacity", "IsActive") VALUES
                ('course-2001', 'Event-Driven Architecture Fundamentals',
                 'Learn reliable messaging, outbox delivery, and idempotent consumers.', 20, TRUE),
                ('course-2002', 'ASP.NET Core Web API',
                 'Build maintainable HTTP APIs with ASP.NET Core.', 15, TRUE),
                ('course-2003', 'Infrastructure with Terraform',
                 'Create and review Azure infrastructure as code.', 10, TRUE);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "ConsumerFailures";
            DROP TABLE IF EXISTS "NotificationLogs";
            DROP TABLE IF EXISTS "CourseCompletionSummaries";
            DROP TABLE IF EXISTS "ProcessedMessages";
            DROP TABLE IF EXISTS "OutboxMessages";
            DROP TABLE IF EXISTS "CourseCompletions";
            DROP TABLE IF EXISTS "Enrollments";
            DROP TABLE IF EXISTS "Courses";
            DROP TABLE IF EXISTS "Learners";
            """);
    }
}
