using Xre.App.Import;
using System.Drawing;
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
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 36, ForeColor = Color.LightGray, Text = "Ready — import a mitigation estimate PDF" };
    public MainForm()
    {
        Text = "Xactimate Reconstruction Estimator | v0.1.0";
        Size = new Size(1100, 720);
        BackColor = Color.FromArgb(17, 23, 35);
        ForeColor = Color.White;
        AllowDrop = true;
        var title = new Label { Text = "RECONSTRUCTION ESTIMATOR", Dock = DockStyle.Top, Height = 90, Font = new Font("Segoe UI", 23, FontStyle.Bold), ForeColor = Color.DeepSkyBlue, TextAlign = ContentAlignment.MiddleCenter };
        var drop = new Label { Text = "DROP XACTIMATE MITIGATION PDF HERE", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 17), TextAlign = ContentAlignment.MiddleCenter, BorderStyle = BorderStyle.FixedSingle };
        var bell = new Button { Text = "🔔 Updates", Dock = DockStyle.Top, Height = 38 };
        bell.Click += (_, _) => MessageBox.Show("Release checking is planned for Milestone 6. No updater is active yet.", "Update Center");
        Controls.Add(drop); Controls.Add(status); Controls.Add(bell); Controls.Add(title);
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) => { var files = e.Data?.GetData(DataFormats.FileDrop) as string[]; if (files is { Length: > 0 }) { if (!files[0].EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) { status.Text = "Only PDF imports are supported"; return; } try { var result = XactimatePdfImporter.Import(files[0]); status.Text = $"Parsed {result.Lines.Count} candidate items | Price list: {result.PriceList ?? "not found"} | REVIEW REQUIRED"; MessageBox.Show(string.Join(Environment.NewLine, result.Warnings), "Import validation"); } catch (Exception ex) { status.Text = "Import failed: " + ex.Message; } } };
    }
}
