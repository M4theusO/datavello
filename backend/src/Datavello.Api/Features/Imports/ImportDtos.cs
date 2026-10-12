using System.ComponentModel.DataAnnotations;
using Datavello.Api.Domain;

namespace Datavello.Api.Features.Imports;

public enum DateFormatOption
{
    DayMonthYear,
    MonthDayYear,
    YearMonthDay
}

public enum DecimalSeparatorOption
{
    Comma,
    Dot
}

public enum TypeConvention
{
    /// <summary>A column says "receita/despesa", "entrada/saída", "C/D"...</summary>
    Column,

    /// <summary>Negative amount = expense, positive = income.</summary>
    Sign,

    AllIncome,
    AllExpense
}

/// <summary>Zero-based column indexes in the spreadsheet; null when not mapped.</summary>
public sealed class ColumnMapping
{
    [Required(ErrorMessage = "Selecione a coluna de data.")]
    public int? Date { get; init; }

    [Required(ErrorMessage = "Selecione a coluna de descrição.")]
    public int? Description { get; init; }

    [Required(ErrorMessage = "Selecione a coluna de valor.")]
    public int? Amount { get; init; }

    public int? Type { get; init; }
    public int? Category { get; init; }
}

public sealed class ImportOptions
{
    [Required(ErrorMessage = "Informe o mapeamento das colunas.")]
    public ColumnMapping? Mapping { get; init; }

    public DateFormatOption DateFormat { get; init; } = DateFormatOption.DayMonthYear;
    public DecimalSeparatorOption DecimalSeparator { get; init; } = DecimalSeparatorOption.Comma;
    public TypeConvention TypeConvention { get; init; } = TypeConvention.Column;

    /// <summary>Category used for income rows without a category.</summary>
    public Guid? DefaultIncomeCategoryId { get; init; }

    /// <summary>Category used for expense rows without a category.</summary>
    public Guid? DefaultExpenseCategoryId { get; init; }

    /// <summary>Create categories named in the file that do not exist yet.</summary>
    public bool CreateMissingCategories { get; init; }

    /// <summary>Reject rows that look like duplicates (default). When false they are imported anyway.</summary>
    public bool SkipDuplicates { get; init; } = true;
}

public sealed record SuggestedMapping(int? Date, int? Description, int? Amount, int? Type, int? Category);

public sealed record ImportPreviewDto(
    Guid Id,
    string FileName,
    string FileType,
    IReadOnlyList<string> Headers,
    IReadOnlyList<PreviewRowDto> PreviewRows,
    int TotalRows,
    int EmptyRows,
    SuggestedMapping SuggestedMapping,
    TypeConvention SuggestedTypeConvention);

/// <param name="RowNumber">Linha na planilha (cabeçalho = 1).</param>
public sealed record PreviewRowDto(int RowNumber, IReadOnlyList<string> Cells);

public sealed record RowErrorDto(int RowNumber, string? Field, string Message, string? RawValue);

/// <summary>How a valid row will be imported.</summary>
public sealed record ParsedRowDto(
    int RowNumber,
    DateOnly Date,
    string Description,
    TransactionType Type,
    decimal Amount,
    string CategoryName,
    bool NewCategory,
    bool PossibleDuplicate);

public sealed record ImportValidationDto(
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int DuplicateRows,
    int EmptyRows,
    decimal IncomeTotal,
    decimal ExpenseTotal,
    IReadOnlyList<string> NewCategories,
    IReadOnlyList<ParsedRowDto> Sample,
    IReadOnlyList<RowErrorDto> Errors,
    int TotalErrors);

public sealed record ImportJobDto(
    Guid Id,
    string FileName,
    string FileType,
    ImportStatus Status,
    string UserName,
    int TotalRows,
    int ImportedRows,
    int RejectedRows,
    string? Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record ImportJobDetailDto(ImportJobDto Job, IReadOnlyList<RowErrorDto> Errors, int TotalErrors);
