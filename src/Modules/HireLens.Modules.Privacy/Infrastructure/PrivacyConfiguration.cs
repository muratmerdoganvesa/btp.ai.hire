using HireLens.Modules.Privacy.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Privacy.Infrastructure;

public sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("ConsentRecords");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Purpose).HasMaxLength(64).IsRequired();
        builder.Property(c => c.TextVersion).HasMaxLength(32);
        builder.Property(c => c.ClientIp).HasMaxLength(64);
    }
}
