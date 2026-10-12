using Datavello.Api.Domain;
using Datavello.Api.Features.Transactions;

namespace Datavello.Api.Features.Imports;

public sealed record ImportCandidate(
    int RowNumber,
    DateOnly Date,
    string Description,
    TransactionType Type,
    decimal Amount,
    Guid? CategoryId,
    string CategoryName,
    bool NewCategory)
{
    public bool PossibleDuplicate { get; set; }
    public string DuplicateKey => ImportParsing.DuplicateKey(Date, Type, Amount, Description);
}

public sealed class ImportOutcome
{
    public List<ImportCandidate> Valid { get; } = [];
    public List<RowErrorDto> Errors { get; } = [];
    public HashSet<int> RejectedRows { get; } = [];
    public int DuplicateRows { get; set; }
    public int TotalRows { get; init; }
    public int EmptyRows { get; init; }

    /// <summary>Categories to create, keyed by normalized name.</summary>
    public Dictionary<string, (string Name, HashSet<TransactionType> Types)> NewCategories { get; } = [];
}

/// <summary>
/// Turns staged spreadsheet rows into transactions to import, collecting every problem per row.
/// No database access: categories and existing duplicate keys are passed in.
/// </summary>
public static class ImportValidator
{
    public static ImportOutcome Validate(ParsedSheet sheet, ImportOptions options, IReadOnlyCollection<Category> categories)
    {
        var mapping = options.Mapping!;
        var outcome = new ImportOutcome { TotalRows = sheet.Rows.Count, EmptyRows = sheet.EmptyRows };
        var byName = categories
            .GroupBy(c => TextNormalizer.Normalize(c.Name))
            .ToDictionary(g => g.Key, g => g.First());
        var byId = categories.ToDictionary(c => c.Id);

        foreach (var row in sheet.Rows)
        {
            var errors = new List<RowErrorDto>();
            string Cell(int? column) => column is int c && c < row.Cells.Length ? row.Cells[c] : string.Empty;
            void Fail(string field, string message, string? raw) =>
                errors.Add(new RowErrorDto(row.Number, field, message, Truncate(SpreadsheetReader.StripMarker(raw ?? "").Trim())));

            // Date
            var rawDate = Cell(mapping.Date);
            var date = ImportParsing.ParseDate(rawDate, options.DateFormat);
            if (date is null)
            {
                Fail("Data", string.IsNullOrWhiteSpace(rawDate) ? "Data não informada." : "Data inválida para o formato escolhido.", rawDate);
            }
            else if (TransactionRules.ValidateDate(date.Value) is { } dateError)
            {
                Fail("Data", dateError, rawDate);
            }

            // Amount (signed)
            var rawAmount = Cell(mapping.Amount);
            var (signedAmount, amountError) = ImportParsing.ParseAmount(rawAmount, options.DecimalSeparator);
            if (amountError is not null)
            {
                Fail("Valor", amountError, rawAmount);
            }

            // Type
            TransactionType? type = null;
            switch (options.TypeConvention)
            {
                case TypeConvention.AllIncome:
                    type = TransactionType.Income;
                    break;
                case TypeConvention.AllExpense:
                    type = TransactionType.Expense;
                    break;
                case TypeConvention.Sign:
                    if (signedAmount is { } s)
                    {
                        if (s == 0) Fail("Valor", "Valor zero não indica receita nem despesa.", rawAmount);
                        else type = s < 0 ? TransactionType.Expense : TransactionType.Income;
                    }
                    break;
                case TypeConvention.Column:
                    var rawType = Cell(mapping.Type);
                    type = ImportParsing.ParseType(rawType);
                    if (type is null)
                    {
                        Fail("Tipo", string.IsNullOrWhiteSpace(rawType)
                            ? "Tipo não informado."
                            : "Tipo desconhecido. Use Receita/Despesa, Entrada/Saída ou Crédito/Débito.", rawType);
                    }
                    break;
            }

            var amount = signedAmount is { } signed ? Math.Abs(signed) : 0m;
            if (signedAmount is not null && amount != 0 && ImportParsing.ValidateAmount(amount) is { } rangeError)
            {
                Fail("Valor", rangeError, rawAmount);
            }
            else if (signedAmount == 0 && options.TypeConvention != TypeConvention.Sign)
            {
                Fail("Valor", "O valor deve ser maior que zero.", rawAmount);
            }

            // Description
            var description = SpreadsheetReader.StripMarker(Cell(mapping.Description)).Trim();
            if (ImportParsing.ValidateDescription(description) is { } descriptionError)
            {
                Fail("Descrição", descriptionError, description);
            }

            // Category
            Guid? categoryId = null;
            var categoryName = string.Empty;
            var newCategory = false;
            var rawCategory = SpreadsheetReader.StripMarker(Cell(mapping.Category)).Trim();
            if (type is { } t)
            {
                if (rawCategory.Length > 0)
                {
                    var key = TextNormalizer.Normalize(rawCategory);
                    if (byName.TryGetValue(key, out var existing))
                    {
                        if (existing.Accepts(t))
                        {
                            categoryId = existing.Id;
                            categoryName = existing.Name;
                        }
                        else
                        {
                            Fail("Categoria", $"A categoria \"{existing.Name}\" não aceita {(t == TransactionType.Income ? "receitas" : "despesas")}.", rawCategory);
                        }
                    }
                    else if (options.CreateMissingCategories)
                    {
                        if (rawCategory.Length is < 2 or > 80)
                        {
                            Fail("Categoria", "O nome da categoria deve ter entre 2 e 80 caracteres.", rawCategory);
                        }
                        else
                        {
                            categoryName = outcome.NewCategories.TryGetValue(key, out var planned) ? planned.Name : rawCategory;
                            newCategory = true;
                        }
                    }
                    else
                    {
                        Fail("Categoria", "Categoria não encontrada. Cadastre-a ou ative a criação automática.", rawCategory);
                    }
                }
                else
                {
                    var fallbackId = t == TransactionType.Income ? options.DefaultIncomeCategoryId : options.DefaultExpenseCategoryId;
                    if (fallbackId is { } id && byId.TryGetValue(id, out var fallback))
                    {
                        categoryId = fallback.Id;
                        categoryName = fallback.Name;
                    }
                    else
                    {
                        Fail("Categoria", "Categoria não informada e nenhuma categoria padrão foi escolhida.", rawCategory);
                    }
                }
            }

            if (errors.Count > 0)
            {
                outcome.Errors.AddRange(errors);
                outcome.RejectedRows.Add(row.Number);
                continue;
            }

            if (newCategory)
            {
                var key = TextNormalizer.Normalize(rawCategory);
                if (!outcome.NewCategories.TryGetValue(key, out var planned))
                {
                    planned = (rawCategory, []);
                    outcome.NewCategories[key] = planned;
                }

                planned.Types.Add(type!.Value);
            }

            outcome.Valid.Add(new ImportCandidate(row.Number, date!.Value, description, type!.Value, amount, categoryId, categoryName, newCategory));
        }

        return outcome;
    }

    /// <summary>
    /// Marks rows matching an existing transaction or an earlier row of the same file.
    /// When <paramref name="skip"/> is true they become rejected rows; otherwise they stay valid but flagged.
    /// </summary>
    public static void ApplyDuplicates(ImportOutcome outcome, IReadOnlySet<string> existingKeys, bool skip)
    {
        var seen = new HashSet<string>();
        var kept = new List<ImportCandidate>();

        foreach (var candidate in outcome.Valid)
        {
            var key = candidate.DuplicateKey;
            var duplicate = existingKeys.Contains(key) || !seen.Add(key);
            if (!duplicate)
            {
                kept.Add(candidate);
                continue;
            }

            outcome.DuplicateRows++;
            candidate.PossibleDuplicate = true;
            if (skip)
            {
                var where = existingKeys.Contains(key) ? "já existe uma transação igual cadastrada" : "repete uma linha anterior do arquivo";
                outcome.Errors.Add(new RowErrorDto(candidate.RowNumber, "Duplicidade",
                    $"Possível duplicidade: {where} (mesma data, tipo, valor e descrição).", Truncate(candidate.Description)));
                outcome.RejectedRows.Add(candidate.RowNumber);
            }
            else
            {
                kept.Add(candidate);
            }
        }

        outcome.Valid.Clear();
        outcome.Valid.AddRange(kept);
        outcome.Errors.Sort((a, b) => a.RowNumber.CompareTo(b.RowNumber));

        // New categories only used by rows that were dropped are no longer needed.
        var stillUsed = outcome.Valid.Where(v => v.NewCategory).Select(v => TextNormalizer.Normalize(v.CategoryName)).ToHashSet();
        foreach (var key in outcome.NewCategories.Keys.Where(k => !stillUsed.Contains(k)).ToList())
        {
            outcome.NewCategories.Remove(key);
        }
    }

    private static string? Truncate(string? value) =>
        string.IsNullOrEmpty(value) ? null : value.Length > 120 ? value[..117] + "..." : value;
}
