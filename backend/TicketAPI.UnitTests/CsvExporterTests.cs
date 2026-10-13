using TicketAPI.Domain;
using Xunit;

namespace TicketAPI.UnitTests;

// UC_13.3 / UC_14.2: CSV files must open safely in Excel.
public class CsvExporterTests
{
    [Fact]
    public void PlainValuesAreWrittenAsIs()
    {
        Assert.Equal("hello", CsvExporter.Escape("hello"));
        Assert.Equal(string.Empty, CsvExporter.Escape(null));
    }

    [Fact]
    public void CommasQuotesAndNewlinesAreQuoted()
    {
        Assert.Equal("\"a,b\"", CsvExporter.Escape("a,b"));
        Assert.Equal("\"say \"\"hi\"\"\"", CsvExporter.Escape("say \"hi\""));
        Assert.Equal("\"line1\nline2\"", CsvExporter.Escape("line1\nline2"));
    }

    [Fact]
    public void FormulaStartsAreNeutralised()
    {
        Assert.Equal("'=SUM(A1:A2)", CsvExporter.Escape("=SUM(A1:A2)"));
        Assert.Equal("'@cmd", CsvExporter.Escape("@cmd"));
        Assert.Equal("'+cmd|' /C calc'!A0", CsvExporter.Escape("+cmd|' /C calc'!A0"));
    }

    [Fact]
    public void NegativeNumbersAreNotTreatedAsFormulas()
    {
        Assert.Equal("-5000", CsvExporter.Escape("-5000"));
        Assert.Equal("-12.5", CsvExporter.Escape("-12.5"));
    }

    [Fact]
    public void BuildJoinsHeaderAndRowsWithCrLf()
    {
        var csv = CsvExporter.Build(
            new[] { "Name", "Amount" },
            new[]
            {
                new string?[] { "Tan", "100" },
                new string?[] { "Vinh, N", null },
            });

        Assert.Equal("Name,Amount\r\nTan,100\r\n\"Vinh, N\",\r\n", csv);
    }
}
