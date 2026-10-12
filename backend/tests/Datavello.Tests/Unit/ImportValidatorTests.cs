using Datavello.Api.Domain;
using Datavello.Api.Features.Imports;

namespace Datavello.Tests.Unit;

public sealed class ImportValidatorTests
{
    private static readonly Category Vendas = new() { Name = "Vendas", Kind = CategoryKind.Income };
    private static readonly Category Aluguel = new() { Name = "Aluguel", Kind = CategoryKind.Expense };
    private static readonly Category Outras = new() { Name = "Outras despesas", Kind = CategoryKind.Expense };
    private static readonly Category[] Categories = [Vendas, Aluguel, Outras];

    // Columns: 0 Data, 1 Descrição, 2 Valor, 3 Tipo, 4 Categoria
    private static ParsedSheet Sheet(params string[][] rows) =>
        new(["Data", "Descrição", "Valor", "Tipo", "Categoria"],
            rows.Select((cells, i) => new SheetRow(i + 2, cells)).ToList(),
            0);

    private static ImportOptions Options(TypeConvention convention = TypeConvention.Column, bool createMissing = false, Guid? defaultExpense = null) => new()
    {
        Mapping = new ColumnMapping { Date = 0, Description = 1, Amount = 2, Type = convention == TypeConvention.Column ? 3 : null, Category = 4 },
        TypeConvention = convention,
        CreateMissingCategories = createMissing,
        DefaultExpenseCategoryId = defaultExpense
    };

    [Fact]
    public void Valid_rows_are_parsed_with_their_categories()
    {
        var outcome = ImportValidator.Validate(Sheet(
            ["15/03/2026", "Venda balcão", "1.234,56", "Receita", "vendas"],
            ["16/03/2026", "Aluguel março", "-900,00", "Despesa", "ALUGUEL"]), Options(), Categories);

        Assert.Empty(outcome.Errors);
        Assert.Equal(2, outcome.Valid.Count);
        Assert.Equal(1234.56m, outcome.Valid[0].Amount);
        Assert.Equal(Vendas.Id, outcome.Valid[0].CategoryId);
        Assert.Equal(900m, outcome.Valid[1].Amount); // sign ignored in column mode
        Assert.Equal(TransactionType.Expense, outcome.Valid[1].Type);
    }

    [Fact]
    public void Every_problem_of_a_row_is_reported_with_line_and_raw_value()
    {
        var outcome = ImportValidator.Validate(Sheet(
            ["32/13/2026", "", "abc", "Transferência", "Vendas"]), Options(), Categories);

        Assert.Empty(outcome.Valid);
        Assert.Equal([2], outcome.RejectedRows);
        Assert.Equal(["Data", "Valor", "Tipo", "Descrição"], outcome.Errors.Select(e => e.Field));
        Assert.All(outcome.Errors, e => Assert.Equal(2, e.RowNumber));
        Assert.Equal("32/13/2026", outcome.Errors[0].RawValue);
    }

    [Fact]
    public void Sign_convention_uses_negative_for_expenses_and_rejects_zero()
    {
        var outcome = ImportValidator.Validate(Sheet(
            ["01/03/2026", "Recebimento", "500,00", "", "Vendas"],
            ["02/03/2026", "Pagamento", "-120,00", "", "Aluguel"],
            ["03/03/2026", "Zerado", "0,00", "", "Vendas"]), Options(TypeConvention.Sign), Categories);

        Assert.Equal([TransactionType.Income, TransactionType.Expense], outcome.Valid.Select(v => v.Type));
        Assert.Equal([4], outcome.RejectedRows);
    }

    [Fact]
    public void Category_problems_are_rejected_unless_creation_or_default_applies()
    {
        string[][] rows =
        [
            ["01/03/2026", "Combustível", "100", "Despesa", "Transporte"],
            ["02/03/2026", "Sem categoria", "50", "Despesa", ""],
            ["03/03/2026", "Venda errada", "70", "Receita", "Aluguel"]
        ];

        var strict = ImportValidator.Validate(Sheet(rows), Options(), Categories);
        Assert.Equal([2, 3, 4], strict.RejectedRows.Order());
        Assert.Contains(strict.Errors, e => e.Message.Contains("Categoria não encontrada"));
        Assert.Contains(strict.Errors, e => e.Message.Contains("nenhuma categoria padrão"));
        Assert.Contains(strict.Errors, e => e.Message.Contains("não aceita receitas"));

        var lenient = ImportValidator.Validate(Sheet(rows), Options(createMissing: true, defaultExpense: Outras.Id), Categories);
        Assert.Equal([4], lenient.RejectedRows); // a category of the wrong type is never accepted
        Assert.True(lenient.Valid[0].NewCategory);
        Assert.Equal("Transporte", lenient.NewCategories.Values.Single().Name);
        Assert.Equal(Outras.Id, lenient.Valid[1].CategoryId);
    }

    [Fact]
    public void Duplicates_against_existing_data_and_within_the_file()
    {
        string[][] rows =
        [
            ["01/03/2026", "Conta de Água", "80,00", "Despesa", "Aluguel"],
            ["02/03/2026", "Venda", "10,00", "Receita", "Vendas"],
            ["02/03/2026", "venda", "10,00", "Receita", "Vendas"]
        ];
        var existing = new HashSet<string> { ImportParsing.DuplicateKey(new DateOnly(2026, 3, 1), TransactionType.Expense, 80m, "conta de agua") };

        var skipped = ImportValidator.Validate(Sheet(rows), Options(), Categories);
        ImportValidator.ApplyDuplicates(skipped, existing, skip: true);
        Assert.Equal(["Venda"], skipped.Valid.Select(v => v.Description));
        Assert.Equal(2, skipped.DuplicateRows);
        Assert.Equal([2, 4], skipped.RejectedRows.Order());
        Assert.All(skipped.Errors, e => Assert.Equal("Duplicidade", e.Field));

        var kept = ImportValidator.Validate(Sheet(rows), Options(), Categories);
        ImportValidator.ApplyDuplicates(kept, existing, skip: false);
        Assert.Equal(3, kept.Valid.Count);
        Assert.Equal(2, kept.Valid.Count(v => v.PossibleDuplicate));
        Assert.Empty(kept.Errors);
    }
}
