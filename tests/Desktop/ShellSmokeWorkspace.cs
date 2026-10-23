using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Application.Documents;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Projects;
using Anthea.Presentation.Shell.Tabs;
using X.Core;
using X.Desktop.Views.Shell;

namespace X.Desktop;

internal static partial class ShellSmoke
{
    /// <summary>
    /// ui/shell-smoke-project-workspace: ProjectWorkspaceSmokeChecks (MainWindow.SmokeProjectWorkspace) with CheckProjectRevisionNavigation and
    /// CheckProjectRevisionDeletion on the new shell: the empty project, the overview with the search of «Schede da aggiungere», the tree beside a sheet
    /// (SH24: a column of the window, open by default, closed and opened by «▸» without reloading the sheet), «Duplica» independent, undo and redo, the
    /// preview of a move and its cancellation, nesting and removal undone, revisions without duplicated data, saving and reading with damaged references,
    /// «Uniforma a questo» from the overview and from the sheet without changing page or selection, the read-only revision, the summary of the revisions,
    /// the report of the section with its revision; the revision bar in the same window (creation, switch, deletion, promotion) as the two historic checks.
    /// </summary>
    private static async Task ProjectWorkspace(ShellSmokeBench b)
    {
        string directory = b.Output;
        var shell = b.Shell;
        var workspace = b.Workspace;
        // MainWindow.ShowProjectOverview on an empty archive: the empty tree, nothing created.
        workspace.OpenDocument(ProjectDocuments.CreateArchive(), null, false);
        await b.Idle();
        var empty = (FrameworkElement)b.Window.FindName("EmptyTree");
        b.Check(b.Project.Document.Array("progetti").Count == 0 && empty.IsVisible && shell.Tree.IsEmpty, "Progetto vuoto conservato, albero vuoto con «+  Crea progetto»");
        await b.Capture("progetto-vuoto.png");
        var document = ProjectDocuments.CreateArchive();
        var root = J.Obj(("id", "root"), ("nome", "Ponte di prova"), ("fogli", new JsonArray()), ("strutture", new JsonArray())); document.Array("progetti").Add(root);
        var foundation = ProjectDocuments.AddSection(root, "Fondazioni"); var pile = ProjectDocuments.AddSection(foundation, "Palo P1"); var other = ProjectDocuments.AddSection(root, "Fondazioni B");
        JsonObject Steel(JsonObject owner, string name, double fy)
        {
            var sheet = J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", RebarMaterial.Module), ("dati", RebarMaterial.Defaults()));
            sheet["dati"]!["input"]!["fyk_mpa"] = fy; owner.Array("fogli").Add(sheet); return sheet;
        }
        var material = Steel(foundation, "Acciaio fondazioni", 450); var sheet = Steel(pile, "Acciaio palo", 450); Steel(other, "Acciaio destinazione", 400);
        string pileId = pile.S("id"), sheetId = sheet.S("id"), otherId = other.S("id"), materialId = material.S("id");
        workspace.OpenDocument(document, null, false);
        var session = b.Project;
        JsonObject Find(string id) => session.Find(id) ?? throw new InvalidOperationException("Nodo assente: " + id);
        shell.ShowOverview(pile);
        await b.Idle();
        b.Check(shell.Center is SectionOverviewViewModel { Section: var overview } && ReferenceEquals(overview, pile), "Pagina progetto presente");
        // «Cerca una scheda» of «Schede da aggiungere».
        var search = ShellSmokeBench.Descendants<TextBox>(b.Window).Single(t => System.Windows.Automation.AutomationProperties.GetName(t) == ProjectCatalogViewModel.SearchAutomationName);
        IEnumerable<ProjectCatalogCard> Cards() => ShellSmokeBench.Descendants<Border>(b.Window).Where(c => c.Name == "Card" && c.DataContext is ProjectCatalogCard).Select(c => (c, (ProjectCatalogCard)c.DataContext))
            .Where(p => p.c.IsVisible).Select(p => p.Item2);
        search.Text = "ACCIAIO"; await b.Idle();
        b.Check(Cards().Select(c => c.Module).SequenceEqual([RebarMaterial.Module]), "Ricerca catalogo filtra per nome");
        search.Text = "nessuna-scheda-corrispondente"; await b.Idle();
        b.Check(!Cards().Any() && shell.Catalogue.HasNoResults, "Ricerca senza risultati: nessuna scheda");
        search.Text = ""; await b.Idle();
        b.Check(Cards().Count() == shell.Catalogue.Groups.Sum(g => g.Cards.Count), "Pulizia ricerca: tutti i moduli");
        await b.Capture("riepilogo.png");
        var sheetSession = workspace.OpenSheet(sheet);
        await b.Idle();
        var projectTree = (FrameworkElement)b.Window.FindName("ProjectTree");
        b.Check(shell.Center is SheetTabViewModel { Session: var shown } && ReferenceEquals(shown, sheetSession), "Scheda progetto presente");
        b.Check(shell.IsTreeOpen && projectTree.IsVisible, "SH24: albero del progetto visibile all'apertura della scheda (MainWindow lo chiudeva)");
        b.Check(ReferenceEquals(shell.Tree.Selected?.Node, sheet), "Foglio aperto selezionato nell'albero");
        await b.Capture("foglio-con-struttura.png");
        var activeView = sheetSession.ViewModel;
        var toggle = b.Button(shell.ToggleTreeCommand);
        await b.Press(toggle);
        b.Check(!projectTree.IsVisible && ReferenceEquals(sheetSession.ViewModel, activeView), "Chiusura della struttura senza ricaricare il foglio");
        await b.Capture("foglio.png");
        await b.Press(toggle);
        b.Check(projectTree.IsVisible && ReferenceEquals(sheetSession.ViewModel, activeView), "Apertura della struttura senza ricaricare il foglio");
        async Task OpenFromTree(JsonObject target)
        {
            await b.Press(b.OpenButton(target));
            b.Check(ReferenceEquals(workspace.Active?.Sheet, target), "Miniatura cambia scheda");
        }
        await OpenFromTree(material); b.Check(projectTree.IsVisible, "Cambio scheda non chiude la struttura");
        shell.ShowOverview(pile); workspace.OpenSheet(sheet); await b.Idle();
        b.Check(projectTree.IsVisible, "Ritorno dal riepilogo: struttura aperta");
        activeView = sheetSession.ViewModel;
        await b.Press(toggle);
        b.Check(!projectTree.IsVisible && ReferenceEquals(sheetSession.ViewModel, activeView), "Chiusura struttura senza ricaricare il foglio");
        workspace.OpenSheet(material); workspace.OpenSheet(sheet); await b.Idle();
        b.Check(!projectTree.IsVisible, "Cambio scheda non riapre la struttura chiusa");
        workspace.CommitActive(); shell.ShowOverview(pile); session.History.Reset(session.Document); await b.Idle();
        // SH24: the tree is a column of the window; on the overview it stays as the user left it (MainWindow showed it on the overview): question SH48.
        b.Check(!projectTree.IsVisible && b.Button(shell.ToggleTreeCommand).IsVisible, "SH24/SH48: sul riepilogo l'albero resta chiuso, «▸» lo riapre");
        await b.Press(b.Button(shell.ToggleTreeCommand));
        b.Check(projectTree.IsVisible, "«▸» riapre l'albero sul riepilogo");
        MenuCommand(b, pile, "Duplica");
        await b.Idle();
        var duplicate = foundation.Array("strutture").OfType<JsonObject>().Last();
        b.Check(duplicate.S("id") != pileId && ProjectSharedData.SubtreeSheets(duplicate).Single().S("id") != sheetId, "Duplicato con nuovi identificativi");
        ProjectSharedData.SubtreeSheets(duplicate).Single()["dati"]!["input"]!["fyk_mpa"] = 430;
        b.Check(sheet["dati"]!["input"].D("fyk_mpa") == 450, "La copia non modifica l'originale");
        session.MarkDirty();
        shell.Commands.UndoCommand.Execute(null); shell.Commands.UndoCommand.Execute(null);
        await b.Idle();
        b.Check(session.Find(pileId) is not null && ProjectSharedData.Sections(session.Document.Array("progetti")[0]!.AsObject()).Count(s => s.S("nome").Contains("copia")) == 0, "Annullamento della copia");
        shell.Commands.RedoCommand.Execute(null);
        await b.Idle();
        b.Check(ProjectRevisions.Nodes(session.Document).Any(n => n.S("nome").Contains("copia")), "Ripristino della copia");
        pile = Find(pileId); sheet = Find(sheetId); other = Find(otherId);
        session.History.Reset(session.Document); var beforeMove = session.Document.DeepClone();
        b.Services.Confirmations.ProjectMove = changes => { b.Check(changes.Any(c => c.Contains("450") && c.Contains("400")), "Anteprima dello spostamento con i valori"); return false; };
        b.Drag(other, ProjectTreeInput.SheetFormat, sheet, DragDrop.DropEvent); await b.Idle();
        b.Check(JsonNode.DeepEquals(beforeMove, session.Document), "Spostamento annullato non modifica i dati");
        b.Services.Confirmations.ProjectMove = _ => true;
        b.Drag(other, ProjectTreeInput.SheetFormat, sheet, DragDrop.DropEvent); await b.Idle();
        b.Check(sheet.Parent?.Parent == other && sheet["dati"]!["input"].D("fyk_mpa") == 400, "Spostamento applicato con i dati della destinazione");
        shell.Commands.UndoCommand.Execute(null); await b.Idle();
        sheet = Find(sheetId); pile = Find(pileId);
        b.Check(sheet.Parent?.Parent == pile && sheet["dati"]!["input"].D("fyk_mpa") == 450, "Annulla spostamento completo");
        other = Find(otherId);
        b.Check(!session.CanNestSection(pile, pile), "Ciclo gerarchico rifiutato");
        b.Drag(other, ProjectTreeInput.SectionFormat, pile, DragDrop.DropEvent, .5); await b.Idle();
        b.Check(pile.Parent?.Parent == other && sheet["dati"]!["input"].D("fyk_mpa") == 400, "Spostamento sezione completo");
        shell.Commands.UndoCommand.Execute(null); await b.Idle();
        pile = Find(pileId); sheet = Find(sheetId);
        b.Services.Confirmations.Delete = (title, question) => title == ProjectSession.DeleteTitle;
        MenuCommand(b, pile, "Elimina"); await b.Idle();
        b.Services.Confirmations.Delete = null;
        b.Check(session.Find(sheetId) is null, "Eliminazione sezione completa");
        shell.Commands.UndoCommand.Execute(null); await b.Idle();
        pile = Find(pileId); sheet = Find(sheetId);
        b.Check(sheet["dati"]!["input"].D("fyk_mpa") == 450, "Annulla eliminazione conserva i dati");
        // Revisions from the bar: «+ Nuova revisione» with the note in its dialog.
        async Task NewRevision(JsonObject scope, string note, bool cancel = false)
        {
            shell.ShowOverview(scope); await b.Idle();
            b.Services.Dialog = d =>
            {
                var dialog = (NewRevisionDialogViewModel)d;
                dialog.Note = note;
                (cancel ? dialog.CancelCommand : dialog.CreateCommand).Execute(null);
                return dialog.Result;
            };
            try { await b.Press(b.Button(shell.Revisions!.AddCommand)); }
            finally { b.Services.Dialog = null; }
        }
        await NewRevision(pile, "Nuove sollecitazioni"); session.MarkDirty();
        pile = Find(pileId);
        int blobs = session.Document["archivio_revisioni"]!.AsObject().Count;
        await NewRevision(pile, "Seconda revisione");
        b.Check(session.Document["archivio_revisioni"]!.AsObject().Count == blobs, "Fogli invariati non duplicati in archivio");
        pile = Find(pileId); sheet = Find(sheetId); material = Find(materialId);
        sheet["dati"]!["input"]!["fyk_mpa"] = 420;
        material["dati"]!["input"]!["fyk_mpa"] = 410;
        var entry = pile.Array("revisioni")[0]!.AsObject(); var snapshot = ProjectRevisions.Snapshot(session.Document, entry);
        b.Check(ProjectSharedData.SubtreeSheets(snapshot).All(s => s["dati"]!["input"].D("fyk_mpa") == 450), "Revisione non modificata dai dati correnti");
        b.Check(ProjectRevisions.Changes(session.Document, pile).Any(c => c.Contains("420")), "Riepilogo delle variazioni");
        string filename = Path.Combine(directory, "revisioni.programma");
        b.Services.SaveFile = _ => filename;
        shell.Commands.SaveAsCommand.Execute(null);
        var packed = JsonNode.Parse(File.ReadAllText(filename))!.AsObject();
        b.Check(packed.D("versione") == 2 && ProjectSharedData.SubtreeSheets(packed.Array("progetti")[0]!.AsObject()).All(s => s["dati"] is null && s["dati_ref"] is not null), "Salvataggio con deduplicazione");
        var loaded = DocumentService.Open(filename); var loadedPile = ProjectRevisions.Find(loaded, pileId)!;
        b.Check(loadedPile.Array("revisioni").Count == 2 && ProjectRevisions.Find(loaded, sheetId)!["dati"]!["input"].D("fyk_mpa") == 420, "Riapertura delle revisioni");
        var broken = (JsonObject)packed.DeepClone(); broken["archivio_revisioni"] = new JsonObject();
        bool rejected = false; try { ProjectRevisions.Unpack(broken); } catch (ArgumentException) { rejected = true; }
        b.Check(rejected, "Riferimenti danneggiati rifiutati");
        foreach (bool fromOverview in new[] { true, false })
        {
            material = Find(materialId); sheet = Find(sheetId); pile = Find(pileId);
            var open = workspace.OpenSheet(sheet); workspace.CommitActive();
            if (fromOverview) shell.ShowOverview(pile);
            else if (!shell.IsTreeOpen) shell.ToggleTreeCommand.Execute(null);
            session.MarkDirty(); session.History.Reset(session.Document);
            await b.Idle();
            var pageBefore = shell.Center;
            string? selectionBefore = shell.Tree.Selected?.Id;
            b.Services.Dialog = d =>
            {
                var coherence = (CoherenceViewModel)d;
                var align = coherence.Conflicts.SelectMany(c => c.Sheets).Single(s => ReferenceEquals(s.Sheet, material) && s.Key == "fyk_mpa");
                align.AlignCommand.Execute(align);
                // The same page: the overview of the same section (the shell rebuilds its ViewModel at every change of the project), or the tab of the sheet.
                b.Check(fromOverview ? shell.Center is SectionOverviewViewModel { Section: var same } && same?.S("id") == pileId && pageBefore is SectionOverviewViewModel
                    : ReferenceEquals(shell.Center, pageBefore) && shell.Center is SheetTabViewModel { Session.Id: var reloaded } && reloaded == sheetId,
                    "Uniformazione non cambia la pagina sotto al confronto");
                b.Check(workspace.SheetOf(Find(sheetId))?.ViewModel.Data["input"].D("fyk_mpa") == 410, "Foglio aperto aggiornato dopo l'uniformazione");
                b.Check(!coherence.Conflicts.SelectMany(c => c.Sheets).Any(s => s.Key == "fyk_mpa"), "Conflitto risolto non più presente");
                return coherence.Result;
            };
            try { await shell.CompareCommand.ExecuteAsync(pile); }
            finally { b.Services.Dialog = null; }
            await b.Idle();
            b.Check(fromOverview ? shell.Center is SectionOverviewViewModel : shell.Center is SheetTabViewModel { Session.Id: var id } && id == sheetId, "Chiusura del confronto non cambia pagina");
            b.Check(shell.Tree.Selected?.Id == selectionBefore, "Uniformazione non cambia la selezione nell'albero");
            b.Check(shell.IsTreeOpen && projectTree.IsVisible, "Uniformazione non nasconde la struttura aperta");
            sheet = Find(sheetId);
            workspace.CommitActive();
            b.Check(sheet["dati"]!["input"].D("fyk_mpa") == 410, "La conferma conserva i dati uniformati");
            workspace.OpenSheet(sheet); b.Check(workspace.Active!.ViewModel.Data["input"].D("fyk_mpa") == 410, "Riapertura con i dati uniformati");
            shell.Commands.UndoCommand.Execute(null); await b.Idle();
            pile = Find(pileId); sheet = Find(sheetId);
            b.Check(sheet["dati"]!["input"].D("fyk_mpa") == 420, "Annulla uniformazione ripristina i valori");
        }
        // The read-only revision (MainWindow: a second window on the snapshot, projectReadOnly): the preview of revision 0 of the pile from the bar.
        shell.ShowOverview(Find(pileId)); await b.Idle();
        await b.Press(RevisionButton(b, 0));
        var historical = workspace.OpenSheet(Find(sheetId)); await b.Idle();
        var readOnlyView = new ShellSheet(b, historical).View;
        b.Check(historical.IsReadOnly && readOnlyView.IsEnabled && ShellSmokeBench.Descendants<TextBox>(readOnlyView).All(t => t.IsReadOnly), "Revisione protetta e consultabile");
        await b.Press(RevisionButton(b, null)); await b.Idle();
        b.Check(!session.IsReadOnly && !session.IsPreviewingRevision, "Ritorno alla versione attuale");
        session.MarkDirty(); shell.ShowOverview(Find(pileId));
        await b.Capture("revisioni.png");
        b.Services.DialogAsync = async d =>
        {
            var summary = (RevisionSummaryViewModel)d;
            await b.CaptureDialog(summary, summary.Title, "storico.png");
            return false;
        };
        try { await shell.RevisionSummaryCommand.ExecuteAsync(Find(pileId)); }
        finally { b.Services.DialogAsync = null; }
        // The report of the section from the shell (MainWindow built it with ReportProject from the chapters of the materials): «Rev. 2» in it.
        string reportPath = Path.Combine(directory, "report-revisione.docx");
        var written = await SectionReport(b, Find(pileId), reportPath);
        using (var package = System.IO.Compression.ZipFile.OpenRead(reportPath))
        using (var input = package.GetEntry("word/document.xml")!.Open())
        using (var reader = new StreamReader(input)) b.Check(written is not null && reader.ReadToEnd().Contains("Rev. 2"), "Revisione presente nel report");
        await RevisionNavigation(b);
        await RevisionDeletion(b);
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK (shell nuova): albero richiudibile (SH24), riepilogo, duplicazione indipendente, annulla/ripristina, uniformazione da riepilogo e foglio senza navigazione e senza perdita dei dati, anteprima spostamento e annullamento, revisioni immutabili con contesto, deduplicazione, salvataggio/lettura e dati corrotti, sola lettura navigabile, pulsanti revisioni nella stessa finestra e documento corrente conservato.");
    }

    /// <summary>A command of the menu of a node, as the user picks it (the item of the context menu of the view).</summary>
    private static void MenuCommand(ShellSmokeBench b, JsonObject node, string header)
    {
        var item = b.Menu(node).Items.OfType<MenuItem>().Single(m => m.Header as string == header);
        if (item.Command is null || !item.Command.CanExecute(item.CommandParameter)) throw new InvalidOperationException("Voce del menu non eseguibile: " + header);
        item.Command.Execute(item.CommandParameter);
    }

    /// <summary>A button of the revision bar: «Rev. n» (archived) or the current version (null).</summary>
    private static Button RevisionButton(ShellSmokeBench b, int? number) => ShellSmokeBench.Descendants<Button>(b.Window).Single(button => button.Name == "Revision"
        && button.DataContext is RevisionTab tab && (number is null ? tab.IsCurrent : !tab.IsCurrent && tab.Number == number));

    /// <summary>
    /// The report of a section from the shell, as the user asks it from the overview: «Generate con segnalazioni» when the check is shown, the file of the
    /// check, the progress until the end. Null when nothing was written.
    /// </summary>
    private static async Task<int?> SectionReport(ShellSmokeBench b, JsonObject section, string path)
    {
        b.Services.SaveFile = _ => path;
        b.Services.DialogAsync = async d =>
        {
            switch (d)
            {
                case SectionReportCheckViewModel check: check.GenerateCommand.Execute(null); return check.Result;
                case SectionReportProgressViewModel progress:
                    for (int i = 0; i < 2400 && !progress.IsFinished; i++) await Task.Delay(50);
                    return progress.Result;
                default: throw new InvalidOperationException("Dialogo non previsto: " + d.GetType().Name);
            }
        };
        try { return await b.Shell.ExportSectionReportAsync(section); }
        finally { b.Services.DialogAsync = null; b.Services.SaveFile = null; }
    }

    /// <summary>
    /// CheckProjectRevisionNavigation on the new shell: the revision bar of a section and of a sheet in the same window; creation and cancellation; the
    /// switches 0, 1 and current keep the sheet and the tree; the archived sheets of every module are read-only and navigable; pending edits, saving, undo
    /// and redo of the current document; a sheet missing in a revision (the overview, then the sheet again on the current version); reopening the file; the
    /// revisions of a subsection independent.
    /// </summary>
    private static async Task RevisionNavigation(ShellSmokeBench b)
    {
        string directory = b.Output;
        var shell = b.Shell; var workspace = b.Workspace;
        var document = ProjectDocuments.CreateArchive();
        var root = J.Obj(("id", "revision-root"), ("nome", "Ponte · revisioni"), ("fogli", new JsonArray()), ("strutture", new JsonArray())); document.Array("progetti").Add(root);
        var section = ProjectDocuments.AddSection(root, "Palo P1"); var child = ProjectDocuments.AddSection(section, "Verifiche");
        JsonObject Sheet(string module, string name, JsonObject owner)
        {
            var sheet = J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module)));
            owner.Array("fogli").Add(sheet); return sheet;
        }
        var pile = Sheet("geo_palo_verticale", "Capacità portante", child);
        var ca = Sheet("str_palo", "Verifica in c.a.", child);
        var otherSheets = Archivio.Moduli.Where(m => m is not ("geo_palo_verticale" or "str_palo")).Select(m => Sheet(m, ShellTexts.ModuleName(m), child)).ToArray();
        string scope = section.S("id"), pileId = pile.S("id"), caId = ca.S("id"), childId = child.S("id");
        b.Services.Confirmations.SaveChanges = () => SaveChangesAnswer.Discard;
        workspace.OpenDocument(document, null, false);
        b.Services.Confirmations.SaveChanges = null;
        var session = b.Project;
        JsonObject Find(string id) => session.Find(id) ?? throw new InvalidOperationException("Nodo assente: " + id);
        shell.ShowOverview(Find(scope)); await b.Idle();
        b.Check(shell.Revisions!.Tabs.Any(t => t.Label == "Rev. 0 · attuale") && RevisionButton(b, null).Content as string == "Rev. 0 · attuale", "Revisione iniziale visibile");
        async Task Create(bool cancel, string note)
        {
            b.Services.Dialog = d =>
            {
                var dialog = (NewRevisionDialogViewModel)d;
                dialog.Note = note;
                (cancel ? dialog.CancelCommand : dialog.CreateCommand).Execute(null);
                return dialog.Result;
            };
            try { await b.Press(b.Button(shell.Revisions!.AddCommand)); }
            finally { b.Services.Dialog = null; }
            await b.Idle();
        }
        var beforeCancel = session.Document.DeepClone(); await Create(true, "Annullata");
        b.Check(JsonNode.DeepEquals(beforeCancel, session.Document), "Annullamento non crea una revisione");
        await Create(false, "Verifica della fondazione");
        b.Check(Find(scope)["revisione"].D("numero") == 1 && Find(scope).Array("revisioni").Count == 1, "Prima creazione: Rev. 0 archiviata, Rev. 1 attuale");
        var working = session.Document;
        workspace.OpenSheet(Find(pileId)); await b.Idle();
        b.Sheet.SetGeometry("1.2");
        int windows = System.Windows.Application.Current.Windows.Count;
        async Task Switch(int? number)
        {
            await b.Press(RevisionButton(b, number));
            await b.Idle();
            b.Check(System.Windows.Application.Current.Windows.Count == windows, "Cambio revisione nella stessa finestra");
        }
        await Switch(0);
        b.Check(session.IsReadOnly && workspace.Active?.Id == pileId && workspace.Active.ViewModel.Data["generali"].D("diametro") == 1, "Revisione precedente con il foglio conservato");
        b.Check(ProjectRevisions.Find(working, pileId)!["dati"]!["generali"].D("diametro") == 1.2 && session.IsDirty, "Cambio revisione conserva le modifiche in sospeso");
        var sheetView = b.Sheet.View;
        b.Check(shell.IsTreeOpen && sheetView.IsEnabled, "Consultazione con l'albero e il foglio abilitato");
        b.Check(ShellSmokeBench.Descendants<TextBox>(sheetView).Where(t => t.IsVisible).All(t => t.IsReadOnly), "Input del palo storico protetti: "
            + string.Join("; ", ShellSmokeBench.Descendants<TextBox>(sheetView).Where(t => t.IsVisible && !t.IsReadOnly).Select(t => t.Text + " · " + t.TemplatedParent?.GetType().Name)));
        b.Check(ShellSmokeBench.Descendants<TabControl>(sheetView).All(t => t.IsEnabled), "Schede storiche navigabili");
        b.Check(ShellSmokeBench.Descendants<Button>(sheetView).Any(button => button.Content?.ToString() == "Estendi" && button.IsEnabled), "Pannelli storici estendibili");
        await b.Capture("revisione-0-foglio.png");
        var archivedDocument = session.Document.DeepClone(); var liveBefore = working.DeepClone();
        workspace.OpenSheet(Find(caId)); await b.Idle();
        sheetView = b.Sheet.View;
        bool IsShear(TabItem item) => item.Header is DependencyObject header && ShellSmokeBench.Descendants<TextBlock>(header).Any(text => text.Text.Trim() == "Taglio e torsione");
        var tabs = ShellSmokeBench.Descendants<TabControl>(sheetView).Single(t => t.Items.OfType<TabItem>().Any(IsShear));
        var shearTab = tabs.Items.OfType<TabItem>().Single(IsShear);
        tabs.SelectedItem = shearTab; await b.Idle();
        b.Check(tabs.IsEnabled && ReferenceEquals(tabs.SelectedItem, shearTab), "Taglio consultabile nella revisione precedente");
        b.Check(ShellSmokeBench.Descendants<DataGrid>(sheetView).Where(g => g.IsVisible).All(g => g.IsReadOnly && !g.CanUserAddRows && !g.CanUserDeleteRows), "Tabelle storiche protette");
        await b.Capture("revisione-0-taglio.png");
        workspace.CommitActive();
        b.Check(JsonNode.DeepEquals(liveBefore, working) && JsonNode.DeepEquals(archivedDocument, session.Document), "La navigazione non modifica i dati archiviati o attuali");
        foreach (var original in otherSheets)
        {
            var archived = workspace.OpenSheet(Find(original.S("id"))); await b.Idle();
            var view = new ShellSheet(b, archived).View;
            string module = original.S("modulo_id");
            b.Check(view.IsEnabled && ShellSmokeBench.Descendants<TabControl>(view).All(t => t.IsEnabled), "Consultazione: " + module);
            b.Check(ShellSmokeBench.Descendants<TextBox>(view).Where(t => t.IsVisible).All(t => t.IsReadOnly), "Input storici protetti: " + module);
            b.Check(ShellSmokeBench.Descendants<DataGrid>(view).All(g => g.IsReadOnly), "Tabelle storiche protette: " + module);
            // MainWindow keeps one sheet: the shell closes the tab once read.
            workspace.CloseSheet(archived);
        }
        workspace.OpenSheet(Find(caId)); await b.Idle();
        workspace.CommitActive();
        b.Check(JsonNode.DeepEquals(liveBefore, working) && JsonNode.DeepEquals(archivedDocument, session.Document), "La consultazione degli altri moduli non modifica il documento");
        await Switch(null);
        b.Check(!session.IsReadOnly && ReferenceEquals(session.Document, working) && workspace.Active?.Id == caId, "Ritorno alla revisione corrente con il documento e il foglio");
        workspace.OpenSheet(Find(pileId)); await b.Idle();
        b.Check(workspace.Active!.ViewModel.Data["generali"].D("diametro") == 1.2, "Ritorno alla revisione attuale con i dati");
        await Create(false, "Diametro aggiornato");
        b.Check(Find(scope)["revisione"].D("numero") == 2 && shell.Center is SheetTabViewModel { Session.Id: var kept } && kept == pileId, "Nuova revisione dal foglio: il foglio resta");
        await Switch(1); b.Check(workspace.Active?.ViewModel.Data["generali"].D("diametro") == 1.2, "Rev. 1 archiviata con i suoi dati");
        await Switch(0); b.Check(workspace.Active?.ViewModel.Data["generali"].D("diametro") == 1, "Secondo cambio di revisione");
        await Switch(null);
        // A sheet added in Rev. 2: missing in Rev. 0 (the overview, as MainWindow shows the structure), found again on the current version.
        foreach (var open in workspace.Sheets.ToArray()) workspace.CloseSheet(open);
        // As MainWindow: the sheet written into the document, then one change of the document (MarkDirty, one step of the history).
        var added = Sheet(RebarMaterial.Module, "Acciaio aggiunto in Rev. 2", Find(childId)); session.MarkDirty(); shell.Tree.Refresh();
        workspace.OpenSheet(added); await b.Idle();
        await Switch(0);
        b.Check(workspace.Active is null && shell.Center is SectionOverviewViewModel, "Foglio assente nella revisione: il riepilogo");
        await Switch(null);
        b.Check(workspace.Active?.Id == added.S("id") && shell.Center is SheetTabViewModel, "Il foglio ritrovato nella revisione attuale");
        int undoCount = session.History.UndoCount;
        string path = Path.Combine(directory, "revisioni-navigabili.programma");
        await Switch(1);
        b.Services.SaveFile = _ => path;
        b.Check(b.Shell.Commands.Save(true), "Salvataggio del progetto dalla vista storica");
        var saved = DocumentService.Open(path);
        b.Check(ProjectRevisions.Find(saved, added.S("id")) is not null && ProjectRevisions.Find(saved, scope)!["revisione"].D("numero") == 2, "Salvato il documento corrente, non la revisione storica");
        await Switch(null); b.Check(session.History.UndoCount == undoCount, "La navigazione non altera la cronologia");
        shell.Commands.UndoCommand.Execute(null); await b.Idle();
        b.Check(session.Find(added.S("id")) is null, "Annulla dopo la consultazione storica");
        shell.Commands.RedoCommand.Execute(null); await b.Idle();
        b.Check(session.Find(added.S("id")) is not null, "Ripristina dopo la consultazione storica");
        shell.ShowOverview(Find(scope));
        await b.Capture("revisioni-pulsanti.png");
        b.Check(b.Shell.Commands.Save(false), "Salvataggio prima della riapertura");
        await Switch(0);
        // MainWindow.LoadFile: the archive open is closed (saved, no question) and read again.
        b.Check(workspace.CloseProject(), "Archivio salvato chiuso dall'anteprima senza domande");
        workspace.OpenFile(path); await b.Idle();
        session = b.Project;
        b.Check(!session.IsReadOnly && !session.IsPreviewingRevision && Find(scope)["revisione"].D("numero") == 2, "Riapertura del file sulla revisione corrente");
        var loadedChild = Find(childId);
        shell.ShowOverview(loadedChild); await b.Idle();
        b.Check(ReferenceEquals(shell.Revisions!.Scope, loadedChild), "Revisioni della sottosezione nella barra");
        await Create(false, "Revisione della sola sottosezione");
        b.Check(Find(childId)["revisione"].D("numero") == 1 && Find(scope)["revisione"].D("numero") == 2, "Numerazioni delle sezioni indipendenti");
        b.Services.SaveFile = null;
        File.WriteAllText(Path.Combine(directory, "revisioni-navigazione.txt"), "OK (shell nuova): creazione/annullamento e numerazione; passaggi 0/1/attuale nella stessa finestra; foglio e albero conservati; consultazione CA/geo con input protetti; modifiche in sospeso, salvataggio e undo/redo del documento corrente; fogli mancanti e riapertura.");
    }

    /// <summary>
    /// CheckProjectRevisionDeletion on the new shell: «Elimina revisione» of the bar for an archived and the current revision, the promotion of the previous one
    /// editable with its data and structure, gaps in the numbers, input not confirmed and undo/redo, subsections and other branches, the only version protected,
    /// a new revision after the deletions and saving/reading, sheets moved to another branch without duplicated identifiers.
    /// </summary>
    private static async Task RevisionDeletion(ShellSmokeBench b)
    {
        string directory = b.Output;
        var shell = b.Shell; var workspace = b.Workspace;
        var document = ProjectDocuments.CreateArchive();
        var root = J.Obj(("id", "delete-root"), ("nome", "Progetto revisioni"), ("fogli", new JsonArray()), ("strutture", new JsonArray())); document.Array("progetti").Add(root);
        var section = ProjectDocuments.AddSection(root, "Palo P1"); var sibling = ProjectDocuments.AddSection(root, "Palo P2");
        var child = ProjectDocuments.AddSection(section, "Verifiche");
        JsonObject Sheet(JsonObject owner, string module, string name)
        {
            var result = J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module)));
            owner.Array("fogli").Add(result); return result;
        }
        var material = Sheet(root, RebarMaterial.Module, "Acciaio comune");
        var pile = Sheet(child, "geo_palo_verticale", "Portanza");
        Sheet(sibling, RebarMaterial.Module, "Acciaio P2");
        ProjectRevisions.NewRevision(document, child, "Revisione locale");
        ProjectRevisions.NewRevision(document, section, "Rev. 1");
        pile["dati"]!["generali"]!["diametro"] = "1.5";
        ProjectRevisions.NewRevision(document, section, "Rev. 2");
        ProjectRevisions.NewRevision(document, child, "Revisione locale successiva");
        pile["dati"]!["generali"]!["diametro"] = "2";
        var addedSection = ProjectDocuments.AddSection(section, "Aggiunta in Rev. 2"); Sheet(addedSection, RebarMaterial.Module, "Acciaio nuovo");
        string scopeId = section.S("id"), pileId = pile.S("id"), childId = child.S("id");
        string materialId = material.S("id"), siblingId = sibling.S("id");
        var materialBefore = material.DeepClone(); var siblingBefore = sibling.DeepClone();
        foreach (var open in workspace.Sheets.ToArray()) workspace.CloseSheet(open);
        b.Services.Confirmations.SaveChanges = () => SaveChangesAnswer.Discard;
        workspace.OpenDocument(document, null, false);
        b.Services.Confirmations.SaveChanges = null;
        var session = b.Project;
        JsonObject Scope() => session.Find(scopeId)!;
        shell.ShowOverview(Scope()); await b.Idle();
        Button DeleteButton() => ShellSmokeBench.Descendants<Button>(b.Window).Single(button => ReferenceEquals(button.Command, shell.Revisions!.DeleteCommand));
        async Task Delete()
        {
            b.Check(DeleteButton().IsEnabled, "Eliminazione revisione abilitata");
            await b.Press(DeleteButton());
            b.Check(!session.IsReadOnly && !session.IsPreviewingRevision, "Eliminazione torna alla versione modificabile");
        }
        async Task ShowCurrent()
        {
            if (session.IsReadOnly) await b.Press(RevisionButton(b, null));
            shell.ShowOverview(Scope()); await b.Idle();
        }
        // Removing an archived version keeps the current data and the numbers.
        await b.Press(RevisionButton(b, 1));
        var originalCurrent = ProjectRevisions.Find(session.WorkingDocument, pileId)!["dati"]!.DeepClone();
        await Delete();
        b.Check(Scope()["revisione"].D("numero") == 2 && Scope().Array("revisioni").Select(r => r.D("numero")).SequenceEqual([0d]), "Eliminazione intermedia senza rinumerare");
        b.Check(JsonNode.DeepEquals(originalCurrent, session.Find(pileId)!["dati"]), "Eliminazione intermedia non cambia i dati attuali");
        shell.Commands.UndoCommand.Execute(null); await ShowCurrent(); b.Check(Scope().Array("revisioni").Count == 2, "Annulla recupera la revisione eliminata");
        shell.Commands.RedoCommand.Execute(null); await ShowCurrent(); b.Check(Scope().Array("revisioni").Count == 1, "Ripristina ripete l’eliminazione");
        // With a gap in the numbers, the latest remaining snapshot becomes editable.
        await Delete();
        b.Check(Scope()["revisione"].D("numero") == 0 && session.Find(pileId)!["dati"]!["generali"].D("diametro") == 1, "Promozione con salto di numerazione");
        shell.Commands.UndoCommand.Execute(null); shell.Commands.UndoCommand.Execute(null); await ShowCurrent();
        b.Check(Scope()["revisione"].D("numero") == 2 && Scope().Array("revisioni").Count == 2, "Annullamento della sequenza di eliminazioni");
        // Pending input is kept by undo, never confirmed over restored data (in the shell the overview confirms the sheet when it loses the focus).
        workspace.OpenSheet(session.Find(pileId)!); await b.Idle(); b.Sheet.SetGeometry("2.3");
        shell.ShowOverview(Scope()); await b.Idle();
        var pileTab = workspace.SheetOf(session.Find(pileId)!)!;
        var oldView = pileTab.ViewModel;
        await Delete();
        b.Check(Scope()["revisione"].D("numero") == 1 && Scope().Array("revisioni").Count == 1, "Eliminazione dell’ultima promuove Rev. 1");
        b.Check(workspace.SheetOf(session.Find(pileId)!) is { } reopened && !ReferenceEquals(reopened.ViewModel, oldView) && reopened.ViewModel.Data["generali"].D("diametro") == 1.5,
            "Foglio aperto riaperto sulla versione promossa");
        b.Check(session.Find(addedSection.S("id")) is null, "Struttura precedente ripristinata");
        var restoredChild = session.Find(childId)!;
        b.Check(restoredChild["revisione"].D("numero") == 1 && restoredChild.Array("revisioni").Count == 1, "Storico della sottosezione precedente conservato");
        b.Check(JsonNode.DeepEquals(materialBefore, session.Find(materialId)) && JsonNode.DeepEquals(siblingBefore, session.Find(siblingId)), "Promozione non cambia altri rami o materiali superiori");
        workspace.OpenSheet(session.Find(pileId)!); await b.Idle();
        b.Check(ShellSmokeBench.Descendants<TextBox>(b.Sheet.View).Any(t => t.IsEnabled && !t.IsReadOnly), "La revisione promossa è modificabile");
        shell.Commands.UndoCommand.Execute(null); await ShowCurrent();
        b.Check(session.Find(pileId)!["dati"]!["generali"].D("diametro") == 2.3, "Annulla eliminazione conserva l'input non salvato");
        shell.Commands.RedoCommand.Execute(null); await ShowCurrent();
        workspace.OpenSheet(session.Find(pileId)!); await b.Idle(); b.Sheet.SetGeometry("1.6"); workspace.CommitActive();
        b.Check(session.Find(pileId)!["dati"]!["generali"].D("diametro") == 1.6, "Revisione promossa modificabile");
        var oldest = ProjectRevisions.Snapshot(session.Document, Scope().Array("revisioni")[0]!.AsObject());
        b.Check(ProjectSharedData.SubtreeSheets(oldest).Single(s => s.S("id") == pileId)["dati"]!["generali"].D("diametro") == 1, "La modifica della promossa non altera l'archiviata");
        await ShowCurrent(); await Delete();
        b.Check(Scope()["revisione"].D("numero") == 0 && !DeleteButton().IsEnabled && Scope().Array("revisioni").Count == 0, "Ultima versione rimasta protetta");
        var sole = session.Document.DeepClone();
        b.Check(!shell.Revisions!.DeleteCommand.CanExecute(null) && !session.DeleteRevision(scopeId, 0) && JsonNode.DeepEquals(sole, session.Document), "Eliminazione dell’unica versione rifiutata");
        workspace.OpenSheet(session.Find(pileId)!); await b.Idle(); b.Sheet.SetGeometry("1.1"); workspace.CommitActive();
        await ShowCurrent();
        b.Services.Dialog = d => { var dialog = (NewRevisionDialogViewModel)d; dialog.Note = "Ripartenza dalla revisione 0"; dialog.CreateCommand.Execute(null); return dialog.Result; };
        try { await b.Press(b.Button(shell.Revisions!.AddCommand)); }
        finally { b.Services.Dialog = null; }
        b.Check(Scope()["revisione"].D("numero") == 1 && Scope().Array("revisioni").Count == 1, "Nuova revisione dopo l’eliminazione");
        await b.Press(RevisionButton(b, 0)); await Delete();
        b.Check(Scope()["revisione"].D("numero") == 1 && Scope().Array("revisioni").Count == 0 && !DeleteButton().IsEnabled, "Eliminazione della sola archiviata non altera quella attuale");
        string file = Path.Combine(directory, "revisioni-eliminate.programma");
        b.Services.SaveFile = _ => file;
        b.Check(shell.Commands.Save(true), "Salvataggio dopo le eliminazioni");
        b.Services.SaveFile = null;
        var saved = DocumentService.Open(file);
        b.Check(JsonNode.DeepEquals(ProjectRevisions.Pack(saved), ProjectRevisions.Pack(session.Document)), "Revisioni eliminate assenti alla riapertura");
        b.Check(saved["archivio_revisioni"]!.AsObject().Count < session.Document["archivio_revisioni"]!.AsObject().Count, "Il salvataggio non conserva dati di revisioni non più referenziate");
        await b.Capture("revisione-precedente-editabile.png");
        // Historical sheets moved outside the restored branch must not acquire duplicate IDs.
        var moved = (JsonObject)sole.DeepClone(); var movedScope = ProjectRevisions.Find(moved, scopeId)!;
        ProjectRevisions.NewRevision(moved, movedScope, "Spostamento");
        var movedSheet = ProjectRevisions.Find(moved, pileId)!;
        ((JsonArray)movedSheet.Parent!).Remove(movedSheet); ProjectRevisions.Find(moved, siblingId)!.Array("fogli").Add(movedSheet);
        var untouched = ProjectRevisions.Find(moved, siblingId)!.DeepClone();
        ProjectRevisions.DeleteRevision(moved, scopeId, 1); ProjectRevisions.Validate(moved); Archivio.Valida(moved);
        b.Check(ProjectRevisions.Nodes(moved).Select(n => n.S("id")).Distinct().Count() == ProjectRevisions.Nodes(moved).Count(), "Il ripristino non duplica gli identificativi dei fogli spostati");
        b.Check(JsonNode.DeepEquals(untouched, ProjectRevisions.Find(moved, siblingId)) && ProjectSharedData.SubtreeSheets(movedScope).Any(s => s.S("nome") == "Portanza"), "Il ripristino non altera un foglio spostato in un altro ramo");
        File.WriteAllText(Path.Combine(directory, "revisioni-eliminazione.txt"), "OK (shell nuova): eliminazione intermedia e attuale, promozione editabile con dati e struttura precedenti, salti numerici, input non salvati e undo/redo, sottosezioni e rami separati, protezione dell’unica versione, nuova revisione e salvataggio/riapertura, fogli spostati senza duplicare ID.");
    }
}
