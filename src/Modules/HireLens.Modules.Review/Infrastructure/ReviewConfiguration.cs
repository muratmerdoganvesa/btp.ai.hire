using HireLens.Modules.Review.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Review.Infrastructure;

public sealed class DecisionConfiguration : IEntityTypeConfiguration<Decision>
{
    public void Configure(EntityTypeBuilder<Decision> builder)
    {
        builder.ToTable("Decisions");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Outcome).HasMaxLength(16).IsRequired();
        builder.Property(d => d.Rationale).HasMaxLength(4000).IsRequired();
    }
}
