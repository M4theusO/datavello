namespace Datavello.Api.Domain;

/// <summary>Which transaction types a category can be used with.</summary>
public enum CategoryKind
{
    Income,
    Expense,
    Both
}

public class Category : ITenantOwned, IHasTimestamps
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CategoryKind Kind { get; set; }

    /// <summary>Created automatically with the company; can still be edited or removed.</summary>
    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public bool Accepts(TransactionType type) =>
        Kind == CategoryKind.Both
        || (Kind == CategoryKind.Income && type == TransactionType.Income)
        || (Kind == CategoryKind.Expense && type == TransactionType.Expense);
}
