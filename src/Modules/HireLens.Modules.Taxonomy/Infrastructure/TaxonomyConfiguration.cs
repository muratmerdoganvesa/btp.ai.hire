using HireLens.Modules.Taxonomy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Taxonomy.Infrastructure;

public sealed class SkillTermConfiguration : IEntityTypeConfiguration<SkillTerm>
{
    public void Configure(EntityTypeBuilder<SkillTerm> builder)
    {
        builder.ToTable("SkillTerms");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.RawName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.CanonicalName).HasMaxLength(200).IsRequired();
        builder.HasIndex(s => new { s.TenantId, s.RawName }).IsUnique();
    }
}
