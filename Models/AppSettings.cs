namespace Vydra.Models
{
    public class AppSettings
    {
        public string Proxy { get; set; }
        public string DefaultFolder { get; set; }
        public string DefaultQuality { get; set; }
        public int HistoryLimit { get; set; }

        public AppSettings()
        {
            Proxy = "";
            DefaultFolder = "";
            DefaultQuality = "1080";
            HistoryLimit = 30;
        }
    }
}