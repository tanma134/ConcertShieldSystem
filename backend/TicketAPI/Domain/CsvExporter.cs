using System.Globalization;
using System.Text;

namespace TicketAPI.Domain;

// Writes CSV text that is safe to open in Excel (UC_13.3 and UC_14.2).
public static class CsvExporter
{
    // A cell that starts with = + - @ is treated as a formula by spreadsheet apps
    // (CSV injection). Prefixing a single quote makes it plain text.
    public const string FormulaPrefix = "'";

    private const string FormulaStarts = "=+-@\t\r";

    // Escapes one cell: neutralises formulas, then quotes it when it contains
    // a comma, quote or line break.
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var cell = value;

        if (FormulaStarts.Contains(cell[0]) && !IsPlainNumber(cell))
            cell = FormulaPrefix + cell;

        var needsQuotes = cell.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        return needsQuotes ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
    }

    // Builds the whole file: one header line followed by one line per row.
    // Lines end with CRLF as RFC 4180 asks.
    public static string Build(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var text = new StringBuilder();

        text.Append(string.Join(",", headers.Select(Escape))).Append("\r\n");

        foreach (var row in rows)
            text.Append(string.Join(",", row.Select(Escape))).Append("\r\n");

        return text.ToString();
    }

    // Numbers such as -5000 or -12.5 start with "-" but are not formulas.
    private static bool IsPlainNumber(string value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
    }
}
