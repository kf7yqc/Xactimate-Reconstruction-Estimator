using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Xre.App.Import;

public sealed record ImportedLine(int Number, string Description, decimal Quantity, string Unit, string Room, int Page);
public sealed record ImportResult(string? EstimateNumber, string? PriceList, IReadOnlyList<ImportedLine> Lines, IReadOnlyList<string> Warnings);

/// <summary>
/// Extracts a first-pass, review-required room/line-item index from Xactimate-style PDFs.
/// No proprietary price lists are read or inferred.
/// </summary>
public static class XactimatePdfImporter
{
    private static readonly Regex NumberedLine = new(@"^\s*(?<number>\d{1,4})\.\s+(?<description>.+?)\s+(?<qty>\d[\d,]*\.\d{2})\s+(?<unit>SF|LF|SY|EA|HR|DA|CY|CF|SQ)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PriceList = new(@"Price List:\s*(\S+)", RegexOptions.IgnoreCase);
    private static readonly Regex Estimate = new(@"Estimate:\s*(\S+)", RegexOptions.IgnoreCase);
    private static readonly Regex Continued = new(@"CONTINUED\s*-\s*(Bathroom|Laundry Room|Kitchen|Bedroom|Living Room|Hallway)", RegexOptions.IgnoreCase);
    public static ImportResult Import(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("PDF file not found.", path);
        var lines = new List<ImportedLine>();
        var warnings = new List<string>();
        string? estimate = null, priceList = null;
        string room = "General";
        using var pdf = PdfDocument.Open(path);
        foreach (var page in pdf.GetPages())
        {
            // Group words by their PDF coordinates rather than trusting page.Text ordering.
            // This retains one physical estimate row per line in multi-column Xactimate PDFs.
            var words = page.GetWords().ToList();
            var rows = words.GroupBy(w => Math.Round(w.BoundingBox.Bottom, 0))
                .OrderByDescending(g => g.Key)
                .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)))
                .ToList();
            var text = string.Join("\n", rows);
            estimate ??= Estimate.Match(text) is { Success: true } e ? e.Groups[1].Value : null;
            priceList ??= PriceList.Match(text) is { Success: true } p ? p.Groups[1].Value : null;
            var continued = Continued.Match(text);
            if (continued.Success) room = continued.Groups[1].Value;
            else if (Regex.IsMatch(text, @"\bLaundry Room\b", RegexOptions.IgnoreCase) && Regex.IsMatch(text, @"\bDESCRIPTION\b")) room = "Laundry Room";
            else if (Regex.IsMatch(text, @"\bBathroom\b", RegexOptions.IgnoreCase) && Regex.IsMatch(text, @"\bDESCRIPTION\b")) room = "Bathroom";
            foreach (var raw in Regex.Split(text, @"\r?\n"))
            {
                var match = NumberedLine.Match(raw);
                if (!match.Success) continue;
                if (!decimal.TryParse(match.Groups["qty"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty)) continue;
                lines.Add(new ImportedLine(int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture), match.Groups["description"].Value.Trim(), qty, match.Groups["unit"].Value.ToUpperInvariant(), room, page.Number));
            }
        }
        if (lines.Count == 0) throw new InvalidDataException("No line items detected. This PDF cannot be converted safely. Verify it contains selectable text and supported Xactimate line-item layout.");
        foreach (var pageNumber in Enumerable.Range(1, pdf.NumberOfPages))
        {
            // Diagnostic counts help identify blank or missed estimate pages without exposing customer data.
            var count = lines.Count(x => x.Page == pageNumber);
            if (count > 0) warnings.Add($"Page {pageNumber}: {count} numbered items extracted.");
        }
        var duplicates = lines.GroupBy(x => x.Number).Where(x => x.Count() > 1).Select(x => x.Key).ToArray();
        if (duplicates.Length > 0) warnings.Add("Duplicate source item numbers detected: " + string.Join(", ", duplicates));
        if (lines.Count > 0)
        {
            var numbers = lines.Select(x => x.Number).Distinct().OrderBy(x => x).ToArray();
            var missing = Enumerable.Range(numbers[0], numbers[^1] - numbers[0] + 1).Except(numbers).Take(25).ToArray();
            if (missing.Length > 0) warnings.Add("Possible missing source item numbers: " + string.Join(", ", missing));
        }
        warnings.Add("All parsed quantities and room assignments require review against the source PDF.");
        warnings.Add("This import does not provide reconstruction prices or verify current Xactimate pricing.");
        return new ImportResult(estimate, priceList, lines, warnings);
    }
}
