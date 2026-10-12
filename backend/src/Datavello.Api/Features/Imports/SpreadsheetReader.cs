using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace Datavello.Api.Features.Imports;

/// <summary>A data row with its 1-based line number in the original spreadsheet (header = line 1).</summary>
public sealed record SheetRow(int Number, string[] Cells);

/// <param name="Headers">Header texts (blank headers become "Coluna N").</param>
/// <param name="Rows">Non-empty data rows, padded to the header width.</param>
/// <param name="EmptyRows">Completely empty rows that were skipped.</param>
public sealed record ParsedSheet(string[] Headers, IReadOnlyList<SheetRow> Rows, int EmptyRows);

/// <summary>Expected, user-facing problem with the file itself (shown as 400).</summary>
public sealed class SpreadsheetException(string message) : Exception(message);

public static class SpreadsheetReader
{
    public const string Csv = "csv";
    public const string Xlsx = "xlsx";

    /// <summary>Marks numeric XLSX cells, whose text is invariant ("1234.5") whatever the user's decimal option.</summary>
    public const char NumericCellPrefix = '\u001F';

    static SpreadsheetReader()
    {
        // Windows-1252 (Excel's default for CSV in Portuguese) is not built into .NET Core.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>Returns "csv"/"xlsx" for supported extensions, null otherwise.</summary>
    public static string? FileTypeFor(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".csv" => Csv,
            ".xlsx" => Xlsx,
            _ => null
        };

    public static ParsedSheet Read(byte[] content, string fileType, int maxRows)
    {
        if (content.Length == 0)
        {
            throw new SpreadsheetException("O arquivo está vazio.");
        }

        return fileType == Xlsx ? ReadXlsx(content, maxRows) : ReadCsv(content, maxRows);
    }

    // ---------------- CSV ----------------

    private static ParsedSheet ReadCsv(byte[] content, int maxRows)
    {
        if (content.AsSpan(0, Math.Min(content.Length, 4096)).Contains((byte)0))
        {
            throw new SpreadsheetException("O arquivo não parece ser um CSV de texto. Verifique se escolheu o arquivo correto.");
        }

        var text = DecodeText(content);
        var delimiter = DetectDelimiter(text);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = false,
            BadDataFound = null,
            MissingFieldFound = null,
            IgnoreBlankLines = false,
            DetectColumnCountChanges = false,
            TrimOptions = TrimOptions.None
        };

        var records = new List<(int Line, string[] Cells)>();
        using (var reader = new StringReader(text))
        using (var csv = new CsvParser(reader, config))
        {
            while (csv.Read())
            {
                records.Add((csv.Row, csv.Record ?? []));
                if (records.Count > maxRows + 1 + 1000)
                {
                    break; // far beyond the limit; stop reading (checked again below)
                }
            }
        }

        return Build(records, maxRows);
    }

    private static string DecodeText(byte[] content)
    {
        try
        {
            // Strict UTF-8 first (handles the BOM); invalid sequences mean it was saved in Windows-1252.
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            var text = utf8.GetString(content);
            return text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(content);
        }
    }

    /// <summary>Picks the separator that appears most often (outside quotes) in the first line.</summary>
    private static string DetectDelimiter(string text)
    {
        var firstLine = text.Split('\n', 2)[0];
        var counts = new Dictionary<char, int> { [';'] = 0, [','] = 0, ['\t'] = 0 };
        var inQuotes = false;
        foreach (var ch in firstLine)
        {
            if (ch == '"') inQuotes = !inQuotes;
            else if (!inQuotes && counts.ContainsKey(ch)) counts[ch]++;
        }

        var best = counts.OrderByDescending(kv => kv.Value).First();
        return best.Value == 0 ? ";" : best.Key.ToString();
    }

    // ---------------- XLSX ----------------

    private static ParsedSheet ReadXlsx(byte[] content, int maxRows)
    {
        // XLSX files are ZIP packages: "PK\x03\x04".
        if (content.Length < 4 || content[0] != 0x50 || content[1] != 0x4B || content[2] != 0x03 || content[3] != 0x04)
        {
            throw new SpreadsheetException("O arquivo não é uma planilha .xlsx válida.");
        }

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(new MemoryStream(content, writable: false));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new SpreadsheetException("Não foi possível ler a planilha. O arquivo pode estar corrompido ou protegido por senha.");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(ws => ws.LastRowUsed() is not null)
                ?? throw new SpreadsheetException("A planilha não contém dados.");

            var firstRow = sheet.FirstRowUsed()!.RowNumber();
            var lastRow = sheet.LastRowUsed()!.RowNumber();
            if (lastRow - firstRow > maxRows + 1000)
            {
                throw TooManyRows(maxRows);
            }

            var firstColumn = sheet.FirstColumnUsed()!.ColumnNumber();
            var lastColumn = sheet.LastColumnUsed()!.ColumnNumber();
            if (lastColumn - firstColumn + 1 > 200)
            {
                throw new SpreadsheetException("A planilha tem colunas demais (máximo de 200).");
            }

            var records = new List<(int Line, string[] Cells)>();
            for (var r = firstRow; r <= lastRow; r++)
            {
                var row = sheet.Row(r);
                var cells = new string[lastColumn - firstColumn + 1];
                for (var c = firstColumn; c <= lastColumn; c++)
                {
                    cells[c - firstColumn] = CellText(row.Cell(c));
                }

                records.Add((r, cells));
            }

            return Build(records, maxRows);
        }
    }

    private static string CellText(IXLCell cell)
    {
        // Formulas are never evaluated: the value cached in the file is used.
        var value = cell.HasFormula ? cell.CachedValue : cell.Value;
        return value.Type switch
        {
            XLDataType.Blank => string.Empty,
            XLDataType.DateTime => value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            XLDataType.Number => NumericCellPrefix + value.GetNumber().ToString("R", CultureInfo.InvariantCulture),
            XLDataType.Boolean => value.GetBoolean() ? "VERDADEIRO" : "FALSO",
            XLDataType.TimeSpan => value.GetTimeSpan().ToString(),
            XLDataType.Error => string.Empty,
            _ => value.GetText()
        };
    }

    // ---------------- shared ----------------

    private static ParsedSheet Build(List<(int Line, string[] Cells)> records, int maxRows)
    {
        var headerIndex = records.FindIndex(r => r.Cells.Any(c => !string.IsNullOrWhiteSpace(c)));
        if (headerIndex < 0)
        {
            throw new SpreadsheetException("O arquivo não contém dados.");
        }

        var rawHeaders = records[headerIndex].Cells;
        var width = rawHeaders.Length;
        var headers = rawHeaders
            .Select((h, i) => string.IsNullOrWhiteSpace(h) ? $"Coluna {i + 1}" : StripMarker(h).Trim())
            .ToArray();

        var rows = new List<SheetRow>();
        var empty = 0;
        foreach (var (line, cells) in records.Skip(headerIndex + 1))
        {
            if (cells.All(string.IsNullOrWhiteSpace))
            {
                empty++;
                continue;
            }

            var padded = new string[Math.Max(width, 0)];
            for (var i = 0; i < width; i++)
            {
                padded[i] = i < cells.Length ? cells[i] ?? string.Empty : string.Empty;
            }

            rows.Add(new SheetRow(line, padded));
            if (rows.Count > maxRows)
            {
                throw TooManyRows(maxRows);
            }
        }

        // Trailing empty rows (common in spreadsheets) are not worth reporting.
        var lastDataLine = rows.Count > 0 ? rows[^1].Number : records[headerIndex].Line;
        var trailingEmpty = records.Count(r => r.Line > lastDataLine && r.Cells.All(string.IsNullOrWhiteSpace));
        if (rows.Count == 0)
        {
            throw new SpreadsheetException("O arquivo tem apenas o cabeçalho, sem linhas de dados.");
        }

        return new ParsedSheet(headers, rows, empty - trailingEmpty);
    }

    public static string StripMarker(string cell) =>
        cell.Length > 0 && cell[0] == NumericCellPrefix ? cell[1..] : cell;

    private static SpreadsheetException TooManyRows(int maxRows) =>
        new(string.Create(CultureInfo.GetCultureInfo("pt-BR"), $"O arquivo tem mais de {maxRows:N0} linhas de dados. Divida-o em arquivos menores."));
}
