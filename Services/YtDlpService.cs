using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Vydra.Models;

namespace Vydra.Services
{
    public class YtDlpService
    {
        private readonly string _toolsDir;

        private static readonly Regex ProgressRegex = new Regex(
            @"\[download\]\s+(?<percent>[\d\.]+)%\s+of\s+~?\s*(?<size>[\d\.]+\w+)\s+at\s+(?<speed>[\d\.]+\w+/s)\s+ETA\s+(?<eta>[\d:]+)",
            RegexOptions.Compiled);

        private static readonly Regex MergeRegex = new Regex(
            @"\[Merger\]|\[ffmpeg\]",
            RegexOptions.Compiled);

        private static readonly Regex MergerPathRegex = new Regex(
            @"\[Merger\]\s+Merging formats into\s+""(.+?)""",
            RegexOptions.Compiled);

        private static readonly Regex DestPathRegex = new Regex(
            @"\[download\]\s+Destination:\s+(.+?)$",
            RegexOptions.Compiled);

        private static readonly Regex ExtractAudioRegex = new Regex(
            @"\[ExtractAudio\]\s+Destination:\s+(.+?)$",
            RegexOptions.Compiled);

        public YtDlpService(string toolsDir)
        {
            _toolsDir = toolsDir;
        }

        public string YtDlpPath { get { return Path.Combine(_toolsDir, "yt-dlp.exe"); } }
        public string FfmpegPath { get { return Path.Combine(_toolsDir, "ffmpeg.exe"); } }

        public async Task<List<string>> DownloadAsync(
            string url,
            string outputDir,
            string quality,
            string proxy,
            bool downloadPlaylist,
            int playlistEnd,
            bool audioOnly,
            string fps,
            Action<DownloadProgress> onProgress,
            Action<string> onLog,
            Action<string> onFileDownloaded,
            CancellationToken ct = default(CancellationToken))
        {
            if (!File.Exists(YtDlpPath))
                throw new FileNotFoundException("yt-dlp.exe не найден: " + YtDlpPath);

            Directory.CreateDirectory(outputDir);

            string format;
            if (audioOnly)
            {
                format = "bestaudio/best";
            }
            else
            {
                format = "bestvideo[height<=" + quality + "]";

                if (!string.IsNullOrEmpty(fps) && fps != "Любой")
                {
                    int fpsNum;
                    if (int.TryParse(fps, out fpsNum))
                        format += "[fps<=" + fpsNum + "]";
                }

                format += "+bestaudio/best";
            }

            var args = new StringBuilder();
            args.Append("-f \"").Append(format).Append("\" ");

            if (audioOnly)
            {
                args.Append("-x --audio-format mp3 --audio-quality 0 ");
            }
            else
            {
                args.Append("--merge-output-format mp4 ");
            }

            args.Append("--newline ");
            args.Append("--encoding utf-8 ");
            args.Append("--no-part ");
            args.Append("--force-ipv4 ");
            args.Append("--no-check-certificates ");
            args.Append("--user-agent \"\" ");
            args.Append(downloadPlaylist ? "--yes-playlist " : "--no-playlist ");

            if (downloadPlaylist && playlistEnd > 0)
            {
                args.Append("--playlist-end ").Append(playlistEnd).Append(" ");
            }

            if (!audioOnly)
            {
                int q;
                if (int.TryParse(quality, out q) && q <= 1080)
                    args.Append("-S \"vcodec:h264,res,acodec:m4a\" ");
                else
                    args.Append("-S \"vcodec:av01,res,acodec:opus\" ");
            }

            if (!string.IsNullOrWhiteSpace(proxy))
                args.Append("--proxy \"").Append(proxy).Append("\" ");

            args.Append("--ffmpeg-location \"").Append(_toolsDir).Append("\" ");
            args.Append("-o \"").Append(Path.Combine(outputDir, "%(title)s.%(ext)s")).Append("\" ");
            args.Append("\"").Append(url).Append("\"");

            var psi = new ProcessStartInfo
            {
                FileName = YtDlpPath,
                Arguments = args.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = _toolsDir
            };

            using (var process = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                var tcs = new TaskCompletionSource<bool>();
                var finalFiles = new List<string>();
                bool cancelRequested = false;
                bool insideMerger = false;

                process.OutputDataReceived += (sender, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;
                    onLog(e.Data);

                    if (e.Data.Contains("[Merger]"))
                        insideMerger = true;
                    else if (e.Data.Contains("[download] Downloading item") ||
                             e.Data.Contains("[download] Downloading playlist"))
                        insideMerger = false;

                    ParsePath(e.Data, finalFiles, onFileDownloaded);

                    ParseLine(e.Data, onProgress);

                    if (cancelRequested && !insideMerger)
                    {
                        KillProcessTree(process);
                        tcs.TrySetCanceled();
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;
                    onLog("[err] " + e.Data);

                    ParsePath(e.Data, finalFiles, onFileDownloaded);

                    ParseLine(e.Data, onProgress);
                };

                process.Exited += (sender, e) => tcs.TrySetResult(process.ExitCode == 0);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (ct.Register(() =>
                {
                    cancelRequested = true;

                    if (!insideMerger)
                    {
                        KillProcessTree(process);
                        tcs.TrySetCanceled();
                    }
                }))
                {
                    try
                    {
                        await tcs.Task;
                    }
                    catch (TaskCanceledException)
                    {
                    }

                    return finalFiles;
                }
            }
        }

        private static void KillProcessTree(Process process)
        {
            try
            {
                if (process == null || process.HasExited) return;

                int pid = process.Id;

                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = "/F /T /PID " + pid,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var killer = Process.Start(psi))
                {
                    if (killer != null)
                        killer.WaitForExit(3000);
                }

                try { if (!process.HasExited) process.Kill(); } catch { }
            }
            catch { }
        }

        private static void ParsePath(string line, List<string> files, Action<string> onFileDownloaded)
        {
            var mm = MergerPathRegex.Match(line);
            if (mm.Success)
            {
                var path = mm.Groups[1].Value.Trim();
                if (!files.Contains(path))
                {
                    files.Add(path);
                    if (onFileDownloaded != null)
                        onFileDownloaded(path);
                }
                return;
            }

            var am = ExtractAudioRegex.Match(line);
            if (am.Success)
            {
                var path = am.Groups[1].Value.Trim();
                if (!files.Contains(path))
                {
                    files.Add(path);
                    if (onFileDownloaded != null)
                        onFileDownloaded(path);
                }
                return;
            }

            var dm = DestPathRegex.Match(line);
            if (dm.Success)
            {
                var path = dm.Groups[1].Value.Trim();
                if (!path.EndsWith(".part") &&
                    !path.Contains(".f") &&
                    !path.EndsWith(".webm") &&
                    !path.EndsWith(".m4a"))
                {
                    if (!files.Contains(path))
                        files.Add(path);
                }
            }
        }

        private static void ParseLine(string line, Action<DownloadProgress> onProgress)
        {
            var m = ProgressRegex.Match(line);
            if (m.Success)
            {
                double parsedPercent = 0;
                double.TryParse(m.Groups["percent"].Value,
                    NumberStyles.Any, CultureInfo.InvariantCulture, out parsedPercent);

                onProgress(new DownloadProgress
                {
                    Percent = parsedPercent,
                    Status = "Скачивание",
                    Speed = m.Groups["speed"].Value,
                    Eta = m.Groups["eta"].Value,
                    RawLine = line
                });
                return;
            }

            if (MergeRegex.IsMatch(line))
            {
                onProgress(new DownloadProgress
                {
                    Percent = 100,
                    Status = "Склейка видео и звука",
                    RawLine = line
                });
            }
        }
    }
}