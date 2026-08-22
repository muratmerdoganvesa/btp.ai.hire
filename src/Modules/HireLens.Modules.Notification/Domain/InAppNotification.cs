using HireLens.SharedKernel;

namespace HireLens.Modules.Notification.Domain;

public sealed class InAppNotification : ITenantEntity
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid? CandidateId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public bool Read { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static InAppNotification Raise(Guid tenantId, Guid? candidateId, string title, string body, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CandidateId = candidateId,
            Title = title,
            Body = body,
            CreatedAt = now
        };

    public void MarkRead() => Read = true;
}
