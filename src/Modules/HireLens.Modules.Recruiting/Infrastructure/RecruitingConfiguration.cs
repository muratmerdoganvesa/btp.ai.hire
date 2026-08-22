using HireLens.Modules.Recruiting.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Recruiting.Infrastructure;

public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("Positions");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.JobDescription).IsRequired();
        builder.HasMany(p => p.Criteria).WithOne().HasForeignKey(c => c.PositionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Criteria).HasField("_criteria").AutoInclude();
    }
}

public sealed class PositionCriterionConfiguration : IEntityTypeConfiguration<PositionCriterion>
{
    public void Configure(EntityTypeBuilder<PositionCriterion> builder)
    {
        builder.ToTable("PositionCriteria");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(2000);
    }
}
