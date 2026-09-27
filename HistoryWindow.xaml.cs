using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vydra.Dialogs;
using Vydra.Models;
using Vydra.Services;

namespace Vydra
{
    public partial class HistoryWindow : Window
    {
        private string _currentFilter = "all"; // all / video / audio

        public HistoryWindow()
        {
            InitializeComponent();
            UpdateFilterButtons();
            LoadHistory();
        }

        private void LoadHistory()
        {
            HistoryList.Children.Clear();

            var items = HistoryService.Load();

            if (_currentFilter == "video")
                items = items.FindAll(x => x.Quality != "mp3");
            else if (_currentFilter == "audio")
                items = items.FindAll(x => x.Quality == "mp3");

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

        private void FilterAll_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = "all";
            UpdateFilterButtons();
            LoadHistory();
        }

        private void FilterVideo_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = "video";
            UpdateFilterButtons();
            LoadHistory();
        }

        private void FilterAudio_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = "audio";
            UpdateFilterButtons();
            LoadHistory();
        }

        private void UpdateFilterButtons()
        {
            FilterAllBtn.Background = _currentFilter == "all"
                ? new SolidColorBrush(Color.FromRgb(0x3D, 0x6A, 0x8F))
                : new SolidColorBrush(Color.FromRgb(0x2A, 0x47, 0x5E));
            FilterVideoBtn.Background = _currentFilter == "video"
                ? new SolidColorBrush(Color.FromRgb(0x3D, 0x6A, 0x8F))
                : new SolidColorBrush(Color.FromRgb(0x2A, 0x47, 0x5E));
            FilterAudioBtn.Background = _currentFilter == "audio"
                ? new SolidColorBrush(Color.FromRgb(0x3D, 0x6A, 0x8F))
                : new SolidColorBrush(Color.FromRgb(0x2A, 0x47, 0x5E));

            if (_currentFilter == "video")
                ClearBtn.Content = "Очистить видео";
            else if (_currentFilter == "audio")
                ClearBtn.Content = "Очистить аудио";
            else
                ClearBtn.Content = "Очистить всё";
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

            long size = item.FileSizeBytes;
            if (size == 0 && !string.IsNullOrEmpty(item.FilePath) && File.Exists(item.FilePath))
            {
                try { size = new FileInfo(item.FilePath).Length; } catch { }
            }

            string sizeDisplay = "";
            if (size > 0)
            {
                if (size < 1024) sizeDisplay = size + " B";
                else if (size < 1024 * 1024) sizeDisplay = (size / 1024.0).ToString("F1") + " KB";
                else if (size < 1024L * 1024 * 1024) sizeDisplay = (size / 1024.0 / 1024.0).ToString("F1") + " MB";
                else sizeDisplay = (size / 1024.0 / 1024.0 / 1024.0).ToString("F2") + " GB";
            }

            string qualityDisplay = item.Quality == "mp3" ? "mp3" : item.Quality + "p";

            var sub = new TextBlock
            {
                Text = qualityDisplay + " · " + item.DateDisplay + (sizeDisplay != "" ? " · " + sizeDisplay : ""),
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
                openBtn.Click += (sender, e) =>
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
            copyBtn.Click += (sender, e) =>
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
            string question;
            if (_currentFilter == "video")
                question = "Удалить всю историю видео?";
            else if (_currentFilter == "audio")
                question = "Удалить всю историю аудио?";
            else
                question = "Удалить всю историю загрузок?";

            bool yes = DialogService.Question(question, "Очистка истории");
            if (!yes) return;

            if (_currentFilter == "all")
            {
                HistoryService.Clear();
            }
            else if (_currentFilter == "video")
            {
                var items = HistoryService.Load();
                items.RemoveAll(x => x.Quality != "mp3");
                HistoryService.Save(items);
            }
            else if (_currentFilter == "audio")
            {
                var items = HistoryService.Load();
                items.RemoveAll(x => x.Quality == "mp3");
                HistoryService.Save(items);
            }

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
}