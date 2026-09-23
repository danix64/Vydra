using System.Linq;
using System.Windows;

namespace Vydra.Dialogs;

public static class DialogService
{
    public static void Info(string message, string title = "Информация")
    {
        Show(title, message, VydraDialogType.Info, VydraDialogButtons.Ok);
    }

    public static void Warning(string message, string title = "Внимание")
    {
        Show(title, message, VydraDialogType.Warning, VydraDialogButtons.Ok);
    }

    public static void Error(string message, string title = "Ошибка")
    {
        Show(title, message, VydraDialogType.Error, VydraDialogButtons.Ok);
    }

    public static bool Question(string message, string title = "Вопрос")
    {
        var result = Show(title, message, VydraDialogType.Question, VydraDialogButtons.YesNo);
        return result == VydraDialogResult.Yes;
    }

    public static VydraDialogResult Show(string title, string message,
                                          VydraDialogType type, VydraDialogButtons buttons)
    {
        var dlg = new MessageDialog(title, message, type, buttons);

        var owner = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive);

        if (owner != null && owner != dlg)
            dlg.Owner = owner;

        dlg.ShowDialog();
        return dlg.Result;
    }
}