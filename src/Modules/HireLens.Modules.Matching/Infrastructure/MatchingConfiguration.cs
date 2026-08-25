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
        builder.Property(e => e.RubricVersion).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ModelName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ModelVersion).HasMaxLength(32).IsRequired();
        builder.Property(e => e.CoverageRatio).HasPrecision(9, 4);
        builder.Property(e => e.FailureStage).HasMaxLength(64);
        builder.Property(e => e.FailureMessage).HasMaxLength(1024);
        builder.HasIndex(e => new { e.TenantId, e.CandidateId });
        builder.HasIndex(e => new { e.TenantId, e.Id });
    }
}
