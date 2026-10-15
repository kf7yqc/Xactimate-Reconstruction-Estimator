using Microsoft.VisualBasic.FileIO;
using System.Globalization;

namespace Xre.App.Reconstruction;

public sealed record PriceListItem(string Code, string Description, string Unit, decimal UnitPrice);
public sealed record PriceListImport(string SourceFile, IReadOnlyList<PriceListItem> Items);

public static class PriceListCsvImporter
{
    public static PriceListImport Import(string path)
    {
        using var parser = new TextFieldParser(path);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;
        var headers = parser.ReadFields() ?? throw new InvalidDataException("Missing CSV header.");
        static string Normalize(string value) => new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        int Column(params string[] names) => Array.FindIndex(headers, h => names.Contains(Normalize(h)));
        var code = Column("code", "selector", "itemcode", "lineitemcode", "xactimatecode");
        var description = Column("description", "itemdescription", "lineitemdescription");
        var unit = Column("unit", "uom", "unitofmeasure");
        var price = Column("unitprice", "price", "rate", "unitcost");
        if (code < 0 || description < 0 || unit < 0 || price < 0)
            throw new InvalidDataException("CSV must contain Code/Selector, Description, Unit, and Unit Price columns.");
        var items = new List<PriceListItem>();
        while (!parser.EndOfData)
        {
            var cells = parser.ReadFields();
            if (cells is null || cells.Length <= new[] { code, description, unit, price }.Max()) continue;
            var selector = cells[code].Trim();
            var desc = cells[description].Trim();
            var uom = cells[unit].Trim();
            var raw = cells[price].Trim().Replace("$", "").Replace(",", "");
            if (selector.Length == 0 || desc.Length == 0 || uom.Length == 0) continue;
            if (!decimal.TryParse(raw, NumberStyles.Number | NumberStyles.AllowCurrencySymbol,
                CultureInfo.InvariantCulture, out var amount) || amount < 0) continue;
            items.Add(new PriceListItem(selector, desc, uom, amount));
        }
        if (items.Count == 0) throw new InvalidDataException("No valid pricing line items were found.");
        return new PriceListImport(Path.GetFileName(path), items);
    }

    public static int ApplyExactMatches(IEnumerable<EditableScopeLine> lines, PriceListImport catalog)
    {
        var applied = 0;
        foreach (var line in lines)
        {
            // Do not silently map reconstruction descriptions to unrelated Xactimate selectors.
            var matches = catalog.Items.Where(x =>
                x.Description.Equals(line.Work, StringComparison.OrdinalIgnoreCase) &&
                x.Unit.Equals(line.Unit, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            if (matches.Length != 1) continue;
            line.UnitPrice = matches[0].UnitPrice;
            line.ItemCode = matches[0].Code;
            applied++;
        }
        return applied;
    }
}
