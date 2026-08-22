import type { components } from "./schema";

export type Me = components["schemas"]["Me"];
export type Tenant = components["schemas"]["Tenant"];
export type User = components["schemas"]["User"];
export type CreateUser = components["schemas"]["CreateUser"];
export type Position = components["schemas"]["Position"];
export type UpsertPosition = components["schemas"]["UpsertPosition"];
export type Candidate = components["schemas"]["Candidate"];
export type Evaluation = components["schemas"]["Evaluation"];
export type CriterionScore = components["schemas"]["CriterionScore"];
export type Evidence = components["schemas"]["Evidence"];
export type Decision = components["schemas"]["Decision"];
export type RecordDecision = components["schemas"]["RecordDecision"];
export type JobStatus = components["schemas"]["JobStatus"];
export type UploadSession = components["schemas"]["UploadSession"];
export type CandidateExport = components["schemas"]["CandidateExport"];
export type { paths, components } from "./schema";

export class ApiError extends Error {
  public constructor(
    public readonly status: number,
    message: string
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export class ApiClient {
  public constructor(
    private readonly baseUrl: string,
    private readonly getToken: () => string | null
  ) {}

  public async getMe(): Promise<Me> {
    return this.get<Me>("/api/me");
  }

  public async getCurrentTenant(): Promise<Tenant> {
    return this.get<Tenant>("/api/tenants/current");
  }

  public async listUsers(): Promise<User[]> {
    return this.get<User[]>("/api/identity/users");
  }

  public async listPositions(): Promise<Position[]> {
    return this.get<Position[]>("/api/positions");
  }

  public async getPosition(id: string): Promise<Position> {
    return this.get<Position>(`/api/positions/${id}`);
  }

  public async createPosition(input: UpsertPosition): Promise<Position> {
    return this.send<Position>("/api/positions", "POST", input);
  }

  public async listCandidates(positionId: string): Promise<Candidate[]> {
    return this.get<Candidate[]>(`/api/positions/${positionId}/candidates`);
  }

  public async getCandidate(id: string): Promise<Candidate> {
    return this.get<Candidate>(`/api/candidates/${id}`);
  }

  public async createCandidate(positionId: string, displayName: string): Promise<Candidate> {
    return this.send<Candidate>(`/api/positions/${positionId}/candidates`, "POST", { displayName });
  }

  public async startUpload(positionId: string, candidateId: string, file: File): Promise<UploadSession> {
    return this.send<UploadSession>(
      `/api/positions/${positionId}/candidates/${candidateId}/documents/upload-session`,
      "POST",
      { fileName: file.name, contentType: file.type || "text/plain", sizeBytes: file.size }
    );
  }

  public async putObject(uploadUrl: string, file: File): Promise<void> {
    const response = await fetch(`${this.baseUrl}${uploadUrl}`, {
      method: "PUT",
      credentials: "same-origin",
      headers: this.headers(file.type || "text/plain"),
      body: file
    });
    this.throwIfFailed(response);
  }

  public async completeUpload(documentId: string): Promise<JobStatus> {
    return this.send<JobStatus>(`/api/documents/${documentId}/complete`, "POST");
  }

  public async getJob(jobId: string): Promise<JobStatus> {
    return this.get<JobStatus>(`/api/jobs/${jobId}`);
  }

  public async getEvaluation(candidateId: string): Promise<Evaluation> {
    return this.get<Evaluation>(`/api/candidates/${candidateId}/evaluation`);
  }

  public async listDecisions(candidateId: string): Promise<Decision[]> {
    return this.get<Decision[]>(`/api/candidates/${candidateId}/decisions`);
  }

  public async recordDecision(candidateId: string, input: RecordDecision): Promise<Decision> {
    return this.send<Decision>(`/api/candidates/${candidateId}/decisions`, "POST", input);
  }

  public async exportCandidate(candidateId: string): Promise<CandidateExport> {
    return this.get<CandidateExport>(`/compliance/export/${candidateId}`);
  }

  public async inviteInterview(candidateId: string, positionId: string): Promise<{ sessionId: string; inviteUrl: string }> {
    return this.send("/api/interviews/invites", "POST", { candidateId, positionId });
  }

  public async getInterview(candidateId: string): Promise<{
    status: string;
    interviewScore: number | null;
    turns: { role: string; text: string }[];
    questions: { criterionId: string; prompt: string }[];
    summary: string | null;
  }> {
    return this.get(`/api/candidates/${candidateId}/interview`);
  }

  public async getTheme(): Promise<{ brandHue: number; logoUrl: string | null; radiusScale: number; contrastOk: boolean; interviewWeight: number }> {
    return this.get("/api/theme");
  }

  public async updateTheme(input: { brandHue: number; logoUrl: string | null; radiusScale: number; interviewWeight: number }): Promise<unknown> {
    return this.send("/api/theme", "PUT", { ...input, contrastOk: true });
  }

  public async listRubrics(): Promise<{ id: string; name: string; criteria: { name: string; weight: number }[] }[]> {
    return this.get("/api/rubrics");
  }

  public async createRubric(name: string, criteria: { name: string; description: string; weight: number }[]): Promise<unknown> {
    return this.send("/api/rubrics", "POST", { name, criteria });
  }

  public async listPolicies(): Promise<{ taskType: string; modelId: string; region: string | null }[]> {
    return this.get("/api/model-policies");
  }

  public async upsertPolicy(taskType: string, modelId: string, region: string | null): Promise<unknown> {
    return this.send("/api/model-policies", "PUT", { taskType, modelId, region });
  }

  public async getQuota(): Promise<{ monthlyTokenLimit: number; usedTokens: number; remainingTokens: number }> {
    return this.get("/api/metering/quota");
  }

  public async getFunnel(): Promise<{ positions: number; candidates: number; evaluations: number; interviews: number; decisions: number }> {
    return this.get("/api/analytics/funnel");
  }

  public async getBias(): Promise<{ band: string; count: number }[]> {
    return this.get("/api/analytics/bias");
  }

  public async provisionTenant(input: { tenantId: string; name: string; slug: string; adminSubject: string }): Promise<unknown> {
    return this.send("/api/admin/tenants/provision", "POST", input);
  }

  public async requestDeletion(candidateId: string, reason: string): Promise<void> {
    await this.send<unknown>("/compliance/data-deletion-requests", "POST", { candidateId, reason });
  }

  public async issueDevToken(input: {
    tenantId: string;
    subject: string;
    roles: string[];
  }): Promise<string> {
    const response = await fetch(`${this.baseUrl}/dev/token`, {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ ...input, issuerKind: "xsuaa" })
    });
    this.throwIfFailed(response);
    const payload = (await response.json()) as { accessToken: string };
    return payload.accessToken;
  }

  private async get<T>(path: string): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      credentials: "same-origin",
      headers: this.headers()
    });
    this.throwIfFailed(response);
    return (await response.json()) as T;
  }

  private async send<T>(path: string, method: string, body?: unknown): Promise<T> {
    const headers = this.headers();
    if (body !== undefined) {
      headers.set("Content-Type", "application/json");
    }

    const response = await fetch(`${this.baseUrl}${path}`, {
      method,
      credentials: "same-origin",
      headers,
      body: body === undefined ? undefined : JSON.stringify(body)
    });
    this.throwIfFailed(response);
    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }

  private headers(contentType?: string): Headers {
    const headers = new Headers();
    const token = this.getToken();
    if (token) {
      headers.set("Authorization", `Bearer ${token}`);
    }

    if (contentType) {
      headers.set("Content-Type", contentType);
    }

    return headers;
  }

  private throwIfFailed(response: Response): void {
    if (!response.ok) {
      throw new ApiError(response.status, `http_${response.status}`);
    }
  }
}
