using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Modules.EmbeddedWalls;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Organizzazione del modulo paratie (progetto del 16/11), step 1: pages of the command bar and documents opened on request. The section of
/// the phase is always open; the other views open from the commands and close from their tab; closing the active one returns to the one used
/// before. Pages and documents are state of the view: neither the data nor the revision change.
/// </summary>
internal static partial class EmbeddedWallChecks
{
    private static void CommandBarChecks(Expect e)
    {
        using var sheet = Open(EmbeddedWallDocument.Create());
        string original = sheet.Data.ToJsonString(); long revision = sheet.Revision;
        var documents = sheet.Documents;
        e.That(documents.All.Count == 13 && documents.Active == EmbeddedWallDocuments.Section && documents.Open.Single().Key == EmbeddedWallDocuments.Section,
            "Passo 1: all'apertura il solo documento aperto deve essere la sezione.");
        e.That(!documents[EmbeddedWallDocuments.Section].CanClose && !documents[EmbeddedWallDocuments.Section].CloseCommand.CanExecute(null)
            && documents.All.Where(d => d.Key != EmbeddedWallDocuments.Section).All(d => d.CanClose && !d.IsOpen), "Passo 1: la sezione si può chiudere o un documento nasce aperto.");
        e.That(EmbeddedWallSheetViewModel.CommandPages.Count == 9 && sheet.CommandPage == "modello", "Passo 1: pagine della barra o pagina iniziale errate.");
        sheet.CommandPage = "risultati"; sheet.CommandPage = "inesistente";
        e.That(sheet.CommandPage == "risultati", "Passo 1: la pagina della barra accetta una chiave sconosciuta.");
        sheet.OpenResultsCommand.Execute(null);
        e.That(documents.Active == EmbeddedWallDocuments.Diagrams && documents[EmbeddedWallDocuments.Diagrams].IsOpen, "Passo 1: «Diagrammi e risultati» non si apre.");
        documents.ShowCommand.Execute(EmbeddedWallDocuments.Envelopes);
        e.That(documents.Active == EmbeddedWallDocuments.Envelopes && documents.Open.Select(d => d.Key).SequenceEqual(
            [EmbeddedWallDocuments.Section, EmbeddedWallDocuments.Diagrams, EmbeddedWallDocuments.Envelopes]), "Passo 1: documenti aperti o ordine delle schede errati.");
        sheet.EditModelCommand.Execute("left_soils");
        e.That(documents.Active == EmbeddedWallDocuments.Section && sheet.InspectorTable?.Key == "left_soils" && documents[EmbeddedWallDocuments.Envelopes].IsOpen,
            "Passo 1: un comando di modifica non torna alla sezione o chiude gli altri documenti.");
        documents.Show(EmbeddedWallDocuments.Envelopes);
        documents[EmbeddedWallDocuments.Envelopes].CloseCommand.Execute(null);
        e.That(documents.Active == EmbeddedWallDocuments.Section && !documents[EmbeddedWallDocuments.Envelopes].IsOpen,
            "Passo 1: chiudendo il documento attivo non si torna a quello usato prima.");
        documents.Show(EmbeddedWallDocuments.Diagrams); documents.Show(EmbeddedWallDocuments.Envelopes); documents.Close(EmbeddedWallDocuments.Envelopes);
        e.That(documents.Active == EmbeddedWallDocuments.Diagrams, "Passo 1: chiudendo il documento attivo non si attiva il precedente ancora aperto.");
        documents.Close(EmbeddedWallDocuments.Section); documents.Active = "inesistente";
        e.That(documents[EmbeddedWallDocuments.Section].IsOpen && documents.Active == EmbeddedWallDocuments.Diagrams, "Passo 1: chiusa la sezione o accettata una chiave sconosciuta.");
        sheet.OpenDataCommand.Execute(null);
        e.That(documents.Active == EmbeddedWallDocuments.Tables, "Passo 1: «Apri tutte le tabelle» non apre le tabelle.");
        documents.CloseAllCommand.Execute(null);
        e.That(documents.Active == EmbeddedWallDocuments.Section && documents.Open.Count == 1, "Passo 1: «Chiudi tutti i documenti» lascia documenti aperti.");
        e.Throws<ArgumentException>(() => documents.Show("inesistente"), "Passo 1: aperto un documento sconosciuto.");
        e.That(sheet.Data.ToJsonString() == original && sheet.Revision == revision && sheet.Current is null,
            "Passo 1: pagine o documenti modificano i dati, la revisione o avviano un calcolo.");
    }
}
