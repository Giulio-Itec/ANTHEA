using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private async Task CheckProjectRevisionNavigation(string directory)
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        async Task Layout() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        ExitRevisionPreview(); DisposeRevisionEditor();
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray()));
        var root = J.Obj(("id", "revision-root"), ("nome", "Ponte · revisioni"), ("fogli", new JsonArray()), ("strutture", new JsonArray())); document.Array("progetti").Add(root);
        var section = CreateProjectSection(root, "Palo P1"); var child = CreateProjectSection(section, "Verifiche");
        JsonObject Sheet(string module, string name, JsonObject owner)
        {
            var sheet = J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module)));
            owner.Array("fogli").Add(sheet); return sheet;
        }
        var pile = Sheet("geo_palo_verticale", "Capacità portante", child);
        var ca = Sheet("str_palo", "Verifica in c.a.", child);
        var otherSheets = Archivio.Moduli.Where(m => m is not ("geo_palo_verticale" or "str_palo"))
            .Select(m => Sheet(m, ModuleName(m), child)).ToArray();
        string scope = section.S("id"), pileId = pile.S("id"), caId = ca.S("id");
        ShowProjectOverview(section); ResetProjectHistory(); await Layout();
        Check(Ui.Descendants<Button>(projectRevisionHost).Any(b => b.Content?.ToString() == "Rev. 0 · attuale"), "Revisione iniziale non visibile");
        async Task Create(bool cancel, string note)
        {
            Exception? failure = null;
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var dialog = Application.Current.Windows.OfType<Window>().Single(w => w != this && w.Title.StartsWith("Nuova revisione"));
                try
                {
                    Ui.Descendants<TextBox>(dialog).Single().Text = note;
                    Ui.Descendants<Button>(dialog).Single(b => b.Content?.ToString() == (cancel ? "Annulla" : "Crea revisione")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch (Exception ex) { failure = ex; dialog.Close(); }
            }));
            Ui.Descendants<Button>(projectRevisionHost).Single(b => b.Content?.ToString() == "+ Nuova revisione").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (failure is not null) throw failure;
            await Layout();
        }
        var beforeCancel = document.DeepClone(); await Create(true, "Annullata");
        Check(JsonNode.DeepEquals(beforeCancel, document), "Annullamento crea una revisione");
        await Create(false, "Verifica della fondazione");
        Check(section["revisione"].D("numero") == 1 && section.Array("revisioni").Count == 1, "Prima creazione non archivia Rev. 0 e seleziona Rev. 1");
        var working = document;
        ShowSheet(pile); SetProjectSheetTreeVisible(true); await Layout();
        editor!.SetGeometryForSmoke("1.2");
        int windows = Application.Current.Windows.Count;
        async Task Switch(int? number)
        {
            Ui.Descendants<Button>(projectRevisionHost).Single(b => b.Tag is ValueTuple<string, int?> tag && tag.Item1 == scope && tag.Item2 == number)
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Layout();
            Check(Application.Current.Windows.Count == windows, "Cambio revisione apre una finestra");
        }
        await Switch(0);
        Check(projectReadOnly && currentSheet?.S("id") == pileId && editor!.Data["generali"].D("diametro") == 1, "Revisione precedente errata o foglio non conservato");
        Check(pile["dati"]!["generali"].D("diametro") == 1.2 && dirty, "Cambio revisione perde modifiche in sospeso");
        Check(projectTreePane.IsVisible && sheetContent.IsEnabled, "Consultazione nasconde l’albero o disabilita il foglio");
        Check(Ui.Descendants<TextBox>(sheetContent).Where(t => t.IsVisible).All(t => t.IsReadOnly), "Input del palo storico modificabili: " + string.Join("; ", Ui.Descendants<TextBox>(sheetContent).Where(t => t.IsVisible && !t.IsReadOnly).Select(t => t.Text + " · " + t.TemplatedParent?.GetType().Name)));
        Check(Ui.Descendants<TabControl>(sheetContent).All(t => t.IsEnabled), "Schede storiche non navigabili");
        Check(Ui.Descendants<Button>(sheetContent).Any(b => b.Content?.ToString() == "Estendi" && b.IsEnabled), "Impossibile estendere i pannelli storici");
        File.WriteAllBytes(Path.Combine(directory, "revisione-0-foglio.png"), Ui.Snapshot(this));
        var archivedDocument = document.DeepClone(); var liveBefore = working.DeepClone();
        ShowSheet(ProjectRevisions.Find(document, caId)!); await Layout();
        var tabs = Ui.Descendants<TabControl>(sheetContent).First(t => t.Items.Count == 7);
        tabs.SelectedIndex = 4; await Layout();
        Check(tabs.IsEnabled && tabs.SelectedIndex == 4, "Impossibile consultare Taglio nella revisione precedente");
        Check(Ui.Descendants<DataGrid>(sheetContent).Where(g => g.IsVisible).All(g => g.IsReadOnly && !g.CanUserAddRows && !g.CanUserDeleteRows), "Tabelle storiche modificabili");
        File.WriteAllBytes(Path.Combine(directory, "revisione-0-taglio.png"), Ui.Snapshot(this));
        Commit(); Check(JsonNode.DeepEquals(liveBefore, working) && JsonNode.DeepEquals(archivedDocument, document), "La navigazione modifica i dati archiviati o attuali");
        foreach (var original in otherSheets)
        {
            ShowSheet(ProjectRevisions.Find(document, original.S("id"))!); await Layout();
            Check(sheetContent.IsEnabled && Ui.Descendants<TabControl>(sheetContent).All(t => t.IsEnabled), "Consultazione bloccata: " + original.S("modulo_id"));
            Check(Ui.Descendants<TextBox>(sheetContent).Where(t => t.IsVisible).All(t => t.IsReadOnly), "Input storici modificabili: " + original.S("modulo_id"));
            Check(Ui.Descendants<DataGrid>(sheetContent).All(g => g.IsReadOnly), "Tabella storica modificabile: " + original.S("modulo_id"));
        }
        ShowSheet(ProjectRevisions.Find(document, caId)!); await Layout();
        Commit(); Check(JsonNode.DeepEquals(liveBefore, working) && JsonNode.DeepEquals(archivedDocument, document), "La consultazione degli altri moduli modifica il documento");
        await Switch(null);
        Check(!projectReadOnly && ReferenceEquals(document, working) && currentSheet?.S("id") == caId, "Ritorno alla revisione corrente perde il documento o il foglio");
        ShowSheet(pile); await Layout(); Check(editor!.Data["generali"].D("diametro") == 1.2, "Ritorno alla revisione attuale perde i dati");
        await Create(false, "Diametro aggiornato");
        Check(section["revisione"].D("numero") == 2 && ReferenceEquals(projectContent.Content, moduleView), "Nuova revisione dal foglio non mantiene il foglio aperto");
        await Switch(1); Check(editor!.Data["generali"].D("diametro") == 1.2, "Rev. 1 archiviata con dati errati");
        await Switch(0); Check(editor!.Data["generali"].D("diametro") == 1, "Secondo cambio revisione legge dati errati");
        await Switch(null);
        var added = Sheet(RebarMaterial.Module, "Acciaio aggiunto in Rev. 2", child); MarkDirty(); ShowSheet(added); await Layout();
        await Switch(0); Check(currentSheet is null && !ReferenceEquals(projectContent.Content, moduleView), "Foglio assente nella revisione non torna alla struttura");
        await Switch(null); Check(ReferenceEquals(currentSheet, added), "Il foglio non viene ritrovato nella revisione attuale");
        int undoCount = projectUndo.Count;
        string oldPath = path ?? ""; path = Path.Combine(directory, "revisioni-navigabili.programma");
        await Switch(1); Check(Save(false), "Salvataggio del progetto dalla vista storica fallito");
        var saved = Archivio.Leggi(path); Check(ProjectRevisions.Find(saved, added.S("id")) is not null && ProjectRevisions.Find(saved, scope)!["revisione"].D("numero") == 2, "Salvata la revisione storica al posto del documento corrente");
        await Switch(null); Check(projectUndo.Count == undoCount, "Navigazione altera la cronologia annulla/ripristina");
        RestoreProjectHistory(false); Check(ProjectRevisions.Find(document, added.S("id")) is null, "Annulla dopo consultazione storica non funziona");
        RestoreProjectHistory(true); Check(ProjectRevisions.Find(document, added.S("id")) is not null, "Ripristina dopo consultazione storica non funziona");
        ShowProjectOverview(ProjectRevisions.Find(document, scope)); await Layout();
        File.WriteAllBytes(Path.Combine(directory, "revisioni-pulsanti.png"), Ui.Snapshot(this));
        Check(Save(false), "Salvataggio prima della riapertura fallito");
        await Switch(0); LoadFile(path); await Layout();
        Check(!projectReadOnly && revisionWorkingDocument is null && ProjectRevisions.Find(document, scope)!["revisione"].D("numero") == 2, "Riapertura del file resta sulla revisione storica");
        var loadedChild = ProjectRevisions.Find(document, child.S("id"))!;
        ShowProjectOverview(loadedChild); await Layout();
        Check(revisionScopeId == loadedChild.S("id"), "Non è possibile creare una revisione della sottosezione");
        await Create(false, "Revisione della sola sottosezione");
        Check(loadedChild["revisione"].D("numero") == 1 && ProjectRevisions.Find(document, scope)!["revisione"].D("numero") == 2, "Numerazioni delle sezioni non indipendenti");
        path = oldPath.Length == 0 ? null : oldPath;
        File.WriteAllText(Path.Combine(directory, "revisioni-navigazione.txt"), "OK: creazione/annullamento e numerazione; passaggi 0/1/attuale nella stessa finestra; foglio e albero conservati; consultazione CA/geo con input protetti; modifiche in sospeso, salvataggio e undo/redo del documento corrente; fogli mancanti e riapertura.");
    }
}
