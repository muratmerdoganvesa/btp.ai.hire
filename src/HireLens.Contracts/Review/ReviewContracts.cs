namespace HireLens.Contracts.Review;

public sealed record DecisionDto(
    Guid Id,
    Guid CandidateId,
    string Outcome,
    string Rationale,
    DateTimeOffset DecidedAt);

public sealed record RecordDecisionRequest(string Outcome, string Rationale);
