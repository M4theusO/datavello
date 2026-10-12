using System.Globalization;
using System.Text.RegularExpressions;
using Datavello.Api.Domain;
using Datavello.Api.Features.Transactions;

namespace Datavello.Api.Features.Imports;

/// <summary>Header recognition and cell parsing. Pure functions; rules documented in docs/data-import.md.</summary>
public static partial class ImportParsing
{
    private static readonly string[] DateHeaders =
        ["data", "date", "dt", "data lancamento", "data do lancamento", "data da transacao", "data movimento", "data pagamento"];

    private static readonly string[] DescriptionHeaders =
        ["descricao", "historico", "description", "detalhe", "detalhes", "lancamento", "memo"];

    private static readonly string[] AmountHeaders =
        ["valor", "value", "amount", "valor (r$)", "valor r$", "total", "quantia", "montante"];

    private static readonly string[] TypeHeaders =
        ["tipo", "type", "natureza", "operacao", "entrada/saida", "e/s", "credito/debito", "d/c", "c/d"];

    private static readonly string[] CategoryHeaders =
        ["categoria", "category", "classificacao", "grupo", "plano de contas"];

    private static readonly HashSet<string> IncomeWords =
        ["receita", "receitas", "entrada", "entradas", "credito", "creditos", "c", "cr", "+", "income", "recebimento", "recebido"];

    private static readonly HashSet<string> ExpenseWords =
        ["despesa", "despesas", "saida", "saidas", "debito", "debitos", "d", "db", "-", "expense", "pagamento", "pago", "gasto"];

    public static SuggestedMapping SuggestMapping(IReadOnlyList<string> headers)
    {
        var normalized = headers.Select(TextNormalizer.Normalize).ToList();
        var used = new HashSet<int>();

        int? Find(string[] candidates)
        {
            // Exact matches first, then headers that start with a candidate ("data de emissão").
            foreach (var predicate in new Func<string, string, bool>[] { (h, c) => h == c, (h, c) => h.StartsWith(c + " ", StringComparison.Ordinal) })
            {
                foreach (var candidate in candidates)
                {
                    var index = normalized.FindIndex(h => predicate(h, candidate));
                    if (index >= 0 && used.Add(index))
                    {
                        return index;
                    }
                }
            }

            return null;
        }

        return new SuggestedMapping(Find(DateHeaders), Find(DescriptionHeaders), Find(AmountHeaders), Find(TypeHeaders), Find(CategoryHeaders));
    }

    // ---------------- dates ----------------

    private static readonly Dictionary<DateFormatOption, string[]> DateFormats = new()
    {
        [DateFormatOption.DayMonthYear] = ["d/M/yyyy", "d/M/yy", "d-M-yyyy", "d-M-yy", "d.M.yyyy", "d.M.yy"],
        [DateFormatOption.MonthDayYear] = ["M/d/yyyy", "M/d/yy", "M-d-yyyy", "M-d-yy"],
        [DateFormatOption.YearMonthDay] = ["yyyy-M-d", "yyyy/M/d", "yyyy.M.d"]
    };

    /// <summary>Parses a date cell; ISO dates (and XLSX date cells) are always accepted.</summary>
    public static DateOnly? ParseDate(string raw, DateFormatOption format)
    {
        var text = SpreadsheetReader.StripMarker(raw).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        // "15/03/2026 10:30" or "2026-03-15T00:00:00" -> keep only the date part
        var cut = text.IndexOfAny([' ', 'T']);
        if (cut > 0)
        {
            text = text[..cut];
        }

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
        {
            return iso;
        }

        return DateOnly.TryParseExact(text, DateFormats[format], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    // ---------------- amounts ----------------

    [GeneratedRegex(@"^\d+(\.\d+)?$")]
    private static partial Regex PlainNumber();

    [GeneratedRegex(@"^-?\d+(\.\d+)?([eE][-+]?\d+)?$")]
    private static partial Regex InvariantNumber();

    /// <summary>
    /// Parses an amount cell to a signed value. Returns null with a message when invalid.
    /// Numeric XLSX cells are invariant; text cells follow the decimal separator option.
    /// </summary>
    public static (decimal? Value, string? Error) ParseAmount(string raw, DecimalSeparatorOption separator)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0 || trimmed == SpreadsheetReader.NumericCellPrefix.ToString())
        {
            return (null, "Valor não informado.");
        }

        if (trimmed[0] == SpreadsheetReader.NumericCellPrefix)
        {
            var number = trimmed[1..];
            if (!InvariantNumber().IsMatch(number)
                || !decimal.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var cellValue))
            {
                return (null, "Valor inválido.");
            }

            // Spreadsheet doubles such as 10.100000000000001 are rounded to cents when that is what they mean.
            var rounded = decimal.Round(cellValue, 2);
            return Math.Abs(cellValue - rounded) < 0.000001m ? (rounded, null) : (null, "O valor deve ter no máximo 2 casas decimais.");
        }

        var text = trimmed.Replace("R$", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "").Replace(" ", "");
        var negative = false;
        if (text.StartsWith('(') && text.EndsWith(')'))
        {
            negative = true;
            text = text[1..^1];
        }

        if (text.StartsWith('-'))
        {
            negative = !negative;
            text = text[1..];
        }
        else if (text.EndsWith('-'))
        {
            negative = !negative;
            text = text[..^1];
        }
        else if (text.StartsWith('+'))
        {
            text = text[1..];
        }

        text = separator == DecimalSeparatorOption.Comma
            ? text.Replace(".", "").Replace(',', '.')
            : text.Replace(",", "");

        if (!PlainNumber().IsMatch(text) || !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            var hint = separator == DecimalSeparatorOption.Comma ? "1.234,56" : "1,234.56";
            return (null, $"Valor inválido. Use o formato {hint}.");
        }

        if (decimal.Round(value, 2) != value)
        {
            return (null, "O valor deve ter no máximo 2 casas decimais.");
        }

        return (negative ? -value : value, null);
    }

    // ---------------- type ----------------

    public static TransactionType? ParseType(string raw)
    {
        var word = TextNormalizer.Normalize(SpreadsheetReader.StripMarker(raw));
        if (IncomeWords.Contains(word)) return TransactionType.Income;
        if (ExpenseWords.Contains(word)) return TransactionType.Expense;
        return null;
    }

    /// <summary>Suggests the type convention: a type column if mapped, otherwise the amount sign when negatives exist.</summary>
    public static TypeConvention SuggestTypeConvention(SuggestedMapping mapping, IReadOnlyList<SheetRow> rows)
    {
        if (mapping.Type is not null)
        {
            return TypeConvention.Column;
        }

        if (mapping.Amount is int amountColumn
            && rows.Any(r => ParseAmount(r.Cells[amountColumn], DecimalSeparatorOption.Comma).Value < 0))
        {
            return TypeConvention.Sign;
        }

        return TypeConvention.AllExpense;
    }

    /// <summary>Key used for duplicate detection: same date, type, amount and normalized description.</summary>
    public static string DuplicateKey(DateOnly date, TransactionType type, decimal amount, string description) =>
        $"{date:yyyy-MM-dd}|{type}|{amount.ToString("0.00", CultureInfo.InvariantCulture)}|{TextNormalizer.Normalize(description)}";

    public static string? ValidateDescription(string description) =>
        description.Length switch
        {
            0 => "Descrição não informada.",
            < 2 => "A descrição deve ter pelo menos 2 caracteres.",
            > 200 => "A descrição tem mais de 200 caracteres.",
            _ => null
        };

    public static string? ValidateAmount(decimal amount) => TransactionRules.ValidateAmount(amount);
}
