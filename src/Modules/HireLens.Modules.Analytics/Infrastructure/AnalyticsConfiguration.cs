using HireLens.Modules.Analytics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Analytics.Infrastructure;

public sealed class PromptExperimentConfiguration : IEntityTypeConfiguration<PromptExperiment>
{
    public void Configure(EntityTypeBuilder<PromptExperiment> builder)
    {
        builder.ToTable("PromptExperiments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TaskType).HasMaxLength(64).IsRequired();
        builder.HasIndex(e => new { e.TenantId, e.TaskType }).IsUnique();
    }
}

public sealed class BenchmarkRunConfiguration : IEntityTypeConfiguration<BenchmarkRun>
{
    public void Configure(EntityTypeBuilder<BenchmarkRun> builder)
    {
        builder.ToTable("BenchmarkRuns");
        builder.HasKey(r => r.Id);
    }
}

public sealed class ParseCacheConfiguration : IEntityTypeConfiguration<ParseCache>
{
    public void Configure(EntityTypeBuilder<ParseCache> builder)
    {
        builder.ToTable("ParseCaches");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ContentHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(c => new { c.TenantId, c.ContentHash }).IsUnique();
    }
}
