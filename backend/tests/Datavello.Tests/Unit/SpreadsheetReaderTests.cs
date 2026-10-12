using System.Text;
using ClosedXML.Excel;
using Datavello.Api.Features.Imports;

namespace Datavello.Tests.Unit;

public sealed class SpreadsheetReaderTests
{
    private static ParsedSheet Csv(string text, Encoding? encoding = null, int maxRows = 100) =>
        SpreadsheetReader.Read((encoding ?? Encoding.UTF8).GetBytes(text), SpreadsheetReader.Csv, maxRows);

    [Fact]
    public void Reads_semicolon_csv_with_quotes_and_blank_lines()
    {
        var sheet = Csv("Data;Descrição;Valor\n01/02/2026;\"Venda; balcão\";1.234,56\n\n02/02/2026;Aluguel;-900,00\n\n\n");

        Assert.Equal(["Data", "Descrição", "Valor"], sheet.Headers);
        Assert.Equal(2, sheet.Rows.Count);
        Assert.Equal(["01/02/2026", "Venda; balcão", "1.234,56"], sheet.Rows[0].Cells);
        Assert.Equal(2, sheet.Rows[0].Number);
        Assert.Equal(4, sheet.Rows[1].Number);
        Assert.Equal(1, sheet.EmptyRows); // trailing blank lines are not counted
    }

    [Fact]
    public void Detects_comma_and_tab_delimiters()
    {
        Assert.Equal(["a", "b", "c"], Csv("a,b,c\n1,2,3").Headers);
        Assert.Equal(["a", "b"], Csv("a\tb\n1\t2").Headers);
    }

    [Fact]
    public void Handles_utf8_bom_and_windows_1252()
    {
        var withBom = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Descrição;Valor\nÁgua;10")).ToArray();
        Assert.Equal("Descrição", SpreadsheetReader.Read(withBom, SpreadsheetReader.Csv, 10).Headers[0]);

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var latin = Csv("Descrição;Valor\nÁgua e luz;10", Encoding.GetEncoding(1252));
        Assert.Equal("Água e luz", latin.Rows[0].Cells[0]);
    }

    [Fact]
    public void Pads_short_rows_and_names_blank_headers()
    {
        var sheet = Csv("Data;;Valor\n01/02/2026");

        Assert.Equal(["Data", "Coluna 2", "Valor"], sheet.Headers);
        Assert.Equal(["01/02/2026", "", ""], sheet.Rows[0].Cells);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Data;Valor\n")]
    public void Rejects_files_without_data(string text)
    {
        Assert.Throws<SpreadsheetException>(() => Csv(text));
    }

    [Fact]
    public void Enforces_the_row_limit()
    {
        var text = "Data;Valor\n" + string.Concat(Enumerable.Repeat("01/01/2026;1\n", 11));

        var ex = Assert.Throws<SpreadsheetException>(() => Csv(text, maxRows: 10));
        Assert.Contains("10", ex.Message);
    }

    [Fact]
    public void Rejects_binary_content_as_csv_and_non_zip_content_as_xlsx()
    {
        Assert.Throws<SpreadsheetException>(() => SpreadsheetReader.Read([0x50, 0x4B, 0x00, 0x01, 0x02], SpreadsheetReader.Csv, 10));
        Assert.Throws<SpreadsheetException>(() => SpreadsheetReader.Read(Encoding.UTF8.GetBytes("Data;Valor\n1;2"), SpreadsheetReader.Xlsx, 10));
        Assert.Throws<SpreadsheetException>(() => SpreadsheetReader.Read([0x50, 0x4B, 0x03, 0x04, 0x00, 0x00], SpreadsheetReader.Xlsx, 10));
    }

    [Theory]
    [InlineData("extrato.csv", "csv")]
    [InlineData("Planilha.XLSX", "xlsx")]
    [InlineData("antiga.xls", null)]
    [InlineData("macro.xlsm", null)]
    [InlineData("dados.txt", null)]
    public void Accepts_only_csv_and_xlsx(string fileName, string? expected)
    {
        Assert.Equal(expected, SpreadsheetReader.FileTypeFor(fileName));
    }

    [Fact]
    public void Reads_xlsx_dates_numbers_text_and_cached_formula_values()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Lançamentos");
        sheet.Cell(1, 1).Value = "Data";
        sheet.Cell(1, 2).Value = "Descrição";
        sheet.Cell(1, 3).Value = "Valor";
        sheet.Cell(2, 1).Value = new DateTime(2026, 3, 15);
        sheet.Cell(2, 2).Value = "Venda";
        sheet.Cell(2, 3).Value = 1234.5;
        sheet.Cell(3, 1).Value = "16/03/2026";
        sheet.Cell(3, 2).Value = "Fórmula";
        sheet.Cell(3, 3).FormulaA1 = "C2*2";
        sheet.Cell(4, 1).Value = "17/03/2026";
        sheet.Cell(4, 2).Value = "Fórmula sem valor salvo";
        sheet.Cell(4, 3).FormulaA1 = "C2*3";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        // Excel saves the last calculated value of each formula (<v>) next to it; ClosedXML does not,
        // so add it to the first formula the way Excel would. The reader must use that cached value
        // and must never evaluate formulas itself (the second formula has no cached value).
        var content = WithCachedFormulaValue(stream.ToArray(), "<x:f>C2*2</x:f>", "2469");

        var parsed = SpreadsheetReader.Read(content, SpreadsheetReader.Xlsx, 100);

        Assert.Equal(["Data", "Descrição", "Valor"], parsed.Headers);
        Assert.Equal("2026-03-15", parsed.Rows[0].Cells[0]);
        Assert.Equal("\u001F1234.5", parsed.Rows[0].Cells[2]);
        Assert.Equal("16/03/2026", parsed.Rows[1].Cells[0]);
        Assert.Equal("\u001F2469", parsed.Rows[1].Cells[2]);
        Assert.Equal(3, parsed.Rows[1].Number);
        Assert.Equal("", parsed.Rows[2].Cells[2]);
    }

    private static byte[] WithCachedFormulaValue(byte[] xlsx, string formulaElement, string cachedValue)
    {
        using var buffer = new MemoryStream();
        buffer.Write(xlsx);
        using (var zip = new System.IO.Compression.ZipArchive(buffer, System.IO.Compression.ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!;
            string xml;
            using (var reader = new StreamReader(entry.Open()))
            {
                xml = reader.ReadToEnd();
            }

            Assert.Contains(formulaElement, xml);
            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open());
            writer.Write(xml.Replace(formulaElement, formulaElement + "<x:v>" + cachedValue + "</x:v>"));
        }

        return buffer.ToArray();
    }
}
