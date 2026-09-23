using System.IO;
using System.Text.Json;
using Vydra.Models;

namespace Vydra.Services;

public static class HistoryService
{
    private const int MaxItems = 30;

    private static readonly string HistoryDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vydra");

    private static readonly string HistoryPath =
        Path.Combine(HistoryDir, "history.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static List<DownloadHistoryItem> Load()
    {
        try
        {
            if (File.Exists(HistoryPath))
            {
                var json = File.ReadAllText(HistoryPath);
                var list = JsonSerializer.Deserialize<List<DownloadHistoryItem>>(json, JsonOptions);
                if (list != null)
                    return list.OrderByDescending(x => x.Date).ToList();
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
            var trimmed = items
                .OrderByDescending(x => x.Date)
                .Take(MaxItems)
                .ToList();

            var json = JsonSerializer.Serialize(trimmed, JsonOptions);
            File.WriteAllText(HistoryPath, json);
        }
        catch { }
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