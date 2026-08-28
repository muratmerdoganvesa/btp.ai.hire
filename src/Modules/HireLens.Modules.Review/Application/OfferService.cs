using HireLens.Contracts.Candidates;
using HireLens.Contracts.Notifications;
using HireLens.Contracts.Recruiting;
using HireLens.Contracts.Review;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Review.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Review.Application;

public interface IOfferService
{
    Task<Result<IReadOnlyList<OfferDto>>> ListAsync(CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<OfferDto>>> ListForCandidateAsync(Guid candidateId, CancellationToken cancellationToken);

    Task<Result<OfferDto>> CreateAsync(Guid candidateId, CreateOfferRequest request, CancellationToken cancellationToken);

    Task<Result<OfferDto>> UpdateDraftAsync(Guid offerId, UpdateOfferRequest request, CancellationToken cancellationToken);

    Task<Result<OfferDto>> SendAsync(Guid offerId, CancellationToken cancellationToken);

    Task<Result<OfferDto>> AcceptAsync(Guid offerId, CancellationToken cancellationToken);

    Task<Result<OfferDto>> DeclineAsync(Guid offerId, CancellationToken cancellationToken);

    Task<Result<OfferDto>> WithdrawAsync(Guid offerId, CancellationToken cancellationToken);
}

public sealed class OfferService(
    HireLensDbContext db,
    ITenantContext tenant,
    IClock clock,
    ICandidateReadPort candidates,
    IPositionReadPort positions,
    ICandidateEvaluationSummaryPort summaries,
    INotificationSink notifications) : IOfferService
{
    public Task<Result<IReadOnlyList<OfferDto>>> ListAsync(CancellationToken cancellationToken) =>
        ListInternalAsync(null, cancellationToken);

    public Task<Result<IReadOnlyList<OfferDto>>> ListForCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken) =>
        ListInternalAsync(candidateId, cancellationToken);

    public async Task<Result<OfferDto>> CreateAsync(
        Guid candidateId,
        CreateOfferRequest request,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var candidate = await candidates.GetAsync(candidateId, cancellationToken);
        if (candidate is null)
        {
            return Result.Failure<OfferDto>(Error.NotFound("Candidate was not found."));
        }

        var openExists = await db.Set<Offer>()
            .AnyAsync(o => o.CandidateId == candidateId && (o.Status == "draft" || o.Status == "sent"), cancellationToken);
        if (openExists)
        {
            return Result.Failure<OfferDto>(Error.Conflict("This candidate already has a draft or sent offer."));
        }

        var summaryMap = await summaries.GetForCandidatesAsync([candidateId], cancellationToken);
        var score = summaryMap.GetValueOrDefault(candidateId)?.OverallScore;
        var created = Offer.Draft(
            tenant.TenantId,
            candidateId,
            candidate.PositionId,
            request.PackageText,
            request.Note,
            score,
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<OfferDto>(created.Error);
        }

        db.Set<Offer>().Add(created.Value);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await ToDtoAsync(created.Value, cancellationToken));
    }

    public async Task<Result<OfferDto>> UpdateDraftAsync(
        Guid offerId,
        UpdateOfferRequest request,
        CancellationToken cancellationToken)
    {
        var offer = await GetOwnedAsync(offerId, cancellationToken);
        if (offer is null)
        {
            return Result.Failure<OfferDto>(Error.NotFound("Offer was not found."));
        }

        var updated = offer.UpdateDraft(request.PackageText, request.Note, clock.UtcNow);
        if (updated.IsFailure)
        {
            return Result.Failure<OfferDto>(updated.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await ToDtoAsync(offer, cancellationToken));
    }

    public Task<Result<OfferDto>> SendAsync(Guid offerId, CancellationToken cancellationToken) =>
        TransitionAsync(offerId, offer => offer.Send(clock.UtcNow), notify: true, cancellationToken);

    public Task<Result<OfferDto>> AcceptAsync(Guid offerId, CancellationToken cancellationToken) =>
        TransitionAsync(offerId, offer => offer.Accept(clock.UtcNow), notify: false, cancellationToken);

    public Task<Result<OfferDto>> DeclineAsync(Guid offerId, CancellationToken cancellationToken) =>
        TransitionAsync(offerId, offer => offer.Decline(clock.UtcNow), notify: false, cancellationToken);

    public Task<Result<OfferDto>> WithdrawAsync(Guid offerId, CancellationToken cancellationToken) =>
        TransitionAsync(offerId, offer => offer.Withdraw(clock.UtcNow), notify: false, cancellationToken);

    private async Task<Result<IReadOnlyList<OfferDto>>> ListInternalAsync(
        Guid? candidateId,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var query = db.Set<Offer>().AsNoTracking().AsQueryable();
        if (candidateId is { } id)
        {
            query = query.Where(o => o.CandidateId == id);
        }

        var rows = await query.OrderByDescending(o => o.UpdatedAt).ToListAsync(cancellationToken);
        var dtos = new List<OfferDto>(rows.Count);
        foreach (var row in rows)
        {
            dtos.Add(await ToDtoAsync(row, cancellationToken));
        }

        return Result.Success<IReadOnlyList<OfferDto>>(dtos);
    }

    private async Task<Result<OfferDto>> TransitionAsync(
        Guid offerId,
        Func<Offer, Result> act,
        bool notify,
        CancellationToken cancellationToken)
    {
        var offer = await GetOwnedAsync(offerId, cancellationToken);
        if (offer is null)
        {
            return Result.Failure<OfferDto>(Error.NotFound("Offer was not found."));
        }

        var result = act(offer);
        if (result.IsFailure)
        {
            return Result.Failure<OfferDto>(result.Error);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (notify)
        {
            var dto = await ToDtoAsync(offer, cancellationToken);
            await notifications.SendAsync(
                new NotificationDraft(
                    offer.CandidateId,
                    "email",
                    $"HireLens offer: {dto.PositionTitle}",
                    dto.PackageText,
                    null),
                cancellationToken);
            return Result.Success(dto);
        }

        return Result.Success(await ToDtoAsync(offer, cancellationToken));
    }

    private async Task<Offer?> GetOwnedAsync(Guid offerId, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        return await db.Set<Offer>().FirstOrDefaultAsync(o => o.Id == offerId, cancellationToken);
    }

    private async Task<OfferDto> ToDtoAsync(Offer offer, CancellationToken cancellationToken)
    {
        var candidate = await candidates.GetAsync(offer.CandidateId, cancellationToken);
        var position = await positions.GetAsync(offer.PositionId, cancellationToken);
        return new OfferDto(
            offer.Id,
            offer.CandidateId,
            offer.PositionId,
            candidate?.DisplayName ?? "—",
            position?.Title ?? "—",
            offer.Status,
            offer.PackageText,
            offer.Note,
            offer.ScoreSnapshot,
            offer.CreatedAt,
            offer.UpdatedAt,
            offer.SentAt,
            offer.RespondedAt);
    }
}
