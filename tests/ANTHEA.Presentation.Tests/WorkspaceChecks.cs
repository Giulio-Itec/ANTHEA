using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 a3: sheets and coordinator of the open documents without WPF (proposal 4.1, 4.4, 3.6, 3.7). The sheets are the probe module
/// (tests/Shared/ProbeSheetModule.cs) registered for real modules of the directory, opened through <see cref="RegistrySheetFactory"/>.
/// Scenarios: single files, the same file twice, one project at a time, the focus that confirms the active sheet, the shared data that
/// reach the other open sheets, undo and revisions with several tabs, the copy in another module, the soil, save and close.
/// </summary>
internal static class WorkspaceChecks
{
    internal static int Run(string output) => SingleThreadContext.Run(_ => Task.FromResult(RunAll(output)));

    private static int RunAll(string output)
    {
        var expect = new Expect();
        string folder = Path.Combine(output, "shell-workspace");
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        Factory(expect);
        SingleFiles(expect, folder);
        Projects(expect, folder);
        SharedData(expect);
        UndoAndRevisions(expect);
        Requests(expect);
        Closing(expect);
        return expect.Count;
    }

    private static ProbeSheetViewModel Probe(SheetSession sheet) => ShellBench.Probe(sheet);

    private static void Factory(Expect expect)
    {
        var bench = new ShellBench();
        var data = DocumentService.CreateSheet("str_palo");
        var sheet = bench.Factory.Open("str_palo", data, true, new ShellBench.NullHost());
        expect.That(sheet.ModuleId == "str_palo" && sheet.IsReadOnly && JsonNode.DeepEquals(sheet.Data, data) && !ReferenceEquals(sheet.Data, data), "Foglio della fabbrica");
        sheet.Dispose();
        expect.Throws<InvalidOperationException>(() => bench.Factory.Open("geo_muri_sostegno", new JsonObject(), false, new ShellBench.NullHost()), "Modulo senza implementazione MVVM aperto");
        expect.Throws<ArgumentNullException>(() => bench.Factory.Open("str_palo", data, false, null!), "Foglio senza ospite");
    }

    private static void SingleFiles(Expect expect, string folder)
    {
        string examples = Path.Combine(folder, "Esempi");
        Directory.CreateDirectory(examples);
        string saved = Path.Combine(folder, "Sezione.programma");
        var bench = new ShellBench(dialogs: new ScriptedFileDialogs().Then(saved).Then(null).Then(Path.Combine(examples, "Copia.programma")), examples: examples);
        var workspace = bench.Workspace;
        int activations = 0;
        workspace.ActiveChanged += (_, _) => activations++;

        // «Nuovo …»: a new unsaved file in a new active tab; the second does not replace the first (SH12).
        var first = workspace.NewCalculation("str_palo");
        var second = workspace.NewCalculation("mat_calcestruzzo");
        expect.That(workspace.Calculations.Count == 2 && workspace.Sheets.Count == 2 && ReferenceEquals(workspace.Active, second) && activations == 2
            && first.Owner is CalculationDocumentSession { Module: "str_palo" } && ReferenceEquals(first.Sheet, first.Owner.Document) && first.Id == ""
            && first.Heading == ShellTexts.ModuleName("str_palo") && ReferenceEquals(workspace.CurrentDocument, second.Owner), "Due file singoli");

        // The focus confirms the active sheet (Q9 a): its draft reaches the document when another tab is activated.
        Probe(second).Note = "bozza";
        expect.That(second.Owner.Document["dati"]!["nota"] is null, "Bozza scritta prima della conferma");
        workspace.Activate(first);
        expect.That(second.Owner.Document["dati"].S("nota") == "bozza" && Probe(second).Commits == 1 && ReferenceEquals(workspace.Active, first) && second.IsUnsaved, "Conferma alla perdita del fuoco");
        expect.That(second.Owner.IsDirty, "Bozza confermata senza segnare il documento come modificato");
        Probe(first).Label = "A";
        expect.That(first.Owner.IsDirty && first.Owner.Title == "ANTHEA — Nuovo documento *" && first.Owner.Document["dati"]!["etichetta"] is null, "Modifica del foglio");

        // Save: a new file asks the file («Calcolo.programma»), the working document is written, the sheet is saved.
        expect.That(workspace.Save(first.Owner, false) && first.Owner.Path == saved && !first.Owner.IsDirty && !first.IsUnsaved
            && DocumentService.Open(saved)["dati"].S("etichetta") == "A", "Salvataggio di un file nuovo");
        var request = (SaveFileRequest)bench.Dialogs.Requests.Single();
        expect.That(request.FileName == "Calcolo.programma" && FileFilter.Join(request.Filters) == "File ANTHEA|*.programma", "Dialogo del salvataggio");
        expect.That(!workspace.Save(first.Owner, true) && ((SaveFileRequest)bench.Dialogs.Requests[1]).FileName == "Sezione.programma", "Salva con nome annullato");
        Probe(first).Label = "B";
        expect.That(!workspace.Save(first.Owner, true) && first.Owner.IsDirty && first.Owner.Path == saved, "Salvataggio negli esempi accettato");
        var failure = bench.Messages.Failures.Single();
        expect.That(failure.Title == WorkspaceSession.SaveFailure && failure.Error.Message == WorkspaceSession.ExamplesFolderRefused, "Errore del salvataggio negli esempi");
        expect.That(workspace.Save(first.Owner, false) && !first.Owner.IsDirty && bench.Dialogs.Requests.Count == 3, "Salva su un file esistente senza dialogo");

        // The same file twice: activated, not opened again (SH11).
        workspace.Activate(second);
        var again = workspace.OpenFile(saved.ToUpperInvariant());
        expect.That(ReferenceEquals(again, first.Owner) && workspace.Calculations.Count == 2 && ReferenceEquals(workspace.Active, first), "Stesso file aperto due volte");

        // Another file: read, validated and opened in a new tab.
        string other = Path.Combine(folder, "Calcestruzzo.programma");
        DocumentService.Save(other, DocumentService.CreateDocument("mat_calcestruzzo"));
        var opened = workspace.OpenFile(other);
        expect.That(opened is CalculationDocumentSession { Module: "mat_calcestruzzo" } && opened.Path == other && !opened.IsDirty && workspace.Calculations.Count == 3
            && ReferenceEquals(workspace.Active!.Owner, opened), "Apertura di un file");

        // Closing the tab of a single file closes the file, after the question when it is not saved.
        Probe(workspace.Active!).Label = "C";
        bench.Confirmations.Then(SaveChangesAnswer.Cancel).Then(SaveChangesAnswer.Discard);
        var tab = workspace.Active!;
        expect.That(!workspace.CloseSheet(tab) && workspace.Sheets.Contains(tab) && workspace.Calculations.Contains(opened), "Chiusura annullata");
        expect.That(workspace.CloseSheet(tab) && !workspace.Sheets.Contains(tab) && !workspace.Calculations.Contains(opened) && tab.IsDisposed
            && bench.Closed.Single() == (tab, SheetCloseReason.DocumentClosed) && workspace.Active is null, "Chiusura di un file singolo");
        expect.That(bench.Confirmations.Asked.SequenceEqual(["salvataggio", "salvataggio"]), "Domande della chiusura");
        workspace.Dispose();
        expect.That(first.IsDisposed && second.IsDisposed, "Fogli rilasciati con lo spazio di lavoro");
        expect.Throws<ObjectDisposedException>(() => workspace.NewCalculation("str_palo"), "Spazio di lavoro usato dopo il rilascio");
    }

    private static void Projects(Expect expect, string folder)
    {
        var bench = new ShellBench(new ScriptedConfirmations().Then(true).Then(SaveChangesAnswer.Cancel).Then(SaveChangesAnswer.Discard));
        var workspace = bench.Workspace;
        var single = workspace.NewCalculation("str_palo");
        int documents = 0;
        workspace.DocumentsChanged += (_, _) => documents++;
        var project = workspace.NewProject()!;
        expect.That(ReferenceEquals(workspace.Project, project) && documents == 1 && ReferenceEquals(workspace.EnsureProject(), project)
            && workspace.Documents.SequenceEqual(new DocumentSession[] { project, single.Owner }), "Progetto aperto accanto ai file singoli");
        var root = project.AddProject()!;
        var section = project.AddSection(root)!;
        var cls = project.AddSheet("mat_calcestruzzo", section)!;
        var ca = project.AddSheet("str_palo", section)!;

        // A sheet in at most one tab; opening it again only activates it.
        var clsTab = workspace.OpenSheet(cls);
        var caTab = workspace.OpenSheet(ca);
        expect.That(ReferenceEquals(workspace.OpenSheet(cls), clsTab) && workspace.Sheets.Count(s => s.Owner == project) == 2 && ReferenceEquals(workspace.Active, clsTab)
            && clsTab.Id == cls.S("id") && ReferenceEquals(workspace.SheetOf(cls), clsTab), "Foglio in una sola scheda");
        expect.Throws<ArgumentException>(() => workspace.OpenSheet(section), "Sezione aperta come foglio");
        expect.Throws<ArgumentException>(() => workspace.OpenSheet((JsonObject)ca.DeepClone()), "Foglio fuori dal progetto aperto");

        // A draft of the active sheet is confirmed before an operation of the project (MainWindow: Commit before every command).
        Probe(clsTab).Note = "prima della sezione";
        project.AddSection(root);
        expect.That(cls["dati"].S("nota") == "prima della sezione", "Operazione del progetto senza conferma del foglio attivo");

        // Rename: the headings follow. Delete: the tabs of the removed sheets close.
        int refreshed = 0;
        caTab.StateChanged += (_, _) => refreshed++;
        project.Rename(ca, "Verifica pila");
        expect.That(caTab.Heading == "Verifica pila" && refreshed > 0, "Intestazione dopo la rinomina");
        project.Delete(ca, out _);
        expect.That(caTab.IsDisposed && !workspace.Sheets.Contains(caTab) && bench.Closed.Last() == (caTab, SheetCloseReason.Removed), "Scheda del foglio eliminato");

        // One project at a time: «Annulla» keeps it, «Non salvare» replaces it and closes its tabs (SH12).
        expect.That(workspace.NewProject() is null && ReferenceEquals(workspace.Project, project) && !clsTab.IsDisposed, "Secondo progetto con «Annulla»");
        var next = workspace.NewProject()!;
        expect.That(!ReferenceEquals(next, project) && clsTab.IsDisposed && bench.Closed.Last() == (clsTab, SheetCloseReason.DocumentClosed)
            && workspace.Sheets.Single() == single && workspace.Calculations.Count == 1, "Secondo progetto con «Non salvare»");

        // A project file opens in place of the open project (a saved project closes without a question).
        string path = Path.Combine(folder, "Progetto.programma");
        var saved = ProjectDocuments.CreateArchive(); ProjectDocuments.AddProject(saved, "Ponte salvato");
        DocumentService.Save(path, saved);
        var opened = workspace.OpenFile(path) as ProjectSession;
        expect.That(opened is not null && ReferenceEquals(workspace.Project, opened) && opened.Projects.Single().S("nome") == "Ponte salvato" && !opened.IsDirty, "Apertura di un progetto");
        expect.That(ReferenceEquals(workspace.OpenFile(path), opened), "Progetto aperto due volte");
        expect.That(workspace.CloseProject() && workspace.Project is null && workspace.CloseProject(), "Chiusura del progetto salvato");
        expect.That(bench.Confirmations.Asked.SequenceEqual(["Elimina: " + ProjectSession.DeleteQuestion, "salvataggio", "salvataggio"]), "Domande dei progetti: " + string.Join(" | ", bench.Confirmations.Asked));
    }

    /// <summary>A project with a section of three linked sheets (as SharedDataCoordinatorChecks) open in the workspace.</summary>
    private static (ShellBench Bench, ProjectSession Project, JsonObject Section, JsonObject Material, JsonObject Second, JsonObject Concrete) Linked(ScriptedConfirmations confirmations)
    {
        var bench = new ShellBench(confirmations);
        var document = ProjectDocuments.CreateArchive();
        var root = ProjectDocuments.AddProject(document, "Progetto condiviso");
        var section = ProjectDocuments.AddSection(root, "Pila P1");
        var material = ProjectDocuments.AddSheet(section, "mat_calcestruzzo", "CLS di riferimento");
        var second = ProjectDocuments.AddSheet(section, "mat_calcestruzzo", "CLS seconda verifica");
        var concrete = ProjectDocuments.AddSheet(section, "str_palo", "Verifica sezione");
        var project = (ProjectSession)bench.Workspace.OpenDocument(document, null, false)!;
        return (bench, project, section, material, second, concrete);
    }

    private static void SharedData(Expect expect)
    {
        var (bench, project, _, material, second, concrete) = Linked(new ScriptedConfirmations().Then(true).Then(false));
        var workspace = bench.Workspace;
        var materialTab = workspace.OpenSheet(material);
        var secondTab = workspace.OpenSheet(second);
        var secondModel = secondTab.ViewModel;
        workspace.Activate(materialTab);

        // «Aggiorna tutti i fogli collegati»: the other open sheet reopens on the new data; the sheet not open changes in the document.
        materialTab.ViewModel.Data["classe"] = "C40/50";
        workspace.Activate(secondTab);
        expect.That(bench.Confirmations.Asked.Single() == "dati condivisi: Pila P1" && second["dati"].S("classe") == "C40/50" && project.IsDirty, "Dati condivisi non aggiornati");
        expect.That(!ReferenceEquals(secondTab.ViewModel, secondModel) && secondModel is ISheetViewModel { } old && ((ProbeSheetViewModel)old).IsDisposed
            && secondTab.ViewModel.Data.S("classe") == "C40/50", "Scheda collegata non riaperta");
        var reload = bench.Reloads.Single();
        expect.That(ReferenceEquals(reload.Source, materialTab) && reload.Sheets.SequenceEqual([secondTab]) && reload.Change is null, "Evento della ricarica");
        expect.That(J.Number(ProjectSharedData.Fields(concrete)["CLS · fck [MPa]"].Value) == 40, "Foglio chiuso non aggiornato");

        // «Solo questo foglio»: nothing reaches the other sheets.
        secondTab.ViewModel.Data["classe"] = "C25/30";
        workspace.Activate(materialTab);
        expect.That(bench.Confirmations.Asked.Count == 2 && material["dati"].S("classe") == "C40/50" && materialTab.ViewModel.Data.S("classe") == "C40/50"
            && bench.Reloads.Count == 1, "Scelta locale propagata");

        // Error while the linked sheets are updated: every sheet of the section is restored (deferred from F5.2), the focus stays.
        var (failing, failingProject, _, m2, s2, c2) = Linked(new ScriptedConfirmations().Then(true));
        var m2Tab = failing.Workspace.OpenSheet(m2);
        var s2Tab = failing.Workspace.OpenSheet(s2);
        failing.Workspace.Activate(m2Tab);
        var before = (m2["dati"]!.DeepClone(), s2["dati"]!.DeepClone(), c2["dati"]!.DeepClone());
        bool fail = true;
        failingProject.StateChanged += (_, _) => { if (fail) { fail = false; throw new InvalidOperationException("Errore di prova"); } };
        m2Tab.ViewModel.Data["classe"] = "C40/50";
        expect.Throws<InvalidOperationException>(() => failing.Workspace.Activate(s2Tab), "Errore dell'aggiornamento non propagato");
        expect.That(JsonNode.DeepEquals(m2["dati"], before.Item1) && JsonNode.DeepEquals(s2["dati"], before.Item2) && JsonNode.DeepEquals(c2["dati"], before.Item3)
            && ReferenceEquals(failing.Workspace.Active, m2Tab), "Fogli collegati non ripristinati dopo l'errore");
    }

    private static void UndoAndRevisions(Expect expect)
    {
        var (bench, project, section, material, second, _) = Linked(new ScriptedConfirmations().Then(true));
        var workspace = bench.Workspace;
        var materialTab = workspace.OpenSheet(material);
        var secondTab = workspace.OpenSheet(second);
        workspace.Activate(materialTab);
        materialTab.ViewModel.Data["classe"] = "C40/50";
        workspace.Activate(secondTab);
        var added = project.AddSheet("str_palo", section)!;
        var addedTab = workspace.OpenSheet(added);
        bench.Reloads.Clear();

        // Undo (3.7): the active sheet confirmed, the tabs reopen on the restored document by identifier, the missing one closes with a notice.
        expect.That(project.Restore(false) && addedTab.IsDisposed && bench.Closed.Last() == (addedTab, SheetCloseReason.Missing), "Scheda di un foglio annullato");
        expect.That(bench.Messages.Shown.Single() == ("ANTHEA", WorkspaceSession.MissingSheet(added.S("nome")), false), "Avviso della scheda chiusa: " + string.Join(" | ", bench.Messages.Shown));
        expect.That(project.Contains(materialTab.Sheet) && project.Contains(secondTab.Sheet) && secondTab.ViewModel.Data.S("classe") == "C40/50"
            && bench.Reloads.Single().Change == ProjectChange.Restored && bench.Reloads.Single().Sheets.Count == 2, "Schede non riaperte dopo l'annulla");
        expect.That(project.Restore(false) && secondTab.ViewModel.Data.S("classe") != "C40/50" && materialTab.Sheet["dati"].S("classe") != "C40/50", "Secondo annulla");
        expect.That(project.Restore(true) && project.Restore(true) && project.Find(added.S("id")) is not null && workspace.SheetOf(project.Find(added.S("id"))!) is null, "Ripristina: il foglio torna, la scheda resta chiusa");

        // Revisions (SH6): the tabs follow the snapshot read-only; a sheet absent from the revision closes without a notice.
        var liveSection = project.Find(section.S("id"))!;
        project.NewRevision(liveSection, "Prima emissione");
        var late = project.AddSheet("mat_calcestruzzo", liveSection)!;
        var lateTab = workspace.OpenSheet(late);
        workspace.Activate(secondTab);
        Probe(secondTab).Note = "in sospeso";
        int notices = bench.Messages.Shown.Count;
        expect.That(project.SwitchRevision(liveSection.S("id"), 0) && project.Find(second.S("id"))!["dati"] is not null, "Revisione 0");
        expect.That(lateTab.IsDisposed && bench.Closed.Last() == (lateTab, SheetCloseReason.Missing) && bench.Messages.Shown.Count == notices, "Scheda assente dalla revisione");
        expect.That(secondTab.IsReadOnly && secondTab.ViewModel.IsReadOnly && materialTab.ViewModel.IsReadOnly && project.Contains(secondTab.Sheet)
            && secondTab.Heading.EndsWith(" · Rev. 0 · sola lettura", StringComparison.Ordinal), "Schede non in sola lettura");
        expect.That(project.WorkingDocument.Array("progetti").Count == 1 && ProjectRevisions.Find(project.WorkingDocument, second.S("id"))!["dati"].S("nota") == "in sospeso", "Bozza persa al cambio di revisione");
        Probe(secondTab).Label = "vietato";
        secondTab.Commit();
        expect.That(project.Find(second.S("id"))!["dati"]!["etichetta"] is null, "Modifica nella revisione archiviata");
        expect.That(project.SwitchRevision(liveSection.S("id"), null) && !secondTab.IsReadOnly && !secondTab.ViewModel.IsReadOnly
            && ReferenceEquals(secondTab.Sheet, ProjectRevisions.Find(project.WorkingDocument, second.S("id"))), "Ritorno alla versione attuale");
        project.SwitchRevision(liveSection.S("id"), 0);
        expect.That(project.ExitPreview() && !materialTab.IsReadOnly && project.Contains(materialTab.Sheet), "Uscita dalla revisione");
    }

    private static void Requests(Expect expect)
    {
        var (bench, project, section, material, _, _) = Linked(new ScriptedConfirmations());
        var workspace = bench.Workspace;
        var pile = project.AddSheet("geo_palo_verticale", section)!;
        var strata = project.AddSheet("geo_stratigrafia", section)!;
        var materialTab = workspace.OpenSheet(material);

        // The copy of a sheet in another module: in the section, as a new sheet; outside a project, an unsaved single file (SH10).
        Probe(materialTab).CopyCommand.Execute(null);
        var copyTab = workspace.Active!;
        expect.That(copyTab.Owner == project && copyTab.Sheet.S("nome") == "Copia di prova 1" && section.Array("fogli").Contains(copyTab.Sheet)
            && copyTab.Module == "mat_calcestruzzo", "Copia nel progetto");
        var single = workspace.NewCalculation("str_palo");
        Probe(single).CopyCommand.Execute(null);
        var copied = workspace.Active!;
        expect.That(copied.Owner is CalculationDocumentSession { IsDirty: true } && copied.Owner.Document.S("nome") == "Copia di prova" && workspace.Calculations.Count == 2, "Copia fuori dal progetto");

        // Soil sources: the other sheets of the projects that support the soil, «nome · modulo»; none for a single file.
        var pileTab = workspace.OpenSheet(pile);
        Probe(pileTab).SourcesCommand.Execute(null);
        var sources = Probe(pileTab).Sources;
        expect.That(sources.Count == 1 && sources[0].Id == strata.S("id") && sources[0].Label == strata.S("nome") + " · " + ShellTexts.ModuleName("geo_stratigrafia")
            && ReferenceEquals(sources[0].Data, strata["dati"]), "Sorgenti del terreno");
        expect.That(workspace.SoilSources(single).Count == 0, "Sorgenti del terreno di un file singolo");

        // Soil replaced: validated, written, the sheet reopened, the document modified.
        var soil = DocumentService.CreateSheet("geo_palo_verticale"); soil["generali"]!["diametro"] = 1.5;
        var model = pileTab.ViewModel;
        project.MarkSaved(Path.Combine(Path.GetTempPath(), "Terreno.programma"));
        workspace.ReplaceSoil(pileTab, soil);
        expect.That(J.Number(pile["dati"]!["generali"]!["diametro"]) == 1.5 && !ReferenceEquals(pileTab.ViewModel, model) && J.Number(pileTab.ViewModel.Data["generali"]!["diametro"]) == 1.5, "Terreno sostituito");
        expect.That(project.IsDirty, "Documento non modificato dalla sostituzione del terreno");

        // The report asked by a sheet reaches the commands of the shell.
        SheetSession? asked = null;
        workspace.ReportRequested += (_, sheet) => asked = sheet;
        Probe(pileTab).ReportCommand.Execute(null);
        expect.That(ReferenceEquals(asked, pileTab), "Report richiesto dal foglio");
    }

    private static void Closing(Expect expect)
    {
        // The project first, then the single files; the first «Annulla» keeps everything (SH8).
        var bench = new ShellBench(new ScriptedConfirmations().Then(true).Then(SaveChangesAnswer.Discard).Then(SaveChangesAnswer.Cancel)
            .Then(true).Then(SaveChangesAnswer.Discard).Then(SaveChangesAnswer.Discard));
        var workspace = bench.Workspace;
        var first = workspace.NewCalculation("str_palo");
        Probe(first).Label = "x";
        var project = workspace.NewProject()!;
        project.AddProject();
        var second = workspace.NewCalculation("mat_calcestruzzo");
        expect.That(!workspace.ConfirmClose() && ReferenceEquals(workspace.Active, first), "Chiusura annullata al secondo documento");
        expect.That(workspace.ConfirmClose() && bench.Confirmations.Asked.SequenceEqual(["chiusura", "salvataggio", "salvataggio", "chiusura", "salvataggio", "salvataggio"]),
            "Domande della chiusura: " + string.Join(" | ", bench.Confirmations.Asked));
        expect.That(!second.Owner.IsDirty, "File salvato chiesto alla chiusura");
    }
}
