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
    Check(File.ReadAllText(html).Contains("NOT A PRICED ESTIMATE"), "Report clearly states unpriced status");
    Check(File.ReadAllText(csv).Contains("Unit Price,Line Total"), "CSV has blank pricing columns");
}
finally { Directory.Delete(temp, true); }
Console.WriteLine("All smoke tests passed.");
