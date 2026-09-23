namespace Vydra.Models;

public class DownloadHistoryItem
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Quality { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Now;
    public bool Success { get; set; } = false;
    public long FileSizeBytes { get; set; } = 0;

    public string DateDisplay => Date.ToString("dd.MM.yyyy HH:mm");

    public string SizeDisplay
    {
        get
        {
            if (FileSizeBytes <= 0) return "";
            if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
            if (FileSizeBytes < 1024 * 1024) return $"{FileSizeBytes / 1024.0:F1} KB";
            if (FileSizeBytes < 1024L * 1024 * 1024) return $"{FileSizeBytes / 1024.0 / 1024.0:F1} MB";
            return $"{FileSizeBytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
        }
    }

    public string StatusDisplay => Success ? "✓ Готово" : "✗ Ошибка";
}