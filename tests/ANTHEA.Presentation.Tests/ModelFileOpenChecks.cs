using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Tabs;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Rilievo R3 della prova dell'utente del 16/11/2026 (registro MOD-RIL-R3): a file of a calculation model chosen in «Apri…» says where it opens
/// and shows the page «Modelli», instead of «Formato o versione del file non supportati.»; the command line refuses it with the same text.
/// </summary>
internal static class ModelFileOpenChecks
{
    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    internal static int Run() => SingleThreadContext.Run(_ => Task.FromResult(RunAll()));

    private static int RunAll()
    {
        var expect = new Expect();
        foreach (string name in new[] { "modello.antheamodel", "ponte.mct", "telaio.MGT", "NODE.json", "pila.st7", "pila.lsa", "pila.nla" })
            expect.That(WorkspaceSession.IsModelFile(Path.Combine("C:\\x", name)), "R3: file di un modello riconosciuto: " + name);
        expect.That(!WorkspaceSession.IsModelFile("C:\\x\\Calcolo.programma") && !WorkspaceSession.IsModelFile("C:\\x\\sezione.anthea") && !WorkspaceSession.IsModelFile("C:\\x\\dati.json"),
            "R3: i documenti di ANTHEA non sono file di modelli");
        string path = Path.Combine(Path.GetTempPath(), "modello.antheamodel");
        var bench = new ShellBench(dialogs: new ScriptedFileDialogs().Then(path));
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(), new SheetFileExports());
        shell.Commands.OpenCommand.Execute(null);
        var shown = bench.Messages.Shown.LastOrDefault();
        expect.That(shown.Title == WorkspaceSession.ModelFileTitle && shown.Message.Contains("«modello.antheamodel»", StringComparison.Ordinal) &&
                    shown.Message.Contains("«Modelli»", StringComparison.Ordinal) && shown.Message.Contains("Importa modello…", StringComparison.Ordinal) &&
                    !shown.Message.Contains("non supportati", StringComparison.Ordinal) && shell.Navigation.Page == ShellPage.Models && bench.Messages.Failures.Count == 0,
            "R3: «Apri…» su un .antheamodel dice dove si apre e mostra la pagina «Modelli»");
        expect.That(WorkspaceSession.ModelFileMessage("C:\\x\\pila.st7").Contains("Straus7", StringComparison.Ordinal), "R3: un file Straus7 rimanda alla sorgente Straus7");
        expect.Throws<ArgumentException>(() => shell.Workspace.OpenFile(path), "R3: aperto dalla riga di comando, un file di modello è rifiutato con lo stesso messaggio");
        return expect.Count;
    }
}
