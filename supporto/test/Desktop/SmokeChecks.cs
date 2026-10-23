using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;
using Anthea.Application.Documents;

namespace X.Desktop;

internal sealed partial class ShellSmokeWindow
{
    internal async Task Smoke(string directory, JsonArray cases)
    {
        Directory.CreateDirectory(directory); var log = new List<string>();
        async Task Capture(string name) { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, name + ".png"), Ui.Snapshot(Window)); }
        ShowHome(); await Capture("home"); ShowModules(); await Capture("moduli"); ShowProjects(); await Capture("progetti");
        document = Archivio.Documento("geo_palo_verticale"); ShowSheet(document); await Capture("palo_vuoto"); await openSheet.WaitForAutomatic();
        foreach (var (kind, module, name) in new[] { ("palo", "geo_palo_verticale", "palo_storico_0"), ("micropalo", "geo_micropalo_verticale", "micropalo_IRS_45_Feld"), ("sezione", "str_palo", "") })
        {
            JsonObject data = kind == "sezione" ? SezioneCA.DefaultData() : cases.First(c => c.S("nome") == name)!["input"]!.AsObject();
            document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", module), ("dati", data)); currentSheet = null; dirty = false; ShowSheet(document);
            if (kind == "sezione")
            {
                var activeEditor = editor!; var calculation = activeEditor.CalculateAsync();
                await Dispatcher.Yield(DispatcherPriority.Background);
                if (!activeEditor.Busy) throw new Exception("Sezione: calcolo asincrono non avviato");
                ShowHome();
                if (!ReferenceEquals(body.Content, dashboardViewport) || !ReferenceEquals(editor, activeEditor)) throw new Exception("Sezione: navigazione Home bloccata durante il calcolo");
                ResumeCalculation();
                if (!ReferenceEquals(body.Content, moduleView) || !ReferenceEquals(editor, activeEditor)) throw new Exception("Sezione: ripresa calcolo non conserva l'editor");
                await calculation;
            }
            else await editor!.CalculateAsync();
            if (editor.Result is null || editor.Result.S("errore") != "") throw new Exception(kind + ": calcolo non riuscito");
            var expected = kind == "sezione" ? null : Calcolo.Calcola(editor.Data, kind == "micropalo");
            if (kind != "sezione" && !JsonNode.DeepEquals(expected, editor.Result)) throw new Exception(kind + ": risultato WPF diverso dal Core");
            await Capture(kind); Commit(); string file = Path.Combine(directory, kind + ".programma"); DocumentService.Save(file, document);
            if (!ConcretePersistenceComparison.Equivalent(DocumentService.Open(file), document)) throw new Exception("Round trip " + kind);
            editor.ExportResult(Path.Combine(directory, kind + ".json"));
            if (kind != "sezione") editor.ExportReport(Path.Combine(directory, kind + ".docx"), kind, ReportWord.Sezioni.Select(s => s.Key).ToHashSet());
            await openSheet.VerifyWpf(directory, kind); log.Add(kind + ": layout WPF, input, invalidazione, calcoli, archivi, export OK");
            double width = Width, height = Height; Width = 1366; Height = 850; await Capture(kind + "_1366"); Width = width; Height = height;
            ShowHome(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); body.Content = moduleView; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            if (editor.Result is null) throw new Exception("Navigazione ha perso i risultati " + kind);
        }
        // A real project tree selection must commit the previous editor before opening another sheet.
        Commit(); editor!.Dispose(); editor = null; currentSheet = null;
        var first = J.Obj(("id", "f1"), ("nome", "Sezione A"), ("modulo_id", "str_palo"), ("dati", SezioneCA.DefaultData()));
        var second = J.Obj(("id", "f2"), ("nome", "Sezione B"), ("modulo_id", "str_palo"), ("dati", SezioneCA.DefaultData()));
        var structure = J.Obj(("id", "s1"), ("nome", "Struttura"), ("fogli", new JsonArray(first, second)));
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray(J.Obj(("id", "p1"), ("nome", "Progetto WPF"), ("strutture", new JsonArray(structure))))));
        first = document["progetti"]![0]!["strutture"]![0]!["fogli"]![0]!.AsObject();
        second = document["progetti"]![0]!["strutture"]![0]!["fogli"]![1]!.AsObject();
        ShowProjects(); await Capture("progetti_popolati");
        SelectInTree(first); ShowSheet(first);
        openSheet.SetGeometryForSmoke("1100"); ShowProjects(); SelectInTree(second); Commit(); ShowSheet(second);
        if (first["dati"]?["input"].S("diameter_mm") != "1100" || currentSheet != second) throw new Exception("Cambio foglio non conserva i dati");
        Commit(); string archive = Path.Combine(directory, "progetti.programma"); DocumentService.Save(archive, document);
        if (!ConcretePersistenceComparison.Equivalent(DocumentService.Open(archive), document)) throw new Exception("Round trip progetti WPF");
        log.Add("Progetti: selezione, cambio foglio, conservazione input e salvataggio OK");
        File.WriteAllLines(Path.Combine(directory, "smoke.txt"), log); dirty = false;
    }
}

// F5.13 g: the partial of the historic SheetEditor (SetGeometryForSmoke, WaitForAutomatic, VerifyWpf, without historic sheets since 23/10) went with
// it to supporto/SUPERATI/ui-storica/shell; OpenSheet (tests/Desktop/OpenSheetChecks.cs) drives the XAML views.
