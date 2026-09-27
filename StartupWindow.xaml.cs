using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Vydra.Dialogs;
using Vydra.Services;

namespace Vydra
{
    public partial class StartupWindow : Window
    {
        private readonly ToolManager _tools;
        private CancellationTokenSource _cts;
        private bool _running;

        public bool CompletedSuccessfully { get; private set; }

        public StartupWindow(ToolManager tools)
        {
            InitializeComponent();
            _tools = tools;
            CompletedSuccessfully = false;

            var missing = _tools.GetMissingTools();
            StatusText.Text = "Отсутствуют: " + string.Join(", ", missing);
            DetailText.Text = "Нажмите «Скачать», чтобы продолжить.";
        }

        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_running) return;
            _running = true;

            StartBtn.IsEnabled = false;
            SkipBtn.IsEnabled = false;

            _cts = new CancellationTokenSource();

            try
            {
                await _tools.DownloadMissingAsync((tool, percent, status) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        Progress.Value = percent;
                        StatusText.Text = tool + ": " + percent.ToString("F0") + "%";
                        DetailText.Text = status;
                    });
                }, _cts.Token);

                CompletedSuccessfully = true;

                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Готово";
                    DetailText.Text = "Все утилиты скачаны. Запускаем Vydra...";
                });

                await Task.Delay(700);
                Dispatcher.Invoke(() => Close());
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() => Close());
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    DialogService.Error(
                        "Не удалось скачать утилиты:\n" + ex.Message +
                        "\n\nПроверьте интернет-соединение и попробуйте снова.");

                    StartBtn.IsEnabled = true;
                    SkipBtn.IsEnabled = true;
                    _running = false;
                });
            }
        }

        private void SkipBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null) _cts.Cancel();
            CompletedSuccessfully = false;
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
            if (_cts != null) _cts.Cancel();
            CompletedSuccessfully = false;
            Close();
        }
    }
}