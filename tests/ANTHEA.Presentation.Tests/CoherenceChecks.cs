using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Projects;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Projects;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 b4: consistency of the common data without WPF: the status line above a sheet (MainWindow.RefreshSharedStatus), the comparison
/// of a section with «Uniforma a questo», the read-only state, the link of the stratigraphies (MainWindow.ShowCoherence) and the overview
/// of a section (BuildSectionOverview), with the texts of today; the changes reach the open sheets through the coordinator.
/// </summary>
internal static class CoherenceChecks
{
    internal static int Run() => SingleThreadContext.Run(RunAsync);

    private static async Task<int> RunAsync(SingleThreadContext ui)
    {
        var expect = new Expect();
        var dialogs = new ScriptedDialogs()
            .Then(d => ((SoilLinkDialogViewModel)d).CancelCommand.Execute(null))
            .Then(d => ((SoilLinkDialogViewModel)d).ConfirmCommand.Execute(null));
        var bench = new ShellBench(windows: dialogs);
        var workspace = bench.Workspace;
        var project = workspace.NewProject()!;
        var root = project.AddProject()!;
        var section = project.AddSection(root)!;
        project.Rename(section, "Pila P1");
        var material = project.AddSheet("mat_calcestruzzo", section)!;
        var second = project.AddSheet("mat_calcestruzzo", section)!;
        var concrete = project.AddSheet("str_palo", section)!;
        string fck = SharedDataCoordinator.FieldLabel("CLS · fck [MPa]");

        // Status line: the edits of the open sheet against the others, before the confirmation.
        var tab = workspace.OpenSheet(material);
        var status = new SharedStatusViewModel();
        status.Update(null);
        expect.That(!status.IsVisible, "Riga di stato senza foglio");
        tab.ViewModel.Data["classe"] = "C40/50";
        status.Update(tab);
        expect.That(status.IsVisible && status.IsWarning && status.Text.StartsWith("⚠ Dati diversi tra i fogli: " + fck, StringComparison.Ordinal)
            && status.Text.Contains(". Apri il confronto nella pagina del progetto per uniformare.", StringComparison.Ordinal) && status.ToolTip.StartsWith(fck, StringComparison.Ordinal)
            && material["dati"].S("classe") != "C40/50", "Riga di stato con differenze: " + status.Text);
        tab.ViewModel.Data["classe"] = material["dati"].S("classe");
        status.Update(tab);
        expect.That(!status.Text.Contains("⚠ Dati diversi", StringComparison.Ordinal) || !status.IsVisible, "Riga di stato dopo il ritorno al valore");
        var single = workspace.NewCalculation("mat_calcestruzzo");
        status.Update(single);
        expect.That(!status.IsVisible, "Riga di stato di un file singolo");

        // Comparison: one conflict on the class of the concrete, with every sheet involved in the order of the project.
        second["dati"]!["classe"] = "C40/50";
        workspace.Activate(tab);
        var compare = new CoherenceViewModel(project, section, bench.Services);
        expect.That(compare.Title == "Confronto · Pila P1" && compare.Theory.Length > 0 && !compare.HasNoConflicts && CoherenceViewModel.Heading == "Confronto tra fogli", "Testata del confronto");
        var conflict = compare.Conflicts.Single();
        expect.That(conflict.Title == "Conflitto 1: " + fck && conflict.Sheets.Select(s => s.Sheet).SequenceEqual([material, second, concrete]), "Conflitto: " + conflict.Title + " · " + string.Join(", ", conflict.Sheets.Select(s => s.Name)));
        var source = conflict.Sheets[1];
        expect.That(source.Name == second.S("nome") && source.Location == ProjectSharedData.Location(second) && source.Value == fck + ": 40" && source.CanAlign
            && source.AlignLabel == "Uniforma a questo" && source.AlignToolTip == "Uniforma " + fck + " nella sezione di questo foglio e nelle sue sottosezioni." && source.SoilTypes is null, "Foglio del conflitto: " + source.Value);
        var materialModel = tab.ViewModel;
        int changed = 0;
        compare.Changed += (_, _) => changed++;
        source.AlignCommand.Execute(source);
        expect.That(compare.HasNoConflicts && compare.Conflicts.Count == 0 && changed == 1 && material["dati"].S("classe") == "C40/50" && project.IsDirty
            && J.Number(ProjectSharedData.Fields(concrete)["CLS · fck [MPa]"].Value) == 40, "Uniforma a questo");
        expect.That(!ReferenceEquals(tab.ViewModel, materialModel) && tab.ViewModel.Data.S("classe") == "C40/50", "Scheda aperta non riaperta dopo «Uniforma a questo»");
        expect.That(compare.Notices.All(n => n.Text.StartsWith("• ", StringComparison.Ordinal)) && CoherenceViewModel.NoNotices == "Nessun avviso.", "Avvisi del confronto");

        // Read-only (an archived revision): nothing can be aligned.
        second["dati"]!["classe"] = "C25/30";
        project.NewRevision(section, "Prima");
        project.SwitchRevision(section.S("id"), 0);
        var archived = new CoherenceViewModel(project, project.Find(section.S("id"))!, bench.Services);
        expect.That(archived.Conflicts.Single().Sheets.All(s => !s.CanAlign && !s.AlignCommand.CanExecute(s)) && !archived.CanLink, "Confronto in sola lettura");
        project.ExitPreview();

        // Link of the stratigraphies: two sheets with soil; «Annulla» keeps them, the confirmation links them and closes the comparison.
        var pile = project.AddSheet("geo_palo_verticale", section)!;
        var other = project.AddSheet("geo_palo_verticale", section)!;
        var withSoil = new CoherenceViewModel(project, section, bench.Services);
        expect.That(withSoil.CanLink && withSoil.LinkSheets.Count == 2 && ReferenceEquals(withSoil.LinkSource?.Sheet, pile) && ReferenceEquals(withSoil.LinkTarget?.Sheet, other)
            && withSoil.LinkSource!.ToString() == pile.S("nome"), "Collegamento delle stratigrafie disponibile");
        var beforeLink = other["dati"]!.DeepClone();
        bool closed = false;
        withSoil.CloseRequested += (_, _) => closed = true;
        await Expect.Within(withSoil.LinkCommand.ExecuteAsync(null));
        var preview = (SoilLinkDialogViewModel)dialogs.Shown[0];
        expect.That(!closed && JsonNode.DeepEquals(beforeLink, other["dati"]) && preview.Title == "Collegamento dei sondaggi" && preview.Heading == pile.S("nome") + " → " + other.S("nome")
            && preview.Preview.Length > 0, "Collegamento annullato");
        var staged = SoilLink.Stage(pile, other).StagedTarget["dati"]!.DeepClone();
        await Expect.Within(withSoil.LinkCommand.ExecuteAsync(null));
        expect.That(closed, "Confronto non chiuso dopo il collegamento");
        expect.That(!JsonNode.DeepEquals(beforeLink, other["dati"]) && staged["stratigrafie"]?.AsArray().Count == other["dati"]!["stratigrafie"]?.AsArray().Count && other["dati"]!.ToJsonString().Contains("shared_layer_id", StringComparison.Ordinal), "Collegamento applicato");

        // Overview of the section: controls, common data in the order of today, revisions, links; the empty overview.
        var overview = new SectionOverviewViewModel(project, section);
        int conflicts = ProjectSharedData.Differences(section).Select(d => d.Key).Distinct().Count() + ProjectSharedData.MissingSoilLayers(section).Count;
        int notices = ProjectValidation.Warnings(section).Length;
        expect.That(overview.Name == "Pila P1" && overview.ConflictsText == $"{conflicts} {(conflicts == 1 ? "conflitto" : "conflitti")}" && overview.HasConflicts == conflicts > 0
            && overview.NoticesText == $"{notices} {(notices == 1 ? "avviso" : "avvisi")}" && overview.CompareLabel == "Confronto e avvisi", "Controlli del riepilogo");
        expect.That(overview.CommonData.Count > 0 && overview.CommonData[0].Key == "CLS · fck [MPa]" && overview.CommonData[0].Label == fck, "Dati comuni: " + string.Join(", ", overview.CommonData.Select(r => r.Key)));
        expect.That(overview.RevisionTitle == "Revisione 01" && overview.HasRevisionSummary && overview.RevisionNote == "Prima" && overview.CanReport && overview.CanDuplicate
            && overview.Links.SequenceEqual(section.Array("fogli").OfType<JsonObject>()) && overview.ModelsText.StartsWith("0 modelli", StringComparison.Ordinal) && overview.CanImportModel, "Riepilogo della sezione");
        var empty = new SectionOverviewViewModel(project, null);
        expect.That(empty.Section is null && empty.EmptyCards.Count == 2 && SectionOverviewViewModel.EmptyTitle == "Tutto sotto controllo", "Riepilogo vuoto");
        var fresh = new SectionOverviewViewModel(project, project.AddSection(root)!);
        expect.That(fresh.RevisionTitle == "Versione corrente" && !fresh.HasRevisionSummary && !fresh.CanReport && fresh.ConflictsText == "0 conflitti"
            && fresh.NoticesText == "0 avvisi" && fresh.CommonData.Count == 0, "Riepilogo di una sezione nuova");
        return expect.Count;
    }
}
