using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Candidate.Infrastructure;

public sealed class CandidateConfiguration : IEntityTypeConfiguration<Domain.Candidate>
{
    public void Configure(EntityTypeBuilder<Domain.Candidate> builder)
    {
        builder.ToTable("Candidates");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(c => new { c.TenantId, c.PositionId });
    }
}
