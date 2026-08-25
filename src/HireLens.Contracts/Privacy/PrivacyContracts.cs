namespace HireLens.Contracts.Privacy;

public sealed record ConsentRecordDto(
    Guid Id,
    Guid CandidateId,
    string Purpose,
    DateTimeOffset AcceptedAt,
    string? TextVersion = null,
    string? ClientIp = null);

public interface IPrivacyConsentPort
{
    Task<bool> HasAsync(Guid candidateId, string purpose, CancellationToken cancellationToken);

    Task<ConsentRecordDto> GrantAsync(Guid candidateId, string purpose, CancellationToken cancellationToken);

    Task<ConsentRecordDto> GrantAsync(
        Guid candidateId,
        string purpose,
        string textVersion,
        string? clientIp,
        CancellationToken cancellationToken);
}
