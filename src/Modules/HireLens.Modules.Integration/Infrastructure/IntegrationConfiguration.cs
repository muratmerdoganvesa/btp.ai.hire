using HireLens.Modules.Integration.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Integration.Infrastructure;

public sealed class IntegrationRunConfiguration : IEntityTypeConfiguration<IntegrationRun>
{
    public void Configure(EntityTypeBuilder<IntegrationRun> builder)
    {
        builder.ToTable("IntegrationRuns");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.System).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(32).IsRequired();
    }
}
