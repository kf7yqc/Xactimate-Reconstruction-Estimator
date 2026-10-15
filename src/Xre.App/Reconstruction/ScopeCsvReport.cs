using System.Globalization;
using System.Text;

namespace Xre.App.Reconstruction;

/// <summary>
/// Exports review lines to a spreadsheet-compatible CSV. No proprietary selectors or prices.
/// </summary>
public static class ScopeCsvReport
{
    private static string Cell(string? value)
    {
        var text = value ?? "";
        // Avoid spreadsheet formula injection from untrusted PDF descriptions.
        if (text.Length > 0 && "=+-@".Contains(text[0])) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    public static void Export(string path, IEnumerable<EditableScopeLine> lines)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Source Line,Room,Proposed Work,Quantity,Unit,Review Note,Approved,Unit Price,Line Total");
        foreach (var line in lines)
        {
            csv.Append(string.Join(",", new[]
            {
                line.SourceLine.ToString(CultureInfo.InvariantCulture),
                Cell(line.Room),
                Cell(line.Work),
                line.Quantity.ToString(CultureInfo.InvariantCulture),
                Cell(line.Unit),
                Cell(line.ReviewNote),
                line.Approved ? "Yes" : "No",
                "", ""
            })).AppendLine();
        }
        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
    }
}
