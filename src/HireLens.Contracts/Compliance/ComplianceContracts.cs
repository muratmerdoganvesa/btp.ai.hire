namespace HireLens.Contracts.Compliance;

public sealed record DataDeletionRequestDto(
    Guid Id,
    Guid CandidateId,
    string Status,
    DateTimeOffset RequestedAt);

public sealed record CreateDeletionRequest(Guid CandidateId, string Reason);

public sealed record CandidateExportDto(
    Guid CandidateId,
    string DisplayName,
    object Payload,
    DateTimeOffset ExportedAt);
