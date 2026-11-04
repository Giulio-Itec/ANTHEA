using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Tabs;
using Anthea.Projects;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Step 3b of docs/sviluppo/progetto-sistema.md (4/11/2026): every operation on the project is a named step of the history. «Annulla» and
/// «Ripristina» say which change they take back or make again; a sheet confirmed before an operation is its own step, so that «Annulla»
/// takes back one operation at a time; a rename shows the new name in the same node of the tree, which is not rebuilt.
/// </summary>
internal static class TransactionChecks
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

        // The history alone: the name goes with the step, to the redo side and back.
        var history = new ProjectHistory();
        var document = ProjectDocuments.CreateArchive();
        history.Reset(document);
        ProjectDocuments.AddProject(document, "Ponte");
        history.Record(document, false, "Nuovo progetto «Ponte»");
        expect.That(history.UndoLabel == "Nuovo progetto «Ponte»" && history.RedoLabel is null, "Storico: nome del passo da annullare");
        var restored = history.Restore(false, false, () => { }, () => document)!;
        expect.That(history.UndoLabel is null && history.RedoLabel == "Nuovo progetto «Ponte»" && restored.Array("progetti").Count == 0, "Storico: nome del passo da ripristinare");
        history.Restore(true, false, () => { }, () => restored);
        expect.That(history.UndoLabel == "Nuovo progetto «Ponte»" && history.RedoLabel is null, "Storico: nome tornato dopo «Ripristina»");

        // The shell: a sheet confirmed, then a rename, taken back one at a time with their names.
        var bench = new ShellBench();
        using var shell = new ShellViewModel(bench.Services, bench.Factory, new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")), new NoWindows(),
            new SheetFileExports());
        var workspace = shell.Workspace;
        var project = workspace.NewProject()!;
        var root = project.AddProject()!;
        var section = project.AddSection(root)!;
        var material = project.AddSheet("mat_calcestruzzo", section)!;
        var other = project.AddSheet("str_palo", section)!;
        string sectionName = section.S("nome"), sheetName = ShellTexts.SheetName(material);
        expect.That(shell.Commands.UndoHint == "Annulla: Nuovo foglio «" + ShellTexts.SheetName(other) + "» · Ctrl+Z" && shell.Commands.RedoHint == ShellCommandsViewModel.RedoToolTip,
            "Annulla con il nome dell'ultima operazione: " + shell.Commands.UndoHint);
        var tab = workspace.OpenSheet(material);
        ShellBench.Probe(tab).Label = "modificato";
        workspace.CommitActive();
        expect.That(shell.Commands.UndoHint == "Annulla: " + ProjectSession.EditLabel(sheetName) + " · Ctrl+Z", "Annulla della conferma di un foglio: " + shell.Commands.UndoHint);
        shell.Tree.Refresh(section);
        var node = shell.Tree.Find(section)!;
        project.Rename(section, "Pila P1");
        expect.That(ReferenceEquals(shell.Tree.Find(section), node) && node.Name == "Pila P1" && ReferenceEquals(shell.Tree.Selected, node)
            && shell.Commands.UndoHint == "Annulla: Rinomina «" + sectionName + "» in «Pila P1» · Ctrl+Z", "Rinomina: lo stesso nodo dell'albero, nome e Annulla: " + shell.Commands.UndoHint);
        project.Restore(false);
        var current = project.Find(section.S("id"))!;
        var edited = project.Find(material.S("id"))!;
        expect.That(current.S("nome") == sectionName && edited["dati"]?["etichetta"]?.ToString() == "modificato"
            && shell.Commands.RedoHint == "Ripristina: Rinomina «" + sectionName + "» in «Pila P1» · Ctrl+Y"
            && shell.Commands.UndoHint == "Annulla: " + ProjectSession.EditLabel(sheetName) + " · Ctrl+Z", "Primo annulla: solo la rinomina, la modifica del foglio resta");
        project.Restore(false);
        expect.That(project.Find(material.S("id"))?["dati"]?["etichetta"]?.ToString() != "modificato"
            && shell.Commands.RedoHint == "Ripristina: " + ProjectSession.EditLabel(sheetName) + " · Ctrl+Y", "Secondo annulla: la modifica del foglio");
        return expect.Count;
    }
}
