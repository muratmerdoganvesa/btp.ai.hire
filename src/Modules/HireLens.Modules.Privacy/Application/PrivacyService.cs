using HireLens.Contracts.Privacy;
using HireLens.Infrastructure.Persistence;
using HireLens.Modules.Privacy.Domain;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Modules.Privacy.Application;

public interface IPrivacyService : IPrivacyConsentPort
{
}

public sealed class PrivacyService(HireLensDbContext db, ITenantContext tenant, IClock clock) : IPrivacyService
{
    public async Task<bool> HasAsync(Guid candidateId, string purpose, CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        return await db.Set<ConsentRecord>()
            .AnyAsync(c => c.CandidateId == candidateId && c.Purpose == purpose, cancellationToken);
    }

    public async Task<ConsentRecordDto> GrantAsync(Guid candidateId, string purpose, CancellationToken cancellationToken) =>
        await GrantAsync(candidateId, purpose, textVersion: null, clientIp: null, cancellationToken);

    public async Task<ConsentRecordDto> GrantAsync(
        Guid candidateId,
        string purpose,
        string? textVersion,
        string? clientIp,
        CancellationToken cancellationToken)
    {
        RepositoryGuard.RequireTenant(tenant);
        var existing = await db.Set<ConsentRecord>()
            .SingleOrDefaultAsync(c => c.CandidateId == candidateId && c.Purpose == purpose, cancellationToken);
        if (existing is not null)
        {
            return new ConsentRecordDto(
                existing.Id,
                existing.CandidateId,
                existing.Purpose,
                existing.AcceptedAt,
                existing.TextVersion,
                existing.ClientIp);
        }

        var row = ConsentRecord.Grant(tenant.TenantId, candidateId, purpose, clock.UtcNow, textVersion, clientIp);
        db.Set<ConsentRecord>().Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return new ConsentRecordDto(row.Id, row.CandidateId, row.Purpose, row.AcceptedAt, row.TextVersion, row.ClientIp);
    }
}
