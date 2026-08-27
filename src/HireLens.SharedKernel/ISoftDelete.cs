namespace HireLens.SharedKernel;

/// <summary>
/// Operational soft-delete. Filtered out of list/get queries; not a KVKK anonymize.
/// </summary>
public interface ISoftDelete
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAt { get; }
}
