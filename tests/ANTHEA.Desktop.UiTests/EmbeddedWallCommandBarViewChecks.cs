using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.EmbeddedWalls;

namespace X.Desktop;

/// <summary>
/// Organizzazione del modulo paratie (progetto del 16/11), step 1, off screen: every page of the Navy command bar, every button with a command
/// of the sheet (no button without a function, D8), the documents opened from the bar and closed from their tab. Captures barra-* and
/// documenti-*; the data and the revision do not change.
/// </summary>
internal static partial class EmbeddedWallViewChecks
{
    private static async Task CheckCommandBar(FrameworkElement view, EmbeddedWallSheetViewModel sheet,
        Func<Task> settle, Action<bool, string> check, Action<string> capture)
    {
        string before = sheet.Data.ToJsonString(); long revision = sheet.Revision;
        var pages = Visual(view).OfType<TabControl>().Single(t => t.Name == "CommandPages");
        var documents = Visual(view).OfType<TabControl>().Single(t => t.Name == "Documents");
        check(pages.Items.OfType<TabItem>().Select(t => t.Tag as string).SequenceEqual(EmbeddedWallSheetViewModel.CommandPages)
            && pages.SelectedValue as string == "modello", "Passo 1: pagine della barra dei comandi assenti o fuori ordine.");
        foreach (string page in EmbeddedWallSheetViewModel.CommandPages)
        {
            sheet.CommandPage = page; await settle();
            var buttons = Visual((DependencyObject)pages.SelectedContent).OfType<Button>().ToArray();
            check(pages.SelectedValue as string == page && buttons.Length > 0 && buttons.All(b => b.Command is not null || b.ContextMenu is not null),
                $"Passo 1: la pagina «{page}» non è mostrata o ha un pulsante senza comando.");
            capture("barra-" + page);
        }
        sheet.CommandPage = "modello"; await settle();
        sheet.OpenResultsCommand.Execute(null); sheet.Documents.Show(EmbeddedWallDocuments.Envelopes); sheet.Documents.Show(EmbeddedWallDocuments.Tables); await settle();
        var shown = documents.Items.OfType<TabItem>().Where(t => t.Visibility == Visibility.Visible).Select(t => t.Tag as string).ToArray();
        check(shown.SequenceEqual([EmbeddedWallDocuments.Section, EmbeddedWallDocuments.Tables, EmbeddedWallDocuments.Diagrams, EmbeddedWallDocuments.Envelopes])
            && documents.SelectedValue as string == EmbeddedWallDocuments.Tables, "Passo 1: schede dei documenti aperti o documento attivo errati: " + string.Join(", ", shown));
        capture("documenti-aperti");
        var close = Visual(documents).OfType<Button>().Single(b => b.DataContext is EmbeddedWallDocumentTab { Key: EmbeddedWallDocuments.Tables } && b.Content as string == "×");
        close.Command!.Execute(null); await settle();
        check(documents.SelectedValue as string == EmbeddedWallDocuments.Envelopes && !sheet.Documents[EmbeddedWallDocuments.Tables].IsOpen
            && !Visual(documents).OfType<Button>().Any(b => b.DataContext is EmbeddedWallDocumentTab { Key: EmbeddedWallDocuments.Section } && b.IsVisible && b.Content as string == "×"),
            "Passo 1: la chiusura dalla scheda non torna al documento precedente, o la sezione si può chiudere.");
        sheet.Documents.CloseAll(); await settle();
        check(documents.SelectedValue as string == EmbeddedWallDocuments.Section && sheet.Data.ToJsonString() == before && sheet.Revision == revision,
            "Passo 1: la barra o i documenti modificano i dati o la revisione.");
    }
}
