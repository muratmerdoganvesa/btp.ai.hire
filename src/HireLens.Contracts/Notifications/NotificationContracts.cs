namespace HireLens.Contracts.Notifications;

public sealed record NotificationDraft(
    Guid? CandidateId,
    string Channel,
    string Subject,
    string Body,
    string? InviteUrl);

public sealed record InAppNotificationDto(
    Guid Id,
    string Title,
    string Body,
    bool Read,
    DateTimeOffset CreatedAt);

public interface INotificationSink
{
    Task SendAsync(NotificationDraft draft, CancellationToken cancellationToken);

    string? LastInviteUrl { get; }
}
