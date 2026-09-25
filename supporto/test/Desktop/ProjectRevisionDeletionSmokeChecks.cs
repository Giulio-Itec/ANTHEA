using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private async Task CheckProjectRevisionDeletion(string directory)
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        async Task Layout() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        ExitRevisionPreview(); DisposeRevisionEditor();
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray()));
        var root = J.Obj(("id", "delete-root"), ("nome", "Progetto revisioni"), ("fogli", new JsonArray()), ("strutture", new JsonArray())); document.Array("progetti").Add(root);
        var section = CreateProjectSection(root, "Palo P1"); var sibling = CreateProjectSection(root, "Palo P2");
        var child = CreateProjectSection(section, "Verifiche");
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
        var addedSection = CreateProjectSection(section, "Aggiunta in Rev. 2"); Sheet(addedSection, RebarMaterial.Module, "Acciaio nuovo");
        string scopeId = section.S("id"), pileId = pile.S("id"), childId = child.S("id");
        string materialId = material.S("id"), siblingId = sibling.S("id");
        var materialBefore = material.DeepClone(); var siblingBefore = sibling.DeepClone();
        ShowProjectOverview(section); ResetProjectHistory(); await Layout();
        JsonObject Scope() => ProjectRevisions.Find(document, scopeId)!;
        Button DeleteButton() => Ui.Descendants<Button>(projectRevisionHost).Single(b => b.Content?.ToString() == "Elimina revisione");
        async Task Delete()
        {
            Check(DeleteButton().IsEnabled, "Eliminazione revisione disabilitata");
            DeleteButton().RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Layout();
            Check(!projectReadOnly && revisionWorkingDocument is null, "Eliminazione non torna alla versione modificabile");
        }
        async Task ShowCurrent()
        {
            if (projectReadOnly) SwitchProjectRevision(scopeId, null);
            ShowProjectOverview(Scope()); await Layout();
        }
        // Removing an archived version preserves current data and leaves numbering stable.
        SwitchProjectRevision(scopeId, 1); await Layout();
        var originalCurrent = ProjectRevisions.Find(WorkingProjectDocument, pileId)!["dati"]!.DeepClone();
        await Delete();
        Check(Scope()["revisione"].D("numero") == 2 && Scope().Array("revisioni").Select(r => r.D("numero")).SequenceEqual(new[] { 0d }), "Eliminazione intermedia rinumera o perde lo storico");
        Check(JsonNode.DeepEquals(originalCurrent, ProjectRevisions.Find(document, pileId)!["dati"]), "Eliminazione intermedia cambia i dati attuali");
        RestoreProjectHistory(false); await ShowCurrent(); Check(Scope().Array("revisioni").Count == 2, "Annulla non recupera la revisione eliminata");
        RestoreProjectHistory(true); await ShowCurrent(); Check(Scope().Array("revisioni").Count == 1, "Ripristina non ripete l’eliminazione");
        // With a numbering gap, the latest remaining snapshot becomes editable.
        await Delete();
        Check(Scope()["revisione"].D("numero") == 0 && ProjectRevisions.Find(document, pileId)!["dati"]!["generali"].D("diametro") == 1, "Promozione con salto di numerazione errata");
        RestoreProjectHistory(false); RestoreProjectHistory(false); await ShowCurrent();
        Check(Scope()["revisione"].D("numero") == 2 && Scope().Array("revisioni").Count == 2, "Annullamento sequenza di eliminazioni incompleto");
        // Pending editor input is retained by Undo, never committed over restored data.
        ShowSheet(ProjectRevisions.Find(document, pileId)!); await Layout(); editor!.SetGeometryForSmoke("2.3");
        // The sheet has its own revision scope; select the parent revision explicitly.
        ShowProjectOverview(Scope()); revisionNavigationSheetId = pileId; await Layout();
        var oldEditor = editor;
        await Delete();
        Check(Scope()["revisione"].D("numero") == 1 && Scope().Array("revisioni").Count == 1, "Eliminazione dell’ultima non promuove Rev. 1");
        Check(editor is not null && !ReferenceEquals(oldEditor, editor) && currentSheet?.S("id") == pileId && editor.Data["generali"].D("diametro") == 1.5, "Editor rimane sulla versione eliminata");
        Check(ProjectRevisions.Find(document, addedSection.S("id")) is null, "Struttura precedente non ripristinata");
        var restoredChild = ProjectRevisions.Find(document, childId)!;
        Check(restoredChild["revisione"].D("numero") == 1 && restoredChild.Array("revisioni").Count == 1, "Storico della sottosezione precedente perso");
        Check(JsonNode.DeepEquals(materialBefore, ProjectRevisions.Find(document, materialId)) && JsonNode.DeepEquals(siblingBefore, ProjectRevisions.Find(document, siblingId)), "Promozione cambia altri rami o materiali superiori");
        Check(Ui.Descendants<TextBox>(sheetContent).Any(t => t.IsEnabled && !t.IsReadOnly), "La revisione promossa non è modificabile");
        RestoreProjectHistory(false); await ShowCurrent();
        Check(ProjectRevisions.Find(document, pileId)!["dati"]!["generali"].D("diametro") == 2.3, "Annulla eliminazione perde input non salvati");
        RestoreProjectHistory(true); await ShowCurrent();
        ShowSheet(ProjectRevisions.Find(document, pileId)!); editor!.SetGeometryForSmoke("1.6"); Commit();
        Check(ProjectRevisions.Find(document, pileId)!["dati"]!["generali"].D("diametro") == 1.6, "Impossibile modificare la revisione promossa");
        var oldest = ProjectRevisions.Snapshot(document, Scope().Array("revisioni")[0]!.AsObject());
        Check(ProjectSharedData.SubtreeSheets(oldest).Single(s => s.S("id") == pileId)["dati"]!["generali"].D("diametro") == 1, "Modifica della revisione promossa altera quella archiviata");
        await ShowCurrent(); await Delete();
        Check(Scope()["revisione"].D("numero") == 0 && !DeleteButton().IsEnabled && Scope().Array("revisioni").Count == 0, "Ultima versione rimasta non protetta");
        var sole = document.DeepClone(); DeleteProjectRevision(scopeId, 0);
        Check(JsonNode.DeepEquals(sole, document), "Eliminazione dell’unica versione cancella la struttura");
        ShowSheet(ProjectRevisions.Find(document, pileId)!); editor!.SetGeometryForSmoke("1.1"); Commit();
        await ShowCurrent(); CompleteProjectRevision(Scope(), "Ripartenza dalla revisione 0"); await Layout();
        Check(Scope()["revisione"].D("numero") == 1 && Scope().Array("revisioni").Count == 1, "Nuova revisione dopo eliminazione non valida");
        SwitchProjectRevision(scopeId, 0); await Layout(); await Delete();
        Check(Scope()["revisione"].D("numero") == 1 && Scope().Array("revisioni").Count == 0 && !DeleteButton().IsEnabled, "Eliminazione della sola archiviata altera quella attuale");
        Archivio.Scrivi(Path.Combine(directory, "revisioni-eliminate.programma"), document);
        var saved = Archivio.Leggi(Path.Combine(directory, "revisioni-eliminate.programma"));
        Check(JsonNode.DeepEquals(ProjectRevisions.Pack(saved), ProjectRevisions.Pack(document)), "Revisioni eliminate riappaiono alla riapertura");
        Check(saved["archivio_revisioni"]!.AsObject().Count < document["archivio_revisioni"]!.AsObject().Count, "Salvataggio conserva dati di revisioni non più referenziate");
        File.WriteAllBytes(Path.Combine(directory, "revisione-precedente-editabile.png"), Ui.Snapshot(this));
        // Historical sheets moved outside the restored branch must not acquire duplicate IDs.
        var moved = (JsonObject)sole.DeepClone(); var movedScope = ProjectRevisions.Find(moved, scopeId)!;
        ProjectRevisions.NewRevision(moved, movedScope, "Spostamento");
        var movedSheet = ProjectRevisions.Find(moved, pileId)!;
        ((JsonArray)movedSheet.Parent!).Remove(movedSheet); ProjectRevisions.Find(moved, siblingId)!.Array("fogli").Add(movedSheet);
        var untouched = ProjectRevisions.Find(moved, siblingId)!.DeepClone();
        ProjectRevisions.DeleteRevision(moved, scopeId, 1); ProjectRevisions.Validate(moved); Archivio.Valida(moved);
        Check(ProjectRevisions.Nodes(moved).Select(n => n.S("id")).Distinct().Count() == ProjectRevisions.Nodes(moved).Count(), "Ripristino duplica gli identificativi dei fogli spostati");
        Check(JsonNode.DeepEquals(untouched, ProjectRevisions.Find(moved, siblingId)) && ProjectSharedData.SubtreeSheets(movedScope).Any(s => s.S("nome") == "Portanza"), "Ripristino altera un foglio spostato in un altro ramo");
        File.WriteAllText(Path.Combine(directory, "revisioni-eliminazione.txt"), "OK: eliminazione intermedia e attuale, promozione editabile con dati e struttura precedenti, salti numerici, input non salvati e undo/redo, sottosezioni e rami separati, protezione dell’unica versione, nuova revisione e salvataggio/riapertura, fogli spostati senza duplicare ID.");
    }
}
