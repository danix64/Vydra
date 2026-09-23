using Microsoft.Win32;

namespace Vydra.Services;

public static class LavFilterService
{
    private static bool? _cached;
    private static DateTime _lastCheck = DateTime.MinValue;
    private static readonly TimeSpan CacheTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Проверяет, установлены ли LAV Filters в системе.
    /// Результат кешируется на 30 секунд.
    /// </summary>
    public static bool IsInstalled(bool forceRefresh = false)
    {
        if (!forceRefresh &&
            _cached.HasValue &&
            DateTime.Now - _lastCheck < CacheTime)
        {
            return _cached.Value;
        }

        bool result = CheckRegistryKey() || CheckComFilters();

        _cached = result;
        _lastCheck = DateTime.Now;
        return result;
    }

    private static bool CheckRegistryKey()
    {
        try
        {
            using var k1 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\LAV Filters");
            if (k1 != null) return true;

            using var k2 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\LAV Filters");
            if (k2 != null) return true;

            using var k3 = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\LAV Filters");
            if (k3 != null) return true;
        }
        catch { }
        return false;
    }

    private static bool CheckComFilters()
    {
        try
        {
            using var clsid = Registry.ClassesRoot.OpenSubKey(@"CLSID");
            if (clsid == null) return false;

            foreach (var subKeyName in clsid.GetSubKeyNames())
            {
                using var subKey = clsid.OpenSubKey(subKeyName);
                if (subKey == null) continue;

                var name = subKey.GetValue(null) as string;
                if (string.IsNullOrEmpty(name)) continue;

                if (name.Contains("LAV Splitter", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("LAV Video", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("LAV Audio", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }
}