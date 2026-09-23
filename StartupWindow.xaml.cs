using System.Windows;
using Vydra.Dialogs;
using Vydra.Services;

namespace Vydra;

public partial class StartupWindow : Window
{
    private readonly ToolManager _tools;
    private CancellationTokenSource? _cts;
    private bool _running;

    public bool CompletedSuccessfully { get; private set; } = false;

    public StartupWindow(ToolManager tools)
    {
        InitializeComponent();
        _tools = tools;

        var missing = _tools.GetMissingTools();
        StatusText.Text = $"Отсутствуют: {string.Join(", ", missing)}";
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
                    StatusText.Text = $"{tool}: {percent:F0}%";
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
            Close();
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
        _cts?.Cancel();
        CompletedSuccessfully = false;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        CompletedSuccessfully = false;
        Close();
    }
}