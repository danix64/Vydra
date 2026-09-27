using Newtonsoft.Json;
using System;
using System.IO;
using System.Xml;
using Vydra.Models;

namespace Vydra.Services
{
    public static class SettingsService
    {
        private static readonly string SettingsDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vydra");

        private static readonly string SettingsPath =
            Path.Combine(SettingsDir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var s = JsonConvert.DeserializeObject<AppSettings>(json);
                    if (s != null) return s;
                }
            }
            catch { }

            var settings = new AppSettings();
            settings.DefaultFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Vydra");
            settings.DefaultQuality = "1080";
            settings.HistoryLimit = 30;
            return settings;
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var json = JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}