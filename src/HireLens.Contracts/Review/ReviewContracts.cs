namespace HireLens.Contracts.Review;

public sealed record DecisionDto(
    Guid Id,
    Guid CandidateId,
    string Outcome,
    string Rationale,
    DateTimeOffset DecidedAt);

public sealed record RecordDecisionRequest(string Outcome, string Rationale);

public sealed record OfferDto(
    Guid Id,
    Guid CandidateId,
    Guid PositionId,
    string CandidateName,
    string PositionTitle,
    string Status,
    string PackageText,
    string? Note,
    int? ScoreSnapshot,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? RespondedAt);

public sealed record CreateOfferRequest(string PackageText, string? Note);

public sealed record UpdateOfferRequest(string PackageText, string? Note);
