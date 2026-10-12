using Datavello.Api.Domain;
using Datavello.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Datavello.Api.Features.Companies;

/// <summary>
/// Creates a ready-to-use company: owner membership, default categories and the
/// default dashboard. Adds entities to the context; the caller saves (inside its transaction).
/// </summary>
public sealed class CompanyProvisioningService(AppDbContext db)
{
    public static readonly IReadOnlyList<(string Name, CategoryKind Kind)> DefaultCategories =
    [
        ("Vendas", CategoryKind.Income),
        ("Serviços", CategoryKind.Income),
        ("Outras receitas", CategoryKind.Income),
        ("Fornecedores", CategoryKind.Expense),
        ("Aluguel", CategoryKind.Expense),
        ("Salários", CategoryKind.Expense),
        ("Impostos", CategoryKind.Expense),
        ("Marketing", CategoryKind.Expense),
        ("Água, luz e internet", CategoryKind.Expense),
        ("Outras despesas", CategoryKind.Expense)
    ];

    public async Task<Company> ProvisionAsync(AppUser owner, string companyName, CancellationToken ct = default)
    {
        var company = new Company { Name = companyName.Trim() };
        db.Companies.Add(company);
        db.CompanyMembers.Add(new CompanyMember { CompanyId = company.Id, UserId = owner.Id, Role = CompanyRole.Owner });

        foreach (var (name, kind) in DefaultCategories)
        {
            db.Categories.Add(new Category { CompanyId = company.Id, Name = name, Kind = kind, IsDefault = true });
        }

        var template = await db.DashboardTemplates.AsNoTracking()
            .SingleAsync(t => t.Key == DashboardTemplateConfiguration.DefaultFinancialTemplateKey, ct);

        var dashboard = new Dashboard
        {
            CompanyId = company.Id,
            Name = "Visão geral",
            TemplateId = template.Id,
            DefaultPeriod = template.DefaultPeriod,
            IsDefault = true
        };
        db.Dashboards.Add(dashboard);

        for (var i = 0; i < template.Widgets.Length; i++)
        {
            db.WidgetConfigs.Add(new WidgetConfig
            {
                CompanyId = company.Id,
                DashboardId = dashboard.Id,
                WidgetType = template.Widgets[i],
                Position = i,
                IsEnabled = true
            });
        }

        return company;
    }
}
