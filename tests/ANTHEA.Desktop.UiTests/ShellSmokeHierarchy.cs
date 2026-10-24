using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using Anthea.Application.Documents;
using Anthea.Presentation.Projects;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Projects;
using X.Core;
using X.Desktop.Views.Shell;

namespace X.Desktop;

internal static partial class ShellSmoke
{
    /// <summary>
    /// ui/shell-smoke-hierarchy: ProjectHierarchySmokeChecks (MainWindow.SmokeHierarchy) on the new shell: modules dropped on a project and on sections with
    /// children, the sheets before the subsections in the tree, inheritance through several levels and empty groups, the precedence of the upper reference
    /// (concrete, steel, geometry, reinforcement) propagated without questions, conflicts and «Uniforma a questo» across the levels, the warnings of the cover,
    /// separate branches and projects, reorder and moves of sheets, saving and reopening, ambiguous references never chosen by order.
    /// </summary>
    private static async Task Hierarchy(ShellSmokeBench b)
    {
        string directory = b.Output;
        var shell = b.Shell; var workspace = b.Workspace;
        static JsonObject Sheet(string module, string name) => J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module)));
        DragEventArgs Drop(JsonObject destination, string format, object value)
        {
            var args = b.Drag(destination, format, value, DragDrop.DropEvent, .5);
            b.Check(args.Effects != DragDropEffects.None, "Drop accettato su " + destination.S("nome"));
            return args;
        }
        var document = ProjectDocuments.CreateArchive();
        // Deliberately an old project with no «fogli» array at its root.
        var project = J.Obj(("nome", "Ponte"), ("strutture", new JsonArray())); document.Array("progetti").Add(project);
        var upper = ProjectDocuments.AddSection(project, "Pali"); var child = ProjectDocuments.AddSection(upper, "Spalla A"); var deep = ProjectDocuments.AddSection(child, "Fondazioni");
        var other = ProjectDocuments.AddSection(project, "Travi");
        var unrelatedProject = J.Obj(("nome", "Altro progetto"), ("strutture", new JsonArray())); document.Array("progetti").Add(unrelatedProject);
        var unrelated = ProjectDocuments.AddSection(unrelatedProject, "Sezione indipendente"); var untouched = Sheet("str_palo", "Verifica indipendente"); unrelated.Array("fogli").Add(untouched);
        var untouchedBefore = untouched.DeepClone();
        workspace.OpenDocument(document, null, false);
        await b.Idle();
        var session = b.Project;
        Drop(project, ProjectTreeInput.ModuleFormat, "mat_calcestruzzo"); await b.Idle();
        var cls = project.Array("fogli")[0]!.AsObject(); cls["nome"] = "CLS di progetto";
        b.Check(ReferenceEquals(b.Node(project).Children[0].Node, cls), "Scheda del progetto collocata prima dei gruppi");
        Drop(upper, ProjectTreeInput.ModuleFormat, RebarMaterial.Module); await b.Idle();
        var steel = upper.Array("fogli")[0]!.AsObject(); steel["nome"] = "Acciaio pali";
        shell.Tree.AddSheet("str_palo", upper, false); var reference = upper.Array("fogli")[1]!.AsObject(); reference["nome"] = "Sezione di riferimento";
        var input = reference["dati"]!["input"]!; input["shape"] = "Circolare"; input["diameter_mm"] = "1200"; input["cover_mm"] = "60";
        shell.Tree.AddSheet(PaloOrizzontale.Module, deep, false); var pile = deep.Array("fogli")[0]!.AsObject(); pile["nome"] = "Capacità orizzontale";
        shell.Tree.AddSheet("str_palo", deep, false); var ca = deep.Array("fogli")[1]!.AsObject(); ca["nome"] = "Verifica fondazione";
        shell.Tree.AddSheet("str_palo", other, false); var independent = other.Array("fogli")[0]!.AsObject(); independent["nome"] = "Verifica trave";
        b.Check(pile["dati"]!["generali"].D("diametro") == 1.2 && ca["dati"]!["input"].D("diameter_mm") == 1200, "Geometria ereditata attraverso un gruppo vuoto");
        b.Check(ca["dati"]!["input"].D("fck_mpa") == 30 && ca["dati"]!["workspace_ca"]!["sle_comuni"].S("esposizione") == "XC1", "Materiale del progetto ereditato");
        b.Check(independent["dati"]!["input"].S("shape") == "Rettangolare", "Il ramo parallelo non eredita la geometria dei pali");
        shell.Navigation.Select(ShellPage.Projects); shell.Tree.Refresh(); await b.Idle();
        b.Check(b.Node(upper).Children.Take(2).All(n => n.IsSheet), "Schede subito sotto la sezione");
        var fullSnapshot = session.Document.DeepClone();
        _ = ProjectSharedData.Differences(project); _ = ProjectSharedData.Limitations(project); _ = ProjectValidation.CoverChecks(project, null);
        b.Check(JsonNode.DeepEquals(fullSnapshot, session.Document), "Il confronto gerarchico non modifica il documento");
        b.Check(!ProjectValidation.CoverChecks(deep, null).Any(c => c.Text.Contains("aggiungere una scheda")), "Il copriferro considera il CLS superiore");

        // Editing the parent propagates through all the depths, empty groups included, without asking.
        workspace.OpenSheet(cls); await b.Idle();
        var material = (JsonObject)b.Sheet.ViewModel.PreviewData.DeepClone(); material["classe"] = "C40/50";
        b.Sheet.SetConcreteState(material);
        b.Services.Confirmations.SharedUpdate = () => throw new InvalidOperationException("Richiesta conferma sulla propagazione del riferimento superiore");
        workspace.CommitActive();
        foreach (var target in new[] { reference, ca, pile, independent })
            b.Check(ProjectSharedData.Fields(target)["CLS · fck [MPa]"].Value!.GetValue<double>() == 40 || ProjectSharedData.Text(ProjectSharedData.Fields(target)["CLS · fck [MPa]"].Value) == "40", "CLS propagato a " + target.S("nome"));
        b.Check(JsonNode.DeepEquals(untouchedBefore, untouched), "Nessun aggiornamento oltre il progetto");
        workspace.OpenSheet(steel); await b.Idle();
        b.Sheet.Steel.Type = "B450A"; workspace.CommitActive();
        b.Check(ca["dati"]!["input"].S("classe_acciaio") == "B450A" && pile["dati"]!["sezione"].S("classe_acciaio") == "B450A" && independent["dati"]!["input"].S("classe_acciaio") == "B450C", "Acciaio propagato solo al proprio ramo");
        workspace.OpenSheet(reference); await b.Idle();
        var data = b.Sheet.Data; data["input"]!["diameter_mm"] = "1400"; data["input"]!["cover_mm"] = "65"; data["input"]!["longitudinal_bar_count"] = "20";
        workspace.CommitActive();
        b.Check(pile["dati"]!["generali"].D("diametro") == 1.4 && ca["dati"]!["input"].D("cover_mm") == 65 && ca["dati"]!["input"].D("longitudinal_bar_count") == 20, "Geometria e armatura propagate");
        b.Check(independent["dati"]!["input"].D("cover_mm") == 70, "La propagazione non invade la sezione sorella");
        b.Services.Confirmations.SharedUpdate = null;
        // A lower edit cannot rewrite or broadcast over the reference of its ancestor.
        workspace.OpenSheet(ca); await b.Idle();
        data = b.Sheet.Data; data["input"]!["cover_mm"] = "42"; data["input"]!["fck_mpa"] = "25";
        workspace.CommitActive();
        b.Check(reference["dati"]!["input"].D("cover_mm") == 65 && pile["dati"]!["sezione"].D("cover_mm") == 65 && cls["dati"].S("classe") == "C40/50", "Il foglio inferiore non comanda sui riferimenti");
        b.Check(ProjectSharedData.Differences(deep).Any(d => ReferenceEquals(d.First, cls) && ReferenceEquals(d.Second, ca)), "Conflitto con l'antenato visibile nella sottosezione");
        await b.Idle();
        b.Check(shell.SharedStatus.IsVisible && shell.SharedStatus.Text.Contains("Dati diversi tra i fogli"), "Avviso gerarchico nella scheda");
        // The badge as AddCoherenceBadge builds it (SharedDataCoordinator.Coherence); the tree shows it when it is rebuilt (as MainWindow, a confirmation does not
        // rebuild the tree).
        b.Check(SharedDataCoordinator.Coherence(child)?.Title.Contains("Differenze") == true, "Il gruppo vuoto mostra i conflitti dei discendenti");
        shell.Tree.Refresh(); await b.Idle();
        b.Check(ShellSmokeBench.Descendants<System.Windows.Controls.Button>(b.Item(child)).Any(button => button.Name == "Badge" && System.Windows.Automation.AutomationProperties.GetName(button).Contains("Differenze")),
            "Il badge dell'albero mostra i conflitti dei discendenti");
        CoherenceViewModel? captured = null;
        b.Services.DialogAsync = async d =>
        {
            var coherence = captured = (CoherenceViewModel)d;
            await b.CaptureDialog(coherence, coherence.Title, "conflitti-gerarchici.png");
            ConflictSheetViewModel Align(JsonObject sheet, string key) => coherence.Conflicts.SelectMany(c => c.Sheets).Single(s => ReferenceEquals(s.Sheet, sheet) && s.Key == key);
            b.Check(!Align(ca, "cover_mm").CanAlign && !Align(ca, "cover_mm").AlignCommand.CanExecute(Align(ca, "cover_mm")), "La sottosezione non si sceglie contro il riferimento superiore");
            b.Check(Align(reference, "cover_mm").CanAlign, "Riferimento superiore utilizzabile");
            var first = Align(reference, "cover_mm"); first.AlignCommand.Execute(first);
            var second = Align(cls, "CLS · fck [MPa]"); second.AlignCommand.Execute(second);
            return coherence.Result;
        };
        try { await shell.CompareCommand.ExecuteAsync(deep); }
        finally { b.Services.DialogAsync = null; }
        b.Check(captured is not null && ca["dati"]!["input"].D("cover_mm") == 65 && ca["dati"]!["input"].D("fck_mpa") == 40, "Uniformazione gerarchica completa");
        // The intermediate reference disagrees and the leaf matches the root: the root is an enabled resolution in the dialog of the leaf.
        reference["dati"]!["input"]!["fck_mpa"] = "35";
        b.Services.Dialog = d =>
        {
            var coherence = (CoherenceViewModel)d;
            var root = coherence.Conflicts.SelectMany(c => c.Sheets).Single(s => ReferenceEquals(s.Sheet, cls) && s.Key == "CLS · fck [MPa]");
            b.Check(root.CanAlign, "Riferimento superiore coerente con il foglio locale presente nel conflitto intermedio");
            root.AlignCommand.Execute(root);
            return coherence.Result;
        };
        try { await shell.CompareCommand.ExecuteAsync(deep); }
        finally { b.Services.Dialog = null; }
        b.Check(reference["dati"]!["input"].D("fck_mpa") == 40, "Riferimento intermedio riallineato");
        // Reordering never changes authority; moving a sheet keeps its input and changes its scope.
        shell.Navigation.Select(ShellPage.Projects); await b.Idle();
        var beforeOrder = ProjectSharedData.Differences(project).Select(d => (d.Key, d.Left, d.Right)).OrderBy(x => x.Key).ToArray();
        b.Drag(steel, ProjectTreeInput.SheetFormat, reference, DragDrop.DropEvent, .1); await b.Idle();
        b.Check(ReferenceEquals(upper.Array("fogli")[0], reference), "Riordino del riferimento prima dell'acciaio");
        b.Check(beforeOrder.SequenceEqual(ProjectSharedData.Differences(project).Select(d => (d.Key, d.Left, d.Right)).OrderBy(x => x.Key)), "Il riordino non cambia i conflitti");
        Drop(other, ProjectTreeInput.SheetFormat, pile); await b.Idle();
        b.Check(!ProjectSharedData.AncestorReferences(pile, "cover_mm").Any() && pile["dati"]!["sezione"].S("classe_acciaio") == independent["dati"]!["input"].S("classe_acciaio"), "Lo spostamento cambia l'autorità, non i dati");
        Drop(upper, ProjectTreeInput.SheetFormat, pile); await b.Idle();
        b.Check(ReferenceEquals(pile.Parent, upper["fogli"]), "Scheda riportata nel gruppo superiore");
        shell.Navigation.Select(ShellPage.Projects);
        await b.Capture("progetti-gerarchici.png");
        string archive = Path.Combine(directory, "gerarchia.programma");
        b.Services.SaveFile = _ => archive;
        b.Check(shell.Commands.Save(true), "Salvataggio della gerarchia");
        b.Services.SaveFile = null;
        var expected = session.Document.DeepClone();
        b.Check(workspace.CloseProject(), "Archivio salvato chiuso senza domande");
        workspace.OpenFile(archive); await b.Idle();
        b.Check(ConcretePersistenceComparison.Equivalent(expected, b.Project.Document), "Salvataggio e riapertura non cambiano gerarchia o dati");
        var restoredRoot = b.Project.Document.Array("progetti")[0]!.AsObject();
        b.Check(ProjectSharedData.AncestorReferences(ProjectSharedData.SubtreeSheets(restoredRoot).Single(s => s.S("nome") == "Verifica fondazione"), "CLS · fck [MPa]").Single().S("nome") == "CLS di progetto", "Autorità ripristinata");

        // Several conflicting references at one level are never resolved by the order of the array.
        var ambiguity = J.Obj(("nome", "Ambiguità"), ("fogli", new JsonArray()), ("strutture", new JsonArray()));
        var m1 = Sheet("mat_calcestruzzo", "CLS 1"); var m2 = Sheet("mat_calcestruzzo", "CLS 2"); m2["dati"]!["classe"] = "C50/60";
        ambiguity.Array("fogli").Add(m1); ambiguity.Array("fogli").Add(m2);
        var branch = ProjectDocuments.AddSection(ambiguity, "Livello inferiore");
        foreach (bool reverse in new[] { false, true })
        {
            if (reverse) { ambiguity.Array("fogli").Remove(m1); ambiguity.Array("fogli").Add(m1); }
            var target = Sheet("str_palo", "Nuova verifica"); branch.Array("fogli").Add(target); ProjectSharedData.InheritHierarchy(target, branch);
            b.Check(target["dati"]!["input"].D("fck_mpa") == 35 && ProjectSharedData.Differences(branch).Any(d => d.Key == "CLS · fck [MPa]"), "Ambiguità non risolta scegliendo un riferimento a caso");
            branch.Array("fogli").Remove(target);
        }
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK (shell nuova): drop su progetto e sezioni con figli; schede prima dei sottogruppi; eredità a più livelli e gruppi vuoti; precedenza superiore; CLS, acciaio, geometria, armatura; propagazione automatica; conflitti e uniformazione gerarchici; avvisi copriferro; rami/progetti separati; riordino, spostamento, salvataggio e riapertura; riferimenti ambigui non scelti automaticamente.");
    }
}
