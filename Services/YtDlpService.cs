using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Vydra.Models;

namespace Vydra.Services;

public class YtDlpService
{
    private readonly string _toolsDir;

    private static readonly Regex ProgressRegex = new(
        @"\[download\]\s+(?<percent>[\d\.]+)%\s+of\s+~?\s*(?<size>[\d\.]+\w+)\s+at\s+(?<speed>[\d\.]+\w+/s)\s+ETA\s+(?<eta>[\d:]+)",
        RegexOptions.Compiled);

    private static readonly Regex MergeRegex = new(
        @"\[Merger\]|\[ffmpeg\]",
        RegexOptions.Compiled);

    private static readonly Regex MergerPathRegex = new(
        @"\[Merger\]\s+Merging formats into\s+""(.+?)""",
        RegexOptions.Compiled);

    private static readonly Regex DestPathRegex = new(
        @"\[download\]\s+Destination:\s+(.+?)$",
        RegexOptions.Compiled);

    public YtDlpService(string toolsDir)
    {
        _toolsDir = toolsDir;
    }

    public string YtDlpPath => Path.Combine(_toolsDir, "yt-dlp.exe");
    public string FfmpegPath => Path.Combine(_toolsDir, "ffmpeg.exe");

    /// <summary>
    /// Возвращает путь к скачанному файлу или null при ошибке.
    /// </summary>
    public async Task<string?> DownloadAsync(
        string url,
        string outputDir,
        string quality,
        string? proxy,
        Action<DownloadProgress> onProgress,
        Action<string> onLog,
        CancellationToken ct = default)
    {
        if (!File.Exists(YtDlpPath))
            throw new FileNotFoundException($"yt-dlp.exe не найден: {YtDlpPath}");

        Directory.CreateDirectory(outputDir);

        string format = $"bestvideo[height<={quality}]+bestaudio/best";

        var args = new StringBuilder();
        args.Append($"-f \"{format}\" ");
        args.Append("--merge-output-format mp4 ");
        args.Append("--newline ");
        args.Append("--no-part ");
        args.Append("--force-ipv4 ");
        args.Append("--no-check-certificates ");
        args.Append("--user-agent \"\" ");

        // Приоритет кодеков:
        //   до 1080p — H.264 (играется везде без расширений)
        //   2K/4K/8K — AV1 (нужно расширение AV1 Video Extension)
        if (int.TryParse(quality, out int q) && q <= 1080)
            args.Append("-S \"vcodec:h264,res,acodec:m4a\" ");
        else
            args.Append("-S \"vcodec:av01,res,acodec:opus\" ");

        if (!string.IsNullOrWhiteSpace(proxy))
            args.Append($"--proxy \"{proxy}\" ");

        args.Append("--ffmpeg-location \"").Append(_toolsDir).Append("\" ");
        args.Append($"-o \"{Path.Combine(outputDir, "%(title)s.%(ext)s")}\" ");
        args.Append($"\"{url}\"");

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

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tcs = new TaskCompletionSource<bool>();
        string? finalFilePath = null;

        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            onLog(e.Data);

            var mm = MergerPathRegex.Match(e.Data);
            if (mm.Success)
                finalFilePath = mm.Groups[1].Value.Trim();

            var dm = DestPathRegex.Match(e.Data);
            if (dm.Success)
                finalFilePath = dm.Groups[1].Value.Trim();

            ParseLine(e.Data, onProgress);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            onLog("[err] " + e.Data);

            var mm = MergerPathRegex.Match(e.Data);
            if (mm.Success)
                finalFilePath = mm.Groups[1].Value.Trim();

            var dm = DestPathRegex.Match(e.Data);
            if (dm.Success)
                finalFilePath = dm.Groups[1].Value.Trim();

            ParseLine(e.Data, onProgress);
        };

        process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode == 0);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using (ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
            tcs.TrySetCanceled();
        }))
        {
            bool ok = await tcs.Task;
            return ok ? finalFilePath : null;
        }
    }

    private static void ParseLine(string line, Action<DownloadProgress> onProgress)
    {
        var m = ProgressRegex.Match(line);
        if (m.Success)
        {
            onProgress(new DownloadProgress
            {
                Percent = double.TryParse(m.Groups["percent"].Value,
                    CultureInfo.InvariantCulture, out var p) ? p : 0,
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