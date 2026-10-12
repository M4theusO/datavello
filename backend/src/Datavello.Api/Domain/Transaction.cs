namespace Datavello.Api.Domain;

public enum TransactionType
{
    Income,
    Expense
}

public class Transaction : ITenantOwned, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public TransactionType Type { get; set; }

    /// <summary>Always positive; the sign comes from <see cref="Type"/>. Stored as numeric(18,2).</summary>
    public decimal Amount { get; set; }

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string? Notes { get; set; }

    /// <summary>Set when the transaction was created by a spreadsheet import.</summary>
    public Guid? ImportJobId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
