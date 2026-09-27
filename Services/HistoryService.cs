using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Vydra.Models;

namespace Vydra.Services
{
    public static class HistoryService
    {
        private static readonly string HistoryDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vydra");

        private static readonly string HistoryPath =
            Path.Combine(HistoryDir, "history.json");

        public static List<DownloadHistoryItem> Load()
        {
            try
            {
                if (File.Exists(HistoryPath))
                {
                    var bytes = File.ReadAllBytes(HistoryPath);
                    var json = Encoding.UTF8.GetString(bytes);
                    var list = JsonConvert.DeserializeObject<List<DownloadHistoryItem>>(json);
                    if (list != null)
                        return list;
                }
            }
            catch { }

            return new List<DownloadHistoryItem>();
        }

        public static void Save(List<DownloadHistoryItem> items)
        {
            try
            {
                Directory.CreateDirectory(HistoryDir);

                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    StringEscapeHandling = StringEscapeHandling.Default
                };

                var json = JsonConvert.SerializeObject(items, settings);
                File.WriteAllText(HistoryPath, json, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(
                        Path.Combine(HistoryDir, "history_error.log"),
                        DateTime.Now.ToString() + " — " + ex.Message + "\n" + ex.StackTrace + "\n\n",
                        Encoding.UTF8);
                }
                catch { }
            }
        }

        public static void Add(DownloadHistoryItem item)
        {
            var list = Load();
            list.Insert(0, item);
            Save(list);
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(HistoryPath))
                    File.Delete(HistoryPath);
            }
            catch { }
        }
    }
}