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
    private readonly Button csvButton = new() { Text = "Export CSV", Dock = DockStyle.Left, Width = 120 };
    private readonly Button openDraftButton = new() { Text = "Open review draft", Dock = DockStyle.Left, Width = 170 };
    private readonly Button reportButton = new() { Text = "Export scope report", Dock = DockStyle.Left, Width = 180 };
    private readonly Button saveButton = new() { Text = "Save review draft", Dock = DockStyle.Left, Width = 175 };
    private readonly Button addLineButton = new() { Text = "Add scope line", Width = 125, Height = 30 };
    private readonly Button removeLineButton = new() { Text = "Remove selected", Width = 145, Height = 30 };

    public MainForm()
    {
        Text = "Xactimate Reconstruction Estimator | v0.7.0-preview";
        Size = new Size(1280, 760);
        BackColor = Color.FromArgb(17, 23, 35);
        ForeColor = Color.White;
        AllowDrop = true;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        toolbar.Controls.Add(bell);
        toolbar.Controls.Add(saveButton);
        toolbar.Controls.Add(reportButton);
        toolbar.Controls.Add(openDraftButton);
        toolbar.Controls.Add(csvButton);
        toolbar.Controls.Add(toggleButton);
        toolbar.Controls.Add(importButton);
        toolbar.Controls.Add(addLineButton);
        toolbar.Controls.Add(removeLineButton);
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
        reportButton.Click += (_, _) => ExportReport();
        openDraftButton.Click += (_, _) => OpenReview();
        csvButton.Click += (_, _) => ExportCsv();
        addLineButton.Click += (_, _) => AddScopeLine();
        removeLineButton.Click += (_, _) => RemoveScopeLine();
        grid.DataError += (_, e) => { status.Text = "Invalid entry. Quantity must be non-negative."; e.ThrowException = false; };
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                ImportFile(files[0]);
        };
    }

    private void AddScopeLine()
    {
        if (imported is null) { status.Text = "Import an estimate or open a draft first."; return; }
        if (showCandidates && !grid.EndEdit()) { status.Text = "Resolve the current edit first."; return; }
        var next = reviewLines.Count == 0 ? 1 : reviewLines.Max(x => x.SourceLine) + 1;
        reviewLines.Add(new EditableScopeLine
        {
            SourceLine = next, Room = "Manual", Work = "Describe required reconstruction work",
            Quantity = 1m, Unit = "EA", ReviewNote = "MANUALLY ADDED — verify scope and quantity.",
            Approved = false
        });
        showCandidates = true;
        RefreshGrid();
        if (grid.Rows.Count > 0)
        {
            grid.ClearSelection();
            grid.Rows[grid.Rows.Count - 1].Selected = true;
            grid.CurrentCell = grid.Rows[grid.Rows.Count - 1].Cells["Work"];
            grid.BeginEdit(true);
        }
        status.Text = "Manual scope line added. Review description, quantity and unit.";
    }

    private void RemoveScopeLine()
    {
        if (!showCandidates || grid.CurrentRow?.DataBoundItem is not EditableScopeLine line)
        {
            status.Text = "Select a reconstruction scope line to remove.";
            return;
        }
        if (MessageBox.Show(this, "Remove the selected reconstruction scope line?",
            "Confirm removal", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        reviewLines.Remove(line);
        status.Text = "Selected scope line removed from this review.";
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
            var parsed = XactimatePdfImporter.Import(path);
            if (!ConfirmDiscardReview()) return;
            imported = parsed;
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

    private bool ConfirmDiscardReview()
    {
        if (imported is null || reviewLines.Count == 0) return true;
        return MessageBox.Show(this,
            "Loading another estimate or draft will replace the current review. Save your current review draft first if you want to keep it. Continue?",
            "Replace current review?", MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private void SaveReview()
    {
        if (imported is null) { status.Text = "Import an estimate or open a review draft before saving."; return; }
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

    private void OpenReview()
    {
        using var dialog = new OpenFileDialog { Filter = "Review draft (*.json)|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var document = ScopeReviewStore.Load(dialog.FileName);
            if (!ConfirmDiscardReview()) return;
            imported = new ImportResult(document.SourceEstimate, document.SourcePriceList,
                Array.Empty<ImportedLine>(), new[] { "Draft reopened; original mitigation PDF is not included in this draft." });
            reviewLines = new BindingList<EditableScopeLine>(document.Lines);
            showCandidates = true;
            RefreshGrid();
            status.Text = $"Reopened {reviewLines.Count} reconstruction scope lines | REVIEW REQUIRED";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Cannot open review draft"); }
    }

    private void ExportCsv()
    {
        if (imported is null) { status.Text = "Import an estimate or open a review draft first."; return; }
        if (showCandidates && !grid.EndEdit()) { status.Text = "Resolve invalid edits before exporting."; return; }
        using var dialog = new SaveFileDialog
        {
            Filter = "Spreadsheet CSV (*.csv)|*.csv",
            FileName = (imported.EstimateNumber ?? "estimate") + "-reconstruction-scope.csv"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            ScopeCsvReport.Export(dialog.FileName, reviewLines);
            status.Text = "Unpriced reconstruction scope CSV exported.";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "CSV export failed"); }
    }

    private void ExportReport()
    {
        if (imported is null) { status.Text = "Import an estimate before exporting."; return; }
        if (showCandidates && !grid.EndEdit()) { status.Text = "Resolve invalid edits before exporting."; return; }
        using var dialog = new SaveFileDialog
        {
            Filter = "Printable HTML report (*.html)|*.html",
            FileName = (imported.EstimateNumber ?? "estimate") + "-reconstruction-scope.html"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            ScopeHtmlReport.Export(dialog.FileName, imported.EstimateNumber, imported.PriceList, reviewLines);
            status.Text = "Unpriced scope report saved. Open HTML in browser and use Print > Save as PDF.";
            MessageBox.Show(this, "Report saved. Open it in your browser and choose Print > Save as PDF.", "Report ready");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed"); }
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
