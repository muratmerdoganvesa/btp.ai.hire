using HireLens.Modules.Matching.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Matching.Infrastructure;

public sealed class EvaluationConfiguration : IEntityTypeConfiguration<Evaluation>
{
    public void Configure(EntityTypeBuilder<Evaluation> builder)
    {
        builder.ToTable("Evaluations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.PromptVersion).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.CandidateId });
    }
}
