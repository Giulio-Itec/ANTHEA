using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Windows;
using X.Core;
using Anthea.Application.Documents;
using Anthea.Application.Presentation;

namespace X.Desktop;

/// <summary>
/// --check-wall-advanced-offscreen: the advanced inputs of the walls (two zones, details, seismic bearing, settlements, Newmark), the report and
/// the proposal of the reinforcement. Since the Release switch of the walls (f5-pass-muri, 23/10/2026) on the XAML view opened by the factory
/// of the bench in a window outside the screen (<see cref="OffscreenWall"/>), driven through its controls (<see cref="MvvmWallSmoke"/>);
/// before, on the archived RetainingWallWorkspace (supporto/SUPERATI/ui-storica/muri).
/// </summary>
internal static class WallAdvancedChecks
{
    // Figures of the full wall report, in order: section, stem/toe/heel diagrams, the reinforced section (cantilever, 895bf31),
    // the global stability surface when calculated, then every page of the bar schedule (cantilever, 2cdf9fd).
    /// <summary>f5-suite-ui: the captions with <paramref name="pages"/> sheets of the bar schedule.</summary>
    internal static string[] ReportFigureCaptions(int pages, bool global)
    {
        var captions = new List<string> { "Sezione, terreno e armature", "Diagrammi Fusto", "Diagrammi Valle", "Diagrammi Monte", "Sezione armata con pieghe e sovrapposizioni" };
        if (global) captions.Add("Stabilità globale · superficie critica");
        for (int page = 1; page <= pages; page++) captions.Add("Tavola armature e distinta ferri · " + page);
        return captions.ToArray();
    }

    // One embedded image per expected figure and every caption present in the expected order.
    internal static bool ReportHasFigures(byte[] docx, IReadOnlyList<string> captions)
    {
        using var zip = new ZipArchive(new MemoryStream(docx));
        using var read = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        string xml = read.ReadToEnd(); int position = 0;
        foreach (var caption in captions)
        {
            position = xml.IndexOf(caption, position, StringComparison.Ordinal);
            if (position < 0) return false;
            position += caption.Length;
        }
        return captions.Count > 0 && zip.Entries.Count(e => e.FullName.StartsWith("word/media/")) == captions.Count;
    }

    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory); var log = new List<string>();
        File.WriteAllText(Path.Combine(directory, "esecuzione.txt"), DateTimeOffset.Now.ToString("O") + "\n" + typeof(RetainingWall).Assembly.Location);
        void Check(bool ok, string text) { if (!ok) throw new Exception(text); log.Add("OK " + text); }
        void Render(FrameworkElement v, string name)
        {
            v.UpdateLayout();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), Ui.Snapshot(v));
        }
        async Task Shot(OffscreenWall host, string name, double width, double height) => File.WriteAllBytes(Path.Combine(directory, name + ".png"), await host.Png(width, height));
        var d = RetainingWall.Defaults(); d["reinforcement"]!["two_zones"] = true; d["reinforcement"]!["lower_height"] = 1.5;
        d["reinforcement"]!["stem_upper"]!["diameter"] = 12;
        d["detailing"]!["enabled"] = true; d["detailing"]!["tie_spacing"] = 100;
        d["seismic"]!["enabled"] = true; d["seismic"]!["source"] = RetainingWall.SeismicSite; d["seismic"]!["ag_g"] = .12;
        d["seismic"]!["ss_mode"] = RetainingWall.AmplificationAssigned; d["seismic"]!["ss"] = 1.2; d["seismic"]!["st_mode"] = RetainingWall.AmplificationAssigned; d["seismic"]!["st"] = 1;
        d["serviceability"]!["settlement"] = true; d["serviceability"]!["displacement"] = true; d["serviceability"]!["removed_pressure"] = 0; d["serviceability"]!["horizontal_stiffness"] = 100000;
        d["serviceability"]!["layers"] = new JsonArray(J.Obj(("name", "Profilo dimostrativo"), ("thickness", 25), ("modulus", 30000)));
        d["serviceability"]!.Array("histories").Add(J.Obj(("enabled", true), ("name", "Impulso dimostrativo"), ("state", "SLD"), ("yield_g", .1), ("scale", 1), ("limit_mm", 20), ("compatible", true),
            ("samples", WallSheetPresentation.ParseHistory("t; a_g\n0;0\n0,2;0,2\n0,4;0\n1;0"))));
        using var host = await OffscreenWall.OpenAsync(d);
        var w = host.Wall; var sheet = host.Sheet;
        try
        {
            Check(OffscreenWall.NothingOnScreen, "Nessuna finestra sullo schermo (foglio fuori schermo)");
            Check(w.CommandCaption("DesignRebarCommand") == "Calcola armature", "Comando di predimensionamento presente");
            Check(sheet.Forms.ContainsKey("bearing_seismic") && sheet.Forms.ContainsKey("serviceability") && sheet.Forms.ContainsKey("gravity_design"), "Input completi dei nuovi modelli");
            Check(sheet.Forms["rebar_stem"].Contains("opposite_diameter"), "Editor facce indipendenti");
            Check(WallSheetPresentation.ParseHistory("0;0\n1;0.2").Count == 2, "Importazione accelerogrammi");
            try { WallSheetPresentation.ParseHistory("0;0\n0;0.2"); throw new Exception("Tempi duplicati accettati"); } catch (ArgumentException) { Check(true, "CSV non valido non importato"); }
            await w.CalculateAsync(); await host.Settle(); Check(w.Calculation is not null, "Calcolo completo da interfaccia");
            Check(w.Calculation!.Serviceability!.Earthquakes.Count == 1, "Newmark collegato al modulo");
            Check(w.Calculation.Serviceability.Cases.All(c => c.StemDisplacement > .1), "Spostamento GPC del fusto aggiornato e non nullo");
            w.Page = 1; w.ViewMode = "Armature"; await host.Settle();
            Render(host.View.DiagramsDrawing, "armature-sezione");
            await Shot(host, "verifiche-armature-1600", 1600, 1000);
            w.CheckFilter = "Cedimenti e spostamenti";
            await Shot(host, "cedimenti-1600", 1600, 1000);
            w.Combination = w.Calculation.Cases.First(c => c.State == "SISMA").Name;
            w.CheckFilter = "Portanza sismica";
            await Shot(host, "portanza-sismica-1600", 1600, 1000);
            var bytes = host.Editor.BuildReport("Esempio muri portanza sismica cedimenti e armature", []);
            File.WriteAllBytes(Path.Combine(directory, "esempio-completo.docx"), bytes);
            using (var zip = new ZipArchive(new MemoryStream(bytes)))
            using (var read = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
            {
                var xml = read.ReadToEnd();
                Check(xml.Contains("Armature per metro di muro") && xml.Contains("Newmark") && xml.Contains("Portanza sismica con inerzia"), "Relazione Word con nuovi capitoli");
                Check(zip.Entries.Count(e => e.FullName.EndsWith(".png")) == zip.Entries.Count(e => e.FullName.StartsWith("word/media/")), "Figure della relazione in PNG");
            }
            var captions = w.ReportFigureCaptions(global: false);
            Check(captions.Length >= 7 && ReportHasFigures(bytes, captions), "Relazione con quinta figura delle armature e tavole della distinta: " + string.Join(" | ", captions));
            var doc = Archivio.Documento(RetainingWall.Module); doc["dati"] = w.Data.DeepClone();
            DocumentService.Save(Path.Combine(directory, "esempio-completo.anthea"), doc);
            Check(J.Equivalent(DocumentService.Open(Path.Combine(directory, "esempio-completo.anthea"))["dati"], w.Data), "Modello ripercorribile con accelerogramma");
            File.WriteAllText(Path.Combine(directory, "risultati.json"), w.Calculation.Json().ToJsonString(J.Options));
            w.Page = 0; w.Expand("Geometria");
            await Shot(host, "input-1050", 1050, 1000);
            Check(w.PageCount == 2, "Due schede conservate");
            using (var designer = await OffscreenWall.OpenAsync(RetainingWall.Defaults()))
            {
                var p = designer.Wall;
                string original = p.Data["reinforcement"]!.ToJsonString();
                await p.CalculateAsync();
                p.Command("DesignRebarCommand"); await p.WaitIdle(); await designer.Settle();
                Check(p.Data["reinforcement"]!.ToJsonString() == original, "Proposta UI separata prima di Applica");
                p.Click("Applica proposta"); await designer.Settle();
                Check(p.Data["reinforcement"]!.ToJsonString() != original && p.Data["detailing"].B("enabled"), "Applica trasferisce le barre proposte e abilita i dettagli");
                await p.CalculateAsync(); await designer.Settle();
                Check(p.Calculation is not null && designer.View.ViewChoice.SelectedItem as string == "Armature", "Proposta applicata ricalcolata e visualizzata");
                var designed = Archivio.Documento(RetainingWall.Module); designed["dati"] = p.Data.DeepClone();
                DocumentService.Save(Path.Combine(directory, "predimensionamento.anthea"), designed);
            }
            Check(OffscreenWall.NothingOnScreen, "Nessuna interazione con il desktop");
            log.Add("PASS " + log.Count + " controlli interfaccia avanzata");
        }
        finally { File.WriteAllLines(Path.Combine(directory, "test.txt"), log); }
    }
}
