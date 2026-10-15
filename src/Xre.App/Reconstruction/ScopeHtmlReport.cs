using System.Net;
using System.Text;

namespace Xre.App.Reconstruction;

/// <summary>
/// Standalone, printable, unpriced scope review. Open in browser and Print > Save as PDF.
/// </summary>
public static class ScopeHtmlReport
{
    public static void Export(string path, string? estimate, string? priceList, IEnumerable<EditableScopeLine> lines)
    {
        var items = lines.ToList();
        static string H(object? value) => WebUtility.HtmlEncode(Convert.ToString(value) ?? "");
        var html = new StringBuilder();
        html.AppendLine("<!doctype html><html lang='en'><head><meta charset='utf-8'><title>Reconstruction Scope Review</title>");
        html.AppendLine("<style>@page{size:letter;margin:0.6in}body{font:12px 'Segoe UI',Arial,sans-serif;color:#182235;margin:25px}h1{color:#17436b;font-size:22px}header{border-bottom:3px solid #1697bf;padding-bottom:12px}small,.muted{color:#536578}table{width:100%;border-collapse:collapse;margin-top:20px}th{background:#e7f0f7;text-align:left}th,td{padding:8px;border-bottom:1px solid #d8e0e8;vertical-align:top}tr{break-inside:avoid}td.qty{text-align:right;white-space:nowrap}.warning{border:1px solid #d5a34a;padding:10px;margin-top:15px;background:#fff9e9}@media print{body{margin:0}thead{display:table-header-group}}</style></head><body>");
        html.AppendLine("<header><h1>RECONSTRUCTION SCOPE REVIEW</h1><div><strong>Source estimate:</strong> " + H(estimate ?? "Unknown") + " &nbsp; <strong>Source price list:</strong> " + H(priceList ?? "Unknown") + "</div><small>Generated " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm zzz") + " | Review draft — NOT A PRICED ESTIMATE</small></header>");
        html.AppendLine("<div class='warning'><strong>Unpriced scope only.</strong> Quantities and assemblies require field verification. Approval flags indicate internal review only, not carrier approval. No Xactimate pricing, taxes, overhead, profit, or totals are provided.</div>");
        html.AppendLine("<table><thead><tr><th>Source</th><th>Room</th><th>Proposed reconstruction work</th><th>Quantity</th><th>Review</th></tr></thead><tbody>");
        foreach (var line in items)
        {
            html.Append("<tr><td>").Append(line.SourceLine).Append("</td><td>").Append(H(line.Room))
                .Append("</td><td><strong>").Append(H(line.Work)).Append("</strong><div class='muted'>")
                .Append(H(line.ReviewNote)).Append("</div></td><td class='qty'>")
                .Append(H(line.Quantity.ToString("0.##"))).Append(' ').Append(H(line.Unit))
                .Append("</td><td>").Append(line.Approved ? "Reviewed" : "Pending review").AppendLine("</td></tr>");
        }
        html.AppendLine("</tbody></table><p class='muted'>Source references trace back to the imported mitigation PDF. Verify all proposed work against site conditions before preparing a formal estimate.</p></body></html>");
        File.WriteAllText(path, html.ToString(), new UTF8Encoding(false));
    }
}
