-- HireLens Phase 0: append-only audit and AI invocation telemetry.
-- HANA Cloud. TenantId is mandatory on every tenant-owned table.

CREATE COLUMN TABLE AuditEvents (
    Id NVARCHAR(36) NOT NULL,
    TenantId NVARCHAR(36) NOT NULL,
    Action NVARCHAR(32) NOT NULL,
    EntityType NVARCHAR(128) NOT NULL,
    EntityId NVARCHAR(64) NOT NULL,
    ActorSubject NVARCHAR(256),
    OccurredAt TIMESTAMP NOT NULL,
    CorrelationId NVARCHAR(64),
    PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX UX_AuditEvents_TenantId_Id ON AuditEvents (TenantId, Id);
CREATE INDEX IX_AuditEvents_TenantId_OccurredAt ON AuditEvents (TenantId, OccurredAt);

CREATE COLUMN TABLE AiInvocations (
    Id NVARCHAR(36) NOT NULL,
    TenantId NVARCHAR(36) NOT NULL,
    TaskType NVARCHAR(64) NOT NULL,
    ModelId NVARCHAR(128) NOT NULL,
    PromptVersion NVARCHAR(32) NOT NULL,
    PromptHash NVARCHAR(64) NOT NULL,
    InputTokens INTEGER NOT NULL,
    OutputTokens INTEGER NOT NULL,
    EstimatedCost DECIMAL(18, 6) NOT NULL,
    LatencyMs BIGINT NOT NULL,
    Confidence DOUBLE,
    CorrelationId NVARCHAR(64),
    OccurredAt TIMESTAMP NOT NULL,
    PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX UX_AiInvocations_TenantId_Id ON AiInvocations (TenantId, Id);
CREATE INDEX IX_AiInvocations_TenantId_OccurredAt ON AiInvocations (TenantId, OccurredAt);
