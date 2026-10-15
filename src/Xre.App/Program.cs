using Xre.App.Import;
using Xre.App.Reconstruction;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;

namespace Xre.App;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    private readonly Label status = new()
    {
        Dock = DockStyle.Bottom, Height = 40, ForeColor = Color.LightGray,
        Text = "Ready — drop a mitigation estimate PDF anywhere in the window"
    };
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill, BackgroundColor = Color.FromArgb(25, 34, 48),
        ForeColor = Color.Black, AutoGenerateColumns = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        AllowUserToAddRows = false, ReadOnly = true
    };
    private readonly Label heading = new()
    {
        Dock = DockStyle.Top, Height = 70,
        Text = "RECONSTRUCTION ESTIMATOR  |  REVIEW WORKSPACE",
        Font = new Font("Segoe UI", 17, FontStyle.Bold),
        ForeColor = Color.DeepSkyBlue, TextAlign = ContentAlignment.MiddleCenter
    };
    private readonly Button bell = new() { Text = "Updates (planned)", Dock = DockStyle.Right, Width = 180 };
    private readonly Button importButton = new() { Text = "Import mitigation PDF", Dock = DockStyle.Left, Width = 210 };
    private readonly Button toggleButton = new() { Text = "Show reconstruction candidates", Dock = DockStyle.Left, Width = 260 };
    private ImportResult? imported;
    private IReadOnlyList<ReconstructionCandidate> candidates = Array.Empty<ReconstructionCandidate>();
    private bool showCandidates;
    private BindingList<EditableScopeLine> reviewLines = new();
    private readonly Button saveButton = new() { Text = "Save review draft", Dock = DockStyle.Left, Width = 175 };

    public MainForm()
    {
        Text = "Xactimate Reconstruction Estimator | v0.3.0-preview";
        Size = new Size(1280, 760);
        BackColor = Color.FromArgb(17, 23, 35);
        ForeColor = Color.White;
        AllowDrop = true;
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 44 };
        toolbar.Controls.Add(bell);
        toolbar.Controls.Add(saveButton);
        toolbar.Controls.Add(toggleButton);
        toolbar.Controls.Add(importButton);
        Controls.Add(grid);
        Controls.Add(status);
        Controls.Add(toolbar);
        Controls.Add(heading);
        bell.Click += (_, _) => MessageBox.Show("Update installation is planned for Milestone 6.", "Update Center");
        importButton.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "PDF estimate (*.pdf)|*.pdf" };
            if (dialog.ShowDialog(this) == DialogResult.OK) ImportFile(dialog.FileName);
        };
        toggleButton.Click += (_, _) => { showCandidates = !showCandidates; RefreshGrid(); };
        saveButton.Click += (_, _) => SaveReview();
        grid.DataError += (_, e) => { status.Text = "Invalid entry. Quantity must be non-negative."; e.ThrowException = false; };
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                ImportFile(files[0]);
        };
    }

    private void ImportFile(string path)
    {
        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            status.Text = "Only PDF files are supported.";
            return;
        }
        try
        {
            imported = XactimatePdfImporter.Import(path);
            candidates = ReconstructionMapper.Map(imported.Lines);
            reviewLines = ScopeReviewStore.FromCandidates(candidates);
            showCandidates = false;
            RefreshGrid();
            status.Text = $"Estimate: {imported.EstimateNumber ?? "unknown"} | Price list: {imported.PriceList ?? "unknown"} | " +
                          $"{imported.Lines.Count} extracted items | {candidates.Count} scope candidates | REVIEW REQUIRED";
            if (imported.Warnings.Count > 0)
                MessageBox.Show(this, string.Join(Environment.NewLine, imported.Warnings), "Import warnings");
        }
        catch (Exception ex)
        {
            status.Text = "Import failed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Import error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveReview()
    {
        if (imported is null) { status.Text = "Import an estimate before saving."; return; }
        if (!showCandidates) { status.Text = "Switch to reconstruction candidates before saving."; return; }
        if (!Validate() || !grid.EndEdit()) { status.Text = "Resolve invalid edits before saving."; return; }
        using var dialog = new SaveFileDialog
        {
            Filter = "Review draft (*.json)|*.json",
            FileName = (imported.EstimateNumber ?? "estimate") + "-reconstruction-review.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            ScopeReviewStore.Save(dialog.FileName, imported.EstimateNumber, imported.PriceList, reviewLines);
            status.Text = $"Saved {reviewLines.Count} review candidates to {dialog.FileName} (not a priced estimate).";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Save failed"); }
    }

    private void RefreshGrid()
    {
        grid.DataSource = null;
        grid.DataSource = showCandidates
            ? reviewLines
            : imported?.Lines.Select(x => new { x.Number, x.Room, x.Description, x.Quantity, x.Unit, x.Page }).ToList();
        grid.ReadOnly = !showCandidates;
        if (showCandidates)
            foreach (DataGridViewColumn col in grid.Columns)
                col.ReadOnly = col.Name is "SourceLine" or "Room" or "Unit";
        toggleButton.Text = showCandidates ? "Show mitigation items" : "Show reconstruction candidates";
    }
}
