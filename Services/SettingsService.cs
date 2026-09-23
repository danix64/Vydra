using System.IO;
using System.Text.Json;
using Vydra.Models;

namespace Vydra.Services;

public static class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vydra");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var s = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (s != null) return s;
            }
        }
        catch
        {
            // если файл битый — вернём дефолт
        }

        return new AppSettings
        {
            DefaultFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Vydra"),
            DefaultQuality = "1080"
        };
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}