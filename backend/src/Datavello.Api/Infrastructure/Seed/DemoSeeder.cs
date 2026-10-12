using Datavello.Api.Domain;
using Datavello.Api.Features.Companies;
using Datavello.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Datavello.Api.Infrastructure.Seed;

/// <summary>
/// Development-only demo data: one fictitious company, one demo user and ~14 months of
/// transactions. Safe to run repeatedly — it only ever touches the demo user's own company
/// and never deletes anything else.
/// </summary>
public sealed class DemoSeeder(
    AppDbContext db,
    UserManager<AppUser> userManager,
    CompanyProvisioningService provisioning,
    IConfiguration configuration,
    ILogger<DemoSeeder> logger)
{
    public const string DemoEmail = "demo@datavello.local";
    public const string DefaultDemoPassword = "Demo12345";
    public const string DemoCompanyName = "Padaria Pão Dourado (dados fictícios)";
    private const string FictitiousNote = "Dado fictício de demonstração.";

    public async Task RunAsync(bool reset, CancellationToken ct = default)
    {
        var existing = await userManager.FindByEmailAsync(DemoEmail);
        if (existing is not null && !reset)
        {
            logger.LogInformation("Demo data already exists ({Email}). Nothing to do. Use 'seed-demo --reset' to recreate it.", DemoEmail);
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (existing is not null)
        {
            await RemoveDemoAsync(existing, ct);
        }

        var password = configuration["Demo:Password"] ?? DefaultDemoPassword;
        var user = new AppUser { UserName = DemoEmail, Email = DemoEmail, FullName = "Usuário Demonstração", EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Could not create demo user: " + string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        var company = await provisioning.ProvisionAsync(user, DemoCompanyName, ct);
        await db.SaveChangesAsync(ct);

        var categories = await db.Categories.IgnoreQueryFilters()
            .Where(c => c.CompanyId == company.Id)
            .ToDictionaryAsync(c => c.Name, c => c.Id, ct);

        var transactions = Generate(company.Id, categories, DateOnly.FromDateTime(DateTime.Today));
        db.Transactions.AddRange(transactions);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Demo data created: {Count} transactions for '{Company}'. Login: {Email}", transactions.Count, DemoCompanyName, DemoEmail);
    }

    /// <summary>Deletes only the demo user and the companies the demo user owns.</summary>
    private async Task RemoveDemoAsync(AppUser demo, CancellationToken ct)
    {
        var ownedCompanyIds = await db.CompanyMembers
            .Where(m => m.UserId == demo.Id && m.Role == CompanyRole.Owner)
            .Select(m => m.CompanyId)
            .ToListAsync(ct);

        // Safety: never remove a company that has members other than the demo user.
        var shared = await db.CompanyMembers.AnyAsync(m => ownedCompanyIds.Contains(m.CompanyId) && m.UserId != demo.Id, ct);
        if (shared)
        {
            throw new InvalidOperationException("Demo company has other members; refusing to reset it.");
        }

        await db.Companies.Where(c => ownedCompanyIds.Contains(c.Id)).ExecuteDeleteAsync(ct);
        await userManager.DeleteAsync(demo);
    }

    /// <summary>Deterministic, plausible small-bakery cash movements for the last 14 months.</summary>
    private static List<Transaction> Generate(Guid companyId, IReadOnlyDictionary<string, Guid> categories, DateOnly today)
    {
        var random = new Random(20260101);
        var list = new List<Transaction>();
        var start = new DateOnly(today.Year, today.Month, 1).AddMonths(-13);

        void Add(DateOnly date, TransactionType type, string category, string description, decimal amount)
        {
            if (date > today) return;
            list.Add(new Transaction
            {
                CompanyId = companyId,
                Date = date,
                Type = type,
                CategoryId = categories[category],
                Description = description,
                Amount = decimal.Round(amount, 2),
                Notes = FictitiousNote
            });
        }

        decimal Vary(decimal baseValue, double spread) => baseValue * (decimal)(1 + (random.NextDouble() * 2 - 1) * spread);

        for (var month = start; month <= today; month = month.AddMonths(1))
        {
            var days = DateTime.DaysInMonth(month.Year, month.Month);
            var monthIndex = (month.Year - start.Year) * 12 + month.Month - start.Month;
            var growth = 1m + monthIndex * 0.015m; // slow growth over time
            var seasonal = month.Month is 12 or 1 ? 1.25m : month.Month is 6 or 7 ? 1.1m : 1m; // holidays, festas juninas

            // Weekly sales summaries (balcão) and occasional catering orders
            for (var day = 1; day <= days; day += 7)
            {
                // Recorded on the last day of each week (or of the month, for the final partial week).
                Add(month.AddDays(Math.Min(day + 5, days - 1)), TransactionType.Income, "Vendas", "Vendas balcão (semana)", Vary(4800m * growth * seasonal, 0.12));
            }

            if (random.NextDouble() < 0.75)
            {
                Add(month.AddDays(random.Next(3, days - 2)), TransactionType.Income, "Serviços", "Encomenda para eventos", Vary(1200m * seasonal, 0.35));
            }

            if (random.NextDouble() < 0.3)
            {
                Add(month.AddDays(random.Next(1, days - 1)), TransactionType.Income, "Outras receitas", "Venda de equipamento usado", Vary(450m, 0.4));
            }

            // Fixed and variable expenses
            Add(month.AddDays(4), TransactionType.Expense, "Aluguel", "Aluguel do ponto comercial", 3200m);
            Add(month.AddDays(4), TransactionType.Expense, "Salários", "Folha de pagamento", Vary(7800m * (monthIndex >= 7 ? 1.08m : 1m), 0.02));
            Add(month.AddDays(19), TransactionType.Expense, "Impostos", "Simples Nacional (DAS)", Vary(1150m * growth * seasonal, 0.08));
            Add(month.AddDays(9), TransactionType.Expense, "Água, luz e internet", "Conta de energia elétrica", Vary(980m, 0.15));
            Add(month.AddDays(11), TransactionType.Expense, "Água, luz e internet", "Conta de água", Vary(210m, 0.15));
            Add(month.AddDays(14), TransactionType.Expense, "Água, luz e internet", "Internet e telefone", 159.90m);

            for (var day = 2; day <= days; day += 7)
            {
                Add(month.AddDays(day - 1), TransactionType.Expense, "Fornecedores", "Compra de farinha e insumos", Vary(2100m * growth * seasonal, 0.18));
            }

            if (random.NextDouble() < 0.6)
            {
                Add(month.AddDays(random.Next(1, days - 1)), TransactionType.Expense, "Marketing", "Anúncios em redes sociais", Vary(350m, 0.3));
            }

            if (random.NextDouble() < 0.4)
            {
                Add(month.AddDays(random.Next(1, days - 1)), TransactionType.Expense, "Outras despesas", "Manutenção de equipamentos", Vary(620m, 0.5));
            }
        }

        return list;
    }
}
