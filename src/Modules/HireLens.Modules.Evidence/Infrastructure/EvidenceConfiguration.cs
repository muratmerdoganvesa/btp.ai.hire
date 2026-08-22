using HireLens.Modules.Evidence.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Evidence.Infrastructure;

public sealed class CriterionScoreConfiguration : IEntityTypeConfiguration<CriterionScore>
{
    public void Configure(EntityTypeBuilder<CriterionScore> builder)
    {
        builder.ToTable("CriterionScores");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.EvidenceStatus).HasConversion<string>().HasMaxLength(24);
        builder.HasMany(s => s.Evidence).WithOne().HasForeignKey(e => e.CriterionScoreId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Evidence).HasField("_evidence").AutoInclude();
    }
}

public sealed class EvidenceItemConfiguration : IEntityTypeConfiguration<EvidenceItem>
{
    public void Configure(EntityTypeBuilder<EvidenceItem> builder)
    {
        builder.ToTable("EvidenceItems");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Source).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Quote).HasMaxLength(2000).IsRequired();
    }
}
