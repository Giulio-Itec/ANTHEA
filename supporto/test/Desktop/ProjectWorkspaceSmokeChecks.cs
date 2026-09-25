using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeProjectWorkspace(string directory)
    {
        testing = true; Directory.CreateDirectory(directory);
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray()));
        ShowProjectOverview(); UpdateLayout(); await Task.Delay(150);
        Check(document.Array("progetti").Count == 0 && projectEmptyState.IsVisible, "Progetto vuoto non conservato");
        File.WriteAllBytes(Path.Combine(directory, "progetto-vuoto.png"), Ui.Snapshot(this));
        var root = J.Obj(("id", "root"), ("nome", "Ponte di prova"), ("fogli", new JsonArray()), ("strutture", new JsonArray())); document.Array("progetti").Add(root);
        var foundation = CreateProjectSection(root, "Fondazioni"); var pile = CreateProjectSection(foundation, "Palo P1"); var other = CreateProjectSection(root, "Fondazioni B");
        JsonObject Steel(JsonObject owner, string name, double fy)
        {
            var sheet = J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", RebarMaterial.Module), ("dati", RebarMaterial.Defaults()));
            sheet["dati"]!["input"]!["fyk_mpa"] = fy; owner.Array("fogli").Add(sheet); return sheet;
        }
        var material = Steel(foundation, "Acciaio fondazioni", 450); var sheet = Steel(pile, "Acciaio palo", 450); Steel(other, "Acciaio destinazione", 400);
        string pileId = pile.S("id"), sheetId = sheet.S("id"), otherId = other.S("id");
        ShowProjectOverview(pile); Check(ReferenceEquals(body.Content, projectWorkspace), "Pagina progetto assente");
        projectCatalogSearch.Text = "ACCIAIO";
        Check(projectCatalogCards.Where(c => c.Card.Visibility == Visibility.Visible).Select(c => c.Module).SequenceEqual(new[] { RebarMaterial.Module }), "Ricerca catalogo non filtra per nome");
        projectCatalogSearch.Text = "nessuna-scheda-corrispondente";
        Check(projectCatalogCards.All(c => c.Card.Visibility == Visibility.Collapsed), "Ricerca senza risultati mostra schede");
        projectCatalogSearch.Text = "";
        Check(projectCatalogCards.All(c => c.Card.Visibility == Visibility.Visible), "Pulizia ricerca perde moduli");
        UpdateLayout(); await Task.Delay(150); File.WriteAllBytes(Path.Combine(directory, "riepilogo.png"), Ui.Snapshot(this));
        ShowSheet(sheet); Check(ReferenceEquals(body.Content, projectWorkspace) && ReferenceEquals(projectContent.Content, moduleView), "Scheda progetto assente");
        Check(projectTreePane.Visibility == Visibility.Collapsed, "Struttura visibile all'apertura della scheda");
        Check(tree.SelectedItem is TreeViewItem selected && ReferenceEquals(selected.Tag, sheet), "Foglio aperto non selezionato");
        UpdateLayout(); await Task.Delay(150); File.WriteAllBytes(Path.Combine(directory, "foglio.png"), Ui.Snapshot(this));
        var activeEditor = editor;
        projectTreeToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        UpdateLayout(); await Task.Delay(150); File.WriteAllBytes(Path.Combine(directory, "foglio-con-struttura.png"), Ui.Snapshot(this));
        Check(projectTreePane.IsVisible && ReferenceEquals(editor, activeEditor), "Apertura struttura ricarica il foglio");
        void OpenFromTree(JsonObject target)
        {
            Ui.Descendants<Button>(tree).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Apri scheda " + target.S("nome"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(ReferenceEquals(currentSheet, target), "Miniatura non cambia scheda");
        }
        OpenFromTree(material); Check(projectTreePane.IsVisible, "Cambio scheda chiude la struttura");
        ShowProjectOverview(pile); ShowSheet(sheet); UpdateLayout();
        Check(projectTreePane.IsVisible, "Ritorno dal riepilogo dimentica la struttura aperta");
        activeEditor = editor;
        projectTreeToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(projectTreePane.Visibility == Visibility.Collapsed && ReferenceEquals(editor, activeEditor), "Chiusura struttura ricarica il foglio");
        ShowSheet(material); ShowSheet(sheet);
        Check(projectTreePane.Visibility == Visibility.Collapsed, "Cambio scheda riapre la struttura chiusa");
        Commit(); editor?.Dispose(); editor = null; currentSheet = null; ShowProjectOverview(pile); ResetProjectHistory();
        Check(projectTreePane.IsVisible && projectTreeToggle.Visibility == Visibility.Collapsed, "Struttura non ripristinata nel riepilogo");
        ((TreeViewItem)tree.SelectedItem).ContextMenu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Duplica"))
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        var duplicate = foundation.Array("strutture").OfType<JsonObject>().Last();
        Check(duplicate.S("id") != pileId && ProjectSharedData.SubtreeSheets(duplicate).Single().S("id") != sheetId, "ID duplicati");
        ProjectSharedData.SubtreeSheets(duplicate).Single()["dati"]!["input"]!["fyk_mpa"] = 430;
        Check(sheet["dati"]!["input"].D("fyk_mpa") == 450, "Copia modifica originale");
        MarkDirty(); RestoreProjectHistory(false); RestoreProjectHistory(false);
        Check(ProjectRevisions.Find(document, pileId) is not null && ProjectSharedData.Sections(document.Array("progetti")[0]!.AsObject()).Count(s => s.S("nome").Contains("copia")) == 0, "Annullamento copia fallito");
        RestoreProjectHistory(true); Check(ProjectRevisions.Nodes(document).Any(n => n.S("nome").Contains("copia")), "Ripristino copia fallito");
        pile = ProjectRevisions.Find(document, pileId)!; sheet = ProjectRevisions.Find(document, sheetId)!; other = ProjectRevisions.Find(document, otherId)!;
        ResetProjectHistory(); var beforeMove = document.DeepClone();
        projectMoveChoiceForTest = changes => { Check(changes.Any(c => c.Contains("450") && c.Contains("400")), "Anteprima valori mancante"); return false; };
        MoveProjectSheet(sheet, other); Check(JsonNode.DeepEquals(beforeMove, document), "Spostamento annullato modifica dati");
        projectMoveChoiceForTest = _ => true; MoveProjectSheet(sheet, other);
        Check(sheet.Parent?.Parent == other && sheet["dati"]!["input"].D("fyk_mpa") == 400, "Spostamento non applicato");
        RestoreProjectHistory(false); sheet = ProjectRevisions.Find(document, sheetId)!; pile = ProjectRevisions.Find(document, pileId)!;
        Check(sheet.Parent?.Parent == pile && sheet["dati"]!["input"].D("fyk_mpa") == 450, "Annulla spostamento incompleto");
        other = ProjectRevisions.Find(document, otherId)!;
        Check(!CanNestSection(pile, pile), "Ciclo gerarchico ammesso");
        NestProjectSection(pile, other); Check(pile.Parent?.Parent == other && sheet["dati"]!["input"].D("fyk_mpa") == 400, "Spostamento sezione incompleto");
        RestoreProjectHistory(false); pile = ProjectRevisions.Find(document, pileId)!; sheet = ProjectRevisions.Find(document, sheetId)!;
        DeleteProjectNode(pile); Check(ProjectRevisions.Find(document, sheetId) is null, "Eliminazione sezione incompleta");
        RestoreProjectHistory(false); pile = ProjectRevisions.Find(document, pileId)!; sheet = ProjectRevisions.Find(document, sheetId)!;
        Check(sheet["dati"]!["input"].D("fyk_mpa") == 450, "Annulla eliminazione perde dati");
        ProjectRevisions.NewRevision(document, pile, "Nuove sollecitazioni"); MarkDirty();
        int blobs = document["archivio_revisioni"]!.AsObject().Count;
        ProjectRevisions.NewRevision(document, pile, "Seconda revisione");
        Check(document["archivio_revisioni"]!.AsObject().Count == blobs, "Fogli invariati duplicati in archivio");
        sheet["dati"]!["input"]!["fyk_mpa"] = 420;
        material = ProjectRevisions.Find(document, material.S("id"))!; material["dati"]!["input"]!["fyk_mpa"] = 410;
        var entry = pile.Array("revisioni")[0]!.AsObject(); var snapshot = ProjectRevisions.Snapshot(document, entry);
        Check(ProjectSharedData.SubtreeSheets(snapshot).All(s => s["dati"]!["input"].D("fyk_mpa") == 450), "Revisione modificata da dati correnti");
        Check(ProjectRevisions.Changes(document, pile).Any(c => c.Contains("420")), "Riepilogo variazioni mancante");
        string filename = Path.Combine(directory, "revisioni.programma"); Archivio.Scrivi(filename, document);
        var packed = JsonNode.Parse(File.ReadAllText(filename))!.AsObject();
        Check(packed.D("versione") == 2 && ProjectSharedData.SubtreeSheets(packed.Array("progetti")[0]!.AsObject()).All(s => s["dati"] is null && s["dati_ref"] is not null), "Salvataggio senza deduplicazione");
        var loaded = Archivio.Leggi(filename); var loadedPile = ProjectRevisions.Find(loaded, pileId)!;
        Check(loadedPile.Array("revisioni").Count == 2 && ProjectRevisions.Find(loaded, sheetId)!["dati"]!["input"].D("fyk_mpa") == 420, "Riapertura revisioni fallita");
        var broken = (JsonObject)packed.DeepClone(); broken["archivio_revisioni"] = new JsonObject();
        bool rejected = false; try { ProjectRevisions.Unpack(broken); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Riferimenti danneggiati accettati");
        foreach (bool fromOverview in new[] { true, false })
        {
            material = ProjectRevisions.Find(document, material.S("id"))!;
            ShowSheet(sheet); Commit();
            if (fromOverview) ShowProjectOverview(pile);
            else projectTreeToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            MarkDirty(); ResetProjectHistory();
            var pageBefore = projectContent.Content;
            string? selectionBefore = selectedProjectId;
            Exception? dialogError = null;
            _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var dialog = Application.Current.Windows.OfType<Window>().Single(w => w != this && w.Title.StartsWith("Confronto"));
                try
                {
                    var align = Ui.Descendants<Button>(dialog).Single(b => b.Tag is ValueTuple<JsonObject, string> t && ReferenceEquals(t.Item1, material) && t.Item2 == "fyk_mpa");
                    align.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(ReferenceEquals(projectContent.Content, pageBefore), "Uniformazione cambia la pagina sotto al confronto");
                    Check(ReferenceEquals(currentSheet, sheet) && editor!.Data["input"].D("fyk_mpa") == 410, "Editor non aggiornato dopo uniformazione");
                    Check(!Ui.Descendants<Button>(dialog).Any(b => b.Tag is ValueTuple<JsonObject, string> t && t.Item2 == "fyk_mpa"), "Conflitto risolto ancora presente");
                }
                catch (Exception ex) { dialogError = ex; }
                finally { dialog.Close(); }
            }));
            ShowCoherence(pile); if (dialogError is not null) throw dialogError;
            Check(ReferenceEquals(body.Content, projectWorkspace) && ReferenceEquals(projectContent.Content, moduleView) == !fromOverview, "Chiusura confronto cambia pagina");
            Check(selectedProjectId == selectionBefore && tree.SelectedItem is TreeViewItem node && ((JsonObject)node.Tag).S("id") == selectionBefore, "Uniformazione cambia selezione nell'albero");
            Check(projectTreePane.IsVisible, "Uniformazione nasconde la struttura aperta");
            Commit();
            Check(sheet["dati"]!["input"].D("fyk_mpa") == 410, "Commit perde i dati uniformati");
            ShowSheet(sheet); Check(editor!.Data["input"].D("fyk_mpa") == 410, "Riapertura perde i dati uniformati");
            RestoreProjectHistory(false); pile = ProjectRevisions.Find(document, pileId)!; sheet = ProjectRevisions.Find(document, sheetId)!;
            Check(sheet["dati"]!["input"].D("fyk_mpa") == 420, "Annulla uniformazione non ripristina valori");
        }
        var readOnly = new MainWindow { testing = true, projectReadOnly = true };
        readOnly.document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray(snapshot)));
        readOnly.Show(); var historical = ProjectRevisions.Find(readOnly.document, sheetId)!; readOnly.ShowSheet(historical);
        Check(readOnly.sheetContent.IsEnabled && Ui.Descendants<TextBox>(readOnly.sheetContent).All(t => t.IsReadOnly), "Revisione non protetta o consultazione disabilitata"); readOnly.Close();
        MarkDirty(); ShowProjectOverview(pile); UpdateLayout(); await Task.Delay(150);
        File.WriteAllBytes(Path.Combine(directory, "revisioni.png"), Ui.Snapshot(this));
        _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<Window>().Single(w => w != this && w.Title.StartsWith("Revisioni"));
            dialog.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "storico.png"), Ui.Snapshot(dialog)); dialog.Close();
        }));
        ShowProjectRevisions(pile);
        var reportPlan = new ProjectReportPlan(pile);
        var contents = new Dictionary<JsonObject, ReportProject.SheetContent>();
        foreach (var item in reportPlan.Sheets)
        {
            using var reportEditor = new SheetEditor(item.S("modulo_id"), item["dati"]!.AsObject());
            contents[item] = reportEditor.MaterialReportContent();
        }
        string reportPath = Path.Combine(directory, "report-revisione.docx"); ReportProject.Write(reportPath, reportPlan, contents, []);
        using (var package = System.IO.Compression.ZipFile.OpenRead(reportPath))
        using (var input = package.GetEntry("word/document.xml")!.Open())
        using (var reader = new StreamReader(input)) Check(reader.ReadToEnd().Contains("Rev. 2"), "Revisione assente nel report");
        await CheckProjectRevisionNavigation(directory);
        await CheckProjectRevisionDeletion(directory);
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK: albero richiudibile, riepilogo, duplicazione indipendente, annulla/ripristina, uniformazione da riepilogo e foglio senza navigazione e senza perdita dei dati, anteprima spostamento e annullamento, revisioni immutabili con contesto, deduplicazione, salvataggio/lettura e dati corrotti, sola lettura navigabile, pulsanti revisioni nella stessa finestra e documento corrente conservato.");
    }
}
