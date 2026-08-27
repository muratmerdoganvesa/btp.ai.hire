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
            await EnsurePositionSlugColumnAsync(db, logger, cancellationToken);
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
                    "Slug" NVARCHAR(220) DEFAULT '' NOT NULL,
                    "CreatedAt" NVARCHAR(48) NOT NULL,
                    "IsDeleted" BOOLEAN DEFAULT FALSE NOT NULL,
                    "DeletedAt" NVARCHAR(48) NULL
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
        await EnsurePositionSlugColumnAsync(db, logger, cancellationToken);
    }

    public static async Task EnsureCandidateTablesAsync(
        HireLensDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.IsInMemory())
        {
            return;
        }

        if (await TableExistsAsync(db, "CANDIDATES", cancellationToken))
        {
            logger.LogInformation("HireLens candidate tables present (Candidates).");
            return;
        }

        logger.LogWarning("Missing candidate table (Candidates). Creating.");
        await ExecuteIgnoreDuplicateAsync(
            db,
            logger,
            """
            CREATE TABLE "Candidates" (
                "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_Candidates" PRIMARY KEY,
                "TenantId" NVARCHAR(36) NOT NULL,
                "PositionId" NVARCHAR(36) NOT NULL,
                "DisplayName" NVARCHAR(200) NOT NULL,
                "Status" NVARCHAR(32) NOT NULL,
                "CreatedAt" NVARCHAR(48) NOT NULL,
                "IsDeleted" BOOLEAN DEFAULT FALSE NOT NULL,
                "DeletedAt" NVARCHAR(48) NULL
            )
            """,
            cancellationToken);
        await ExecuteIgnoreDuplicateAsync(
            db,
            logger,
            """CREATE UNIQUE INDEX "IX_Candidates_TenantId_Id" ON "Candidates" ("TenantId", "Id")""",
            cancellationToken);
        await ExecuteIgnoreDuplicateAsync(
            db,
            logger,
            """CREATE INDEX "IX_Candidates_TenantId_PositionId" ON "Candidates" ("TenantId", "PositionId")""",
            cancellationToken);

        logger.LogInformation("HireLens candidate tables ready.");
    }

    private static async Task EnsurePositionSlugColumnAsync(
        HireLensDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (db.Database.IsInMemory() || !await TableExistsAsync(db, "POSITIONS", cancellationToken))
        {
            return;
        }

        await ExecuteIgnoreDuplicateAsync(
            db,
            logger,
            """ALTER TABLE "Positions" ADD ("Slug" NVARCHAR(220) DEFAULT '' NOT NULL)""",
            cancellationToken);
        await ExecuteIgnoreDuplicateAsync(
            db,
            logger,
            """CREATE UNIQUE INDEX "IX_Positions_TenantId_Slug" ON "Positions" ("TenantId", "Slug")""",
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "Positions"
            SET "Slug" = LOWER(REPLACE("Title", ' ', '-')) || '-' || SUBSTRING("Id", 1, 8)
            WHERE "Slug" IS NULL OR "Slug" = ''
            """,
            cancellationToken);

        logger.LogInformation("Position slug column ensured.");
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
    /// CV upload, parse jobs, matching scores, and parse cache tables required after Positions exist.
    /// </summary>
    public static async Task EnsureDocumentPipelineTablesAsync(
        HireLensDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.IsInMemory())
        {
            return;
        }

        if (await TableExistsAsync(db, "CVDOCUMENTS", cancellationToken)
            && await TableExistsAsync(db, "ANALYSISJOBS", cancellationToken)
            && await TableExistsAsync(db, "EVALUATIONS", cancellationToken)
            && await TableExistsAsync(db, "CRITERIONSCORES", cancellationToken)
            && await TableExistsAsync(db, "EVIDENCEITEMS", cancellationToken)
            && await TableExistsAsync(db, "PARSECACHES", cancellationToken))
        {
            logger.LogInformation("HireLens document pipeline tables present.");
            return;
        }

        logger.LogWarning("Missing document/matching tables; creating minimal pipeline schema.");

        if (!await TableExistsAsync(db, "CVDOCUMENTS", cancellationToken))
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "CvDocuments" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_CvDocuments" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "CandidateId" NVARCHAR(36) NOT NULL,
                    "PositionId" NVARCHAR(36) NOT NULL,
                    "ObjectKey" NVARCHAR(512) NOT NULL,
                    "ContentType" NVARCHAR(128) NOT NULL,
                    "FileName" NVARCHAR(256) NOT NULL,
                    "SizeBytes" BIGINT NOT NULL,
                    "Status" NVARCHAR(32) NOT NULL,
                    "MaskedText" NCLOB NULL,
                    "PromptVersion" NVARCHAR(32) NULL,
                    "CreatedAt" NVARCHAR(48) NOT NULL
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_CvDocuments_TenantId_Id" ON "CvDocuments" ("TenantId", "Id")""",
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "ANALYSISJOBS", cancellationToken))
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "AnalysisJobs" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_AnalysisJobs" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "Kind" NVARCHAR(32) NOT NULL,
                    "Status" NVARCHAR(32) NOT NULL,
                    "Error" NVARCHAR(1000) NULL,
                    "DocumentId" NVARCHAR(36) NULL,
                    "UpdatedAt" NVARCHAR(48) NOT NULL
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_AnalysisJobs_TenantId_Id" ON "AnalysisJobs" ("TenantId", "Id")""",
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "EVALUATIONS", cancellationToken))
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "Evaluations" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_Evaluations" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "PositionId" NVARCHAR(36) NOT NULL,
                    "CandidateId" NVARCHAR(36) NOT NULL,
                    "DocumentId" NVARCHAR(36) NOT NULL,
                    "OverallScore" INT NULL,
                    "CvScore" INT NULL,
                    "InterviewScore" INT NULL,
                    "CoverageRatio" DECIMAL(9,4) DEFAULT 0 NOT NULL,
                    "Status" NVARCHAR(32) NOT NULL,
                    "PromptVersion" NVARCHAR(32) NOT NULL,
                    "RubricVersion" NVARCHAR(64) DEFAULT '' NOT NULL,
                    "ModelName" NVARCHAR(128) DEFAULT '' NOT NULL,
                    "ModelVersion" NVARCHAR(32) DEFAULT '' NOT NULL,
                    "FailureStage" NVARCHAR(64) NULL,
                    "FailureMessage" NVARCHAR(1024) NULL,
                    "Summary" NCLOB NULL,
                    "FollowUpsJson" NCLOB NULL,
                    "NeedsVerificationJson" NCLOB NULL,
                    "CreatedAt" NVARCHAR(48) NOT NULL,
                    "ExecutedAt" NVARCHAR(48) NULL
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_Evaluations_TenantId_Id" ON "Evaluations" ("TenantId", "Id")""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE INDEX "IX_Evaluations_TenantId_CandidateId" ON "Evaluations" ("TenantId", "CandidateId")""",
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "CRITERIONSCORES", cancellationToken))
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "CriterionScores" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_CriterionScores" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "EvaluationId" NVARCHAR(36) NOT NULL,
                    "CriterionId" NVARCHAR(36) NOT NULL,
                    "Score" INT NULL,
                    "Weight" INT NOT NULL,
                    "Confidence" DOUBLE NOT NULL,
                    "EvidenceStatus" NVARCHAR(24) NOT NULL
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_CriterionScores_TenantId_Id" ON "CriterionScores" ("TenantId", "Id")""",
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "EVIDENCEITEMS", cancellationToken))
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "EvidenceItems" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_EvidenceItems" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "CriterionScoreId" NVARCHAR(36) NOT NULL,
                    "Source" NVARCHAR(64) NOT NULL,
                    "Quote" NVARCHAR(2000) NOT NULL,
                    "StartOffset" INT NOT NULL,
                    "EndOffset" INT NOT NULL
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_EvidenceItems_TenantId_Id" ON "EvidenceItems" ("TenantId", "Id")""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE INDEX "IX_EvidenceItems_CriterionScoreId" ON "EvidenceItems" ("CriterionScoreId")""",
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "PARSECACHES", cancellationToken))
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "ParseCaches" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_ParseCaches" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "ContentHash" NVARCHAR(64) NOT NULL,
                    "MaskedText" NCLOB NOT NULL,
                    "CachedAt" NVARCHAR(48) NOT NULL
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_ParseCaches_TenantId_Id" ON "ParseCaches" ("TenantId", "Id")""",
                cancellationToken);
        }

        logger.LogInformation("HireLens document pipeline tables ready.");
    }

    public static async Task EnsurePrivacyTablesAsync(
        HireLensDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.IsInMemory())
        {
            return;
        }

        if (!await TableExistsAsync(db, "CONSENTRECORDS", cancellationToken))
        {
            logger.LogWarning("Missing privacy table (ConsentRecords). Creating.");
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "ConsentRecords" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_ConsentRecords" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "CandidateId" NVARCHAR(36) NOT NULL,
                    "Purpose" NVARCHAR(64) NOT NULL,
                    "TextVersion" NVARCHAR(32) NULL,
                    "RemoteIp" NVARCHAR(64) NULL,
                    "AcceptedAt" NVARCHAR(48) NOT NULL
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_ConsentRecords_TenantId_Id" ON "ConsentRecords" ("TenantId", "Id")""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE INDEX "IX_ConsentRecords_CandidateId_Purpose" ON "ConsentRecords" ("CandidateId", "Purpose")""",
                cancellationToken);
        }
        else
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """ALTER TABLE "ConsentRecords" ADD ("TextVersion" NVARCHAR(32) NULL)""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """ALTER TABLE "ConsentRecords" ADD ("RemoteIp" NVARCHAR(64) NULL)""",
                cancellationToken);
        }

        logger.LogInformation("HireLens privacy tables ready.");
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

    /// <summary>
    /// Creates InterviewSessions / InterviewQuestions / InterviewTurns when missing.
    /// Without these, public interview prep/start fails with HANA "could not find table/view".
    /// </summary>
    public static async Task EnsureInterviewTablesAsync(
        HireLensDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.IsInMemory())
        {
            return;
        }

        var sessionsOk = await TableExistsAsync(db, "INTERVIEWSESSIONS", cancellationToken);
        var questionsOk = await TableExistsAsync(db, "INTERVIEWQUESTIONS", cancellationToken);
        var turnsOk = await TableExistsAsync(db, "INTERVIEWTURNS", cancellationToken);
        var framesOk = await TableExistsAsync(db, "INTERVIEWFRAMES", cancellationToken);

        if (sessionsOk && questionsOk && turnsOk && framesOk)
        {
            await EnsureInterviewColumnsAsync(db, logger, cancellationToken);
            logger.LogInformation("HireLens interview tables present.");
            return;
        }

        logger.LogWarning(
            "Missing interview tables (Sessions={Sessions}, Questions={Questions}, Turns={Turns}, Frames={Frames}). Creating.",
            sessionsOk,
            questionsOk,
            turnsOk,
            framesOk);

        if (!sessionsOk)
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "InterviewSessions" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_InterviewSessions" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "CandidateId" NVARCHAR(36) NOT NULL,
                    "PositionId" NVARCHAR(36) NOT NULL,
                    "Status" NVARCHAR(32) NOT NULL,
                    "TokenHash" NVARCHAR(128) NOT NULL,
                    "DisclosureAccepted" BOOLEAN DEFAULT FALSE NOT NULL,
                    "InterviewScore" INT NULL,
                    "Summary" NCLOB NULL,
                    "VideoMeetingUrl" NVARCHAR(1000) NULL,
                    "ExpiresAt" NVARCHAR(48) NOT NULL,
                    "CreatedAt" NVARCHAR(48) NOT NULL,
                    "IsDeleted" BOOLEAN DEFAULT FALSE NOT NULL,
                    "DeletedAt" NVARCHAR(48) NULL
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_InterviewSessions_TenantId_Id" ON "InterviewSessions" ("TenantId", "Id")""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE INDEX "IX_InterviewSessions_TenantId_CandidateId" ON "InterviewSessions" ("TenantId", "CandidateId")""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE INDEX "IX_InterviewSessions_TokenHash" ON "InterviewSessions" ("TokenHash")""",
                cancellationToken);
        }

        if (!questionsOk)
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "InterviewQuestions" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_InterviewQuestions" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "SessionId" NVARCHAR(36) NOT NULL,
                    "CriterionId" NVARCHAR(36) NOT NULL,
                    "Prompt" NVARCHAR(2000) NOT NULL,
                    "QuestionOrder" INT NOT NULL
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_InterviewQuestions_TenantId_Id" ON "InterviewQuestions" ("TenantId", "Id")""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE INDEX "IX_InterviewQuestions_SessionId" ON "InterviewQuestions" ("SessionId")""",
                cancellationToken);
        }

        if (!turnsOk)
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "InterviewTurns" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_InterviewTurns" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "SessionId" NVARCHAR(36) NOT NULL,
                    "QuestionId" NVARCHAR(36) NULL,
                    "Role" NVARCHAR(16) NOT NULL,
                    "Text" NVARCHAR(5000) NOT NULL,
                    "CreatedAt" NVARCHAR(48) NOT NULL
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_InterviewTurns_TenantId_Id" ON "InterviewTurns" ("TenantId", "Id")""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE INDEX "IX_InterviewTurns_SessionId" ON "InterviewTurns" ("SessionId")""",
                cancellationToken);
        }

        if (!framesOk)
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """
                CREATE TABLE "InterviewFrames" (
                    "Id" NVARCHAR(36) NOT NULL CONSTRAINT "PK_InterviewFrames" PRIMARY KEY,
                    "TenantId" NVARCHAR(36) NOT NULL,
                    "SessionId" NVARCHAR(36) NOT NULL,
                    "CandidateId" NVARCHAR(36) NOT NULL,
                    "PositionId" NVARCHAR(36) NOT NULL,
                    "QuestionId" NVARCHAR(36) NULL,
                    "TurnId" NVARCHAR(36) NULL,
                    "ContentType" NVARCHAR(64) NOT NULL,
                    "ImageBase64" NCLOB NOT NULL,
                    "CapturedAt" NVARCHAR(48) NOT NULL
                )
                """,
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE UNIQUE INDEX "IX_InterviewFrames_TenantId_Id" ON "InterviewFrames" ("TenantId", "Id")""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE INDEX "IX_InterviewFrames_SessionId" ON "InterviewFrames" ("SessionId")""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """CREATE INDEX "IX_InterviewFrames_TenantId_CandidateId" ON "InterviewFrames" ("TenantId", "CandidateId")""",
                cancellationToken);
        }

        await EnsureInterviewColumnsAsync(db, logger, cancellationToken);
        logger.LogInformation("HireLens interview tables ready.");
    }

    public static async Task EnsureInterviewColumnsAsync(
        HireLensDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.IsInMemory())
        {
            return;
        }

        if (!await TableExistsAsync(db, "INTERVIEWSESSIONS", cancellationToken))
        {
            logger.LogInformation("InterviewSessions table not present yet; skipping column bootstrap.");
            return;
        }

        await ExecuteIgnoreDuplicateAsync(
            db,
            logger,
            """ALTER TABLE "InterviewSessions" ADD ("VideoMeetingUrl" NVARCHAR(1000) NULL)""",
            cancellationToken);
        await ExecuteIgnoreDuplicateAsync(
            db,
            logger,
            """ALTER TABLE "InterviewQuestions" ADD ("QuestionOrder" INT DEFAULT 0 NOT NULL)""",
            cancellationToken);
        await EnsureSoftDeleteColumnsAsync(db, logger, cancellationToken);
        logger.LogInformation("Interview session columns ensured.");
    }

    public static async Task EnsureSoftDeleteColumnsAsync(
        HireLensDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.IsInMemory())
        {
            return;
        }

        if (await TableExistsAsync(db, "POSITIONS", cancellationToken))
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """ALTER TABLE "Positions" ADD ("IsDeleted" BOOLEAN DEFAULT FALSE NOT NULL)""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """ALTER TABLE "Positions" ADD ("DeletedAt" NVARCHAR(48) NULL)""",
                cancellationToken);
        }

        if (await TableExistsAsync(db, "CANDIDATES", cancellationToken))
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """ALTER TABLE "Candidates" ADD ("IsDeleted" BOOLEAN DEFAULT FALSE NOT NULL)""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """ALTER TABLE "Candidates" ADD ("DeletedAt" NVARCHAR(48) NULL)""",
                cancellationToken);
        }

        if (await TableExistsAsync(db, "INTERVIEWSESSIONS", cancellationToken))
        {
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """ALTER TABLE "InterviewSessions" ADD ("IsDeleted" BOOLEAN DEFAULT FALSE NOT NULL)""",
                cancellationToken);
            await ExecuteIgnoreDuplicateAsync(
                db,
                logger,
                """ALTER TABLE "InterviewSessions" ADD ("DeletedAt" NVARCHAR(48) NULL)""",
                cancellationToken);
        }

        logger.LogInformation("Soft-delete columns ensured on Positions, Candidates, InterviewSessions.");
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
