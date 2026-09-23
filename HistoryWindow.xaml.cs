using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vydra.Dialogs;
using Vydra.Models;
using Vydra.Services;

namespace Vydra;

public partial class HistoryWindow : Window
{
    public HistoryWindow()
    {
        InitializeComponent();
        LoadHistory();
    }

    private void LoadHistory()
    {
        HistoryList.Children.Clear();

        var items = HistoryService.Load();

        if (items.Count == 0)
        {
            EmptyText.Visibility = Visibility.Visible;
            ClearBtn.IsEnabled = false;
            return;
        }

        EmptyText.Visibility = Visibility.Collapsed;
        ClearBtn.IsEnabled = true;

        foreach (var item in items)
        {
            HistoryList.Children.Add(BuildItemCard(item));
        }
    }

    private Border BuildItemCard(DownloadHistoryItem item)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x16, 0x20, 0x2D)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x47, 0x5E)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel();

        var title = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(item.Title) ? "(без названия)" : item.Title,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC7, 0xD5, 0xE0)),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        info.Children.Add(title);

        var sub = new TextBlock
        {
            Text = $"{item.Quality}p · {item.DateDisplay} · {item.SizeDisplay}",
            Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0x98, 0xA0)),
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0)
        };
        info.Children.Add(sub);

        var status = new TextBlock
        {
            Text = item.StatusDisplay,
            Foreground = item.Success
                ? new SolidColorBrush(Color.FromRgb(0x66, 0xC0, 0xF4))
                : new SolidColorBrush(Color.FromRgb(0xE0, 0x50, 0x50)),
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0)
        };
        info.Children.Add(status);

        Grid.SetColumn(info, 0);
        grid.Children.Add(info);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (item.Success && !string.IsNullOrEmpty(item.FilePath) && File.Exists(item.FilePath))
        {
            var openBtn = MakeSmallButton("Открыть");
            openBtn.Click += (_, _) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.FilePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    DialogService.Error("Не удалось открыть файл: " + ex.Message);
                }
            };
            buttons.Children.Add(openBtn);
        }

        var copyBtn = MakeSmallButton("URL");
        copyBtn.ToolTip = "Скопировать ссылку";
        copyBtn.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(item.Url);
                copyBtn.Content = "✓";
            }
            catch { }
        };
        buttons.Children.Add(copyBtn);

        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);

        card.Child = grid;
        return card;
    }

    private Button MakeSmallButton(string text)
    {
        return new Button
        {
            Content = text,
            Style = (Style)FindResource("SteamButton"),
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 11,
            Margin = new Thickness(6, 0, 0, 0)
        };
    }

    private void ClearBtn_Click(object sender, RoutedEventArgs e)
    {
        bool yes = DialogService.Question(
            "Удалить всю историю загрузок?",
            "Очистка истории");

        if (!yes) return;

        HistoryService.Clear();
        LoadHistory();
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
        Close();
    }
}