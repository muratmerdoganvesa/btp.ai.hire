using HireLens.Modules.Configuration.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Configuration.Infrastructure;

public sealed class TenantThemeConfiguration : IEntityTypeConfiguration<TenantTheme>
{
    public void Configure(EntityTypeBuilder<TenantTheme> builder)
    {
        builder.ToTable("TenantThemes");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.LogoUrl).HasMaxLength(512);
    }
}

public sealed class RubricTemplateConfiguration : IEntityTypeConfiguration<RubricTemplate>
{
    public void Configure(EntityTypeBuilder<RubricTemplate> builder)
    {
        builder.ToTable("RubricTemplates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.HasMany(t => t.Criteria).WithOne().HasForeignKey(c => c.TemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Criteria).HasField("_criteria").AutoInclude();
    }
}

public sealed class RubricTemplateCriterionConfiguration : IEntityTypeConfiguration<RubricTemplateCriterion>
{
    public void Configure(EntityTypeBuilder<RubricTemplateCriterion> builder)
    {
        builder.ToTable("RubricTemplateCriteria");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
    }
}

public sealed class ModelPolicyConfiguration : IEntityTypeConfiguration<ModelPolicy>
{
    public void Configure(EntityTypeBuilder<ModelPolicy> builder)
    {
        builder.ToTable("ModelPolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.TaskType).HasMaxLength(64).IsRequired();
        builder.Property(p => p.ModelId).HasMaxLength(128).IsRequired();
        builder.HasIndex(p => new { p.TenantId, p.TaskType }).IsUnique();
    }
}

public sealed class PromptOverrideConfiguration : IEntityTypeConfiguration<PromptOverride>
{
    public void Configure(EntityTypeBuilder<PromptOverride> builder)
    {
        builder.ToTable("PromptOverrides");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.TaskType).HasMaxLength(64).IsRequired();
        builder.Property(p => p.Version).HasMaxLength(32).IsRequired();
        builder.HasIndex(p => new { p.TenantId, p.TaskType }).IsUnique();
    }
}
