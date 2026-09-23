using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Vydra.Dialogs;
using Vydra.Models;
using Vydra.Services;

namespace Vydra;

public partial class MainWindow : Window
{
    private readonly string _toolsDir;
    private YtDlpService _ytDlp = null!;
    private ToolManager _toolManager = null!;
    private AppSettings _settings = null!;
    private CancellationTokenSource? _cts;
    private bool _av1Installed;
    private string _lastToolsStatus = "";
    private StreamWriter? _logFile;

    public MainWindow()
    {
        InitializeComponent();

        _toolsDir = Path.Combine(AppContext.BaseDirectory, "Tools");
        Directory.CreateDirectory(_toolsDir);
        _ytDlp = new YtDlpService(_toolsDir);
        _toolManager = new ToolManager(_toolsDir);

        _settings = SettingsService.Load();

        FolderBox.Text = string.IsNullOrWhiteSpace(_settings.DefaultFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Vydra")
            : _settings.DefaultFolder;

        SelectQuality(_settings.DefaultQuality);

        try
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Vydra", "logs");
            Directory.CreateDirectory(logDir);
            string logPath = Path.Combine(logDir, $"{DateTime.Now:yyyy-MM-dd}.log");
            _logFile = new StreamWriter(logPath, append: true) { AutoFlush = true };
            _logFile.WriteLine($"--- Сессия {DateTime.Now:dd.MM.yyyy HH:mm:ss} ---");
        }
        catch { }

        Loaded += MainWindow_Loaded;
    }

    private void SelectQuality(string quality)
    {
        for (int i = 0; i < QualityBox.Items.Count; i++)
        {
            var item = (ComboBoxItem)QualityBox.Items[i];
            if (item.Content.ToString()!.StartsWith(quality))
            {
                QualityBox.SelectedIndex = i;
                return;
            }
        }
        QualityBox.SelectedIndex = 1;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Vydra");
            string markerFile = Path.Combine(appDataDir, "av1_check_shown.txt");

            if (!File.Exists(markerFile))
            {
                DialogService.Info(
                    "Vydra сейчас проверит, установлено ли AV1 Video Extension.\n\n" +
                    "На секунду может моргнуть окно PowerShell — это нормально, оно закроется само.",
                    "Проверка AV1");

                Directory.CreateDirectory(appDataDir);
                File.WriteAllText(markerFile, "1");
            }

            _av1Installed = await Av1Service.IsInstalledAsync();
        }
        catch
        {
            _av1Installed = false;
        }

        UpdateCodecHint();

        Log($"Папка утилит: {_toolsDir}");
        if (_av1Installed)
            Log("AV1 Video Extension: установлено");

        var missing = _toolManager.GetMissingTools();
        _lastToolsStatus = missing.Count == 0
            ? "ok"
            : string.Join(",", missing.OrderBy(x => x));

        if (missing.Count > 0)
        {
            Log($"⚠ Отсутствуют утилиты: {string.Join(", ", missing)}. Откройте настройки ⚙ и нажмите «Скачать утилиты».");
            return;
        }

        if (!string.IsNullOrWhiteSpace(_settings.Proxy))
            Log($"Прокси: {_settings.Proxy}");

        Log("Проверка обновления yt-dlp...");
        try
        {
            string result = await _toolManager.UpdateYtDlpAsync();
            Log(result);
        }
        catch (Exception ex)
        {
            Log($"yt-dlp: ошибка проверки обновления — {ex.Message}");
        }
    }

    private void QualityBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCodecHint();
    }

    private void UpdateCodecHint()
    {
        if (CodecHint == null || QualityBox == null) return;

        if (QualityBox.SelectedItem == null)
        {
            CodecHint.Visibility = Visibility.Collapsed;
            return;
        }

        string q = ((ComboBoxItem)QualityBox.SelectedItem)
            .Content.ToString()!.Split(' ')[0];

        bool needsAv1 = int.TryParse(q, out int n) && n > 1080;

        CodecHint.Visibility = (needsAv1 && !_av1Installed)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void BrowseBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Выберите папку для сохранения видео",
            InitialDirectory = Directory.Exists(FolderBox.Text)
                ? FolderBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };

        if (dialog.ShowDialog() == true)
            FolderBox.Text = dialog.FolderName;
    }

    private async void DownloadBtn_Click(object sender, RoutedEventArgs e)
    {
        string url = UrlBox.Text.Trim();
        string folder = FolderBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http"))
        {
            DialogService.Warning("Введите корректную ссылку.");
            return;
        }
        if (string.IsNullOrWhiteSpace(folder))
        {
            DialogService.Warning("Выберите папку для сохранения.");
            return;
        }

        var missing = _toolManager.GetMissingTools();
        if (missing.Contains("yt-dlp") || missing.Contains("ffmpeg"))
        {
            bool yes = DialogService.Question(
                $"Не хватает утилит: {string.Join(", ", missing)}.\n\n" +
                "Скачать сейчас?",
                "Не хватает утилит");

            if (yes)
            {
                var wnd = new SettingsWindow(_settings) { Owner = this };
                wnd.SetToolManager(_toolManager);
                wnd.ShowDialog();
                RefreshToolsStatus();
            }
            return;
        }

        string quality = ((ComboBoxItem)QualityBox.SelectedItem)
            .Content.ToString()!.Split(' ')[0];

        _cts = new CancellationTokenSource();
        SetUiBusy(true);
        Progress.Value = 0;
        LogBox.Clear();
        Log($"=== Скачивание: {url}");
        Log($"Качество: {quality}p, папка: {folder}");

        try
        {
            string? filePath = await _ytDlp.DownloadAsync(
                url, folder, quality,
                _settings.Proxy,
                p => Dispatcher.Invoke(() => UpdateProgress(p)),
                line => Dispatcher.Invoke(() => Log(line)),
                _cts.Token);

            bool ok = filePath != null;

            if (ok)
            {
                StatusText.Text = "Готово ✓";
                Progress.Value = 100;
                Log("=== Загрузка завершена успешно.");

                try
                {
                    long size = 0;
                    if (File.Exists(filePath))
                        size = new FileInfo(filePath).Length;

                    HistoryService.Add(new DownloadHistoryItem
                    {
                        Url = url,
                        FilePath = filePath!,
                        Title = Path.GetFileNameWithoutExtension(filePath!),
                        Quality = quality,
                        Date = DateTime.Now,
                        Success = true,
                        FileSizeBytes = size
                    });
                }
                catch { }
            }
            else
            {
                StatusText.Text = "Ошибка загрузки";
                Log("=== yt-dlp завершился с ошибкой.");

                try
                {
                    HistoryService.Add(new DownloadHistoryItem
                    {
                        Url = url,
                        FilePath = "",
                        Title = "(не удалось)",
                        Quality = quality,
                        Date = DateTime.Now,
                        Success = false
                    });
                }
                catch { }
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Отменено";
            Log("=== Отменено пользователем.");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Ошибка";
            Log("!!! " + ex.Message);
        }
        finally
        {
            SetUiBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private void HistoryBtn_Click(object sender, RoutedEventArgs e)
    {
        var wnd = new HistoryWindow
        {
            Owner = this
        };
        wnd.ShowDialog();
    }

    private void OpenFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        string folder = FolderBox.Text.Trim();
        if (!Directory.Exists(folder))
        {
            DialogService.Info("Папка ещё не создана.");
            return;
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    private void ZapretHintLink_Click(object sender, MouseButtonEventArgs e)
    {
        DialogService.Info(
            "Ваш провайдер, вероятно, блокирует YouTube\n\n" +
            "Vydra не может обойти блокировку самостоятельно — " +
            "это задача системных утилит\n\n" +
            "Что можно сделать:\n\n" +
            "• Zapret — бесплатная утилита, обходит DPI-блокировки " +
            "без VPN. Ищите на GitHub по названию «zapret»\n\n" +
            "• Любой VPN-сервис или свой сервер,\n" +
            " Главное — стабильный, иначе скачивание " +
            "будет медленным\n\n" +
            "После включения обхода — вернитесь в Vydra " +
            "и попробуйте снова",
            "YouTube не качается?");
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        var wnd = new SettingsWindow(_settings)
        {
            Owner = this
        };
        wnd.SetToolManager(_toolManager);
        wnd.ShowDialog();

        if (wnd.Saved)
        {
            _settings = wnd.Settings;

            _settings.DefaultFolder = FolderBox.Text.Trim();
            _settings.DefaultQuality =
                ((ComboBoxItem)QualityBox.SelectedItem)
                .Content.ToString()!.Split(' ')[0];
            SettingsService.Save(_settings);

            Log("Настройки сохранены.");
        }

        RefreshToolsStatus();
    }

    private void RefreshToolsStatus()
    {
        var missing = _toolManager.GetMissingTools();

        string currentStatus = missing.Count == 0
            ? "ok"
            : string.Join(",", missing.OrderBy(x => x));

        if (currentStatus == _lastToolsStatus)
            return;

        _lastToolsStatus = currentStatus;

        if (missing.Count > 0)
            Log($"⚠ Всё ещё отсутствуют утилиты: {string.Join(", ", missing)}.");
        else
            Log("✓ Все утилиты установлены.");
    }

    private void UpdateProgress(DownloadProgress p)
    {
        Progress.Value = p.Percent;
        StatusText.Text = p.Status;
        SpeedText.Text = string.IsNullOrEmpty(p.Speed) ? "" : $"↓ {p.Speed}";
        EtaText.Text = string.IsNullOrEmpty(p.Eta) ? "" : $"ETA {p.Eta}";
    }

    private void SetUiBusy(bool busy)
    {
        DownloadBtn.IsEnabled = !busy;
        CancelBtn.IsEnabled = busy;
        BrowseBtn.IsEnabled = !busy;
        UrlBox.IsEnabled = !busy;
        FolderBox.IsEnabled = !busy;
        QualityBox.IsEnabled = !busy;
    }

    private void Log(string line)
    {
        string stamped = $"[{DateTime.Now:HH:mm:ss}] {line}";

        LogBox.AppendText(stamped + Environment.NewLine);
        LogBox.ScrollToEnd();

        try { _logFile?.WriteLine(stamped); } catch { }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeBtn.Content = "☐";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeBtn.Content = "❐";
        }
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            _logFile?.WriteLine($"--- Сессия закрыта {DateTime.Now:dd.MM.yyyy HH:mm:ss} ---");
            _logFile?.Dispose();
        }
        catch { }

        base.OnClosed(e);
    }
}