using HireLens.Contracts.Notifications;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Notification.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Notification.Application;

public interface INotificationService
{
    Task<Result<IReadOnlyList<InAppNotificationDto>>> ListAsync(CancellationToken cancellationToken);

    Task RemindRecruitersAsync(CancellationToken cancellationToken);
}

public sealed class NotificationService(
    HireLensDbContext db,
    ITenantContext tenant,
    IClock clock) : INotificationSink, INotificationService
{
    public string? LastInviteUrl { get; private set; }

    public async Task SendAsync(NotificationDraft draft, CancellationToken cancellationToken)
    {
        if (tenant.IsResolved)
        {
            db.Set<InAppNotification>().Add(
                InAppNotification.Raise(tenant.TenantId, draft.CandidateId, draft.Subject, draft.Body, clock.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(draft.InviteUrl))
        {
            LastInviteUrl = draft.InviteUrl;
        }
    }

    public async Task<Result<IReadOnlyList<InAppNotificationDto>>> ListAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var rows = await db.Set<InAppNotification>().OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<InAppNotificationDto>>(
            rows.Select(n => new InAppNotificationDto(n.Id, n.Title, n.Body, n.Read, n.CreatedAt)).ToList());
    }

    public async Task RemindRecruitersAsync(CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        await SendAsync(
            new NotificationDraft(null, "in_app", "Pending review", "Candidates are waiting for a human decision.", null),
            cancellationToken);
    }
}
