using System.Reflection;
using Datavello.Api.Domain;
using Datavello.Api.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Datavello.Api.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant tenant)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMember> CompanyMembers => Set<CompanyMember>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<ImportRowError> ImportRowErrors => Set<ImportRowError>();
    public DbSet<DashboardTemplate> DashboardTemplates => Set<DashboardTemplate>();
    public DbSet<Dashboard> Dashboards => Set<Dashboard>();
    public DbSet<WidgetConfig> WidgetConfigs => Set<WidgetConfig>();

    /// <summary>Read by the global query filters; EF evaluates it per query, not once per model.</summary>
    private Guid? CurrentCompanyId => tenant.CompanyId;

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums are stored as readable strings.
        builder.Properties<CompanyRole>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<CategoryKind>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<TransactionType>().HaveConversion<string>().HaveMaxLength(20);
        builder.Properties<ImportStatus>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<DashboardPeriod>().HaveConversion<string>().HaveMaxLength(30);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        var applyFilter = typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType) && t.BaseType is null))
        {
            applyFilter.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
        }
    }

    /// <summary>
    /// Every tenant-owned query is restricted to the active company. With no active
    /// company the filter matches nothing, so a missing tenant never leaks data.
    /// </summary>
    private void ApplyTenantFilter<TEntity>(ModelBuilder builder) where TEntity : class, ITenantOwned =>
        builder.Entity<TEntity>().HasQueryFilter(e => e.CompanyId == CurrentCompanyId);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyRules()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is ITenantOwned owned && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                EnforceTenant(entry.State, owned);
            }

            if (entry.Entity is IHasTimestamps stamped)
            {
                if (entry.State == EntityState.Added)
                {
                    stamped.CreatedAt = now;
                    stamped.UpdatedAt = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Property(nameof(IHasTimestamps.CreatedAt)).IsModified = false;
                    stamped.UpdatedAt = now;
                }
            }
        }
    }

    private void EnforceTenant(EntityState state, ITenantOwned owned)
    {
        var active = CurrentCompanyId;

        if (state == EntityState.Added && owned.CompanyId == Guid.Empty)
        {
            owned.CompanyId = active ?? throw new InvalidOperationException(
                $"Cannot create {owned.GetType().Name} without an active company.");
            return;
        }

        if (owned.CompanyId == Guid.Empty)
        {
            throw new InvalidOperationException($"{owned.GetType().Name} has no company.");
        }

        // With an active tenant, writes are only allowed inside that tenant.
        // Without one (trusted system code such as registration or seeding) the explicit CompanyId is kept.
        if (active is not null && owned.CompanyId != active)
        {
            throw new InvalidOperationException(
                $"Cross-tenant write blocked for {owned.GetType().Name}.");
        }
    }
}
