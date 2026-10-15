using System.ComponentModel;
using System.Text.Json;

namespace Xre.App.Reconstruction;

public sealed class EditableScopeLine : INotifyPropertyChanged
{
    public int SourceLine { get; set; }
    public string Room { get; set; } = "";
    public string Work { get; set; } = "";
    private decimal quantity;
    public decimal Quantity { get => quantity; set { if (value < 0) throw new ArgumentOutOfRangeException(nameof(Quantity)); quantity = value; Changed(nameof(Quantity)); } }
    public string Unit { get; set; } = "";
    public string ReviewNote { get; set; } = "";
    private bool approved;
    public bool Approved { get => approved; set { approved = value; Changed(nameof(Approved)); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

public sealed record ScopeReviewDocument(string? SourceEstimate, string? SourcePriceList, DateTimeOffset SavedAtUtc, List<EditableScopeLine> Lines);

public static class ScopeReviewStore
{
    public static BindingList<EditableScopeLine> FromCandidates(IEnumerable<ReconstructionCandidate> source) =>
        new(source.Select(x => new EditableScopeLine
        {
            SourceLine = x.SourceLine, Room = x.Room, Work = x.Work, Quantity = x.Quantity,
            Unit = x.Unit, ReviewNote = x.ReviewNote, Approved = false
        }).ToList());

    public static ScopeReviewDocument Load(string filePath)
    {
        var document = JsonSerializer.Deserialize<ScopeReviewDocument>(File.ReadAllText(filePath))
            ?? throw new InvalidDataException("Review draft is empty or invalid.");
        if (document.Lines is null || document.Lines.Count > 5000)
            throw new InvalidDataException("Review draft has invalid line data.");
        foreach (var line in document.Lines)
            if (line.Quantity < 0 || line.SourceLine < 1 || string.IsNullOrWhiteSpace(line.Work))
                throw new InvalidDataException("Review draft contains an invalid line.");
        return document;
    }

    public static void Save(string filePath, string? estimate, string? priceList, IEnumerable<EditableScopeLine> lines)
    {
        var document = new ScopeReviewDocument(estimate, priceList, DateTimeOffset.UtcNow, lines.ToList());
        var temp = filePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, filePath, true);
    }
}
