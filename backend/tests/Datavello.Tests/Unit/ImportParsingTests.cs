using Datavello.Api.Domain;
using Datavello.Api.Features.Imports;

namespace Datavello.Tests.Unit;

public sealed class ImportParsingTests
{
    [Theory]
    [InlineData("15/03/2026", DateFormatOption.DayMonthYear, "2026-03-15")]
    [InlineData("5/3/26", DateFormatOption.DayMonthYear, "2026-03-05")]
    [InlineData("15-03-2026", DateFormatOption.DayMonthYear, "2026-03-15")]
    [InlineData("15/03/2026 10:30", DateFormatOption.DayMonthYear, "2026-03-15")]
    [InlineData("03/15/2026", DateFormatOption.MonthDayYear, "2026-03-15")]
    [InlineData("2026/3/15", DateFormatOption.YearMonthDay, "2026-03-15")]
    [InlineData("2026-03-15", DateFormatOption.DayMonthYear, "2026-03-15")] // ISO always accepted
    public void Parses_dates(string raw, DateFormatOption format, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected), ImportParsing.ParseDate(raw, format));
    }

    [Theory]
    [InlineData("", DateFormatOption.DayMonthYear)]
    [InlineData("31/02/2026", DateFormatOption.DayMonthYear)]
    [InlineData("15/03/2026", DateFormatOption.MonthDayYear)]
    [InlineData("ontem", DateFormatOption.DayMonthYear)]
    public void Rejects_invalid_dates(string raw, DateFormatOption format)
    {
        Assert.Null(ImportParsing.ParseDate(raw, format));
    }

    [Theory]
    [InlineData("1.234,56", DecimalSeparatorOption.Comma, 1234.56)]
    [InlineData("R$ 1.234,5", DecimalSeparatorOption.Comma, 1234.5)]
    [InlineData("-150,00", DecimalSeparatorOption.Comma, -150)]
    [InlineData("(80,10)", DecimalSeparatorOption.Comma, -80.10)]
    [InlineData("80,10-", DecimalSeparatorOption.Comma, -80.10)]
    [InlineData("1,234.56", DecimalSeparatorOption.Dot, 1234.56)]
    [InlineData("1234", DecimalSeparatorOption.Comma, 1234)]
    [InlineData("\u001F1234.5", DecimalSeparatorOption.Comma, 1234.5)] // numeric XLSX cell ignores the option
    [InlineData("\u001F10.100000000000001", DecimalSeparatorOption.Comma, 10.10)] // float noise rounded to cents
    public void Parses_amounts(string raw, DecimalSeparatorOption separator, decimal expected)
    {
        var (value, error) = ImportParsing.ParseAmount(raw, separator);

        Assert.Null(error);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("", "não informado")]
    [InlineData("abc", "Valor inválido")]
    [InlineData("10,555", "2 casas decimais")]
    [InlineData("\u001F10.555", "2 casas decimais")]
    [InlineData("1.2.3,4,5", "Valor inválido")]
    public void Rejects_invalid_amounts(string raw, string message)
    {
        var (value, error) = ImportParsing.ParseAmount(raw, DecimalSeparatorOption.Comma);

        Assert.Null(value);
        Assert.Contains(message, error);
    }

    [Theory]
    [InlineData("Receita", TransactionType.Income)]
    [InlineData("ENTRADA", TransactionType.Income)]
    [InlineData("Crédito", TransactionType.Income)]
    [InlineData("C", TransactionType.Income)]
    [InlineData("Despesa", TransactionType.Expense)]
    [InlineData("saída", TransactionType.Expense)]
    [InlineData("D", TransactionType.Expense)]
    [InlineData("Débito", TransactionType.Expense)]
    public void Parses_transaction_types(string raw, TransactionType expected)
    {
        Assert.Equal(expected, ImportParsing.ParseType(raw));
    }

    [Theory]
    [InlineData("")]
    [InlineData("transferência")]
    public void Unknown_types_are_null(string raw)
    {
        Assert.Null(ImportParsing.ParseType(raw));
    }

    [Fact]
    public void Suggests_mapping_from_common_headers()
    {
        var mapping = ImportParsing.SuggestMapping(["Data do lançamento", "Histórico", "Valor (R$)", "Natureza", "Classificação", "Obs"]);

        Assert.Equal(new SuggestedMapping(0, 1, 2, 3, 4), mapping);
    }

    [Fact]
    public void Suggests_nothing_for_unknown_headers_and_never_reuses_a_column()
    {
        var mapping = ImportParsing.SuggestMapping(["Coluna A", "Data", "Data"]);

        Assert.Equal(1, mapping.Date);
        Assert.Null(mapping.Description);
        Assert.Null(mapping.Amount);
    }

    [Fact]
    public void Duplicate_key_ignores_case_accents_and_extra_spaces()
    {
        var a = ImportParsing.DuplicateKey(new DateOnly(2026, 1, 5), TransactionType.Expense, 10m, "Conta de  Água");
        var b = ImportParsing.DuplicateKey(new DateOnly(2026, 1, 5), TransactionType.Expense, 10.00m, " conta de agua ");
        var c = ImportParsing.DuplicateKey(new DateOnly(2026, 1, 5), TransactionType.Income, 10m, "Conta de Água");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
