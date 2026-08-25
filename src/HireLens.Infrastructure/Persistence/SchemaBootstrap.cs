using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HireLens.Infrastructure.Persistence;

/// <summary>
/// Ensures demo-critical recruiting tables exist on SAP HANA Cloud.
/// Full <c>CreateTables</c> / <c>GenerateCreateScript</c> is unsafe here: DBADMIN schemas
/// are often partial, and HANA rejects NVARCHAR lengths above 5000 (e.g. InterviewTurns.Text).
/// </summary>
public static class SchemaBootstrap
{
    public static async Task EnsureApplicationTablesAsync(
        HireLensDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.IsInMemory())
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogInformation("InMemory schema ensured.");
            return;
        }

        var positionsExists = await TableExistsAsync(db, "POSITIONS", cancellationToken);
        var criteriaExists = await TableExistsAsync(db, "POSITIONCRITERIA", cancellationToken);

        if (positionsExists && criteriaExists)
        {
            logger.LogInformation("HireLens recruiting tables present (Positions, PositionCriteria).");
            return;
        }

        logger.LogWarning(
            "Missing recruiting tables (Positions={Positions}, PositionCriteria={Criteria}). Creating.",
            positionsExists,
            criteriaExists);

        if (!positionsExists)
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "Positions" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_Positions" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "Title" NVARCHAR(200) NOT NULL,
                    "JobDescription" NCLOB NOT NULL,
                    "CreatedAt" NVARCHAR(48) NOT NULL
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_Positions_TenantId_Id" ON "Positions" ("TenantId", "Id")""",
                cancellationToken);
        }

        if (!criteriaExists)
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "PositionCriteria" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_PositionCriteria" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "PositionId" NVARCHAR(36) NOT NULL,
                    "Name" NVARCHAR(200) NOT NULL,
                    "Description" NVARCHAR(2000) NULL,
                    "Weight" INT NOT NULL,
                    CONSTRAINT "FK_PositionCriteria_Positions_PositionId"
                        FOREIGN KEY ("PositionId") REFERENCES "Positions" ("Id") ON DELETE CASCADE
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_PositionCriteria_TenantId_Id" ON "PositionCriteria" ("TenantId", "Id")""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE INDEX "IX_PositionCriteria_PositionId" ON "PositionCriteria" ("PositionId")""",
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "POSITIONS", cancellationToken)
            || !await TableExistsAsync(db, "POSITIONCRITERIA", cancellationToken))
        {
            throw new InvalidOperationException(
                "Schema bootstrap failed: Positions and/or PositionCriteria still missing after CREATE.");
        }

        logger.LogInformation("HireLens recruiting tables ready.");
    }

    /// <summary>
    /// Every write path appends rows via <see cref="AuditSaveChangesInterceptor"/>.
    /// Without these tables, POST /api/positions (and any other write) fails at SaveChanges.
    /// </summary>
    public static async Task EnsureAuditTablesAsync(
        HireLensDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.IsInMemory())
        {
            return;
        }

        var auditExists = await TableExistsAsync(db, "AUDITEVENTS", cancellationToken);
        var aiExists = await TableExistsAsync(db, "AIINVOCATIONS", cancellationToken);

        if (auditExists && aiExists)
        {
            logger.LogInformation("HireLens audit tables present (AuditEvents, AiInvocations).");
            return;
        }

        logger.LogWarning(
            "Missing audit tables (AuditEvents={Audit}, AiInvocations={Ai}). Creating.",
            auditExists,
            aiExists);

        if (!auditExists)
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "AuditEvents" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_AuditEvents" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "Action" NVARCHAR(32) NOT NULL,
                    "EntityType" NVARCHAR(128) NOT NULL,
                    "EntityId" NVARCHAR(64) NOT NULL,
                    "ActorSubject" NVARCHAR(256) NULL,
                    "OccurredAt" NVARCHAR(48) NOT NULL,
                    "CorrelationId" NVARCHAR(64) NULL
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_AuditEvents_TenantId_Id" ON "AuditEvents" ("TenantId", "Id")""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE INDEX "IX_AuditEvents_TenantId_OccurredAt" ON "AuditEvents" ("TenantId", "OccurredAt")""",
                cancellationToken);
        }

        if (!aiExists)
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "AiInvocations" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_AiInvocations" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "TaskType" NVARCHAR(64) NOT NULL,
                    "ModelId" NVARCHAR(128) NOT NULL,
                    "PromptVersion" NVARCHAR(32) NOT NULL,
                    "PromptHash" NVARCHAR(64) NOT NULL,
                    "InputTokens" INT NOT NULL,
                    "OutputTokens" INT NOT NULL,
                    "EstimatedCost" DECIMAL(18, 6) NOT NULL,
                    "LatencyMs" BIGINT NOT NULL,
                    "Confidence" DOUBLE NULL,
                    "CorrelationId" NVARCHAR(64) NULL,
                    "OccurredAt" NVARCHAR(48) NOT NULL
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_AiInvocations_TenantId_Id" ON "AiInvocations" ("TenantId", "Id")""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE INDEX "IX_AiInvocations_TenantId_OccurredAt" ON "AiInvocations" ("TenantId", "OccurredAt")""",
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "AUDITEVENTS", cancellationToken)
            || !await TableExistsAsync(db, "AIINVOCATIONS", cancellationToken))
        {
            throw new InvalidOperationException(
                "Schema bootstrap failed: AuditEvents and/or AiInvocations still missing after CREATE.");
        }

        logger.LogInformation("HireLens audit tables ready.");
    }

    /// <summary>
    /// Adds CV-evaluation audit columns introduced by the AI Core scoring path.
    /// Safe to re-run; ignores "already exists" / duplicate column errors.
    /// </summary>
    public static async Task EnsureEvaluationAuditColumnsAsync(
        HireLensDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.IsInMemory())
        {
            return;
        }

        if (!await TableExistsAsync(db, "EVALUATIONS", cancellationToken))
        {
            logger.LogInformation("Evaluations table not present yet; skipping column bootstrap.");
            return;
        }

        await ExecuteIgnoreDuplicateAsync(
            db,
            logger,
            """ALTER TABLE "Evaluations" ADD ("CoverageRatio" DECIMAL(9,4) DEFAULT 0 NOT NULL)""",
            cancellationToken);
        await ExecuteIgnoreDuplicateAsync(
            db,
            logger,
            """ALTER TABLE "Evaluations" ADD ("RubricVersion" NVARCHAR(64) DEFAULT '' NOT NULL)""",
            cancellationToken);
        await ExecuteIgnoreDuplicateAsync(
            db,
            logger,
            """ALTER TABLE "Evaluations" ADD ("ModelName" NVARCHAR(128) DEFAULT '' NOT NULL)""",
            cancellationToken);
        await ExecuteIgnoreDuplicateAsync(
            db,
            logger,
            """ALTER TABLE "Evaluations" ADD ("ModelVersion" NVARCHAR(32) DEFAULT '' NOT NULL)""",
            cancellationToken);
        await ExecuteIgnoreDuplicateAsync(
            db,
            logger,
            """ALTER TABLE "Evaluations" ADD ("FailureStage" NVARCHAR(64) NULL)""",
            cancellationToken);
        await ExecuteIgnoreDuplicateAsync(
            db,
            logger,
            """ALTER TABLE "Evaluations" ADD ("FailureMessage" NVARCHAR(1024) NULL)""",
            cancellationToken);
        await ExecuteIgnoreDuplicateAsync(
            db,
            logger,
            """ALTER TABLE "Evaluations" ADD ("ExecutedAt" NVARCHAR(48) NULL)""",
            cancellationToken);

        logger.LogInformation("Evaluation audit columns ensured.");
    }

    private static async Task ExecuteIgnoreDuplicateAsync(
        HireLensDbContext db,
        ILogger logger,
        string sql,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        catch (Exception ex) when (LooksLikeAlreadyExists(ex))
        {
            logger.LogInformation("Skip existing object: {Message}", Truncate(ex.Message, 200));
        }
    }

    private static bool LooksLikeAlreadyExists(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        return message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
               || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
               || message.Contains("cannot add duplicate", StringComparison.OrdinalIgnoreCase)
               || message.Contains("column name", StringComparison.OrdinalIgnoreCase)
                  && message.Contains("already used", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private static async Task<bool> TableExistsAsync(
        HireLensDbContext db,
        string upperTableName,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT COUNT(*)
                FROM TABLES
                WHERE SCHEMA_NAME = CURRENT_SCHEMA
                  AND UPPER(TABLE_NAME) = '{upperTableName.Replace("'", "''", StringComparison.Ordinal)}'
                """;
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result) > 0;
        }
        catch (Exception ex)
        {
            // Catalog query failed — treat as missing so CREATE is attempted.
            System.Diagnostics.Debug.WriteLine(ex);
            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
