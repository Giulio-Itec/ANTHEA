using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal void FinishSmoke() { testing = true; dirty = false; editor?.Dispose(); }
    internal async Task Smoke(string directory, JsonArray cases)
    {
        testing = true; Directory.CreateDirectory(directory); var log = new List<string>();
        async Task Capture(string name) { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, name + ".png"), Ui.Snapshot(this)); }
        ShowHome(); await Capture("home"); ShowModules(); await Capture("moduli"); ShowProjects(); await Capture("progetti");
        document = Archivio.Documento("geo_palo_verticale"); ShowSheet(document); await Capture("palo_vuoto"); await editor!.WaitForAutomatic();
        foreach (var (kind, module, name) in new[] { ("palo", "geo_palo_verticale", "palo_storico_0"), ("micropalo", "geo_micropalo_verticale", "micropalo_IRS_45_Feld"), ("sezione", "str_palo", "") })
        {
            JsonObject data = kind == "sezione" ? SezioneCA.DefaultData() : cases.First(c => c.S("nome") == name)!["input"]!.AsObject();
            document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", module), ("dati", data)); currentSheet = null; dirty = false; ShowSheet(document); await editor!.CalculateAsync();
            if (editor.Result is null || editor.Result.S("errore") != "") throw new Exception(kind + ": calcolo non riuscito");
            var expected = kind == "sezione" ? null : Calcolo.Calcola(editor.Data, kind == "micropalo");
            if (kind != "sezione" && !JsonNode.DeepEquals(expected, editor.Result)) throw new Exception(kind + ": risultato WPF diverso dal Core");
            await Capture(kind); Commit(); string file = Path.Combine(directory, kind + ".programma"); Archivio.Scrivi(file, document);
            if (!JsonNode.DeepEquals(Archivio.Leggi(file), document)) throw new Exception("Round trip " + kind);
            editor.ExportResult(Path.Combine(directory, kind + ".json"));
            if (kind != "sezione") editor.ExportReport(Path.Combine(directory, kind + ".docx"), kind, ReportWord.Sezioni.Select(s => s.Key).ToHashSet());
            await editor.VerifyWpf(directory, kind); log.Add(kind + ": layout WPF, input, invalidazione, calcoli, archivi, export OK");
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
        var projectNode = (TreeViewItem)tree.Items[0]; var structureNode = (TreeViewItem)projectNode.Items[0]; ((TreeViewItem)structureNode.Items[0]).IsSelected = true;
        editor!.SetGeometryForSmoke("1100"); ShowProjects(); ((TreeViewItem)((TreeViewItem)((TreeViewItem)tree.Items[0]).Items[0]).Items[1]).IsSelected = true;
        if (first["dati"]?["input"].S("diameter_mm") != "1100" || currentSheet != second) throw new Exception("Cambio foglio non conserva i dati");
        Commit(); string archive = Path.Combine(directory, "progetti.programma"); Archivio.Scrivi(archive, document);
        if (!JsonNode.DeepEquals(Archivio.Leggi(archive), document)) throw new Exception("Round trip progetti WPF");
        log.Add("Progetti: selezione, cambio foglio, conservazione input e salvataggio OK");
        File.WriteAllLines(Path.Combine(directory, "smoke.txt"), log); dirty = false;
    }
}

internal sealed partial class SheetEditor
{
    internal void SetGeometryForSmoke(string value) { if (concrete is not null) concrete.SetGeometry(value); else generalForm.Set("diametro", value); }
    internal async Task WaitForAutomatic()
    {
        for (int i = 0; i < 200 && (timer.IsEnabled || Busy); i++) await Task.Delay(50);
        if (timer.IsEnabled || Busy) throw new Exception("Calcolo automatico non terminato");
    }
    internal async Task VerifyWpf(string directory, string name)
    {
        if (concrete is not null) { await concrete.VerifyWorkspace(directory); return; }
        void Assert(bool value, string message) { if (!value) throw new Exception(name + ": " + message); }
        async Task Capture(string suffix) { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, name + suffix + ".png"), Ui.Snapshot(this)); }
        Assert(cards.Count == (Section ? 8 : 7), "Numero di pannelli diverso");
        Assert(cards.All(c => c.ActualWidth >= 100 && c.ActualHeight >= 100), "Pannelli troppo piccoli");
        var original = (JsonObject)Data.DeepClone(); string key = Section ? "diameter_mm" : "diametro", value = generalForm.Get(key);
        generalForm.Set(key, value + " "); Assert(Result is null, "Risultati non invalidati"); generalForm.Set(key, value);
        Assert(JsonNode.DeepEquals(original, Data), "Modifica/ripristino campo altera dati");
        if (Pile) await WaitForAutomatic(); else await CalculateAsync(); Assert(Result is not null, "Ricalcolo non riuscito");
        if (!Section)
        {
            var unchanged = (JsonObject)Data.DeepClone(); capacityView.SelectedIndex = 1; capacityView.SelectedIndex = 0;
            Assert(JsonNode.DeepEquals(unchanged, Data), "Il filtro modifica gli input");
            expanded = 6; LayoutCards(); await Capture("_grafico_esteso"); Assert(cards.Take(6).All(c => c.Visibility == Visibility.Collapsed), "Espansione grafico");
            expanded = -1; LayoutCards(); Assert(cards.All(c => c.Visibility == Visibility.Visible), "Ripristino pannelli");
            outputs.SelectedIndex = 2; expanded = 6; LayoutCards(); await Capture(Micro ? "_abachi" : "_nq"); outputs.SelectedIndex = 0; expanded = -1; LayoutCards();
            if (layerGrids.Count > 0 && layerGrids[0].Rows.Count > 0)
            {
                var row = layerGrids[0].Rows[0]; string thickness = row.Values.S("spessore"); row["spessore"] = thickness + " "; Assert(Result is null, "Modifica cella non invalida"); row["spessore"] = thickness;
                if (Pile) await WaitForAutomatic(); else await CalculateAsync(); Assert(Result is not null, "Ricalcolo da tabella");
            }
        }
        if (Pile)
        {
            outputs.SelectedIndex = 1; tableSelect.SelectedIndex = 0;
            await Capture("_tabelle_compatte");
            var capacityTables = Ui.Descendants<DataGrid>(tableHost).ToArray();
            Assert(capacityTables.Length == 2 && capacityTables.All(t => t.Columns.Count == 4 && t.Items.Count == 3), "Riepilogo compatto drenate/non drenate");
            expanded = 6; LayoutCards(); await Capture("_tabelle_compatte_estese"); expanded = -1; LayoutCards();
            outputs.SelectedIndex = 0;
            expanded = 4; LayoutCards(); await Capture("_stratigrafia_estesa"); expanded = -1; LayoutCards();
            string length = generalForm.Get("lunghezza"); generalForm.Set("lunghezza", "abc"); await WaitForAutomatic(); Assert(Result is null && plot.Series.Count == 0, "Input invalido conserva risultati");
            generalForm.Set("lunghezza", "3"); generalForm.Set("lunghezza", length); await WaitForAutomatic(); Assert(Result is not null, "Ripristino calcolo automatico");
            var duplicated = Data.Array("stratigrafie")[0]!.DeepClone(); duplicated!.AsArray().Last()!["angolo_attrito"] = "32"; Data.Array("stratigrafie").Add(duplicated); RebuildSondages(1); Changed(); await WaitForAutomatic();
            Assert(stratigraphy.VisibleIndices.SequenceEqual(new[] { 1 }), "Selezione profilo");
            Assert(reference.Markers.Count == 1 && reference.Markers[0].X == 32, "Selezione Nq");
            expanded = 5; LayoutCards(); Assert(stratigraphy.VisibleIndices.Length == 2 && cards[5].Visibility == Visibility.Visible, "Espansione profilo"); await Capture("_profili");
            expanded = -1; Data.Array("stratigrafie").RemoveAt(1); RebuildSondages(0); Changed(); LayoutCards(); await WaitForAutomatic();
        }
        Assert(Result is not null, "Risultato finale assente");
    }
}
