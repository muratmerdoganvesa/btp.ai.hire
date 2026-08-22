using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Infrastructure.Persistence;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Action).HasMaxLength(32).IsRequired();
        builder.Property(e => e.EntityType).HasMaxLength(128).IsRequired();
        builder.Property(e => e.EntityId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ActorSubject).HasMaxLength(256);
        builder.Property(e => e.CorrelationId).HasMaxLength(64);
        builder.HasIndex(e => new { e.TenantId, e.OccurredAt });
    }
}

public sealed class AiInvocationConfiguration : IEntityTypeConfiguration<AiInvocation>
{
    public void Configure(EntityTypeBuilder<AiInvocation> builder)
    {
        builder.ToTable("AiInvocations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TaskType).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ModelId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.PromptVersion).HasMaxLength(32).IsRequired();
        builder.Property(e => e.PromptHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.EstimatedCost).HasPrecision(18, 6);
        builder.Property(e => e.CorrelationId).HasMaxLength(64);
        builder.HasIndex(e => new { e.TenantId, e.OccurredAt });
    }
}
