using HireLens.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireLens.Modules.Identity.Infrastructure;

public sealed class TenantUserConfiguration : IEntityTypeConfiguration<TenantUser>
{
    public void Configure(EntityTypeBuilder<TenantUser> builder)
    {
        builder.ToTable("TenantUsers");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.ExternalSubject).HasMaxLength(256).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.HasIndex(u => new { u.TenantId, u.ExternalSubject }).IsUnique();
        builder.HasMany(u => u.Roles)
            .WithOne()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(u => u.Roles).AutoInclude();
    }
}

public sealed class TenantUserRoleConfiguration : IEntityTypeConfiguration<TenantUserRole>
{
    public void Configure(EntityTypeBuilder<TenantUserRole> builder)
    {
        builder.ToTable("TenantUserRoles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RoleName).HasMaxLength(64).IsRequired();
        builder.HasIndex(r => new { r.TenantId, r.UserId, r.RoleName }).IsUnique();
    }
}
