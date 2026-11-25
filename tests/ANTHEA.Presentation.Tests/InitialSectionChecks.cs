using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Tabs;

namespace Anthea.Presentation.Tests;

internal static class InitialSectionChecks
{
    internal static int Run(string output)
    {
        var expect = new Expect();
        var document = QuickSectionDocuments.Create(SheetLayout.QuickSectionModule);
        QuickSectionDocuments.Add(document, "A"); QuickSectionDocuments.Add(document, "B");
        string path = Path.Combine(output, "apri-sostituisce-iniziale.programma");
        DocumentService.Save(path, document);
        foreach (string state in new[] { "intatta", "bozza", "nome", "salvata", "piu-sezioni", "file-aperto" })
        {
            var bench = new ShellBench();
            using var workspace = bench.Workspace;
            using var windows = new ShellWindowsViewModel(workspace, new Windows(), bench.Messages);
            SheetSession initial;
            if (state == "file-aperto")
            {
                var copy = QuickSectionDocuments.Create(SheetLayout.QuickSectionModule);
                QuickSectionDocuments.Add(copy, "Sezione 1");
                workspace.OpenDocument(copy, null, true); initial = workspace.Active!;
            }
            else initial = workspace.NewCalculation(SheetLayout.QuickSectionModule);
            if (state == "bozza") ShellBench.Probe(initial).Note = "modifica non ancora confermata";
            if (state == "nome") initial.Sheet["nome"] = "Nome assegnato";
            if (state == "salvata")
            {
                bench.Dialogs.Then(Path.Combine(output, "iniziale-salvata.programma")); workspace.Save(initial.Owner, false);
            }
            if (state == "piu-sezioni") workspace.NewCalculation(SheetLayout.QuickSectionModule);
            var opened = workspace.OpenFile(path);
            bool replace = state == "intatta";
            expect.That(opened is QuickSectionsSession && initial.IsDisposed == replace
                && workspace.Documents.Count() == (replace ? 1 : 2) && windows.Detached.Count == (replace ? 1 : 2),
                "Apri iniziale: " + state + (replace ? " sostituita" : " conservata"));
            if (replace)
                expect.That(workspace.Sheets.Count == 2 && workspace.Active?.Heading == "B" && windows.Main.Tabs.Count == 0,
                    "Apri iniziale: solo le sezioni del file, selezione corretta e nessun passaggio dalla principale");
            if (state == "bozza")
                expect.That(initial.ViewModel.Data["nota"]!.ToString() == "modifica non ancora confermata", "Apri iniziale: bozza conservata");
        }
        {
            var bench = new ShellBench();
            using var workspace = bench.Workspace;
            using var windows = new ShellWindowsViewModel(workspace, new Windows(), bench.Messages);
            var initial = workspace.NewCalculation(SheetLayout.QuickSectionModule);
            var invalid = (JsonObject)document.DeepClone(); invalid["attiva"] = "inesistente";
            expect.Throws<ArgumentException>(() => workspace.OpenDocument(invalid, null, false), "Apri iniziale: documento non valido rifiutato");
            expect.That(!initial.IsDisposed && workspace.Sheets.Single() == initial && windows.Detached.Count == 1,
                "Apri iniziale fallita: sezione e finestra originali conservate");
            workspace.OpenFile(path);
            workspace.Activate(null);
            var secondInitial = workspace.NewCalculation(SheetLayout.QuickSectionModule);
            workspace.OpenFile(path);
            expect.That(secondInitial.IsDisposed && workspace.Documents.Count() == 1 && windows.Detached.Count == 1,
                "Apri file già aperto: attiva il file e rimuove la nuova sezione iniziale intatta");
            expect.That(bench.Messages.Failures.Count == 0, "Apri iniziale: nessun errore segnalato");
        }
        return expect.Count;
    }

    private sealed class Windows : IWindowService
    {
        public void Open(TabHostViewModel host) { }
        public void Activate(TabHostViewModel host) { }
        public void Close(TabHostViewModel host) { }
    }
}
