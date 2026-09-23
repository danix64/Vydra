using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Vydra.Dialogs;

public enum VydraDialogType
{
    Info,
    Warning,
    Error,
    Question
}

public enum VydraDialogButtons
{
    Ok,
    YesNo,
    OkCancel
}

public enum VydraDialogResult
{
    None,
    Ok,
    Cancel,
    Yes,
    No
}

public partial class MessageDialog : Window
{
    public VydraDialogResult Result { get; private set; } = VydraDialogResult.None;

    public MessageDialog(string title, string message,
                         VydraDialogType type = VydraDialogType.Info,
                         VydraDialogButtons buttons = VydraDialogButtons.Ok)
    {
        InitializeComponent();

        try
        {
            IconImage.Source = new BitmapImage(
                new Uri("pack://application:,,,/vydra.ico"));
        }
        catch { }

        TitleText.Text = title;
        MessageText.Text = message;

        switch (type)
        {
            case VydraDialogType.Info:
                AccentBar.Background = new SolidColorBrush(Color.FromRgb(0x66, 0xC0, 0xF4));
                break;
            case VydraDialogType.Warning:
                AccentBar.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xA0, 0x30));
                break;
            case VydraDialogType.Error:
                AccentBar.Background = new SolidColorBrush(Color.FromRgb(0xE0, 0x50, 0x50));
                break;
            case VydraDialogType.Question:
                AccentBar.Background = new SolidColorBrush(Color.FromRgb(0x66, 0xC0, 0xF4));
                break;
        }

        switch (buttons)
        {
            case VydraDialogButtons.Ok:
                AddButton("OK", VydraDialogResult.Ok, isPrimary: true, isDefault: true, isCancel: true);
                break;
            case VydraDialogButtons.YesNo:
                AddButton("Нет", VydraDialogResult.No, isPrimary: false, isDefault: false, isCancel: true);
                AddButton("Да", VydraDialogResult.Yes, isPrimary: true, isDefault: true, isCancel: false);
                break;
            case VydraDialogButtons.OkCancel:
                AddButton("Отмена", VydraDialogResult.Cancel, isPrimary: false, isDefault: false, isCancel: true);
                AddButton("OK", VydraDialogResult.Ok, isPrimary: true, isDefault: true, isCancel: false);
                break;
        }

        Loaded += (_, _) =>
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            BeginAnimation(OpacityProperty, anim);
        };
    }

    private void AddButton(string text, VydraDialogResult result,
                            bool isPrimary, bool isDefault, bool isCancel)
    {
        var btn = new Button
        {
            Content = text,
            Style = (Style)FindResource(isPrimary ? "PrimaryButton" : "SteamButton"),
            Margin = new Thickness(8, 0, 0, 0)
        };

        if (isDefault) btn.IsDefault = true;
        if (isCancel) btn.IsCancel = true;

        btn.Click += (_, _) =>
        {
            Result = result;
            Close();
        };
        ButtonsPanel.Children.Add(btn);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseXBtn_Click(object sender, RoutedEventArgs e)
    {
        Result = VydraDialogResult.Cancel;
        Close();
    }
}