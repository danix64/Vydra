using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Vydra.Services
{
    public class LavFilterInstaller
    {
        private readonly HttpClient _http;

        public LavFilterInstaller()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Vydra/0.2");
            _http.Timeout = TimeSpan.FromMinutes(10);
        }

        public async Task<string> DownloadLatestInstallerAsync(
            string targetDir,
            Action<double, string> onProgress,
            CancellationToken ct = default(CancellationToken))
        {
            try
            {
                onProgress(0, "Поиск последней версии LAV Filters...");

                using (var req = new HttpRequestMessage(HttpMethod.Get,
                    "https://api.github.com/repos/Nevcairiel/LAVFilters/releases/latest"))
                {
                    req.Headers.UserAgent.ParseAdd("Vydra/0.2");
                    req.Headers.Accept.ParseAdd("application/vnd.github+json");

                    using (var resp = await _http.SendAsync(req, ct).ConfigureAwait(false))
                    {
                        if (!resp.IsSuccessStatusCode)
                        {
                            onProgress(0, "GitHub вернул ошибку: " + (int)resp.StatusCode);
                            return null;
                        }

                        string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                        string downloadUrl = null;
                        string assetName = null;
                        string tagName = "unknown";

                        var doc = JObject.Parse(json);

                        if (doc["tag_name"] != null)
                            tagName = doc["tag_name"].ToString();

                        var assets = doc["assets"] as JArray;
                        if (assets != null)
                        {
                            foreach (var asset in assets)
                            {
                                string name = asset["name"] != null ? asset["name"].ToString() : "";
                                if (name.IndexOf("Installer", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                    name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    assetName = name;
                                    downloadUrl = asset["browser_download_url"] != null
                                        ? asset["browser_download_url"].ToString()
                                        : null;
                                    break;
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

                        onProgress(0, "Скачивание " + assetName + " (" + tagName + ")...");

                        using (var downloadResp = await _http.GetAsync(
                            downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                        {
                            downloadResp.EnsureSuccessStatusCode();

                            var total = downloadResp.Content.Headers.ContentLength ?? -1L;
                            var buffer = new byte[81920];
                            long read = 0;
                            double lastPercent = -1;

                            using (var stream = await downloadResp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            using (var file = File.Create(destPath))
                            {
                                int n;
                                while ((n = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                                {
                                    await file.WriteAsync(buffer, 0, n, ct).ConfigureAwait(false);
                                    read += n;

                                    if (total > 0)
                                    {
                                        double percent = (double)read / total * 100;
                                        if (percent - lastPercent >= 1)
                                        {
                                            lastPercent = percent;
                                            onProgress(percent,
                                                "Скачивание LAV Filters: " + percent.ToString("F0") + "% (" +
                                                FormatSize(read) + " / " + FormatSize(total) + ")");
                                        }
                                    }
                                }
                            }
                        }

                        onProgress(100, "Скачивание завершено. Запуск установщика...");
                        return destPath;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                onProgress(0, "Ошибка: " + ex.Message);
                return null;
            }
        }

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
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / 1024.0 / 1024.0).ToString("F1") + " MB";
            return (bytes / 1024.0 / 1024.0 / 1024.0).ToString("F2") + " GB";
        }
    }
}