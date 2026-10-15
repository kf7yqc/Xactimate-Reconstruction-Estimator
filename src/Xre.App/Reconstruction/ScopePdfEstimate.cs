using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.Globalization;

namespace Xre.App.Reconstruction;

/// <summary>Original, Xactimate-inspired estimate layout; not an official Xactimate document.</summary>
public static class ScopePdfEstimate
{
    public static void Export(string path, string? estimate, string? priceList, IEnumerable<EditableScopeLine> lines)
    {
        var items = lines.ToList();
        using var document = new PdfDocument();
        document.Info.Title = "Reconstruction Estimate - " + (estimate ?? "Draft");
        var navy = XColor.FromArgb(24, 42, 66);
        var teal = XColor.FromArgb(0, 125, 160);
        var gray = XColor.FromArgb(93, 105, 119);
        var regular = new XFont("Arial", 8, XFontStyleEx.Regular);
        var bold = new XFont("Arial", 8, XFontStyleEx.Bold);
        var heading = new XFont("Arial", 17, XFontStyleEx.Bold);
        PdfPage? page = null;
        XGraphics? graphics = null;
        double y = 0;
        var pageNo = 0;
        void Text(string value, XFont font, XColor color, double x, double top, double width, XStringFormat? format = null)
        {
            graphics!.DrawString(value, font, new XSolidBrush(color),
                new XRect(x, top, width, 16), format ?? XStringFormats.TopLeft);
        }
        void NewPage()
        {
            graphics?.Dispose();
            page = document.AddPage();
            page.Size = PdfSharp.PageSize.Letter;
            graphics = XGraphics.FromPdfPage(page);
            pageNo++;
            y = 40;
            graphics.DrawRectangle(new XSolidBrush(navy), 0, 0, page.Width.Point, 12);
            Text("RECONSTRUCTION ESTIMATE", heading, navy, 38, y, 530);
            y += 32;
            Text("Source estimate: " + (estimate ?? "Unknown"), bold, navy, 38, y, 360);
            Text("Date: " + DateTime.Today.ToString("MM/dd/yyyy"), regular, gray, 410, y, 160);
            y += 18;
            Text("Source price list: " + (priceList ?? "Not supplied"), regular, gray, 38, y, 480);
            y += 25;
            graphics.DrawRectangle(new XSolidBrush(teal), 38, y, 536, 24);
            Text("#", bold, XColors.White, 44, y + 5, 24);
            Text("DESCRIPTION / ROOM", bold, XColors.White, 73, y + 5, 257);
            Text("QTY", bold, XColors.White, 348, y + 5, 56, XStringFormats.TopRight);
            Text("UNIT", bold, XColors.White, 410, y + 5, 36);
            Text("UNIT $", bold, XColors.White, 450, y + 5, 54, XStringFormats.TopRight);
            Text("TOTAL $", bold, XColors.White, 508, y + 5, 60, XStringFormats.TopRight);
            y += 31;
        }
        NewPage();
        decimal total = 0;
        var unpriced = 0;
        var index = 0;
        foreach (var line in items)
        {
            if (y > 700) NewPage();
            index++;
            var value = line.LineTotal;
            if (value.HasValue) total += value.Value; else unpriced++;
            var label = line.Work ?? "";
            // Limit text to table width, and show room on a separate line.
            var chunks = new List<string>();
            while (label.Length > 56)
            {
                var cut = label.LastIndexOf(' ', 56);
                if (cut < 15) cut = 56;
                chunks.Add(label[..cut].Trim());
                label = label[cut..].TrimStart();
            }
            chunks.Add(label);
            var height = Math.Max(35, chunks.Count * 12 + 23);
            if (y + height > 720) NewPage();
            if (index % 2 == 0)
                graphics!.DrawRectangle(new XSolidBrush(XColor.FromArgb(241, 246, 250)), 38, y - 3, 536, height);
            Text(index.ToString(CultureInfo.InvariantCulture), regular, navy, 44, y, 24);
            for (var j = 0; j < chunks.Count; j++)
                Text(chunks[j], j == 0 ? bold : regular, navy, 73, y + j * 12, 268);
            Text(line.Room ?? "", regular, gray, 73, y + chunks.Count * 12, 260);
            Text(line.Quantity.ToString("N2", CultureInfo.InvariantCulture), regular, navy, 343, y, 61, XStringFormats.TopRight);
            Text(line.Unit ?? "", regular, navy, 410, y, 36);
            Text(line.UnitPrice?.ToString("N2", CultureInfo.InvariantCulture) ?? "—", regular, navy, 450, y, 54, XStringFormats.TopRight);
            Text(value?.ToString("N2", CultureInfo.InvariantCulture) ?? "—", regular, navy, 508, y, 60, XStringFormats.TopRight);
            y += height;
            graphics!.DrawLine(new XPen(XColor.FromArgb(222, 229, 236), 0.6), 38, y - 3, 574, y - 3);
        }
        if (y > 665) NewPage();
        y += 15;
        graphics!.DrawLine(new XPen(teal, 2), 350, y, 574, y);
        y += 10;
        Text("PRICED LINE SUBTOTAL", bold, navy, 350, y, 150);
        Text(total.ToString("C2", CultureInfo.GetCultureInfo("en-US")), bold, navy, 495, y, 73, XStringFormats.TopRight);
        y += 22;
        Text($"Unpriced lines: {unpriced} of {items.Count}", regular, gray, 350, y, 220);
        y += 22;
        Text("Taxes, O&P, permits and adjustments are NOT calculated.", regular, gray, 38, y, 530);
        y += 16;
        Text("DRAFT — NOT AN OFFICIAL XACTIMATE ESTIMATE. Verify all scope, quantities and rates.", bold, navy, 38, y, 536);
        graphics.Dispose();
        document.Save(path);
    }
}
