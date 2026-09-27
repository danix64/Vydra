using System;

namespace Vydra.Models
{
    public class DownloadHistoryItem
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string FilePath { get; set; }
        public string Quality { get; set; }
        public DateTime Date { get; set; }
        public bool Success { get; set; }
        public long FileSizeBytes { get; set; }

        public DownloadHistoryItem()
        {
            Url = "";
            Title = "";
            FilePath = "";
            Quality = "";
            Date = DateTime.Now;
            Success = false;
            FileSizeBytes = 0;
        }

        public string DateDisplay
        {
            get { return Date.ToString("dd.MM.yyyy HH:mm"); }
        }

        public string SizeDisplay
        {
            get
            {
                if (FileSizeBytes <= 0) return "";
                if (FileSizeBytes < 1024) return FileSizeBytes + " B";
                if (FileSizeBytes < 1024 * 1024) return (FileSizeBytes / 1024.0).ToString("F1") + " KB";
                if (FileSizeBytes < 1024L * 1024 * 1024) return (FileSizeBytes / 1024.0 / 1024.0).ToString("F1") + " MB";
                return (FileSizeBytes / 1024.0 / 1024.0 / 1024.0).ToString("F2") + " GB";
            }
        }

        public string StatusDisplay
        {
            get { return Success ? "✓ Готово" : "✗ Ошибка"; }
        }
    }
}