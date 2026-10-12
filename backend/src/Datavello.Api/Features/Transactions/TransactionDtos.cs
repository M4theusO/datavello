using System.ComponentModel.DataAnnotations;
using Datavello.Api.Domain;
using Datavello.Api.Infrastructure.Http;

namespace Datavello.Api.Features.Transactions;

public sealed record TransactionDto(
    Guid Id,
    DateOnly Date,
    string Description,
    TransactionType Type,
    decimal Amount,
    Guid CategoryId,
    string CategoryName,
    string? Notes,
    bool Imported,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class TransactionRequest
{
    /// <example>2026-03-15</example>
    [Required(ErrorMessage = "Informe a data.")]
    public DateOnly? Date { get; init; }

    /// <example>Venda balcão</example>
    [Required(ErrorMessage = "Informe a descrição.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "A descrição deve ter entre 2 e 200 caracteres.")]
    public string Description { get; init; } = string.Empty;

    /// <summary>Income (receita) ou Expense (despesa).</summary>
    [Required(ErrorMessage = "Informe o tipo.")]
    [EnumDataType(typeof(TransactionType), ErrorMessage = "Tipo de transação inválido.")]
    public TransactionType? Type { get; init; }

    /// <summary>Valor positivo, com até 2 casas decimais.</summary>
    /// <example>1250.90</example>
    [Required(ErrorMessage = "Informe o valor.")]
    public decimal? Amount { get; init; }

    [Required(ErrorMessage = "Informe a categoria.")]
    public Guid? CategoryId { get; init; }

    [StringLength(1000, ErrorMessage = "A observação deve ter no máximo 1000 caracteres.")]
    public string? Notes { get; init; }
}

public enum TransactionSortField
{
    Date,
    Amount
}

public enum SortDirection
{
    Desc,
    Asc
}

public sealed class TransactionQuery : PageQuery
{
    /// <summary>Data inicial (inclusive), formato yyyy-MM-dd.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Data final (inclusive), formato yyyy-MM-dd.</summary>
    public DateOnly? To { get; init; }

    public Guid? CategoryId { get; init; }
    public TransactionType? Type { get; init; }

    /// <summary>Busca na descrição (sem diferenciar maiúsculas/minúsculas).</summary>
    [StringLength(100, ErrorMessage = "A busca deve ter no máximo 100 caracteres.")]
    public string? Search { get; init; }

    public TransactionSortField SortBy { get; init; } = TransactionSortField.Date;
    public SortDirection SortDir { get; init; } = SortDirection.Desc;
}
