using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Vydra.Services;

public class LavFilterInstaller
{
    private readonly HttpClient _http;

    public LavFilterInstaller()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Vydra/0.1");
        _http.Timeout = TimeSpan.FromMinutes(10);
    }

    /// <summary>
    /// Скачивает последний установщик LAV Filters с GitHub.
    /// Возвращает путь к .exe или null при ошибке.
    /// </summary>
    public async Task<string?> DownloadLatestInstallerAsync(
        string targetDir,
        Action<double, string> onProgress,
        CancellationToken ct = default)
    {
        try
        {
            onProgress(0, "Поиск последней версии LAV Filters...");

            using var req = new HttpRequestMessage(HttpMethod.Get,
                "https://api.github.com/repos/Nevcairiel/LAVFilters/releases/latest");
            req.Headers.UserAgent.ParseAdd("Vydra/0.1");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                onProgress(0, $"GitHub вернул ошибку: {(int)resp.StatusCode}");
                return null;
            }

            string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            string? downloadUrl = null;
            string? assetName = null;
            string tagName = "unknown";

            using (var doc = JsonDocument.Parse(json))
            {
                if (doc.RootElement.TryGetProperty("tag_name", out var tag))
                    tagName = tag.GetString() ?? "unknown";

                if (doc.RootElement.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        if (asset.TryGetProperty("name", out var nameProp))
                        {
                            string name = nameProp.GetString() ?? "";
                            if (name.Contains("Installer", StringComparison.OrdinalIgnoreCase) &&
                                name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                assetName = name;
                                if (asset.TryGetProperty("browser_download_url", out var urlProp))
                                    downloadUrl = urlProp.GetString();
                                break;
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(assetName))
            {
                onProgress(0, "Не найден установщик в релизе GitHub");
                return null;
            }

            Directory.CreateDirectory(targetDir);
            string destPath = Path.Combine(targetDir, assetName);

            onProgress(0, $"Скачивание {assetName} ({tagName})...");

            using var downloadResp = await _http.GetAsync(
                downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            downloadResp.EnsureSuccessStatusCode();

            var total = downloadResp.Content.Headers.ContentLength ?? -1L;
            var buffer = new byte[81920];
            long read = 0;
            double lastPercent = -1;

            using var stream = await downloadResp.Content
                .ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var file = File.Create(destPath);

            int n;
            while ((n = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                read += n;

                if (total > 0)
                {
                    double percent = (double)read / total * 100;
                    if (percent - lastPercent >= 1)
                    {
                        lastPercent = percent;
                        onProgress(percent,
                            $"Скачивание LAV Filters: {percent:F0}% ({FormatSize(read)} / {FormatSize(total)})");
                    }
                }
            }

            onProgress(100, "Скачивание завершено. Запуск установщика...");
            return destPath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            onProgress(0, $"Ошибка: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Запускает установщик с правами администратора (UAC-запрос).
    /// </summary>
    public static bool LaunchInstaller(string exePath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(psi);
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    }
}