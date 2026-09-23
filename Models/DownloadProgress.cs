namespace Vydra.Models;

public class DownloadProgress
{
    public double Percent { get; set; }
    public string Status { get; set; } = "";
    public string Speed { get; set; } = "";
    public string Eta { get; set; } = "";
    public string RawLine { get; set; } = "";
}