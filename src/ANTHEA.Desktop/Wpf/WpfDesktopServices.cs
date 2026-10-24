using System.Windows;
using System.Windows.Controls;
using X.Desktop.Services;

namespace X.Desktop;

/// <summary>
/// The message boxes of the services below. F5.13 g3: the factory of the services of a main window (Create, DesktopServices) is archived with
/// MainWindow in supporto/SUPERATI/ui-storica/shell; the services are those of the composition root (DesktopComposition).
/// </summary>
internal static class WpfDesktopServices
{
    /// <summary>Message box owned by <paramref name="owner"/> when there is one (MessageBox refuses a null owner).</summary>
    internal static MessageBoxResult Show(Window? owner, string text, string caption, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
        => owner is null ? MessageBox.Show(text, caption, buttons, image) : MessageBox.Show(owner, text, caption, buttons, image);
}

/// <summary>
/// Dialogs formerly inline in ProjectSharing.cs, ProjectMovePreview.cs and in the Closing handler of MainWindow; the save and
/// delete questions reproduce MainWindow.ConfirmDiscard and MainWindow.Delete. The owner is read at every question: a main
/// window passes itself, the composition root the active window.
/// </summary>
internal sealed class WpfConfirmationService(Func<Window?> owner) : IConfirmationService
{
    internal WpfConfirmationService(Window owner) : this(() => owner) { }

    private Window Owner => owner() ?? throw new InvalidOperationException("Nessuna finestra di ANTHEA aperta per la domanda.");

    public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt)
    {
        var content = Ui.Stack(Ui.Text("Hai modificato: " + string.Join(", ", prompt.ChangedGroups), 17, true),
            Ui.Text("Sezione: " + prompt.SectionName + "\nAggiorna i parametri compatibili nei seguenti fogli, oppure mantieni la modifica solo qui. Carichi e combinazioni restano specifici di ciascun foglio.", 14),
            Ui.Text(string.Join("\n\n", prompt.Targets.Select(t => t.SheetName + "\n" + string.Join("\n", t.Changes.Select(c => "• " + c)))), 14));
        content.Margin = new Thickness(18);
        var dialog = Ui.Dialog(Owner, "Dati condivisi della sezione", new ScrollViewer { Background = Appearance.Surface, Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 670, 440);
        var all = Ui.Button("Aggiorna tutti i fogli collegati", () => dialog.DialogResult = true, true);
        var local = Ui.Button("Solo questo foglio", () => dialog.DialogResult = false); local.IsCancel = true;
        content.Children.Add(Ui.Bar(all, local));
        return dialog.ShowDialog() == true;
    }

    public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes)
    {
        var panel = Ui.Stack(Ui.Text("Destinazione: " + destination, 18, true),
            Ui.Text("Lo spostamento aggiorna questi dati comuni:", 14), Ui.Text(string.Join("\n\n", changes), 13)); panel.Margin = new Thickness(18);
        var dialog = Ui.Dialog(Owner, "Conferma spostamento", new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 730, 550);
        panel.Children.Add(Ui.Bar(Ui.Button("Sposta e aggiorna", () => dialog.DialogResult = true, true), Ui.Button("Annulla", () => dialog.DialogResult = false)));
        return dialog.ShowDialog() == true;
    }

    public bool ConfirmClose(Func<bool> saveOrDiscard) => saveOrDiscard();

    public SaveChangesAnswer AskSaveChanges()
        => WpfDesktopServices.Show(owner(), "Salvare le modifiche al documento corrente?", "ANTHEA", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
        {
            MessageBoxResult.Yes => SaveChangesAnswer.Save,
            MessageBoxResult.No => SaveChangesAnswer.Discard,
            _ => SaveChangesAnswer.Cancel
        };

    public bool ConfirmDelete(string title, string question)
        => WpfDesktopServices.Show(owner(), question, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <summary>The question of SoilTransferDialog before the replacement of the soil: Yes and No, no icon, owned by the active window (the dialog).</summary>
    public bool ConfirmReplace(string title, string question)
        => WpfDesktopServices.Show(owner(), question, title, MessageBoxButton.YesNo) == MessageBoxResult.Yes;
}

/// <summary>Message box formerly in MainWindow.Safe, and the plain notes and warnings of the views.</summary>
internal sealed class WpfMessageService(Func<Window?> owner) : IMessageService
{
    internal WpfMessageService(Window owner) : this(() => owner) { }

    public void ReportFailure(string title, Exception error) => WpfDesktopServices.Show(owner(), error.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public void Inform(string title, string message) => WpfDesktopServices.Show(owner(), message, title);

    public void Warn(string title, string message) => WpfDesktopServices.Show(owner(), message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
}
