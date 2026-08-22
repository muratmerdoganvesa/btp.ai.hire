namespace HireLens.Contracts.Integration;

public sealed record SfPositionSync(string ExternalId, string Title, string JobDescription);

public sealed record SfCandidateSync(string ExternalId, string DisplayName, string PositionExternalId);

public sealed record IntegrationRunDto(Guid Id, string System, string Status, int Imported, DateTimeOffset RanAt);
