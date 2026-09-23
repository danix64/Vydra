using System.Diagnostics;
using System.Threading.Tasks;

namespace Vydra.Services;

public static class Av1Service
{
    private static bool? _cached;

    public static bool IsInstalled(bool forceRefresh = false)
    {
        if (_cached.HasValue && !forceRefresh)
            return _cached.Value;

        _cached = CheckAv1Internal();
        return _cached.Value;
    }

    /// <summary>
    /// Асинхронная проверка — не блокирует UI.
    /// </summary>
    public static Task<bool> IsInstalledAsync(bool forceRefresh = false)
    {
        if (_cached.HasValue && !forceRefresh)
            return Task.FromResult(_cached.Value);

        return Task.Run(() =>
        {
            bool result = CheckAv1Internal();
            _cached = result;
            return result;
        });
    }

    public static void OpenStorePage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-windows-store://pdp/?productid=9MVZQVXJBQ9V",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private static bool CheckAv1Internal()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -Command \"(Get-AppxPackage *AV1VideoExtension* | Measure-Object).Count\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return false;

            var readTask = p.StandardOutput.ReadToEndAsync();
            if (!readTask.Wait(TimeSpan.FromSeconds(6)))
            {
                try { p.Kill(true); } catch { }
                return false;
            }

            string output = readTask.Result.Trim();
            p.WaitForExit(2000);

            return int.TryParse(output, out int count) && count > 0;
        }
        catch
        {
            return false;
        }
    }
}