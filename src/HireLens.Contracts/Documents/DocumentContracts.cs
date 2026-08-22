namespace HireLens.Contracts.Documents;

public sealed record UploadSessionRequest(string FileName, string ContentType, long SizeBytes);

public sealed record UploadSessionDto(Guid DocumentId, string UploadUrl, string Method);

public sealed record JobStatusDto(Guid JobId, string Kind, string Status, string? Error, DateTimeOffset UpdatedAt);

public sealed record DocumentTextSnapshot(Guid DocumentId, Guid CandidateId, Guid PositionId, string MaskedText);

public interface IDocumentTextPort
{
    Task<DocumentTextSnapshot?> GetMaskedTextAsync(Guid documentId, CancellationToken cancellationToken);
}

public interface IParseCache
{
    Task<string?> TryGetAsync(string contentHash, CancellationToken cancellationToken);

    Task PutAsync(string contentHash, string maskedText, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);
}
