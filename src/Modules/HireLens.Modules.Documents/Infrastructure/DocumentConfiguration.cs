using HireLens.Modules.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Documents.Infrastructure;

public sealed class CvDocumentConfiguration : IEntityTypeConfiguration<CvDocument>
{
    public void Configure(EntityTypeBuilder<CvDocument> builder)
    {
        builder.ToTable("CvDocuments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.ObjectKey).HasMaxLength(512).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(256).IsRequired();
        builder.Property(d => d.Status).HasMaxLength(32).IsRequired();
    }
}

public sealed class AnalysisJobConfiguration : IEntityTypeConfiguration<AnalysisJob>
{
    public void Configure(EntityTypeBuilder<AnalysisJob> builder)
    {
        builder.ToTable("AnalysisJobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Kind).HasMaxLength(32).IsRequired();
        builder.Property(j => j.Status).HasMaxLength(32).IsRequired();
        builder.Property(j => j.Error).HasMaxLength(1000);
    }
}
