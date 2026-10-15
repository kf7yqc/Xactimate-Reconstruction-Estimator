using Xre.App.Import;
using Xre.App.Reconstruction;
using Xre.App.Updates;
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

public sealed partial class MainForm : Form
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
    private readonly Button bell = new() { Text = "Updates", Dock = DockStyle.Right, Width = 180 };
    private AvailableUpdate? availableUpdate;
    private readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 30 * 60 * 1000 };
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
    private readonly Button summaryButton = new() { Text = "Scope summary", Width = 130, Height = 30 };
    private readonly Button pdfButton = new() { Text = "Export PDF Estimate", Width = 175, Height = 32 };

    public MainForm()
    {
        Text = "Xactimate Reconstruction Estimator | v1.0.0-preview";
        Size = new Size(1280, 760);
        BackColor = Color.FromArgb(17, 23, 35);
        MinimumSize = new Size(950, 550);
        ForeColor = Color.White;
        AllowDrop = true;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 55, AutoScroll = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(8, 8, 8, 3), BackColor = Color.FromArgb(27, 42, 61) };
        toolbar.Controls.Add(bell);
        toolbar.Controls.Add(saveButton);
        toolbar.Controls.Add(reportButton);
        toolbar.Controls.Add(openDraftButton);
        toolbar.Controls.Add(csvButton);
        toolbar.Controls.Add(toggleButton);
        toolbar.Controls.Add(importButton);
        toolbar.Controls.Add(addLineButton);
        toolbar.Controls.Add(removeLineButton);
        toolbar.Controls.Add(summaryButton);
        toolbar.Controls.Add(pdfButton);
        BuildExecutiveShell(toolbar);
        foreach (var button in toolbar.Controls.OfType<Button>())
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = button == pdfButton ? Color.FromArgb(0, 130, 166) : Color.FromArgb(43, 65, 88);
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            button.Height = 33;
            button.Margin = new Padding(3, 0, 3, 0);
        }
        bell.Click += async (_, _) => await OpenUpdateCenterAsync();
        Shown += async (_, _) => { await CheckForUpdatesAsync(false); updateTimer.Start(); };
        updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync(false);
        FormClosed += (_, _) => updateTimer.Dispose();
        importButton.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "PDF estimate (*.pdf)|*.pdf" };
            if (dialog.ShowDialog(this) == DialogResult.OK) ImportFile(dialog.FileName);
        };
        toggleButton.Click += (_, _) =>
        {
            if (imported is null) { status.Text = "Import an estimate or open a review draft first."; return; }
            if (imported.Lines.Count == 0 && showCandidates)
            {
                status.Text = "Original mitigation items are not stored in this reopened draft.";
                return;
            }
            if (showCandidates && !grid.EndEdit()) return;
            showCandidates = !showCandidates;
            RefreshGrid();
        };
        saveButton.Click += (_, _) => SaveReview();
        reportButton.Click += (_, _) => ExportReport();
        openDraftButton.Click += (_, _) => OpenReview();
        csvButton.Click += (_, _) => ExportCsv();
        addLineButton.Click += (_, _) => AddScopeLine();
        removeLineButton.Click += (_, _) => RemoveScopeLine();
        summaryButton.Click += (_, _) => ShowScopeSummary();
        pdfButton.Click += (_, _) => ExportPdf();
        reviewLines.ListChanged += (_, _) => UpdateExecutiveKpis();
        grid.CellValueChanged += (_, _) => UpdateExecutiveKpis();
        grid.DataError += (_, e) => { status.Text = "Invalid entry. Quantity must be non-negative."; e.ThrowException = false; };
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                ImportFile(files[0]);
        };
    }

    private async Task CheckForUpdatesAsync(bool interactive)
    {
        try
        {
            bell.Enabled = false;
            bell.Text = "Checking...";
            availableUpdate = await UpdateService.CheckAsync();
            bell.Text = availableUpdate is null ? "Updates ✓" : "Updates ● NEW";
            if (availableUpdate is not null)
                status.Text = $"Update available: {availableUpdate.Tag}. Click the notification bell.";
            else if (interactive)
                MessageBox.Show(this, "You have the newest published build.", "Update Center");
        }
        catch (Exception ex)
        {
            bell.Text = "Updates !";
            if (interactive) MessageBox.Show(this, ex.Message, "Update check failed");
        }
        finally { bell.Enabled = true; }
    }

    private async Task OpenUpdateCenterAsync()
    {
        await CheckForUpdatesAsync(false);
        if (availableUpdate is null)
        {
            MessageBox.Show(this, "No newer published build was found, or the update service is unavailable.",
                "Update Center");
            return;
        }
        var answer = MessageBox.Show(this,
            $"Build {availableUpdate.BuildNumber} is available.\\n\\n" +
            "Save your current review draft before updating. The application will close and restart. Install now?",
            "Install verified update", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;
        try
        {
            bell.Enabled = false;
            bell.Text = "Downloading...";
            await UpdateService.StageAndRestartAsync(availableUpdate);
            Close();
        }
        catch (Exception ex)
        {
            bell.Enabled = true;
            bell.Text = "Updates !";
            MessageBox.Show(this, ex.Message, "Update installation failed");
        }
    }

    private void ExportPdf()
    {
        if (imported is null) { status.Text = "Import an estimate or open a draft first."; return; }
        if (showCandidates && !grid.EndEdit()) { status.Text = "Resolve invalid edits first."; return; }
        using var dialog = new SaveFileDialog
        {
            Filter = "PDF Estimate (*.pdf)|*.pdf",
            FileName = (imported.EstimateNumber ?? "estimate") + "-reconstruction-estimate.pdf"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            ScopePdfEstimate.Export(dialog.FileName, imported.EstimateNumber, imported.PriceList, reviewLines);
            status.Text = "Draft PDF estimate exported. Verify all scope, pricing, and calculations.";
            MessageBox.Show(this, "Draft estimate PDF saved. Unpriced items show a dash; no pricing is invented.",
                "PDF estimate ready");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "PDF export failed"); }
    }

    private void ShowScopeSummary()
    {
        if (imported is null) { status.Text = "Import an estimate or open a draft first."; return; }
        if (showCandidates && !grid.EndEdit()) { status.Text = "Resolve the current edit first."; return; }
        var rooms = reviewLines.GroupBy(x => x.Room, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key)
            .Select(x => $"{x.Key}: {x.Count()} items ({x.Count(y => y.Approved)} reviewed)")
            .ToArray();
        var summary = $"Source estimate: {imported.EstimateNumber ?? "Unknown"}" + Environment.NewLine +
            $"Mitigation items: {imported.Lines.Count}" + Environment.NewLine +
            $"Reconstruction scope items: {reviewLines.Count}" + Environment.NewLine +
            $"Reviewed: {reviewLines.Count(x => x.Approved)}" + Environment.NewLine +
            $"Pending: {reviewLines.Count(x => !x.Approved)}" + Environment.NewLine +
            Environment.NewLine + "By room:" + Environment.NewLine + string.Join(Environment.NewLine, rooms) +
            Environment.NewLine + Environment.NewLine +
            "Unpriced review only. Verify materials, quantities, assemblies and pricing.";
        MessageBox.Show(this, summary, "Reconstruction Scope Summary",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(32, 57, 79);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        grid.ColumnHeadersHeight = 40;
        grid.DefaultCellStyle.BackColor = Color.FromArgb(238, 243, 248);
        grid.DefaultCellStyle.ForeColor = Color.FromArgb(24, 42, 66);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(16, 115, 153);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.RowTemplate.Height = 31;
        grid.BorderStyle = BorderStyle.None;
        addLineButton.Enabled = imported is not null;
        removeLineButton.Enabled = showCandidates && reviewLines.Count > 0;
        if (showCandidates)
            foreach (DataGridViewColumn col in grid.Columns)
                col.ReadOnly = col.Name is "SourceLine" or "Room" or "Unit" or "LineTotal";
        toggleButton.Text = showCandidates ? "Show mitigation items" : "Show reconstruction candidates";
        UpdateExecutiveKpis();
    }
}
