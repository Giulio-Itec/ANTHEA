using Anthea.Presentation.Services;

namespace Anthea.Presentation.Tests;

internal static class ServicesChecks
{
    internal static async Task<int> RunAsync()
    {
        var expect = new Expect();
        // Typed filters give the same text the views pass to the Windows dialogs today.
        expect.That(FileFilter.Join([new("Prezzari", "*.csv", "*.xlsx", "*.json"), new("CSV", "*.csv"), new("Excel", "*.xlsx"), new("Catalogo ANTHEA", "*.json")])
            == "Prezzari|*.csv;*.xlsx;*.json|CSV|*.csv|Excel|*.xlsx|Catalogo ANTHEA|*.json", "Filtro composto dei prezzari");
        expect.That(FileFilter.Join([new("File ANTHEA", "*.programma", "*.anthea"), new("Tutti i file", "*.*")]) == "File ANTHEA|*.programma;*.anthea|Tutti i file|*.*", "Filtro dei documenti");
        expect.That(FileFilter.Join([new("Word e PDF affiancati", "*.docx")]) == "Word e PDF affiancati|*.docx", "Filtro dei report");
        expect.That(FileFilter.Join([]) == "", "Filtro vuoto");
        expect.That(new FileFilter("CSV", "*.csv") == new FileFilter("CSV", "*.csv") && new FileFilter("CSV", "*.csv") != new FileFilter("CSV", "*.txt"), "Uguaglianza dei filtri per valore");
        expect.Throws<ArgumentException>(() => _ = new FileFilter("A|B", "*.csv"), "Separatore nel nome accettato");
        expect.Throws<ArgumentException>(() => _ = new FileFilter("CSV", "*.csv;*.txt"), "Due modelli in uno accettati");
        expect.Throws<ArgumentException>(() => _ = new FileFilter("CSV"), "Filtro senza modelli accettato");
        expect.Throws<ArgumentException>(() => _ = new FileFilter(" ", "*.csv"), "Filtro senza nome accettato");
        var request = new SaveFileRequest([new("CSV", "*.csv")], "Palo_orizzontale.csv");
        expect.That(request.OverwritePrompt && request.Title is null, "Valori predefiniti del salvataggio");

        // The composition is complete: no service may be missing.
        expect.Throws<ArgumentNullException>(() => _ = new PresentationServices(null!, new ScriptedConfirmations(), new ScriptedFileDialogs(), new ScriptedDialogs(),
            new RecordingClipboard(), new RecordingLinks(), new RecordingWiki()), "Servizio dei messaggi mancante accettato");
        expect.Throws<ArgumentNullException>(() => _ = new PresentationServices(new ScriptedMessages(), new ScriptedConfirmations(), new ScriptedFileDialogs(), new ScriptedDialogs(),
            new RecordingClipboard(), new RecordingLinks(), null!), "Navigatore della Wiki mancante accettato");

        // Scripted services: answers in order, every call recorded, an unexpected question fails.
        var confirmations = new ScriptedConfirmations().Then(true).Then(SaveChangesAnswer.Cancel);
        var services = Scripted.Services(confirmations: confirmations, files: new ScriptedFileDialogs().Then(@"C:\dati\muro.csv").Then(null));
        expect.That(services.Confirmations.ConfirmDelete("Elimina", "Eliminare il foglio?"), "Risposta a copione persa");
        expect.That(services.Confirmations.AskSaveChanges() == SaveChangesAnswer.Cancel, "Risposta al salvataggio persa");
        expect.Throws<InvalidOperationException>(() => services.Confirmations.ConfirmProjectMove("Fase 2", []), "Domanda non prevista accettata");
        expect.That(confirmations.Asked.SequenceEqual(["Elimina: Eliminare il foglio?", "salvataggio", "spostamento: Fase 2"]), "Domande non registrate");
        expect.That(services.Files.SaveFile(request) == @"C:\dati\muro.csv" && services.Files.OpenFile(new([new("CSV", "*.csv")])) is null, "Percorsi a copione");
        services.Clipboard.SetText("a\tb");
        expect.That(services.Clipboard.GetText() == "a\tb", "Appunti a copione");
        services.Links.Open(new Uri("https://example.test/riferimento"));
        services.Wiki.Show("guida-palificata-orizzontale#metodo-e-applicabilita");
        expect.That(((RecordingLinks)services.Links).Opened.Single().Host == "example.test" && ((RecordingWiki)services.Wiki).Shown.Single() == "guida-palificata-orizzontale#metodo-e-applicabilita", "Collegamenti registrati");

        // Dialog ViewModels: the result arrives only through Close; cancel gives the default.
        var dialogs = new ScriptedDialogs().Then(d => ((RenameDialog)d).Confirm("Fase 2")).Then(d => ((RenameDialog)d).Dismiss()).Then(_ => { });
        using var rename = new RenameDialog();
        int closes = 0; rename.CloseRequested += (_, _) => closes++;
        expect.That(await Expect.Within(dialogs.ShowDialogAsync(rename)) == "Fase 2" && rename.Result == "Fase 2" && closes == 1, "Risultato del dialogo");
        expect.That(await Expect.Within(dialogs.ShowDialogAsync(rename)) is null && closes == 2, "Annullamento del dialogo");
        expect.That(await Expect.Within(dialogs.ShowDialogAsync(new RenameDialog())) is null, "Dialogo chiuso senza risposta");
        expect.That(rename.Title == "Rinomina sezione" && dialogs.Shown.Count == 3, "Dialoghi non registrati");
        return expect.Count;
    }

    private sealed class RenameDialog() : DialogViewModel<string>("Rinomina sezione")
    {
        internal void Confirm(string name) => Close(name);
        internal void Dismiss() => Cancel();
    }
}
