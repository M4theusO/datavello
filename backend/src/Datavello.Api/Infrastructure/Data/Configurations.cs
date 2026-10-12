using Datavello.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Datavello.Api.Infrastructure.Data;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.Property(u => u.FullName).HasMaxLength(150).IsRequired();
    }
}

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.Property(c => c.Name).HasMaxLength(150).IsRequired();
        b.Property(c => c.Document).HasMaxLength(30);
        b.Property(c => c.Currency).HasMaxLength(3).IsRequired();
    }
}

internal sealed class CompanyMemberConfiguration : IEntityTypeConfiguration<CompanyMember>
{
    public void Configure(EntityTypeBuilder<CompanyMember> b)
    {
        b.HasIndex(m => new { m.CompanyId, m.UserId }).IsUnique();
        b.HasIndex(m => m.UserId);
        b.HasOne(m => m.Company).WithMany(c => c.Members).HasForeignKey(m => m.CompanyId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(m => m.User).WithMany(u => u.Memberships).HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(c => c.Name).HasMaxLength(80).IsRequired();
        b.HasIndex(c => new { c.CompanyId, c.Name }).IsUnique();
        b.HasOne<Company>().WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        b.Property(t => t.Description).HasMaxLength(200).IsRequired();
        b.Property(t => t.Amount).HasPrecision(18, 2);
        b.Property(t => t.Notes).HasMaxLength(1000);
        b.ToTable(t => t.HasCheckConstraint("CK_Transactions_Amount_Positive", "\"Amount\" > 0"));

        b.HasIndex(t => new { t.CompanyId, t.Date });
        b.HasIndex(t => new { t.CompanyId, t.Type, t.Date });
        b.HasIndex(t => new { t.CompanyId, t.CategoryId });
        b.HasIndex(t => t.ImportJobId);

        b.HasOne<Company>().WithMany().HasForeignKey(t => t.CompanyId).OnDelete(DeleteBehavior.Cascade);
        // A category in use cannot be deleted; the user must reclassify its transactions first.
        b.HasOne(t => t.Category).WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ImportJob>().WithMany().HasForeignKey(t => t.ImportJobId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> b)
    {
        b.Property(j => j.FileName).HasMaxLength(255).IsRequired();
        b.Property(j => j.FileType).HasMaxLength(10).IsRequired();
        b.Property(j => j.StagingData).HasColumnType("jsonb");
        b.Property(j => j.Options).HasColumnType("jsonb");
        b.Property(j => j.Message).HasMaxLength(1000);
        b.HasIndex(j => new { j.CompanyId, j.CreatedAt });

        b.HasOne<Company>().WithMany().HasForeignKey(j => j.CompanyId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(j => j.User).WithMany().HasForeignKey(j => j.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(j => j.Errors).WithOne().HasForeignKey(e => e.ImportJobId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ImportRowErrorConfiguration : IEntityTypeConfiguration<ImportRowError>
{
    public void Configure(EntityTypeBuilder<ImportRowError> b)
    {
        b.Property(e => e.Field).HasMaxLength(50);
        b.Property(e => e.Message).HasMaxLength(500).IsRequired();
        b.Property(e => e.RawValue).HasMaxLength(500);
        b.HasIndex(e => new { e.ImportJobId, e.RowNumber });
        b.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DashboardTemplateConfiguration : IEntityTypeConfiguration<DashboardTemplate>
{
    public static readonly Guid DefaultFinancialTemplateId = new("0199d6a0-0000-7000-8000-000000000001");
    public const string DefaultFinancialTemplateKey = "financial-default";

    public void Configure(EntityTypeBuilder<DashboardTemplate> b)
    {
        b.Property(t => t.Key).HasMaxLength(50).IsRequired();
        b.Property(t => t.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(t => t.Key).IsUnique();

        b.HasData(new DashboardTemplate
        {
            Id = DefaultFinancialTemplateId,
            Key = DefaultFinancialTemplateKey,
            Name = "Financeiro padrão",
            DefaultPeriod = DashboardPeriod.Last6Months,
            Widgets = [.. WidgetCatalog.All]
        });
    }
}

internal sealed class DashboardConfiguration : IEntityTypeConfiguration<Dashboard>
{
    public void Configure(EntityTypeBuilder<Dashboard> b)
    {
        b.Property(d => d.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(d => d.CompanyId);
        b.HasOne<Company>().WithMany().HasForeignKey(d => d.CompanyId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(d => d.Template).WithMany().HasForeignKey(d => d.TemplateId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(d => d.Widgets).WithOne().HasForeignKey(w => w.DashboardId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class WidgetConfigConfiguration : IEntityTypeConfiguration<WidgetConfig>
{
    public void Configure(EntityTypeBuilder<WidgetConfig> b)
    {
        b.Property(w => w.WidgetType).HasMaxLength(50).IsRequired();
        b.Property(w => w.Settings).HasColumnType("jsonb");
        b.HasIndex(w => new { w.DashboardId, w.WidgetType }).IsUnique();
        b.HasOne<Company>().WithMany().HasForeignKey(w => w.CompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}
