using Anthea.Calculations;
using Anthea.Presentation.Shell.Projects;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 c, night of 22-23/10 (section 10.1, second item): what the view of the tree asks the ViewModel during a drag (ProjectHierarchy and
/// ProjectSectionReorder of MainWindow): which nodes can be dragged, the hint of the header during the drag («Inserisci in: …», «Prima di …», «Dopo …»,
/// «Sposta dentro: …»), the drop with the errors shown as messages. The rules of the places are those of PreviewDrop (ProjectTreeChecks).
/// </summary>
internal static class ShellTreeDragChecks
{
    internal static int Run() => SingleThreadContext.Run(_ => Task.FromResult(RunAll()));

    private static int RunAll()
    {
        var expect = new Expect();
        var bench = new ShellBench();
        var workspace = bench.Workspace;
        using var tree = new ProjectTreeViewModel(workspace, bench.Messages);
        var project = workspace.NewProject()!;
        var root = project.AddProject()!;
        var left = project.AddSection(root)!;
        var right = project.AddSection(root)!;
        var a = project.AddSheet("mat_calcestruzzo", left)!;
        var b = project.AddSheet("mat_calcestruzzo", left)!;
        tree.Refresh();
        var leftNode = tree.Find(left)!;
        var aNode = tree.Find(a)!;

        // Which nodes can be dragged: projects, sections and sheets outside read-only.
        expect.That(tree.Roots[0].CanDrag && leftNode.CanDrag && aNode.CanDrag && aNode.IsSheet, "Progetti, sezioni e fogli trascinabili");

        // The hint follows the drag and is cleared when the drag leaves.
        expect.That(tree.DropHint == "", "Nessun suggerimento fuori da un trascinamento");
        var drop = tree.DragOver(tree.Find(right)!, b, .5);
        expect.That(drop.Place == ProjectDropPlace.Inside && tree.DropHint == "Inserisci in: " + right.S("nome"), "Foglio sopra un'altra sezione: " + tree.DropHint);
        drop = tree.DragOver(aNode, b, .2);
        expect.That(drop.Place == ProjectDropPlace.Before && tree.DropHint == "Prima di " + a.S("nome") + " · " + left.S("nome"), "Foglio sopra il bordo alto di un foglio: " + tree.DropHint);
        drop = tree.DragOver(tree.Find(right)!, left, .9);
        expect.That(drop.Place == ProjectDropPlace.After && tree.DropHint == "Dopo " + right.S("nome"), "Sezione sopra il bordo basso di un'altra: " + tree.DropHint);
        drop = tree.DragOver(tree.Roots[0], left, .5);
        expect.That(drop.Place == ProjectDropPlace.None && tree.DropHint == "", "Sezione sopra il suo progetto: nessun suggerimento");
        drop = tree.DragOver(leftNode, "mat_calcestruzzo", .5);
        expect.That(drop is { Place: ProjectDropPlace.Inside, Copies: true } && tree.DropHint == "Inserisci in: " + left.S("nome"), "Modulo del catalogo sopra una sezione: copia");
        tree.ClearDropHint();
        expect.That(tree.DropHint == "", "Trascinamento uscito: suggerimento cancellato");

        // The drop of the view: the hint goes, the project changes; nothing for a drop without effect.
        tree.DragOver(tree.Find(right)!, left, .9);
        expect.That(tree.CompleteDrop(tree.Find(right)!, left, .9) && tree.DropHint == "" && ReferenceEquals(root.Array("strutture")[0], right), "Rilascio: sezione riordinata");
        expect.That(!tree.CompleteDrop(tree.Find(a)!, a, .5) && bench.Messages.Failures.Count == 0, "Rilascio di un foglio su se stesso: nessun effetto");

        expect.That(!tree.CompleteDrop(tree.Find(b)!, "modulo sconosciuto", .5) && bench.Messages.Failures.Count == 0, "Modulo sconosciuto sopra un foglio: nessun effetto");

        // Read-only: nothing can be dragged.
        var section = project.Find(left.S("id"))!;
        project.NewRevision(section, "Prima");
        project.SwitchRevision(section.S("id"), 0);
        tree.Refresh();
        expect.That(tree.Roots.All(r => !r.CanDrag) && tree.Find(project.Find(left.S("id"))!)!.Children.All(c => !c.CanDrag), "Revisione in sola lettura: nessun nodo trascinabile");
        project.ExitPreview();
        return expect.Count;
    }
}
