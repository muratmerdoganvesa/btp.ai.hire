export interface paths {
  "/health/live": {
    get: {
      responses: {
        200: { content: { "application/json": { status: string } } };
      };
    };
  };
  "/health/ready": {
    get: {
      responses: {
        200: { content: { "application/json": { status: string } } };
      };
    };
  };
  "/api/me": {
    get: {
      responses: {
        200: { content: { "application/json": components["schemas"]["Me"] } };
      };
    };
  };
  "/api/tenants/current": {
    get: {
      responses: {
        200: { content: { "application/json": components["schemas"]["Tenant"] } };
      };
    };
  };
  "/api/identity/users": {
    get: {
      responses: {
        200: { content: { "application/json": components["schemas"]["User"][] } };
      };
    };
    post: {
      requestBody: { content: { "application/json": components["schemas"]["CreateUser"] } };
      responses: {
        201: { content: { "application/json": components["schemas"]["User"] } };
      };
    };
  };
}

export interface components {
  schemas: {
    Me: {
      subject: string | null;
      tenantId: string | null;
      roles: string[];
    };
    Tenant: {
      id: string;
      name: string;
      slug: string;
      createdAt: string;
    };
    User: {
      id: string;
      tenantId: string;
      externalSubject: string;
      displayName: string;
      roles: string[];
      createdAt: string;
    };
    CreateUser: {
      externalSubject: string;
      displayName: string;
      roles: string[];
    };
    PositionCriterion: {
      id: string;
      name: string;
      description: string;
      weight: number;
    };
    Position: {
      id: string;
      title: string;
      jobDescription: string;
      criteria: components["schemas"]["PositionCriterion"][];
      createdAt: string;
      slug?: string | null;
      stats?: components["schemas"]["PositionStats"] | null;
    };
    PositionStats: {
      totalCandidates: number;
      evaluatedCount: number;
      pendingCount: number;
      failedCount: number;
      reviewPendingCount: number;
    };
    PublicJob: {
      id: string;
      slug: string;
      title: string;
      jobDescription: string;
      criteria: components["schemas"]["PositionCriterion"][];
      isOpen: boolean;
    };
    PublicApplicationResponse: {
      applicationId: string;
      referenceNumber: string;
      documentId: string;
      uploadUrl: string;
      uploadMethod: string;
    };
    PublicApplicationStatus: {
      referenceNumber: string;
      applicationId: string;
      stage: string;
      requiresReupload: boolean;
    };
    UpsertPosition: {
      title: string;
      jobDescription: string;
      criteria: { name: string; description: string; weight: number }[];
    };
    ExtractCriteriaRequest: {
      jobTitle: string;
      jobDescription: string;
    };
    ExtractedCriterion: {
      label: string;
      description: string;
      weight: number;
      mandatory: boolean;
    };
    FlaggedPhrase: {
      phrase: string;
      category: string;
      reason: string;
    };
    UnmeasurablePhrase: {
      phrase: string;
      reason: string;
    };
    ExtractCriteriaResponse: {
      criteria: components["schemas"]["ExtractedCriterion"][];
      flaggedPhrases: components["schemas"]["FlaggedPhrase"][];
      unmeasurable: components["schemas"]["UnmeasurablePhrase"][];
      totalWeight: number;
    };
    Candidate: {
      id: string;
      positionId: string;
      displayName: string;
      overallScoreLabel: string | null;
      overallScore: number | null;
      status: string;
      createdAt: string;
      coverageRatio?: number | null;
      recommendedAction?: string | null;
      evaluationStatus?: string | null;
      riskFlagCount?: number;
    };
    CreateCandidate: {
      displayName: string;
    };
    UploadSession: {
      documentId: string;
      uploadUrl: string;
      method: string;
    };
    JobStatus: {
      jobId: string;
      kind: string;
      status: string;
      error: string | null;
      updatedAt: string;
    };
    Evidence: {
      source: string;
      quote: string;
      startOffset: number;
      endOffset: number;
    };
    CriterionScore: {
      criterionId: string;
      criterionName: string;
      score: number | null;
      weight: number;
      confidence: number;
      evidenceStatus: "Sufficient" | "Insufficient" | "Unknown";
      evidence: components["schemas"]["Evidence"][];
    };
    Evaluation: {
      id: string;
      positionId: string;
      candidateId: string;
      overallScore: number | null;
      coverageRatio: number;
      status: string;
      promptVersion: string;
      rubricVersion: string;
      modelName: string;
      modelVersion: string;
      summary: string | null;
      followUps: string[];
      needsVerification: string[];
      scores: components["schemas"]["CriterionScore"][];
      executedAt?: string | null;
      failureStage?: string | null;
      failureMessage?: string | null;
    };
    EvaluationAudit: {
      evaluationId: string;
      promptVersion: string;
      rubricVersion: string;
      modelName: string;
      modelVersion: string;
      coverageRatio: number;
      executedAt: string | null;
      status: string;
    };
    Decision: {
      id: string;
      candidateId: string;
      outcome: string;
      rationale: string;
      decidedAt: string;
    };
    RecordDecision: {
      outcome: "advance" | "hold" | "reject";
      rationale: string;
    };
    CandidateExport: {
      candidateId: string;
      displayName: string;
      payload: unknown;
      exportedAt: string;
    };
    DataDeletionRequest: {
      id: string;
      candidateId: string;
      status: string;
      requestedAt: string;
    };
  };
}
