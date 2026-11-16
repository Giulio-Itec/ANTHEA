using ANTHEA.ModelWorkspace;
using Anthea.Calculations;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Rilievi R8 e R5 della prova dell'utente del 16/11/2026 (registro MOD-RIL-R8), in the shell without WPF: the copy of a model read on another
/// thread at the opening and after «↶ Annulla», the same copy kept by an undo of another change, the last reading asked winning over an
/// abandoned one. The thread of the interface of the checks (<see cref="SingleThreadContext"/>).
/// </summary>
internal static class ModelTabLoadingChecks
{
    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    internal static int Run(string output) => SingleThreadContext.Run(_ => RunAll(output));

    private static ModelSnapshot Copy(string name) => new() { Name = name, Nodes = [new ModelNode(1, 0, 0, 0)], Results = [new ModelResultSet("Caso " + name, false, [])] };

    private static async Task<int> RunAll(string output)
    {
        var expect = new Expect();
        string folder = Path.Combine(output, "modelli-rilievi"); Directory.CreateDirectory(folder);
        await Loading(expect, folder);
        return expect.Count;
    }

    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Expect.Deadline;
        while (!condition()) { if (DateTime.UtcNow > deadline) throw new InvalidOperationException("Lettura del modello non conclusa entro il limite."); await Task.Delay(10); }
    }

    /// <summary>R8 and R5: with the threshold at zero every copy is read in the background, as the large ones.</summary>
    private static async Task Loading(Expect expect, string folder)
    {
        string path = Path.Combine(folder, "Progetto con modello.programma");
        var bench = new ShellBench(dialogs: new ScriptedFileDialogs().Then(path));
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(), new SheetFileExports());
        shell.Models.NewProjectCommand.Execute(null);
        var project = shell.Workspace.Project!;
        var section = project.AddSection(shell.Tree.AddProject()!)!;
        shell.Models.OpenModelCommand.Execute(shell.Models.Cards.Single(c => ReferenceEquals(c.Container, section)).NewModel);
        var model = (ModelTabViewModel)shell.Windows.Pages.Single().Content!;
        // The checks of the archive use a limit of one byte: the copies go to the archive as the large ones do.
        model.Imported(ModelDocumentEntry.Prepare(Copy("A"), 1));
        string id = model.ModelId!;
        expect.That(shell.Workspace.Save(project, true), "Salvataggio del progetto con il modello nell'archivio");
        shell.Windows.Close(shell.Windows.Pages.Single());

        project.ModelReadThreshold = 0;
        var opened = shell.ShowModel(section, id)!;
        expect.That(opened.IsLoading && opened.Snapshot is null && opened.LoadingText.StartsWith("Lettura del modello", StringComparison.Ordinal),
            "R8: la scheda si apre subito e legge la copia in un altro thread, con il testo dell'avanzamento");
        bool raised = false; opened.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(ModelTabViewModel.Snapshot);
        await Until(() => !opened.IsLoading);
        expect.That(raised && opened.Snapshot?.Name == "A" && opened.LoadFailure is null && opened.LoadingPercent == 100, "R8: al termine la copia letta diventa il modello della scheda");

        // An undo of another change of the project keeps the copy on screen: no new reading, no new viewer.
        var shown = opened.Snapshot;
        project.Rename(section, "Sezione rinominata");
        raised = false;
        expect.That(project.Restore(false) && !opened.IsLoading && ReferenceEquals(opened.Snapshot, shown) && !raised,
            "R5: «↶ Annulla» di un'altra modifica conserva la copia sullo schermo, senza rileggerla");

        // An undo of a change of the model reads the restored copy in the background.
        opened.Imported(ModelDocumentEntry.Prepare(Copy("B"), 1));
        expect.That(opened.Snapshot?.Name == "B", "Una nuova copia del modello");
        raised = false;
        expect.That(project.Restore(false) && opened.IsLoading && opened.Snapshot is null && raised, "R5: «↶ Annulla» della modifica del modello rilegge la copia in un altro thread");
        await Until(() => !opened.IsLoading);
        expect.That(opened.Snapshot?.Name == "A", "R5: la copia ripristinata arriva al termine della lettura");

        // A reading abandoned for a newer one never replaces it; closing the tab stops the reading.
        opened.Imported(ModelDocumentEntry.Prepare(Copy("C"), 1));
        expect.That(project.Restore(false) && opened.IsLoading && project.Restore(true), "Annulla e Ripristina di seguito, durante la lettura");
        await Until(() => !opened.IsLoading);
        expect.That(opened.Snapshot?.Name == "C" && bench.Messages.Failures.Count == 0, "R5: vale l'ultima lettura chiesta, quella del Ripristina");
        shell.Windows.Close(shell.Windows.Pages.Single());
        expect.That(opened.IsDisposed, "La scheda chiusa è liberata");
    }
}
