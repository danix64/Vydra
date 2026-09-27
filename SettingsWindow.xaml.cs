using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using Vydra.Dialogs;
using Vydra.Models;
using Vydra.Services;

namespace Vydra
{
    public partial class SettingsWindow : Window
    {
        public AppSettings Settings { get; private set; }
        public bool Saved { get; private set; }

        private ToolManager _tools;
        private LavFilterInstaller _lavInstaller;
        private CancellationTokenSource _lavCts;

        private readonly HttpClient _http;

        private const string AppRepoApi = "https://api.github.com/repos/danix64/Vydra/releases/latest";
        private const string AppVersion = "0.2.0";

        public SettingsWindow(AppSettings current)
        {
            InitializeComponent();

            Saved = false;
            _lavInstaller = new LavFilterInstaller();
            _http = new HttpClient();

            try { _http.DefaultRequestHeaders.UserAgent.ParseAdd("Vydra/0.2"); } catch { }

            Settings = new AppSettings();
            Settings.Proxy = current.Proxy;
            Settings.DefaultFolder = current.DefaultFolder;
            Settings.DefaultQuality = current.DefaultQuality;
            Settings.HistoryLimit = current.HistoryLimit;

            ProxyBox.Text = Settings.Proxy;
            AppVersionText.Text = "Vydra " + AppVersion;

            CheckAv1();
            CheckLavFilters();
            CheckTools();
        }

        public void SetToolManager(ToolManager tm)
        {
            _tools = tm;
            CheckTools();
        }

        private void CheckTools()
        {
            if (_tools == null)
            {
                ToolsCheck.Content = "Утилиты: статус неизвестен";
                ToolsCheck.Visibility = Visibility.Collapsed;
                DownloadToolsBtn.Visibility = Visibility.Collapsed;
                return;
            }

            ToolsCheck.Visibility = Visibility.Visible;

            var missing = _tools.GetMissingTools();
            if (missing.Count == 0)
            {
                ToolsCheck.IsChecked = true;
                ToolsCheck.Content = "Утилиты: установлены ✓";
                DownloadToolsBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                ToolsCheck.IsChecked = false;
                ToolsCheck.Content = "Утилиты: не хватает (" + string.Join(", ", missing) + ")";
                DownloadToolsBtn.Visibility = Visibility.Visible;
            }
        }

        private async void DownloadToolsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_tools == null) return;

            DownloadToolsBtn.IsEnabled = false;
            var cts = new CancellationTokenSource();

            try
            {
                await _tools.DownloadMissingAsync((tool, percent, status) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ToolsCheck.Content = status;
                    });
                }, cts.Token);

                CheckTools();
                DialogService.Info("Утилиты успешно скачаны.");
            }
            catch (Exception ex)
            {
                DialogService.Error("Ошибка скачивания: " + ex.Message);
            }
            finally
            {
                DownloadToolsBtn.IsEnabled = true;
            }
        }

        private async void UpdateYtDlpBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_tools == null) return;

            UpdateYtDlpBtn.IsEnabled = false;
            ToolsCheck.Content = "Проверка обновления...";

            try
            {
                string result = await _tools.UpdateYtDlpAsync();
                CheckTools();
                DialogService.Info(result, "Обновление yt-dlp");
            }
            catch (Exception ex)
            {
                DialogService.Error("Ошибка: " + ex.Message);
            }
            finally
            {
                UpdateYtDlpBtn.IsEnabled = true;
            }
        }

        private async void CheckAv1(bool forceRefresh = false)
        {
            if (forceRefresh)
                Av1Service.ResetCache();

            bool installed = await Av1Service.IsInstalledAsync(forceRefresh);

            if (installed)
            {
                Av1Check.IsChecked = true;
                Av1Check.IsEnabled = false;
                Av1Check.Content = "AV1 Video Extension: установлено ✓";
                InstallAv1Btn.Visibility = Visibility.Collapsed;
            }
            else
            {
                Av1Check.IsChecked = false;
                Av1Check.IsEnabled = false;
                Av1Check.Content = "AV1 Video Extension: не установлено";
                InstallAv1Btn.Visibility = Visibility.Visible;
            }
        }

        private void InstallAv1Btn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-windows-store://pdp/?productid=9MVZQVXJBQ9V",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                DialogService.Warning(
                    "Не удалось открыть Microsoft Store.\n\n" +
                    "Откройте вручную: Microsoft Store → поиск → AV1 Video Extension\n\n" +
                    "Ошибка: " + ex.Message,
                    "Microsoft Store");
                return;
            }

            Task.Run(async () =>
            {
                int[] delays = { 10000, 30000, 60000 };
                foreach (var delay in delays)
                {
                    await Task.Delay(delay);
                    Dispatcher.Invoke(() => CheckAv1(true));

                    bool isChecked = false;
                    Dispatcher.Invoke(() => { isChecked = Av1Check.IsChecked == true; });
                    if (isChecked) break;
                }
            });
        }

        private async void CheckLavFilters()
        {
            bool installed = await Task.Run(() => LavFilterService.IsInstalled());

            if (installed)
            {
                LavCheck.IsChecked = true;
                LavCheck.IsEnabled = false;
                LavCheck.Content = "LAV Filters: установлены ✓";
                InstallLavBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                LavCheck.IsChecked = false;
                LavCheck.IsEnabled = false;
                LavCheck.Content = "LAV Filters: не установлены";
                InstallLavBtn.Visibility = Visibility.Visible;
            }
        }

        private async void InstallLavBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_lavInstaller == null) return;

            InstallLavBtn.IsEnabled = false;
            LavCheck.Content = "LAV Filters: скачивание...";

            _lavCts = new CancellationTokenSource();

            try
            {
                string tempDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Vydra", "temp");

                string installerPath = await _lavInstaller.DownloadLatestInstallerAsync(
                    tempDir,
                    (percent, status) =>
                    {
                        Dispatcher.Invoke(() => LavCheck.Content = status);
                    },
                    _lavCts.Token);

                if (_lavCts.IsCancellationRequested) return;

                if (installerPath == null)
                {
                    LavCheck.Content = "LAV Filters: не установлены";
                    DialogService.Error(
                        "Не удалось скачать установщик LAV Filters.\n\n" +
                        "Проверьте интернет-соединение и попробуйте снова.",
                        "LAV Filters");
                    return;
                }

                bool yes = DialogService.Question(
                    "Установщик LAV Filters скачан.\n\n" +
                    "Сейчас откроется окно установки. Вам нужно будет:\n" +
                    "  1. Нажать «Да» в запросе прав администратора (UAC)\n" +
                    "  2. Пройти мастер установки (Next → Install → Finish)\n\n" +
                    "Запустить установщик?",
                    "Установка LAV Filters");

                if (_lavCts.IsCancellationRequested) return;

                if (!yes)
                {
                    LavCheck.Content = "LAV Filters: не установлены";
                    return;
                }

                LavCheck.Content = "LAV Filters: ожидание установки...";
                LavFilterInstaller.LaunchInstaller(installerPath);

                LavCheck.Content = "LAV Filters: установка... (проверяем)";

                bool installed = false;

                for (int i = 0; i < 60; i++)
                {
                    if (_lavCts.IsCancellationRequested) return;

                    await Task.Delay(5000, _lavCts.Token);

                    installed = await Task.Run(() => LavFilterService.IsInstalled());

                    if (installed) break;
                }

                if (_lavCts.IsCancellationRequested) return;

                if (installed)
                {
                    LavCheck.IsChecked = true;
                    LavCheck.Content = "LAV Filters: установлены ✓";
                    InstallLavBtn.Visibility = Visibility.Collapsed;

                    DialogService.Info("LAV Filters успешно установлены!", "LAV Filters");
                }
                else
                {
                    LavCheck.Content = "LAV Filters: не установлены";
                    DialogService.Warning(
                        "Установка не завершилась за 5 минут.\n\n" +
                        "Если вы установили LAV Filters — перезапустите Vydra.",
                        "LAV Filters");
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (_lavCts != null && !_lavCts.IsCancellationRequested)
                {
                    LavCheck.Content = "LAV Filters: не установлены";
                    DialogService.Error("Ошибка: " + ex.Message, "LAV Filters");
                }
            }
            finally
            {
                InstallLavBtn.IsEnabled = true;
                if (_lavCts != null) { _lavCts.Dispose(); _lavCts = null; }
            }
        }

        private void OpenLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Vydra", "logs");

                if (!Directory.Exists(logDir))
                {
                    DialogService.Info("Логи ещё не создавались.");
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = logDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                DialogService.Error("Не удалось открыть папку: " + ex.Message);
            }
        }

        private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdateBtn.IsEnabled = false;
            CheckUpdateBtn.Content = "Проверка...";

            try
            {
                using (var req = new HttpRequestMessage(HttpMethod.Get, AppRepoApi))
                {
                    req.Headers.UserAgent.ParseAdd("Vydra/0.2");
                    req.Headers.Accept.ParseAdd("application/vnd.github+json");

                    using (var resp = await _http.SendAsync(req))
                    {
                        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            DialogService.Info(
                                "Обновления пока не настроены.\n\n" +
                                "Это нормально — программа ещё не опубликована на GitHub.\n" +
                                "После релиза кнопка начнёт работать.",
                                "Обновление Vydra");
                            return;
                        }

                        if (!resp.IsSuccessStatusCode)
                        {
                            DialogService.Warning(
                                "Не удалось проверить обновления (код " + (int)resp.StatusCode + ").\n\n" +
                                "Проверьте интернет-соединение.",
                                "Обновление Vydra");
                            return;
                        }

                        string json = await resp.Content.ReadAsStringAsync();
                        var doc = JObject.Parse(json);

                        string latestVersion = "unknown";
                        string htmlUrl = "";

                        if (doc["tag_name"] != null)
                        {
                            latestVersion = doc["tag_name"].ToString().TrimStart('v', 'V');
                        }

                        if (doc["html_url"] != null)
                            htmlUrl = doc["html_url"].ToString();

                        if (CompareVersions(latestVersion, AppVersion) <= 0)
                        {
                            DialogService.Info("У вас последняя версия: " + AppVersion, "Обновление Vydra");
                            return;
                        }

                        bool yes = DialogService.Question(
                            "Доступна новая версия Vydra: " + latestVersion + "\n" +
                            "У вас установлена: " + AppVersion + "\n\n" +
                            "Открыть страницу загрузки?",
                            "Доступно обновление");

                        if (yes && !string.IsNullOrEmpty(htmlUrl))
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = htmlUrl,
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                DialogService.Error("Не удалось открыть браузер: " + ex.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DialogService.Warning("Не удалось проверить обновления.\n\n" + ex.Message, "Обновление Vydra");
            }
            finally
            {
                CheckUpdateBtn.IsEnabled = true;
                CheckUpdateBtn.Content = "Проверить обновления";
            }
        }

        private static int CompareVersions(string a, string b)
        {
            try
            {
                var pa = a.Split('.');
                var pb = b.Split('.');

                int len = Math.Max(pa.Length, pb.Length);
                for (int i = 0; i < len; i++)
                {
                    int na = 0, nb = 0;
                    if (i < pa.Length) int.TryParse(pa[i], out na);
                    if (i < pb.Length) int.TryParse(pb[i], out nb);

                    if (na != nb) return na.CompareTo(nb);
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            Settings.Proxy = ProxyBox.Text.Trim();
            SettingsService.Save(Settings);
            Saved = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Saved = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_lavCts != null) _lavCts.Cancel();
            Saved = false;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_lavCts != null) _lavCts.Cancel();
            base.OnClosing(e);
        }
    }
}