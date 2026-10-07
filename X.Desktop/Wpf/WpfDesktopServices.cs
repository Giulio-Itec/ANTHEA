using System.Windows;
using System.Windows.Controls;
using X.Desktop.Services;

namespace X.Desktop;

/// <summary>Production services of a main window: the same dialogs and messages as before, owned by that window.</summary>
internal static class WpfDesktopServices
{
    internal static DesktopServices Create(Window owner) => new(new WpfConfirmationService(owner), new WpfMessageService(owner));
}

/// <summary>Dialogs formerly inline in ProjectSharing.cs, ProjectMovePreview.cs and in the Closing handler of MainWindow.</summary>
internal sealed class WpfConfirmationService(Window owner) : IConfirmationService
{
    public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt)
    {
        var content = Ui.Stack(Ui.Text("Hai modificato: " + string.Join(", ", prompt.ChangedGroups), 17, true),
            Ui.Text("Sezione: " + prompt.SectionName + "\nAggiorna i parametri compatibili nei seguenti fogli, oppure mantieni la modifica solo qui. Carichi e combinazioni restano specifici di ciascun foglio.", 14),
            Ui.Text(string.Join("\n\n", prompt.Targets.Select(t => t.SheetName + "\n" + string.Join("\n", t.Changes.Select(c => "• " + c)))), 14));
        content.Margin = new Thickness(18);
        var dialog = Ui.Dialog(owner, "Dati condivisi della sezione", new ScrollViewer { Background = Appearance.Surface, Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 670, 440);
        var all = Ui.Button("Aggiorna tutti i fogli collegati", () => dialog.DialogResult = true, true);
        var local = Ui.Button("Solo questo foglio", () => dialog.DialogResult = false); local.IsCancel = true;
        content.Children.Add(Ui.Bar(all, local));
        return dialog.ShowDialog() == true;
    }

    public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes)
    {
        var panel = Ui.Stack(Ui.Text("Destinazione: " + destination, 18, true),
            Ui.Text("Lo spostamento aggiorna questi dati comuni:", 14), Ui.Text(string.Join("\n\n", changes), 13)); panel.Margin = new Thickness(18);
        var dialog = Ui.Dialog(owner, "Conferma spostamento", new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 730, 550);
        panel.Children.Add(Ui.Bar(Ui.Button("Sposta e aggiorna", () => dialog.DialogResult = true, true), Ui.Button("Annulla", () => dialog.DialogResult = false)));
        return dialog.ShowDialog() == true;
    }

    public bool ConfirmClose(Func<bool> saveOrDiscard) => saveOrDiscard();
}

/// <summary>Message box formerly in MainWindow.Safe.</summary>
internal sealed class WpfMessageService(Window owner) : IMessageService
{
    public void ReportFailure(string title, Exception error) => MessageBox.Show(owner, error.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
