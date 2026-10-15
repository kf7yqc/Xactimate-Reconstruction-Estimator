using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Xre.App.Updates;

public sealed record AvailableUpdate(int BuildNumber, string Tag, string ZipUrl, string ChecksumUrl, string ReleaseUrl);

public static class UpdateService
{
    private const string Repository = "kf7yqc/Xactimate-Reconstruction-Estimator";
    public static int InstalledBuild
    {
        get
        {
            var text = typeof(UpdateService).Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?.InformationalVersion ?? "";
            var marker = text.IndexOf("build.", StringComparison.OrdinalIgnoreCase);
            if (marker < 0) return 0;
            var digits = new string(text[(marker + 6)..].TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out var number) ? number : 0;
        }
    }

    private static readonly HttpClient Client = CreateClient();
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Xre-Windows-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static async Task<AvailableUpdate?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync(
            $"https://api.github.com/repos/{Repository}/releases/latest", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!tag.StartsWith("build-", StringComparison.Ordinal) ||
            !int.TryParse(tag.AsSpan(6), out var build) || build <= InstalledBuild)
            return null;
        var zip = "";
        var checksum = "";
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var url = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (name == "Xactimate-Reconstruction-Estimator-win-x64-preview.zip") zip = url;
            if (name == "SHA256SUMS.txt") checksum = url;
        }
        if (zip.Length == 0 || checksum.Length == 0) return null;
        return new AvailableUpdate(build, tag, zip, checksum,
            root.GetProperty("html_url").GetString() ?? "");
    }

    public static async Task StageAndRestartAsync(AvailableUpdate update, CancellationToken cancellationToken = default)
    {
        // Only download assets from this repository's GitHub release endpoints.
        var prefix = $"https://github.com/{Repository}/releases/download/{update.Tag}/";
        if (!update.ZipUrl.StartsWith(prefix, StringComparison.Ordinal) ||
            !update.ChecksumUrl.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidDataException("Unexpected release asset location.");

        var folder = Path.Combine(Path.GetTempPath(), "Xre-Update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var archive = Path.Combine(folder, "update.zip");
        var checksums = await Client.GetStringAsync(update.ChecksumUrl, cancellationToken);
        var expected = checksums.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.EndsWith("Xactimate-Reconstruction-Estimator-win-x64-preview.zip", StringComparison.Ordinal))
            .Select(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .FirstOrDefault();
        if (expected is null || expected.Length != 64 || !expected.All(Uri.IsHexDigit))
            throw new InvalidDataException("Release checksum is missing or malformed.");

        using (var response = await Client.GetAsync(update.ZipUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = File.Create(archive);
            await source.CopyToAsync(destination, cancellationToken);
        }
        await using (var stream = File.OpenRead(archive))
        {
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Downloaded update failed SHA-256 verification.");
        }

        // Stage extraction outside the application folder; never extract unverified archives in place.
        var staged = Path.Combine(folder, "staged");
        ZipFile.ExtractToDirectory(archive, staged);
        var exe = Path.Combine(staged, "Xre.App.exe");
        if (!File.Exists(exe)) throw new InvalidDataException("Release archive is missing Xre.App.exe.");
        var destinationFolder = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var currentExe = Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable.");
        var pid = Environment.ProcessId;
        static string Quote(string s) => "'" + s.Replace("'", "''") + "'";
        var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Xre", "updater.log");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        var script = $@"$ErrorActionPreference = 'Stop'
$log = {Quote(log)}
try {{
    Add-Content -LiteralPath $log -Value ('Update started ' + (Get-Date))
    Wait-Process -Id {pid} -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    $copied = $false
    for ($attempt = 1; $attempt -le 8; $attempt++) {{
        try {{
            Get-ChildItem -LiteralPath {Quote(staged)} -Force | Copy-Item -Destination {Quote(destinationFolder)} -Recurse -Force -ErrorAction Stop
            $copied = $true
            break
        }} catch {{
            Add-Content -LiteralPath $log -Value ('Copy attempt ' + $attempt + ' failed: ' + $_.Exception.Message)
            Start-Sleep -Seconds 2
        }}
    }}
    if (-not $copied) {{ throw 'Unable to replace application files after 8 attempts.' }}
    Add-Content -LiteralPath $log -Value 'Files copied successfully.'
    if (-not (Test-Path -LiteralPath {Quote(currentExe)})) {{ throw 'Application executable missing after update.' }}
    $restarted = Start-Process -FilePath {Quote(currentExe)} -WorkingDirectory {Quote(destinationFolder)} -PassThru -ErrorAction Stop
    Start-Sleep -Seconds 3
    if ($restarted.HasExited) {{ throw ('Application exited immediately with code ' + $restarted.ExitCode) }}
    Add-Content -LiteralPath $log -Value ('Restart verified, PID ' + $restarted.Id)
}} catch {{
    Add-Content -LiteralPath $log -Value ('UPDATE FAILED: ' + $_.Exception.ToString())
}}
";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var updater = Process.Start(new ProcessStartInfo("powershell.exe",
            "-NoProfile -NonInteractive -EncodedCommand " + encoded)
        { UseShellExecute = false, CreateNoWindow = true });
        if (updater is null) throw new InvalidOperationException("Could not start update helper.");
    }
}
