using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Tabs;
using X.Core;

namespace Anthea.Presentation.Tests;

internal static class QuickSectionsFileChecks
{
    internal static int Run(string output)
    {
        var expect = new Expect(); var bench = new ShellBench();
        string path = Path.GetFullPath(Path.Combine(output, "tutte-sezioni.programma"));
        using (var workspace = bench.Workspace)
        using (var windows = new ShellWindowsViewModel(workspace, new Windows(), bench.Messages))
        {
            bool touchedMain = false;
            windows.Main.Tabs.CollectionChanged += (_, _) => touchedMain = true;
            var first = workspace.NewCalculation(SheetLayout.QuickSectionModule);
            var second = workspace.NewCalculation(SheetLayout.QuickSectionModule);
            expect.That(!touchedMain, "Nuove sezioni: nessun passaggio dalla finestra principale");
            var group = (QuickSectionsSession)first.Owner;
            expect.That(ReferenceEquals(second.Owner, group) && workspace.Documents.Count() == 1, "Salva sezioni: un documento per tutte le sezioni");
            ShellBench.Probe(first).Note = "prima non attiva";
            ShellBench.Probe(second).Note = "seconda attiva";
            first.ViewModel.Data["input"]!["width_mm"] = "550";
            ConcreteQuickSection.ReplaceCombinations(first.ViewModel.Data, [new("SLU A", -900, 310, 0), new("SLE A", -400, 120, 0, "SLE")]);
            ConcreteQuickSection.WriteCreep(first.ViewModel.Data, "2");
            ConcreteQuickSection.View(first.ViewModel.Data)["titolo"] = "Spalla";
            ConcreteQuickSection.View(second.ViewModel.Data)["titolo"] = "Pila";
            windows.Reorder(windows.TabOf(second), windows.TabOf(first), false);
            bench.Dialogs.Then(path);
            expect.That(workspace.Save(group, false) && first.Owner.Path == path && second.Owner.Path == path && !group.IsDirty,
                "Salva sezioni: primo salvataggio con un solo percorso condiviso");
            var saved = DocumentService.Open(path); var items = QuickSectionDocuments.Sections(saved);
            expect.That(saved.S("tipo") == QuickSectionDocuments.Kind && items.Count == 2 && items[0].S("id") == second.Id && saved.S("attiva") == second.Id,
                "Salva sezioni: ordine e selezione nel file");
            expect.That(JsonNode.DeepEquals(items[1]["dati"], first.ViewModel.Data) && JsonNode.DeepEquals(items[0]["dati"], second.ViewModel.Data)
                && items[1]["dati"].S("nota") == "prima non attiva", "Salva sezioni: tutti i dati e le bozze della scheda non attiva conservati");
            ShellBench.Probe(first).Note = "prima sovrascritta"; ShellBench.Probe(second).Note = "seconda sovrascritta";
            expect.That(workspace.Save(group, false) && bench.Dialogs.Requests.Count == 1
                && QuickSectionDocuments.Sections(DocumentService.Open(path)).All(s => s["dati"].S("nota").EndsWith("sovrascritta", StringComparison.Ordinal)),
                "Salva sezioni: sovrascrittura dello stesso file, tutte le sezioni, nessun secondo dialogo");
            bench.Dialogs.Then(null);
            expect.That(!workspace.Save(group, true) && group.Path == path && QuickSectionDocuments.Sections(DocumentService.Open(path)).Count == 2,
                "Salva sezioni: Salva con nome annullato lascia il file e la raccolta intatti");
            string copy = Path.Combine(output, "copia-tutte-sezioni.programma"); bench.Dialogs.Then(copy);
            expect.That(workspace.Save(group, true) && group.Path == copy && JsonNode.DeepEquals(DocumentService.Open(copy), DocumentService.Open(path)),
                "Salva sezioni: Salva con nome copia l’intera raccolta e conserva il file originale");
        }
        var reopened = new ShellBench();
        using (var workspace = reopened.Workspace)
        using (var windows = new ShellWindowsViewModel(workspace, new Windows(), reopened.Messages))
        {
            bool touchedMain = false;
            windows.Main.Tabs.CollectionChanged += (_, _) => touchedMain = true;
            var group = (QuickSectionsSession)workspace.OpenFile(path)!;
            expect.That(!touchedMain, "Apri sezioni: nessun passaggio dalla finestra principale");
            expect.That(workspace.Sheets.Count == 2 && windows.Detached.Count == 1 && windows.Detached[0].Tabs.Count == 2
                && workspace.Active!.Id == group.SelectedId, "Apri sezioni: tutte le schede, stessa finestra e selezione ripristinata");
            expect.That(windows.Detached[0].Tabs.OfType<SheetTabViewModel>().Select(t => t.Session.Id).SequenceEqual(group.Sections.Select(s => s.S("id")))
                && workspace.Sheets.All(s => s.ViewModel.Data.S("nota").EndsWith("sovrascritta", StringComparison.Ordinal)), "Apri sezioni: ordine e modifiche di entrambe le sezioni ripristinati");
            expect.That(ReferenceEquals(workspace.OpenFile(path), group) && workspace.Documents.Count() == 1 && workspace.Sheets.Count == 2,
                "Apri sezioni: stesso file già aperto senza duplicare schede");
            var first = workspace.Sheets[0];
            expect.That(workspace.CloseSheet(first) && group.Sections.Count == 2 && workspace.Save(group, false), "Salva sezioni: chiudere una scheda non elimina la sezione salvata");
            workspace.OpenFile(path);
            expect.That(workspace.Sheets.Count == 2, "Apri sezioni: riapre anche una scheda precedentemente chiusa");
            var third = workspace.NewCalculation(SheetLayout.QuickSectionModule);
            expect.That(ReferenceEquals(third.Owner, group) && group.Sections.Count == 3 && workspace.Save(group, false)
                && QuickSectionDocuments.Sections(DocumentService.Open(path)).Count == 3, "Salva sezioni: nuova sezione aggiunta allo stesso file");
        }
        var broken = DocumentService.Open(path); broken["sezioni"]![1]!["id"] = broken["sezioni"]![0]!.S("id");
        string before = File.ReadAllText(path);
        expect.Throws<ArgumentException>(() => DocumentService.Save(path, broken), "Salva sezioni: identità duplicate rifiutate");
        expect.That(File.ReadAllText(path) == before, "Salva sezioni: validazione fallita non sostituisce il file esistente");
        broken = DocumentService.Open(path); broken["attiva"] = "inesistente";
        expect.Throws<ArgumentException>(() => DocumentService.Save(path, broken), "Salva sezioni: selezione inesistente rifiutata");
        broken = DocumentService.Open(path); broken["sezioni"]![0]!["modulo_id"] = SheetLayout.QuickCompositeModule;
        expect.Throws<ArgumentException>(() => DocumentService.Save(path, broken), "Salva sezioni: modulo estraneo alla raccolta rifiutato");
        var bridge = QuickSectionDocuments.Create(SheetLayout.QuickCompositeModule);
        QuickSectionDocuments.Add(bridge, "Ponte A"); QuickSectionDocuments.Add(bridge, "Ponte B");
        string bridgePath = Path.Combine(output, "sezioni-ponte.programma"); DocumentService.Save(bridgePath, bridge);
        expect.That(JsonNode.DeepEquals(DocumentService.Open(bridgePath), bridge), "Salva sezioni: raccolta della sezione composta da ponte interamente ripristinata");
        expect.That(bench.Messages.Failures.Count == 0 && reopened.Messages.Failures.Count == 0, "Salva sezioni: nessun errore segnalato");
        return expect.Count;
    }

    private sealed class Windows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
    }
}
