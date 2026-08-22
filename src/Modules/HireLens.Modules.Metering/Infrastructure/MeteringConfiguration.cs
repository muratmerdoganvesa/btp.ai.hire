using HireLens.Modules.Metering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Metering.Infrastructure;

public sealed class TenantQuotaConfiguration : IEntityTypeConfiguration<TenantQuota>
{
    public void Configure(EntityTypeBuilder<TenantQuota> builder)
    {
        builder.ToTable("TenantQuotas");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.OveragePolicy).HasMaxLength(16).IsRequired();
    }
}
