using ANTHEA.ModelWorkspace;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Rilievo R4 della prova dell'utente del 16/11/2026 (registro MOD-RIL-R4): the changes of a model have their name in the history of the project
/// («↶ Annulla: Libera i risultati di 3 combinazioni»), and «↶ Annulla Libera» of the viewer undoes the project only when its last change is
/// still the one named.
/// </summary>
internal static class ModelUndoLabelChecks
{
    private sealed class NoWindows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
    }

    internal static int Run() => SingleThreadContext.Run(_ => Task.FromResult(RunAll()));

    private static ModelSnapshot Copy(string name) => new() { Name = name, Nodes = [new ModelNode(1, 0, 0, 0)], Results = [new ModelResultSet("Caso " + name, false, [])] };

    private static ModelDocumentEntry Labelled(ModelSnapshot snapshot, string label) { var entry = ModelDocumentEntry.Prepare(snapshot); entry.ChangeLabel = label; return entry; }

    private static int RunAll()
    {
        var expect = new Expect();
        var bench = new ShellBench();
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(), new SheetFileExports());
        shell.Models.NewProjectCommand.Execute(null);
        var project = shell.Workspace.Project!;
        var section = project.AddSection(shell.Tree.AddProject()!)!;
        shell.Models.OpenModelCommand.Execute(shell.Models.Cards.Single(c => ReferenceEquals(c.Container, section)).NewModel);
        var model = (ModelTabViewModel)shell.Windows.Pages.Single().Content!;
        model.Imported(Copy("A"));
        expect.That(project.UndoLabel == "Importazione del modello", "R4: l'importazione ha il suo nome nello storico del progetto");
        model.Imported(Copy("A2"));
        expect.That(project.UndoLabel == "Modifica del modello «A»", "R4: una modifica del modello senza nome proprio porta il nome del modello, non «Modifica dei dati dei fogli»");
        string label = "Libera i risultati di 3 combinazioni";
        model.Imported(Labelled(Copy("B"), label));
        expect.That(project.UndoLabel == label && shell.Commands.UndoHint.Contains(label, StringComparison.Ordinal),
            "R4: «Libera» ha il suo nome nello storico e nel suggerimento di «↶ Annulla»");
        expect.That(!model.UndoChange("Altra modifica") && model.Snapshot?.Name == "B", "R4: un nome diverso dall'ultima modifica non annulla nulla");
        expect.That(model.UndoChange(label) && model.Snapshot?.Name == "A2", "R4: «↶ Annulla Libera» annulla la modifica «Libera» del progetto");
        return expect.Count;
    }
}
