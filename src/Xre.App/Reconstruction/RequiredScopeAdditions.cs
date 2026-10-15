using Xre.App.Import;

namespace Xre.App.Reconstruction;

/// <summary>Required estimate additions; selectors and prices must be matched against licensed data.</summary>
public static class RequiredScopeAdditions
{
    public static IReadOnlyList<ReconstructionCandidate> Generate(
        IEnumerable<ImportedLine> sourceLines, IEnumerable<ReconstructionCandidate> proposed)
    {
        var source = sourceLines.ToList();
        var work = proposed.ToList();
        if (work.Count == 0) return work;
        var commercial = source.Any(x =>
            x.Description.Contains("commercial", StringComparison.OrdinalIgnoreCase) ||
            x.Room.Contains("commercial", StringComparison.OrdinalIgnoreCase));
        var propertyType = commercial ? "Commercial" : "Residential";
        var rooms = work.Select(x => x.Room).Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var tradeTypes = work.Select(x => x.Work.ToLowerInvariant()).Select(x =>
            x.Contains("floor") || x.Contains("tile") ? "flooring" :
            x.Contains("drywall") || x.Contains("insulation") ? "wall" :
            x.Contains("cabinet") || x.Contains("counter") ? "cabinetry" : "general")
            .Distinct().Count();
        // Heuristic, deliberately surfaced for estimator approval; not a billed-hours standard.
        var suggestedHours = Math.Max(1m, Math.Ceiling(work.Count / 8m) + Math.Max(0, rooms - 1) +
            Math.Max(0, tradeTypes - 1) + (commercial ? 1 : 0));
        var nextLine = Math.Max(1, source.Select(x => x.Number).DefaultIfEmpty(0).Max() + 1);
        work.Add(new ReconstructionCandidate(nextLine++, "General",
            propertyType + " reconstruction supervisor / project oversight", suggestedHours, "HR",
            "REQUIRED REVIEW: heuristic hours based on item count, rooms and trades. Confirm property classification, actual supervision, rate, and whether this work is already included in line-item labor or O&P."));
        work.Add(new ReconstructionCandidate(nextLine++, "General",
            propertyType + " post-reconstruction cleaning", 1m, "EA",
            "REQUIRED REVIEW: determine actual cleanable area, appropriate unit (SF/HR/EA), cleaning level, and applicable price-list selector. Avoid duplication."));
        return work;
    }
}
