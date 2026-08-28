import type { components } from "./schema";

export type Me = components["schemas"]["Me"];
export type Tenant = components["schemas"]["Tenant"];
export type User = components["schemas"]["User"];
export type CreateUser = components["schemas"]["CreateUser"];
export type Position = components["schemas"]["Position"];
export type UpsertPosition = components["schemas"]["UpsertPosition"];
export type Candidate = components["schemas"]["Candidate"];
export type CandidateBoardItem = {
  id: string;
  positionId: string;
  positionTitle: string;
  displayName: string;
  personKey: string;
  siblingApplicationCount: number;
  overallScoreLabel: string | null;
  overallScore: number | null;
  status: string;
  pipelineStage: string;
  recommendedAction: string | null;
  createdAt: string;
};
export type InterviewBoardItem = {
  id: string;
  candidateId: string;
  candidateName: string;
  positionId: string;
  positionTitle: string;
  status: string;
  interviewScore: number | null;
  questionCount: number;
  answerCount: number;
  createdAt: string;
  expiresAt: string;
};
export type InterviewSessionDetail = {
  id: string;
  candidateId: string;
  positionId: string;
  status: string;
  disclosureAccepted: boolean;
  interviewScore: number | null;
  questions: { id: string; criterionId: string; prompt: string; order: number }[];
  turns: { id: string; role: string; text: string; questionId: string | null; createdAt: string }[];
  summary: string | null;
  videoMeetingUrl?: string | null;
  expiresAt?: string | null;
  frames?: {
    id: string;
    questionId: string | null;
    turnId: string | null;
    contentType: string;
    imageBase64: string;
    capturedAt: string;
  }[];
  candidateName?: string | null;
  positionTitle?: string | null;
  createdAt?: string | null;
};
export type Evaluation = components["schemas"]["Evaluation"];
export type CriterionScore = components["schemas"]["CriterionScore"];
export type Evidence = components["schemas"]["Evidence"];
export type Decision = components["schemas"]["Decision"];
export type RecordDecision = components["schemas"]["RecordDecision"];
export type Offer = {
  id: string;
  candidateId: string;
  positionId: string;
  candidateName: string;
  positionTitle: string;
  status: string;
  packageText: string;
  note: string | null;
  scoreSnapshot: number | null;
  createdAt: string;
  updatedAt: string;
  sentAt: string | null;
  respondedAt: string | null;
};
export type JobStatus = components["schemas"]["JobStatus"];
export type UploadSession = components["schemas"]["UploadSession"];
export type EvaluationAudit = components["schemas"]["EvaluationAudit"];
export type PublicJob = components["schemas"]["PublicJob"];
export type PublicApplicationResponse = components["schemas"]["PublicApplicationResponse"];
export type PublicApplicationStatus = components["schemas"]["PublicApplicationStatus"];
export type PositionStats = components["schemas"]["PositionStats"];
export type CandidateExport = components["schemas"]["CandidateExport"];
export type ExtractCriteriaRequest = components["schemas"]["ExtractCriteriaRequest"];
export type ExtractCriteriaResponse = components["schemas"]["ExtractCriteriaResponse"];
export type ExtractedInterviewQuestion = components["schemas"]["ExtractedInterviewQuestion"];
export type FlaggedPhrase = components["schemas"]["FlaggedPhrase"];
export type UnmeasurablePhrase = components["schemas"]["UnmeasurablePhrase"];
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

  public async listPositions(includeStats = false): Promise<Position[]> {
    const query = includeStats ? "?includeStats=true" : "";
    return this.get<Position[]>(`/api/positions${query}`);
  }

  public async getPosition(id: string): Promise<Position> {
    return this.get<Position>(`/api/positions/${id}`);
  }

  public async createPosition(input: UpsertPosition): Promise<Position> {
    return this.send<Position>("/api/positions", "POST", input);
  }

  public async updatePosition(id: string, input: UpsertPosition): Promise<Position> {
    return this.send<Position>(`/api/positions/${id}`, "PUT", input);
  }

  public async deletePosition(id: string): Promise<void> {
    await this.send<unknown>(`/api/positions/${id}`, "DELETE");
  }

  public async extractCriteria(input: ExtractCriteriaRequest): Promise<ExtractCriteriaResponse> {
    return this.send<ExtractCriteriaResponse>("/api/positions/criteria/extract", "POST", input);
  }

  public async listCandidates(positionId: string): Promise<Candidate[]> {
    return this.get<Candidate[]>(`/api/positions/${positionId}/candidates`);
  }

  public async listCandidateBoard(): Promise<CandidateBoardItem[]> {
    return this.get<CandidateBoardItem[]>(`/api/candidates`);
  }

  public async getCandidate(id: string): Promise<Candidate> {
    return this.get<Candidate>(`/api/candidates/${id}`);
  }

  public async deleteCandidate(id: string): Promise<void> {
    await this.send<unknown>(`/api/candidates/${id}`, "DELETE");
  }

  public async createCandidate(positionId: string, displayName: string): Promise<Candidate> {
    return this.send<Candidate>(`/api/positions/${positionId}/candidates`, "POST", { displayName });
  }

  public async pullSfCandidates(
    positionId: string,
    candidates?: { externalId: string; displayName: string }[]
  ): Promise<{ imported: number; system: string; ranAt: string }> {
    return this.send<{ imported: number; system: string; ranAt: string }>(
      `/api/positions/${positionId}/integrations/successfactors/pull`,
      "POST",
      { candidates: candidates ?? null }
    );
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
    if (!response.ok) {
      await this.throwHttpError(response);
    }
    this.throwIfHtml(response);
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

  public async getEvaluationAudit(evaluationId: string): Promise<EvaluationAudit> {
    return this.get<EvaluationAudit>(`/api/evaluations/${evaluationId}/audit`);
  }

  public async getPublicJob(slug: string): Promise<PublicJob> {
    return this.getPublic<PublicJob>(`/api/public/jobs/${encodeURIComponent(slug)}`);
  }

  public async submitPublicApplication(form: FormData): Promise<PublicApplicationResponse> {
    return this.sendPublic<PublicApplicationResponse>("/api/public/applications", "POST", form);
  }

  public async getPublicApplicationStatus(reference: string): Promise<PublicApplicationStatus> {
    return this.getPublic<PublicApplicationStatus>(`/api/public/applications/${encodeURIComponent(reference)}`);
  }

  public async reuploadPublicCv(reference: string, file: File): Promise<PublicApplicationResponse> {
    const form = new FormData();
    form.append("cv", file);
    return this.sendPublic<PublicApplicationResponse>(
      `/api/public/applications/${encodeURIComponent(reference)}/cv`,
      "POST",
      form
    );
  }

  public async listDecisions(candidateId: string): Promise<Decision[]> {
    return this.get<Decision[]>(`/api/candidates/${candidateId}/decisions`);
  }

  public async recordDecision(candidateId: string, input: RecordDecision): Promise<Decision> {
    return this.send<Decision>(`/api/candidates/${candidateId}/decisions`, "POST", input);
  }

  public async listOffers(): Promise<Offer[]> {
    return this.get<Offer[]>("/api/offers");
  }

  public async listCandidateOffers(candidateId: string): Promise<Offer[]> {
    return this.get<Offer[]>(`/api/candidates/${candidateId}/offers`);
  }

  public async createOffer(candidateId: string, input: { packageText: string; note?: string | null }): Promise<Offer> {
    return this.send<Offer>(`/api/candidates/${candidateId}/offers`, "POST", input);
  }

  public async updateOffer(offerId: string, input: { packageText: string; note?: string | null }): Promise<Offer> {
    return this.send<Offer>(`/api/offers/${offerId}`, "PATCH", input);
  }

  public async sendOffer(offerId: string): Promise<Offer> {
    return this.send<Offer>(`/api/offers/${offerId}/send`, "POST");
  }

  public async acceptOffer(offerId: string): Promise<Offer> {
    return this.send<Offer>(`/api/offers/${offerId}/accept`, "POST");
  }

  public async declineOffer(offerId: string): Promise<Offer> {
    return this.send<Offer>(`/api/offers/${offerId}/decline`, "POST");
  }

  public async withdrawOffer(offerId: string): Promise<Offer> {
    return this.send<Offer>(`/api/offers/${offerId}/withdraw`, "POST");
  }

  public async exportCandidate(candidateId: string): Promise<CandidateExport> {
    return this.get<CandidateExport>(`/compliance/export/${candidateId}`);
  }

  public async inviteInterview(
    candidateId: string,
    positionId: string,
    videoMeetingUrl?: string | null
  ): Promise<{ sessionId: string; inviteUrl: string; expiresAt: string; videoMeetingUrl?: string | null }> {
    return this.send("/api/interviews/invites", "POST", {
      candidateId,
      positionId,
      videoMeetingUrl: videoMeetingUrl?.trim() || null
    });
  }

  public async listInterviews(): Promise<InterviewBoardItem[]> {
    return this.get<InterviewBoardItem[]>("/api/interviews");
  }

  public async getInterviewSessionById(sessionId: string): Promise<InterviewSessionDetail> {
    return this.get<InterviewSessionDetail>(`/api/interviews/${sessionId}`);
  }

  public async getInterview(candidateId: string): Promise<InterviewSessionDetail> {
    return this.get<InterviewSessionDetail>(`/api/candidates/${candidateId}/interview`);
  }

  public async listCandidateInterviews(candidateId: string): Promise<InterviewSessionDetail[]> {
    return this.get<InterviewSessionDetail[]>(`/api/candidates/${candidateId}/interviews`);
  }

  public async deleteInterview(candidateId: string): Promise<void> {
    await this.send<unknown>(`/api/candidates/${candidateId}/interview`, "DELETE");
  }

  public async evaluateInterview(candidateId: string): Promise<{
    status: string;
    interviewScore: number | null;
    summary: string | null;
    turns: { role: string; text: string }[];
    questions: { criterionId: string; prompt: string }[];
  }> {
    return this.send(`/api/candidates/${candidateId}/interview/evaluate`, "POST");
  }

  public async evaluateInterviewSession(sessionId: string): Promise<InterviewSessionDetail> {
    return this.send<InterviewSessionDetail>(`/api/interviews/${sessionId}/evaluate`, "POST");
  }

  public async getInterviewPrep(token: string): Promise<{
    whatToExpect: string;
    estimatedMinutes: number;
    dataUse: string;
    disclosureRequired: boolean;
    videoMeetingUrl?: string | null;
    expiresAt?: string | null;
  }> {
    return this.getPublic(`/api/interviews/public/${encodeURIComponent(token)}/prep`);
  }

  public async getInterviewSession(token: string): Promise<{
    status: string;
    disclosureAccepted: boolean;
    turns: { role: string; text: string }[];
    questions: { criterionId: string; prompt: string }[];
    summary: string | null;
    videoMeetingUrl?: string | null;
    expiresAt?: string | null;
  }> {
    return this.getPublic(`/api/interviews/public/${encodeURIComponent(token)}`);
  }

  public async discloseInterview(token: string): Promise<{
    status: string;
    disclosureAccepted: boolean;
    turns: { role: string; text: string }[];
    questions: { criterionId: string; prompt: string }[];
    summary: string | null;
    videoMeetingUrl?: string | null;
    expiresAt?: string | null;
  }> {
    return this.sendPublicJson(`/api/interviews/public/${encodeURIComponent(token)}/disclose`, "POST");
  }

  public async startInterview(token: string): Promise<{
    status: string;
    disclosureAccepted: boolean;
    turns: { role: string; text: string }[];
    questions: { criterionId: string; prompt: string }[];
    summary: string | null;
    videoMeetingUrl?: string | null;
    expiresAt?: string | null;
  }> {
    return this.sendPublicJson(`/api/interviews/public/${encodeURIComponent(token)}/start`, "POST");
  }

  public async pauseInterview(token: string): Promise<{
    status: string;
    disclosureAccepted: boolean;
    turns: { role: string; text: string }[];
    questions: { criterionId: string; prompt: string }[];
    summary: string | null;
    videoMeetingUrl?: string | null;
    expiresAt?: string | null;
  }> {
    return this.sendPublicJson(`/api/interviews/public/${encodeURIComponent(token)}/pause`, "POST");
  }

  public async resumeInterview(token: string): Promise<{
    status: string;
    disclosureAccepted: boolean;
    turns: { role: string; text: string }[];
    questions: { criterionId: string; prompt: string }[];
    summary: string | null;
    videoMeetingUrl?: string | null;
    expiresAt?: string | null;
  }> {
    return this.sendPublicJson(`/api/interviews/public/${encodeURIComponent(token)}/resume`, "POST");
  }

  public async answerInterview(
    token: string,
    text: string,
    framesBase64?: string[]
  ): Promise<{
    status: string;
    disclosureAccepted: boolean;
    turns: { role: string; text: string }[];
    questions: { criterionId: string; prompt: string }[];
    summary: string | null;
    videoMeetingUrl?: string | null;
    expiresAt?: string | null;
  }> {
    return this.sendPublicJson(`/api/interviews/public/${encodeURIComponent(token)}/answers`, "POST", {
      text,
      framesBase64: framesBase64?.length ? framesBase64 : null
    });
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

  public async seedDemo(): Promise<{ skipped: boolean; positions: number; candidates: number; documents: number }> {
    return this.send("/api/admin/seed-demo", "POST");
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
    const payload = await this.readJson<{ accessToken: string }>(response);
    return payload.accessToken;
  }

  private async getPublic<T>(path: string): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      credentials: "same-origin",
      headers: { Accept: "application/json", "X-Requested-With": "XMLHttpRequest" }
    });
    return this.readJson<T>(response);
  }

  private async sendPublic<T>(path: string, method: string, body: FormData): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method,
      credentials: "same-origin",
      headers: { Accept: "application/json", "X-Requested-With": "XMLHttpRequest" },
      body
    });
    return this.readJson<T>(response);
  }

  private async sendPublicJson<T>(path: string, method: string, body?: unknown): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method,
      credentials: "same-origin",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        "X-Requested-With": "XMLHttpRequest"
      },
      body: body === undefined ? undefined : JSON.stringify(body)
    });
    return this.readJson<T>(response);
  }

  private async get<T>(path: string): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      credentials: "same-origin",
      headers: this.headers()
    });
    return this.readJson<T>(response);
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
    if (response.status === 204) {
      this.throwIfHtml(response);
      if (!response.ok) {
        await this.throwHttpError(response);
      }
      return undefined as T;
    }

    return this.readJson<T>(response);
  }

  private headers(contentType?: string): Headers {
    const headers = new Headers();
    headers.set("Accept", "application/json");
    headers.set("X-Requested-With", "XMLHttpRequest");
    const token = this.getToken();
    if (token) {
      headers.set("Authorization", `Bearer ${token}`);
    }

    if (contentType) {
      headers.set("Content-Type", contentType);
    }

    return headers;
  }

  private async readJson<T>(response: Response): Promise<T> {
    const text = await response.text();
    this.throwIfHtmlText(response, text);
    if (!response.ok) {
      throw new ApiError(response.status, this.errorDetail(response.status, text));
    }

    return text ? (JSON.parse(text) as T) : (undefined as T);
  }

  private errorDetail(status: number, text: string): string {
    try {
      const payload = JSON.parse(text) as { error?: string; detail?: string };
      if (payload.error === "validation" && payload.detail) {
        return `validation:${payload.detail}`;
      }
      const parts = [payload.error, payload.detail].filter((part) => Boolean(part));
      if (parts.length > 0) {
        return parts.join(":");
      }
    } catch {
      /* plain text */
    }

    const trimmed = text.replace(/\s+/g, " ").trim();
    if (trimmed) {
      return `http_${status}:${trimmed.slice(0, 180)}`;
    }

    return `http_${status}:empty_body`;
  }

  private throwIfHtml(response: Response): void {
    const contentType = response.headers.get("content-type") ?? "";
    if (contentType.includes("text/html")) {
      throw new ApiError(response.status, "html_instead_of_json");
    }
  }

  private throwIfHtmlText(response: Response, text: string): void {
    const contentType = response.headers.get("content-type") ?? "";
    if (contentType.includes("text/html") || text.trimStart().startsWith("<")) {
      throw new ApiError(response.status, "html_instead_of_json");
    }
  }

  private async throwHttpError(response: Response): Promise<never> {
    const text = await response.text();
    throw new ApiError(response.status, this.errorDetail(response.status, text));
  }
}

/** Anonymous API client for candidate-facing public endpoints. */
export class PublicApi extends ApiClient {
  public constructor(baseUrl = "") {
    super(baseUrl, () => null);
  }
}
