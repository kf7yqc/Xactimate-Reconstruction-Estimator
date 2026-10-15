using Xre.App.Import;

namespace Xre.App.Reconstruction;

public sealed record ReconstructionCandidate(
    int SourceLine, string Room, string Work, decimal Quantity, string Unit,
    string ReviewNote, bool Approved = false);

public static class ReconstructionMapper
{
    // These are suggested scope descriptions, NOT licensed Xactimate selectors or prices.
    // Exact material, assembly and quantities must be confirmed by an estimator.
    public static IReadOnlyList<ReconstructionCandidate> Map(IEnumerable<ImportedLine> lines)
    {
        var output = new List<ReconstructionCandidate>();
        foreach (var item in lines)
        {
            var d = item.Description.ToLowerInvariant();
            void Add(string work, decimal qty, string unit, string note) =>
                output.Add(new(item.Number, item.Room, work, qty, unit, note));

            if (d.Contains("tear out wet drywall") && item.Unit == "LF")
                Add("Install and finish replacement drywall (maximum 2 ft height)", Math.Round(item.Quantity * 2m, 2), "SF",
                    "VERIFY actual removal height, drywall type, finish and paint extent.");
            else if (d.Contains("tear out and bag wet insulation") && item.Unit == "LF")
                Add("Install matching cavity insulation (maximum 2 ft height)", Math.Round(item.Quantity * 2m, 2), "SF",
                    "VERIFY actual height, insulation R-value, cavity depth and code requirements.");
            else if (d.Contains("tear out non-salvageable tile floor"))
                Add("Install replacement tile flooring", item.Quantity, item.Unit,
                    "VERIFY tile type, matching, waste, substrate and waterproofing.");
            else if (d.Contains("tear out mortar bed") || d.Contains("add on to tear out mortar bed"))
                Add("Restore tile mortar/setting assembly", item.Quantity, item.Unit,
                    "VERIFY assembly; avoid overlapping underlayment/backer-board items.");
            else if (d.Contains("tear out non-salvageable cement"))
                Add("Install replacement cement backer board", item.Quantity, item.Unit,
                    "VERIFY substrate assembly and appropriate installation.");
            else if (d.Contains("tear out non-salv underlayment"))
                Add("Install replacement underlayment", item.Quantity, item.Unit,
                    "VERIFY material and potential overlap with other assembly items.");
            else if (d.Contains("tear out non-salv floating floor"))
                Add("Install matching floating floor", item.Quantity, item.Unit,
                    "VERIFY flooring product, transitions, waste and adjoining areas.");
            else if (d.Contains("baseboard - detach"))
                Add("Reset existing baseboard", item.Quantity, item.Unit,
                    "VERIFY baseboard is retained and serviceable; finish work separately.");
            else if (d.Contains("tear out toe kick"))
                Add("Repair/replace removed toe kick", item.Quantity, item.Unit,
                    "CHECK source estimate for already included re-skin to prevent duplication.");
            else if ((d.Contains("washer") || d.Contains("dryer")) && d.Contains("detach"))
                Add("Reset existing appliance", item.Quantity, item.Unit,
                    "VERIFY retained appliance, connection requirements and serviceability.");
            else if (d.Contains("bathtub - detach") || d.Contains("sink - single basin - detach") ||
                     (d.Contains("cabinet - lower") && d.Contains("detach")) ||
                     (d.Contains("countertop") && d.Contains("detach")))
                Add("Reset existing detached component", item.Quantity, item.Unit,
                    "VERIFY retained component, condition, and required reconnections.");
        }
        return output;
    }
}
