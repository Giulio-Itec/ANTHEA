using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Tabs;
using X.Core;

namespace Anthea.Presentation.Tests;

internal static class QuickSectionTransferChecks
{
    internal static int Run(string output)
    {
        var expect = new Expect();
        var local = new ShellBench();
        using (var workspace = local.Workspace)
        using (var windows = new ShellWindowsViewModel(workspace, new Windows(), local.Messages))
        {
            var first = workspace.NewCalculation(SheetLayout.QuickSectionModule);
            var other = workspace.NewCalculation(SheetLayout.QuickSectionModule);
            var source = (QuickSectionsSession)first.Owner;
            var document = QuickSectionDocuments.Create(SheetLayout.QuickSectionModule);
            QuickSectionDocuments.Add(document, "Destinazione");
            var destination = (QuickSectionsSession)workspace.OpenDocument(document, null, true)!;
            var target = workspace.Active!;
            ShellBench.Probe(first).Note = "bozza da trasferire";
            first.ViewModel.Data["prova_trasferimento"] = new JsonObject { ["armature"] = new JsonArray(12, 16, 20), ["combo"] = new JsonArray("SLU", "SLE") };
            var targetData = target.ViewModel.Data.DeepClone();
            expect.That(windows.CanDropTab(windows.TabOf(first), windows.TabOf(target), false)
                && windows.DropTab(windows.TabOf(first), windows.TabOf(target), false), "Unisci: trascinamento tra raccolte accettato");
            var moved = workspace.Active!;
            expect.That(first.IsDisposed && source.Sections.Count == 1 && ReferenceEquals(source.Sections[0], other.Sheet)
                && destination.Sections.Count == 2 && ReferenceEquals(moved.Owner, destination), "Unisci: si sposta solo la sezione trascinata");
            expect.That(moved.ViewModel.Data.S("nota") == "bozza da trasferire"
                && JsonNode.DeepEquals(moved.ViewModel.Data["prova_trasferimento"], first.Sheet["dati"]!["prova_trasferimento"])
                && JsonNode.DeepEquals(targetData, target.ViewModel.Data), "Unisci: bozze, dati completi e destinazione conservati");
            expect.That(moved.Id != first.Id && windows.TabOf(moved)!.Host == windows.TabOf(target)!.Host
                && destination.Sections[0] == moved.Sheet && source.IsDirty && destination.IsDirty, "Unisci: identità distinta, ordine e stato da salvare");
            var editor = moved.ViewModel;
            expect.That(windows.DropTab(windows.TabOf(moved), windows.TabOf(target), true) && ReferenceEquals(moved.ViewModel, editor)
                && destination.Sections[^1] == moved.Sheet, "Stessa raccolta: il gesto continua a riordinare senza trasferire o ricreare");
            string sourcePath = Path.Combine(output, "unisci-origine.programma"), targetPath = Path.Combine(output, "unisci-destinazione.programma");
            local.Dialogs.Then(sourcePath); workspace.Save(source, false);
            local.Dialogs.Then(targetPath); workspace.Save(destination, false);
            expect.That(QuickSectionDocuments.Sections(DocumentService.Open(sourcePath)).Count == 1
                && QuickSectionDocuments.Sections(DocumentService.Open(targetPath)).Count == 2, "Unisci: salvataggio delle due raccolte dopo lo spostamento");
            var material = workspace.NewCalculation("mat_calcestruzzo");
            expect.That(!windows.CanDropTab(windows.TabOf(material), windows.TabOf(other), false), "Unisci: moduli estranei rifiutati tra finestre");
            expect.That(local.Messages.Failures.Count == 0, "Unisci: nessun errore nei trasferimenti locali");
        }

        var origin = new ShellBench(); var receiver = new ShellBench();
        using (var sourceWorkspace = origin.Workspace)
        using (var targetWorkspace = receiver.Workspace)
        using (var sourceWindows = new ShellWindowsViewModel(sourceWorkspace, new Windows(), origin.Messages))
        using (var targetWindows = new ShellWindowsViewModel(targetWorkspace, new Windows(), receiver.Messages))
        {
            var moving = sourceWorkspace.NewCalculation(SheetLayout.QuickSectionModule);
            var hidden = sourceWorkspace.NewCalculation(SheetLayout.QuickSectionModule);
            var source = (QuickSectionsSession)moving.Owner;
            sourceWorkspace.CloseSheet(hidden);
            var destination = (QuickSectionsSession)targetWorkspace.OpenDocument((JsonObject)source.Document.DeepClone(), null, false)!;
            var target = targetWorkspace.Active!;
            var gesture = sourceWindows.BeginTabDrag(sourceWindows.TabOf(moving)!);
            expect.That(ReferenceEquals(sourceWindows.FindTabDrag(gesture.Token), gesture) && targetWindows.FindTabDrag(gesture.Token) is null,
                "Trascinamento: il token risolve solo la scheda della propria istanza");
            sourceWindows.EndTabDrag(gesture);
            expect.That(sourceWindows.FindTabDrag(gesture.Token) is null, "Trascinamento terminato: token non più utilizzabile");
            ShellBench.Probe(moving).Note = "due istanze Ω";
            string payload = sourceWorkspace.ExportSection(moving);
            expect.That(sourceWorkspace.Sheets.Contains(moving) && source.Sections.Count == 2, "Trasferimento annullato: esportare non rimuove nulla");
            var before = destination.Document.DeepClone();
            expect.Throws<ArgumentException>(() => targetWorkspace.ImportSection(payload.Replace("ANTHEA.Sezione", "Sconosciuto"), target, false),
                "Trasferimento non valido rifiutato");
            expect.That(JsonNode.DeepEquals(before, destination.Document), "Trasferimento rifiutato: destinazione intatta");
            var bridge = QuickSectionDocuments.Create(SheetLayout.QuickCompositeModule); QuickSectionDocuments.Add(bridge, "Ponte");
            expect.Throws<ArgumentException>(() => QuickSectionTransfer.PrepareImport(payload, bridge), "Unisci: c.a. e composta non si mescolano");
            var imported = targetWorkspace.ImportSection(payload, target, false);
            expect.That(sourceWorkspace.Sheets.Contains(moving) && imported.Id != moving.Id
                && destination.Sections.Select(s => s.S("id")).Distinct().Count() == 3
                && imported.ViewModel.Data.S("nota") == "due istanze Ω", "Due istanze: destinatario riceve tutto prima della rimozione, anche da copie dello stesso file");
            expect.That(sourceWorkspace.CompleteSectionExport(moving, payload) && moving.IsDisposed && source.Sections.Count == 1
                && sourceWorkspace.Sheets.Single().Id == hidden.Id && source.SelectedId == hidden.Id,
                "Due istanze: dopo la ricezione resta aperta la sezione di origine prima nascosta");

            moving = sourceWorkspace.Sheets.Single();
            payload = sourceWorkspace.ExportSection(moving);
            ShellBench.Probe(moving).Note = "modificata durante il gesto";
            expect.That(!sourceWorkspace.CompleteSectionExport(moving, payload) && !moving.IsDisposed
                && moving.ViewModel.Data.S("nota") == "modificata durante il gesto", "Trasferimento: modifiche successive impediscono la rimozione dell’originale");
            payload = sourceWorkspace.ExportSection(moving);
            string savedSource = Path.Combine(output, "unisci-originale-conservato.programma");
            origin.Dialogs.Then(savedSource); sourceWorkspace.Save(source, false);
            byte[] originalFile = File.ReadAllBytes(savedSource);
            imported = targetWorkspace.ImportSection(payload, target, true);
            expect.That(sourceWorkspace.CompleteSectionExport(moving, payload) && !sourceWorkspace.Documents.Any()
                && sourceWindows.Detached.Count == 0 && source.Lock is null, "Ultima sezione trasferita: raccolta e finestra di origine chiuse");
            expect.That(File.ReadAllBytes(savedSource).SequenceEqual(originalFile), "Unisci: il file originale sul disco non viene cancellato o riscritto");
            string savedTarget = Path.Combine(output, "unisci-raccolta-finale.programma");
            receiver.Dialogs.Then(savedTarget); targetWorkspace.Save(destination, false);
            expect.That(QuickSectionDocuments.Sections(DocumentService.Open(savedTarget)).Count == 4
                && JsonNode.DeepEquals(QuickSectionDocuments.Sections(DocumentService.Open(savedTarget)).Single(s => s.S("id") == imported.Id)["dati"], imported.ViewModel.Data),
                "Due istanze: la raccolta finale conserva tutte le sezioni e i dati trasferiti");
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
