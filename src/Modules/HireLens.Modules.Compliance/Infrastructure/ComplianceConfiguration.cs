using HireLens.Modules.Compliance.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Compliance.Infrastructure;

public sealed class DataDeletionRequestConfiguration : IEntityTypeConfiguration<DataDeletionRequest>
{
    public void Configure(EntityTypeBuilder<DataDeletionRequest> builder)
    {
        builder.ToTable("DataDeletionRequests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.Status).HasMaxLength(32).IsRequired();
    }
}
