using Xre.App.Import;
using Xre.App.Reconstruction;

static void Check(bool pass, string message)
{
    if (!pass) throw new Exception("FAILED: " + message);
    Console.WriteLine("PASS: " + message);
}

var sample = new[]
{
    new ImportedLine(38, "Tear out non-salvageable tile floor", 38.72m, "SF", "Bathroom", 4),
    new ImportedLine(39, "Add on to tear out mortar bed for", 38.72m, "SF", "Bathroom", 4),
    new ImportedLine(40, "Tear out non-salvageable cement", 38.72m, "SF", "Bathroom", 4),
    new ImportedLine(55, "Baseboard - Detach", 69.17m, "LF", "Laundry Room", 5),
    new ImportedLine(61, "Tear out wet drywall, cleanup, bag,", 69.17m, "LF", "Laundry Room", 5),
    new ImportedLine(62, "Tear out and bag wet insulation,", 69.17m, "LF", "Laundry Room", 6),
    new ImportedLine(63, "Tear out non-salv floating floor &", 185.89m, "SF", "Laundry Room", 6),
    new ImportedLine(64, "Tear out non-salv underlayment &", 185.89m, "SF", "Laundry Room", 6),
};
var toeKickSource = new[]
{
    new ImportedLine(34, "Tear out toe kick and bag for disposal", 4m, "LF", "Bathroom", 4),
    new ImportedLine(35, "Re-skin toe kick", 4m, "LF", "Bathroom", 4)
};
Check(!ReconstructionMapper.Map(toeKickSource).Any(x => x.SourceLine == 34),
    "Do not duplicate matching toe-kick re-skin already in source estimate");

var mapped = ReconstructionMapper.Map(sample);
Check(mapped.Count == sample.Length, "Every known sample work item maps");
Check(mapped.Single(x => x.SourceLine == 61).Quantity == 138.34m, "Drywall maximum 2-foot quantity");
Check(mapped.Single(x => x.SourceLine == 62).Quantity == 138.34m, "Insulation maximum 2-foot quantity");
Check(mapped.Single(x => x.SourceLine == 39).Work.Contains("mortar"), "Mortar-bed wording");
Check(mapped.Single(x => x.SourceLine == 40).Work.Contains("cement"), "Cement-board wording");
Check(mapped.Single(x => x.SourceLine == 55).Work.Contains("Reset"), "Detached baseboard is reset, not replaced");

var draft = ScopeReviewStore.FromCandidates(mapped);
draft[0].Quantity = 40.00m;
draft[0].Approved = true;
var temp = Path.Combine(Path.GetTempPath(), "xre-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var json = Path.Combine(temp, "review.json");
    var html = Path.Combine(temp, "scope.html");
    var csv = Path.Combine(temp, "scope.csv");
    ScopeReviewStore.Save(json, "SAMPLE-11", "REGION-MONTH", draft);
    var reopened = ScopeReviewStore.Load(json);
    Check(reopened.Lines.Count == draft.Count && reopened.Lines[0].Quantity == 40.00m && reopened.Lines[0].Approved,
        "Save/reopen preserves quantities and approval");
    ScopeHtmlReport.Export(html, reopened.SourceEstimate, reopened.SourcePriceList, reopened.Lines);
    ScopeCsvReport.Export(csv, reopened.Lines);
    reopened.Lines[0].UnitPrice = 12.50m;
    Check(reopened.Lines[0].LineTotal == 500m, "Unit pricing multiplies reviewed quantity");
    var pdf = Path.Combine(temp, "estimate.pdf");
    ScopePdfEstimate.Export(pdf, reopened.SourceEstimate, reopened.SourcePriceList, reopened.Lines);
    Check(File.Exists(pdf) && new FileInfo(pdf).Length > 1000, "Native PDF estimate created");
    var cumulativeBase = 10000m;
    var overhead = decimal.Round(cumulativeBase * 0.10m, 2, MidpointRounding.AwayFromZero);
    var profit = decimal.Round((cumulativeBase + overhead) * 0.10m, 2, MidpointRounding.AwayFromZero);
    Check(cumulativeBase + overhead + profit == 12100m, "Cumulative 10/10 O&P equals 21 percent markup");
    Check(File.ReadAllText(html).Contains("NOT A PRICED ESTIMATE"), "Report clearly states unpriced status");
    Check(File.ReadAllText(csv).Contains("Unit Price,Line Total"), "CSV has blank pricing columns");
}
finally { Directory.Delete(temp, true); }
// Optional private sample validation: set XRE_SAMPLE_PDF to the local PDF path.
// The sample file is never committed to the public repository.
var pdfPath = Environment.GetEnvironmentVariable("XRE_SAMPLE_PDF");
if (!string.IsNullOrWhiteSpace(pdfPath))
{
    var parsed = XactimatePdfImporter.Import(pdfPath);
    Check(parsed.Lines.Count == 71, "Windows PdfPig importer extracts all 71 sample items");
    Check(parsed.Lines.Select(x => x.Number).OrderBy(x => x).SequenceEqual(Enumerable.Range(1, 71)),
        "Windows PdfPig importer has no missing/duplicate sample item numbers");
    foreach (var (room, expected) in new[] { ("General", 13), ("Bathroom", 34), ("Laundry Room", 24) })
        Check(parsed.Lines.Count(x => x.Room.Equals(room, StringComparison.OrdinalIgnoreCase)) == expected,
            $"Room count: {room}");
    foreach (var (number, quantity, unit) in new[]
    {
        (38, 38.72m, "SF"), (55, 69.17m, "LF"), (61, 69.17m, "LF"),
        (62, 69.17m, "LF"), (63, 185.89m, "SF"), (64, 185.89m, "SF")
    })
    {
        var line = parsed.Lines.Single(x => x.Number == number);
        Check(line.Quantity == quantity && line.Unit == unit, $"Sample item {number} quantity");
    }
}
else
{
    Console.WriteLine("Sample PDF acceptance test SKIPPED: XRE_SAMPLE_PDF is not set.");
}
Console.WriteLine("All executed smoke tests passed.");
