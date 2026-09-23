using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace Vydra.Services;

public class ToolManager
{
    private readonly string _toolsDir;
    private readonly HttpClient _http;

    private const string YtDlpUrl =
        "https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download/yt-dlp.exe";

    private const string FfmpegZipUrl =
        "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    private const string DenoZipUrl =
        "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";

    public ToolManager(string toolsDir)
    {
        _toolsDir = toolsDir;
        Directory.CreateDirectory(_toolsDir);

        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Vydra/0.1");
        _http.Timeout = TimeSpan.FromMinutes(10);
    }

    public string YtDlpPath => Path.Combine(_toolsDir, "yt-dlp.exe");
    public string FfmpegPath => Path.Combine(_toolsDir, "ffmpeg.exe");
    public string FfprobePath => Path.Combine(_toolsDir, "ffprobe.exe");
    public string DenoPath => Path.Combine(_toolsDir, "deno.exe");

    public List<string> GetMissingTools()
    {
        var missing = new List<string>();
        if (!File.Exists(YtDlpPath)) missing.Add("yt-dlp");
        if (!File.Exists(FfmpegPath) || !File.Exists(FfprobePath)) missing.Add("ffmpeg");
        if (!File.Exists(DenoPath)) missing.Add("deno");
        return missing;
    }

    public async Task DownloadMissingAsync(
        Action<string, double, string> onProgress,
        CancellationToken ct = default)
    {
        var missing = GetMissingTools();

        foreach (var tool in missing)
        {
            ct.ThrowIfCancellationRequested();

            switch (tool)
            {
                case "yt-dlp":
                    await DownloadFileAsync(YtDlpUrl, YtDlpPath, "yt-dlp",
                        onProgress, ct);
                    break;
                case "ffmpeg":
                    await DownloadFfmpegAsync(onProgress, ct);
                    break;
                case "deno":
                    await DownloadDenoAsync(onProgress, ct);
                    break;
            }
        }
    }

    public async Task<string> UpdateYtDlpAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(YtDlpPath))
            {
                await DownloadFileAsync(YtDlpUrl, YtDlpPath, "yt-dlp",
                    (_, _, _) => { }, ct);
                return "yt-dlp: установлен свежий (был отсутствовал)";
            }

            string localVersion = await GetLocalYtDlpVersionAsync();
            string? latestVersion = await GetLatestYtDlpVersionAsync(ct);

            if (latestVersion == null)
                return "yt-dlp: не удалось проверить обновление (нет ответа от GitHub)";

            if (string.IsNullOrEmpty(localVersion))
                return "yt-dlp: не удалось определить локальную версию";

            if (string.Equals(localVersion, latestVersion, StringComparison.OrdinalIgnoreCase))
                return $"yt-dlp: актуальная версия {localVersion}";

            if (CompareYtDlpVersions(localVersion, latestVersion) >= 0)
                return $"yt-dlp: локальная версия {localVersion} новее или равна GitHub ({latestVersion}) — оставляем";

            await DownloadFileAsync(YtDlpUrl, YtDlpPath, "yt-dlp",
                (_, _, _) => { }, ct);

            return $"yt-dlp: обновлён с {localVersion} на {latestVersion}";
        }
        catch (Exception ex)
        {
            return $"yt-dlp: ошибка обновления — {ex.Message}";
        }
    }

    private async Task<string> GetLocalYtDlpVersionAsync()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = YtDlpPath,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };

            using var p = System.Diagnostics.Process.Start(psi);
            if (p == null) return "";

            var readTask = p.StandardOutput.ReadToEndAsync();
            var completed = await Task.WhenAny(readTask, Task.Delay(8000));

            if (completed != readTask)
            {
                try { p.Kill(true); } catch { }
                return "";
            }

            string output = (await readTask).Trim();
            await p.WaitForExitAsync();
            return output;
        }
        catch
        {
            return "";
        }
    }

    private async Task<string?> GetLatestYtDlpVersionAsync(CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                "https://api.github.com/repos/yt-dlp/yt-dlp-nightly-builds/releases/latest");

            req.Headers.UserAgent.ParseAdd("Vydra/0.1");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                return tag.GetString();

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static int CompareYtDlpVersions(string a, string b)
    {
        try
        {
            var pa = a.Split('.');
            var pb = b.Split('.');

            for (int i = 0; i < Math.Min(3, Math.Min(pa.Length, pb.Length)); i++)
            {
                if (int.TryParse(pa[i], out int na) && int.TryParse(pb[i], out int nb))
                {
                    if (na != nb) return na.CompareTo(nb);
                }
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task DownloadFileAsync(
        string url, string destPath, string displayName,
        Action<string, double, string> onProgress,
        CancellationToken ct)
    {
        onProgress(displayName, 0, $"Скачивание {displayName}...");

        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[81920];
        long read = 0;
        double lastPercent = -1;

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var file = File.Create(destPath);

        int n;
        while ((n = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;

            if (total > 0)
            {
                double percent = (double)read / total * 100;
                if (percent - lastPercent >= 1)
                {
                    lastPercent = percent;
                    onProgress(displayName, percent,
                        $"{displayName}: {percent:F0}% ({FormatSize(read)} / {FormatSize(total)})");
                }
            }
        }

        onProgress(displayName, 100, $"{displayName}: готово");
    }

    private async Task DownloadFfmpegAsync(
        Action<string, double, string> onProgress,
        CancellationToken ct)
    {
        string zipPath = Path.Combine(_toolsDir, "_ffmpeg.zip");

        await DownloadFileAsync(FfmpegZipUrl, zipPath, "ffmpeg", onProgress, ct);

        onProgress("ffmpeg", 100, "ffmpeg: распаковка...");

        using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in archive.Entries)
            {
                var name = Path.GetFileName(entry.FullName);
                if (string.Equals(name, "ffmpeg.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "ffprobe.exe", StringComparison.OrdinalIgnoreCase))
                {
                    string target = Path.Combine(_toolsDir, name);
                    entry.ExtractToFile(target, overwrite: true);
                }
            }
        }

        try { File.Delete(zipPath); } catch { }

        onProgress("ffmpeg", 100, "ffmpeg: готово");
    }

    private async Task DownloadDenoAsync(
        Action<string, double, string> onProgress,
        CancellationToken ct)
    {
        string zipPath = Path.Combine(_toolsDir, "_deno.zip");

        await DownloadFileAsync(DenoZipUrl, zipPath, "deno", onProgress, ct);

        onProgress("deno", 100, "deno: распаковка...");

        using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in archive.Entries)
            {
                var name = Path.GetFileName(entry.FullName);
                if (string.Equals(name, "deno.exe", StringComparison.OrdinalIgnoreCase))
                {
                    string target = Path.Combine(_toolsDir, name);
                    entry.ExtractToFile(target, overwrite: true);
                }
            }
        }

        try { File.Delete(zipPath); } catch { }

        onProgress("deno", 100, "deno: готово");
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    }
}