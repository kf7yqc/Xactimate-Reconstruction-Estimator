using System.Drawing;
using System.Windows.Forms;

namespace Xre.App;

public sealed partial class MainForm
{
    private readonly Label importedKpi = MakeKpi("MITIGATION ITEMS", "0");
    private readonly Label proposedKpi = MakeKpi("RECONSTRUCTION LINES", "0");
    private readonly Label reviewedKpi = MakeKpi("REVIEWED", "0");
    private readonly Label pricedKpi = MakeKpi("PRICED SUBTOTAL", "$0.00");
    private readonly Label workspaceTitle = new()
    {
        Text = "ESTIMATE WORKSPACE", ForeColor = Color.White,
        Font = new Font("Segoe UI", 15, FontStyle.Bold),
        Dock = DockStyle.Top, Height = 42, TextAlign = ContentAlignment.MiddleLeft
    };

    private static Label MakeKpi(string caption, string value) => new()
    {
        Text = caption + Environment.NewLine + value,
        ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill,
        Padding = new Padding(15, 4, 5, 4)
    };

    private void BuildExecutiveShell(FlowLayoutPanel toolbar)
    {
        heading.Text = "XRE   /   RECONSTRUCTION ESTIMATOR";
        heading.TextAlign = ContentAlignment.MiddleLeft;
        heading.Padding = new Padding(24, 0, 0, 0);
        heading.Font = new Font("Segoe UI", 17, FontStyle.Bold);
        heading.Height = 74;
        heading.BackColor = Color.FromArgb(15, 24, 38);

        var sidebar = new FlowLayoutPanel
        {
            Dock = DockStyle.Left, Width = 218, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(12, 20, 10, 10),
            BackColor = Color.FromArgb(16, 29, 46)
        };
        var sidebarTitle = new Label
        {
            Text = "WORKSPACE", ForeColor = Color.FromArgb(108, 166, 192),
            Font = new Font("Segoe UI", 9, FontStyle.Bold), Height = 35, Width = 184
        };
        sidebar.Controls.Add(sidebarTitle);
        void Nav(string label, EventHandler click)
        {
            var button = new Button
            {
                Text = label, Width = 184, Height = 43, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0), Margin = new Padding(0, 2, 0, 6),
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(27, 47, 68),
                ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Regular)
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += click;
            sidebar.Controls.Add(button);
        }
        Nav("DASHBOARD", (_, _) => ShowScopeSummary());
        Nav("IMPORT ESTIMATE", (_, _) => importButton.PerformClick());
        Nav("RECONSTRUCTION BUILDER", (_, _) => { showCandidates = true; RefreshGrid(); });
        Nav("MITIGATION SOURCE", (_, _) => { if (imported?.Lines.Count > 0) { showCandidates = false; RefreshGrid(); } });
        Nav("SAVE REVIEW", (_, _) => saveButton.PerformClick());
        Nav("OPEN DRAFT", (_, _) => openDraftButton.PerformClick());
        Nav("EXPORT PDF", (_, _) => pdfButton.PerformClick());
        Nav("ACTIVITY / STATUS", (_, _) => MessageBox.Show(this, status.Text, "Workspace status"));
        Nav("UPDATE CENTER", async (_, _) => await OpenUpdateCenterAsync());

        var kpis = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 102, ColumnCount = 4,
            RowCount = 1, Padding = new Padding(0, 8, 0, 8),
            BackColor = Color.FromArgb(18, 31, 49)
        };
        for (var i = 0; i < 4; i++)
            kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        var labels = new[] { importedKpi, proposedKpi, reviewedKpi, pricedKpi };
        foreach (var label in labels)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill, Margin = new Padding(5),
                BackColor = Color.FromArgb(32, 53, 76)
            };
            card.Controls.Add(label);
            kpis.Controls.Add(card);
        }
        var workspace = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 12, 18, 12) };
        var titlePanel = new Panel { Dock = DockStyle.Top, Height = 45 };
        titlePanel.Controls.Add(workspaceTitle);
        grid.Dock = DockStyle.Fill;
        workspace.Controls.Add(grid);
        workspace.Controls.Add(titlePanel);
        workspace.Controls.Add(kpis);
        workspace.Controls.Add(toolbar);
        Controls.Add(workspace);
        Controls.Add(sidebar);
        Controls.Add(status);
        Controls.Add(heading);
        UpdateExecutiveKpis();
    }

    private void UpdateExecutiveKpis()
    {
        importedKpi.Text = "MITIGATION ITEMS" + Environment.NewLine + (imported?.Lines.Count ?? 0);
        proposedKpi.Text = "RECONSTRUCTION LINES" + Environment.NewLine + reviewLines.Count;
        reviewedKpi.Text = "REVIEWED" + Environment.NewLine + reviewLines.Count(x => x.Approved) + " / " + reviewLines.Count;
        pricedKpi.Text = "PRICED SUBTOTAL" + Environment.NewLine +
            reviewLines.Sum(x => x.LineTotal ?? 0m).ToString("C2");
        workspaceTitle.Text = showCandidates ? "RECONSTRUCTION BUILDER   /   EDITABLE SCOPE"
            : "MITIGATION SOURCE   /   IMPORTED LINE ITEMS";
    }
}
