using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Projects;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 b2: the tree of the project and the catalogue of the sheets without WPF: nodes, names, depths and order of MainWindow.RefreshTree,
/// expansion remembered, selection, summary, commands of the nodes and of the header, rename in place, read-only, drag and drop with the
/// hints of ProjectHierarchy and ProjectSectionReorder, the «•» of the unsaved sheets; the search of the catalogue.
/// </summary>
internal static class ProjectTreeChecks
{
    internal static int Run() => SingleThreadContext.Run(_ => Task.FromResult(RunAll()));

    private static int RunAll()
    {
        var expect = new Expect();
        Tree(expect);
        Drops(expect);
        Catalogue(expect);
        return expect.Count;
    }

    private static void Tree(Expect expect)
    {
        var bench = new ShellBench(new ScriptedConfirmations().Then(true));
        var workspace = bench.Workspace;
        using var tree = new ProjectTreeViewModel(workspace, bench.Messages);
        var overviews = new List<JsonObject?>();
        tree.OverviewRequested += (_, section) => overviews.Add(section);
        expect.That(tree.Project is null && tree.IsEmpty && tree.Roots.Count == 0 && tree.Summary == "" && !tree.AddProjectCommand.CanExecute(null), "Albero senza progetto");

        // + Progetto opens an archive when none is open; the new project is selected and shown.
        var root = tree.AddProject()!;
        expect.That(tree.Project is not null && tree.Roots.Single().Name == "Progetto 1" && tree.Roots[0].Kind == ProjectNodeKind.Project && tree.Roots[0].Depth == 0
            && ReferenceEquals(tree.Selected?.Node, root) && ReferenceEquals(overviews.Single(), root) && !tree.IsEmpty
            && tree.Summary == "1 progetto · 0 sezioni · 0 fogli", "Nuovo progetto");

        // + Sezione in the selection; then a sheet in the selected section, opened in a tab and selected.
        tree.AddSectionCommand.Execute(null);
        var pier = (JsonObject)root.Array("strutture")[0]!;
        expect.That(ReferenceEquals(tree.Selected?.Node, pier) && tree.Selected!.Depth == 1 && tree.Selected.Name == "Sezione 1" && ReferenceEquals(overviews[^1], pier)
            && tree.Summary == "1 progetto · 1 sezione · 0 fogli", "Nuova sezione");
        var sheet = tree.AddSheet("mat_calcestruzzo")!;
        var node = tree.Find(sheet)!;
        expect.That(node.Kind == ProjectNodeKind.Sheet && ReferenceEquals(workspace.Active?.Sheet, sheet) && node.IsSelected && ReferenceEquals(tree.Selected, node)
            && tree.Summary == "1 progetto · 1 sezione · 1 foglio" && ReferenceEquals(tree.SelectedContainer, pier), "Nuovo foglio aperto");
        expect.That(node.Name == sheet.S("nome") && node.OpenToolTip == "Apri scheda · " + sheet.S("nome") && node.OpenAutomationName == "Apri scheda " + sheet.S("nome")
            && node.ToolTip == "Doppio clic per rinominare · F2" && node.Module == "mat_calcestruzzo" && node.CanRename && node.CanDelete && !node.CanDuplicate
            && !node.CanAddSection && !node.CanReport, "Nodo del foglio");
        var pierNode = tree.Find(pier)!;
        expect.That(pierNode.CanReport && pierNode.ReportToolTip == "Report Word della sezione, comprese tutte le sottosezioni" && pierNode.ReportAutomationName == "Genera report Sezione 1"
            && pierNode.CanDuplicate && pierNode.CanAddSection && pierNode.ToolTip == "Trascina sul bordo per riordinare, al centro per inserire in una sezione · Doppio clic per rinominare · F2"
            && tree.Roots[0].ToolTip.StartsWith("Trascina per riordinare i progetti", StringComparison.Ordinal), "Nodo della sezione");
        var empty = tree.Project!.AddSection(root)!;
        var emptyNode = tree.Find(empty)!;
        expect.That(!emptyNode.CanReport && emptyNode.ReportToolTip == "Aggiungi una scheda alla sezione o alle sue sottosezioni", "Report di una sezione vuota");

        // Children: sheets before sections (models first, when there are any).
        var projectSheet = tree.Project.AddSheet("str_palo", root)!;
        expect.That(tree.Roots[0].Children.Select(c => c.Kind).SequenceEqual([ProjectNodeKind.Sheet, ProjectNodeKind.Section, ProjectNodeKind.Section])
            && ReferenceEquals(tree.Roots[0].Children[0].Node, projectSheet), "Ordine dei figli");

        // Expansion remembered by identifier; the ancestors of the selection expand.
        tree.Find(pier)!.IsExpanded = false;
        tree.Refresh(empty);
        expect.That(!tree.Find(pier)!.IsExpanded && ReferenceEquals(tree.Selected?.Node, empty), "Espansione ricordata");
        tree.Refresh(sheet);
        expect.That(tree.Find(pier)!.IsExpanded && ReferenceEquals(tree.Selected?.Node, sheet), "Antenati della selezione espansi");

        // Rename in place: trimmed, saved by Enter, cancelled by Esc; another rename saves the open one.
        pierNode = tree.Find(pier)!;
        pierNode.BeginRenameCommand.Execute(null);
        expect.That(pierNode.IsRenaming && pierNode.RenameText == "Sezione 1", "Inizio della rinomina");
        pierNode.RenameText = "  Pila P1  ";
        pierNode.EndRename(true);
        expect.That(pier.S("nome") == "Pila P1" && tree.Find(pier)!.Name == "Pila P1" && !pierNode.IsRenaming, "Rinomina salvata");
        pierNode = tree.Find(pier)!;
        pierNode.BeginRenameCommand.Execute(null); pierNode.RenameText = "Annullata"; pierNode.EndRename(false);
        expect.That(pier.S("nome") == "Pila P1", "Rinomina annullata");
        pierNode.BeginRenameCommand.Execute(null); pierNode.RenameText = "Pila P2";
        tree.Find(empty)!.BeginRenameCommand.Execute(null);
        expect.That(pier.S("nome") == "Pila P2", "Rinomina aperta non salvata da un'altra rinomina");
        tree.FinishRename();

        // The «•» of a sheet with confirmed changes not saved (SH15).
        var tab = workspace.SheetOf(sheet)!;
        ShellBench.Probe(tab).Label = "modificato";
        tree.Find(projectSheet)!.OpenCommand.Execute(null);
        expect.That(ReferenceEquals(workspace.Active?.Sheet, projectSheet) && tree.Find(sheet)!.IsUnsaved, "Foglio non salvato nell'albero");

        // A section opens its overview and takes the focus from the sheet; duplicate and delete show the overview of today.
        overviews.Clear();
        tree.Find(empty)!.OpenCommand.Execute(null);
        expect.That(workspace.Active is null && ReferenceEquals(overviews.Single(), empty) && ReferenceEquals(tree.Selected?.Node, empty), "Riepilogo della sezione");
        tree.Find(pier)!.DuplicateCommand.Execute(null);
        var copy = (JsonObject)root.Array("strutture")[1]!;
        expect.That(copy.S("nome") == "Pila P2 copia 1" && ReferenceEquals(overviews[^1], copy), "Duplica dall'albero");
        tree.Find(copy)!.DeleteCommand.Execute(null);
        expect.That(!root.Array("strutture").Contains(copy) && ReferenceEquals(overviews[^1], root), "Elimina dall'albero");

        // Without a section the note of today.
        tree.Selected = null;
        expect.That(tree.AddSheet("str_palo") is null && bench.Messages.Shown.Single() == ("", ProjectTreeViewModel.SelectSection, false), "Foglio senza sezione");

        // Read-only during the preview of a revision: no commands of change.
        tree.Project.NewRevision(pier, "Prima");
        tree.Project.SwitchRevision(pier.S("id"), 0);
        var archived = tree.Roots[0];
        expect.That(tree.IsReadOnly && !tree.AddProjectCommand.CanExecute(null) && !tree.AddSectionCommand.CanExecute(null) && !archived.CanRename
            && !archived.CanDelete && !archived.CanAddSection && archived.ToolTip == "Apri il riepilogo", "Albero in sola lettura");
        var archivedPier = tree.Find(tree.Project.Find(pier.S("id"))!)!;
        archivedPier.BeginRenameCommand.Execute(null);
        expect.That(!archivedPier.IsRenaming, "Rinomina in sola lettura");
        tree.Project.ExitPreview();
        expect.That(!tree.IsReadOnly && tree.Roots[0].CanRename, "Albero dopo la consultazione");
    }

    private static void Drops(Expect expect)
    {
        // A possible question of the nesting (inherited data) is answered; the drops before it ask nothing.
        var bench = new ShellBench(new ScriptedConfirmations().Then(true));
        var workspace = bench.Workspace;
        using var tree = new ProjectTreeViewModel(workspace, bench.Messages);
        var overviews = new List<JsonObject?>();
        tree.OverviewRequested += (_, section) => overviews.Add(section);
        var root = tree.AddProject()!;
        var project = tree.Project!;
        var left = project.AddSection(root)!;
        var right = project.AddSection(root)!;
        var a = project.AddSheet("str_palo", left)!;
        var b = project.AddSheet("str_palo", left)!;
        var leftNode = tree.Find(left)!;
        var aNode = tree.Find(a)!;

        var module = tree.PreviewDrop(leftNode, "mat_calcestruzzo", .5);
        expect.That(module.Place == ProjectDropPlace.Inside && module.Copies && module.Hint == "Inserisci in: " + left.S("nome"), "Modulo sopra una sezione");
        expect.That(tree.PreviewDrop(aNode, "mat_calcestruzzo", .5) == ProjectDrop.None && tree.PreviewDrop(leftNode, "sconosciuto", .5) == ProjectDrop.None, "Modulo sopra un foglio");
        var inside = tree.PreviewDrop(tree.Find(right)!, b, .5);
        expect.That(inside.Place == ProjectDropPlace.Inside && !inside.Copies && inside.Hint == "Inserisci in: " + right.S("nome")
            && tree.PreviewDrop(leftNode, b, .5) == ProjectDrop.None, "Foglio sopra una sezione");
        var before = tree.PreviewDrop(aNode, b, .2);
        var after = tree.PreviewDrop(aNode, b, .8);
        expect.That(before.Place == ProjectDropPlace.Before && before.Hint == "Prima di " + a.S("nome") + " · " + left.S("nome")
            && after.Place == ProjectDropPlace.After && after.Hint == "Dopo " + a.S("nome") + " · " + left.S("nome"), "Foglio sopra un foglio");
        var nest = tree.PreviewDrop(tree.Find(right)!, left, .5);
        var edge = tree.PreviewDrop(tree.Find(right)!, left, .1);
        expect.That(nest.Place == ProjectDropPlace.Inside && nest.Hint == "Sposta dentro: " + right.S("nome")
            && edge.Place == ProjectDropPlace.Before && edge.Hint == "Prima di " + right.S("nome"), "Sezione sopra una sezione");
        expect.That(tree.PreviewDrop(tree.Roots[0], left, .5) == ProjectDrop.None && tree.PreviewDrop(aNode, left, .5) == ProjectDrop.None, "Sezione sopra il suo progetto o un foglio");

        // Drops: a module adds a sheet without opening it and shows the section; sheets and sections move.
        expect.That(tree.Drop(leftNode, "mat_calcestruzzo", .5) && left.Array("fogli").Count == 3 && workspace.Active is null && ReferenceEquals(overviews[^1], left), "Modulo trascinato");
        expect.That(tree.Drop(tree.Find(a)!, b, .2) && ReferenceEquals(left.Array("fogli")[0], b), "Foglio riordinato");
        expect.That(tree.Drop(tree.Find(right)!, a, .5) && right.Array("fogli").Contains(a) && ReferenceEquals(tree.Selected?.Node, a), "Foglio spostato");
        expect.That(tree.Drop(tree.Find(right)!, left, .9) && ReferenceEquals(root.Array("strutture")[0], right), "Sezione riordinata");
        expect.That(tree.Drop(tree.Find(right)!, left, .5) && right.Array("strutture").Contains(left), "Sezione annidata");
        expect.That(!tree.Drop(tree.Find(a)!, left, .5) && !tree.Drop(tree.Find(left)!, left, .5), "Trascinamento senza effetto");
    }

    private static void Catalogue(Expect expect)
    {
        var catalogue = new ProjectCatalogViewModel();
        expect.That(catalogue.Groups.Select(g => g.Title).SequenceEqual(Archivio.Moduli.Select(m => Anthea.Application.Modules.ModuleDirectory.Get(m).Area.ToUpperInvariant()).Distinct())
            && catalogue.Groups.Sum(g => g.Cards.Count) == Archivio.Moduli.Length && catalogue.ShowsPlaceholder && !catalogue.HasNoResults, "Catalogo delle schede");
        var concrete = catalogue.Groups.SelectMany(g => g.Cards).Single(c => c.Module == "str_palo");
        var info = Anthea.Application.Modules.ModuleDirectory.Get("str_palo");
        expect.That(concrete.Title == info.ProjectTitle && concrete.Subtitle == info.ProjectSubtitle && concrete.ToolTip == "Trascina nella sezione oppure fai doppio clic per aggiungere.", "Scheda del catalogo");
        catalogue.Groups[0].IsExpanded = false;
        catalogue.Filter = "  CAPACITA  ";
        var visible = catalogue.Groups.SelectMany(g => g.Cards).Where(c => c.IsVisible).ToArray();
        expect.That(visible.Length > 0 && visible.All(c => c.Subtitle.Contains("Capacità", StringComparison.Ordinal) || c.Title.Contains("Capacità", StringComparison.Ordinal))
            && !catalogue.ShowsPlaceholder && catalogue.Groups.Where(g => g.IsVisible).All(g => g.IsExpanded) && catalogue.Groups.Any(g => !g.IsVisible), "Ricerca senza accenti e maiuscole");
        catalogue.Filter = "nessuna scheda così";
        expect.That(catalogue.HasNoResults && catalogue.Groups.All(g => !g.IsVisible), "Ricerca senza risultati");
        catalogue.Filter = "";
        expect.That(!catalogue.HasNoResults && catalogue.Groups.All(g => g.IsVisible) && catalogue.ShowsPlaceholder, "Ricerca vuota");
    }
}
