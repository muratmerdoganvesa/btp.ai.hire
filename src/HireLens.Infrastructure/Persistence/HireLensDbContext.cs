using System.Linq.Expressions;
using System.Reflection;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Infrastructure.Persistence;

public sealed class HireLensDbContext(
    DbContextOptions<HireLensDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public const string ModuleAssemblyMarker = "HireLens.Modules.";

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<AiInvocation> AiInvocations => Set<AiInvocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HireLensDbContext).Assembly);

        foreach (var assembly in DiscoverModuleAssemblies())
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }

        ApplyTenantFilters(modelBuilder);
        ApplyTenantIndexes(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(HireLensDbContext)
                .GetMethod(nameof(SetTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(this, [modelBuilder]);
        }
    }

    private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        // Closure over this instance so the current request's tenant is evaluated
        // at query time, not at model build time.
        Expression<Func<TEntity, bool>> filter = entity =>
            tenantContext.IsResolved && entity.TenantId == tenantContext.TenantId;

        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }

    private static void ApplyTenantIndexes(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var idProperty = entityType.FindProperty("Id");
            if (idProperty is null)
            {
                continue;
            }

            modelBuilder.Entity(entityType.ClrType)
                .HasIndex("TenantId", "Id")
                .IsUnique();
        }
    }

    private static IEnumerable<Assembly> DiscoverModuleAssemblies()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly =>
                assembly.GetName().Name is { } name &&
                name.StartsWith(ModuleAssemblyMarker, StringComparison.Ordinal));
    }
}
