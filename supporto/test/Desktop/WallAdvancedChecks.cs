using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal static class WallAdvancedChecks
{
    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory); var log = new List<string>();
        File.WriteAllText(Path.Combine(directory, "esecuzione.txt"), DateTimeOffset.Now.ToString("O") + "\n" + typeof(RetainingWall).Assembly.Location);
        void Check(bool ok, string text) { if (!ok) throw new Exception(text); log.Add("OK " + text); }
        void Render(FrameworkElement v, string name, double width = 1100, double height = 500)
        {
            v.Width = width; v.Height = height; v.Measure(new(width, height)); v.Arrange(new(0, 0, width, height)); v.UpdateLayout();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), Ui.Snapshot(v));
        }
        var d = RetainingWall.Defaults(); d["reinforcement"]!["two_zones"] = true; d["reinforcement"]!["lower_height"] = 1.5;
        d["reinforcement"]!["stem_upper"]!["diameter"] = 12;
        d["detailing"]!["enabled"] = true; d["detailing"]!["tie_spacing"] = 100;
        d["seismic"]!["enabled"] = true; d["seismic"]!["source"] = RetainingWall.SeismicSite; d["seismic"]!["ag_g"] = .12;
        d["seismic"]!["ss_mode"] = RetainingWall.AmplificationAssigned; d["seismic"]!["ss"] = 1.2; d["seismic"]!["st_mode"] = RetainingWall.AmplificationAssigned; d["seismic"]!["st"] = 1;
        d["serviceability"]!["settlement"] = true; d["serviceability"]!["displacement"] = true; d["serviceability"]!["removed_pressure"] = 0; d["serviceability"]!["horizontal_stiffness"] = 100000;
        d["serviceability"]!["layers"] = new JsonArray(J.Obj(("name", "Profilo dimostrativo"), ("thickness", 25), ("modulus", 30000)));
        d["serviceability"]!.Array("histories").Add(J.Obj(("enabled", true), ("name", "Impulso dimostrativo"), ("state", "SLD"), ("yield_g", .1), ("scale", 1), ("limit_mm", 20), ("compatible", true),
            ("samples", RetainingWallWorkspace.ParseHistory("t; a_g\n0;0\n0,2;0,2\n0,4;0\n1;0"))));
        using var w = new RetainingWallWorkspace(d);
        try
        {
            Check(Application.Current.Windows.Count == 0, "Nessuna finestra nativa");
            Check(w.DesignButton.Content as string == "Calcola armature", "Comando di predimensionamento presente");
            Check(w.Forms.ContainsKey("bearing_seismic") && w.Forms.ContainsKey("serviceability") && w.Forms.ContainsKey("gravity_design"), "Input completi dei nuovi modelli");
            Check(w.Forms["rebar_stem"].Editors.ContainsKey("opposite_diameter"), "Editor facce indipendenti");
            Check(RetainingWallWorkspace.ParseHistory("0;0\n1;0.2").Count == 2, "Importazione accelerogrammi");
            try { RetainingWallWorkspace.ParseHistory("0;0\n0;0.2"); throw new Exception("Tempi duplicati accettati"); } catch (ArgumentException) { Check(true, "CSV non valido non importato"); }
            await w.CalculateAsync(); Check(w.Calculation is not null, "Calcolo completo da interfaccia");
            Check(w.Calculation!.Serviceability!.Earthquakes.Count == 1, "Newmark collegato al modulo");
            Check(w.Calculation.Serviceability.Cases.All(c => c.StemDisplacement > .1), "Spostamento GPC del fusto aggiornato e non nullo");
            w.Pages.SelectedIndex = 1; w.ViewMode.SelectedItem = "Armature";
            Render(w.Diagrams, "armature-sezione");
            Render(w, "verifiche-armature-1600", 1600, 1000);
            w.CheckFilter.SelectedItem = "Cedimenti e spostamenti";
            Render(w, "cedimenti-1600", 1600, 1000);
            w.Combination.SelectedItem = w.Calculation.Cases.First(c => c.State == "SISMA").Name;
            w.CheckFilter.SelectedItem = "Portanza sismica";
            Render(w, "portanza-sismica-1600", 1600, 1000);
            var bytes = w.BuildReport("Esempio muri portanza sismica cedimenti e armature");
            File.WriteAllBytes(Path.Combine(directory, "esempio-completo.docx"), bytes);
            using (var zip = new ZipArchive(new MemoryStream(bytes)))
            using (var read = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
            {
                var xml = read.ReadToEnd();
                Check(xml.Contains("Distinta di predimensionamento") && xml.Contains("Newmark") && xml.Contains("Portanza sismica con inerzia"), "Relazione Word con nuovi capitoli");
                Check(zip.Entries.Count(e => e.FullName.EndsWith(".png")) == 5, "Relazione con quinta figura delle armature");
            }
            var doc = Archivio.Documento(RetainingWall.Module); doc["dati"] = w.Data.DeepClone();
            Archivio.Scrivi(Path.Combine(directory, "esempio-completo.anthea"), doc);
            Check(J.Equivalent(Archivio.Leggi(Path.Combine(directory, "esempio-completo.anthea"))["dati"], w.Data), "Modello ripercorribile con accelerogramma");
            File.WriteAllText(Path.Combine(directory, "risultati.json"), w.Calculation.Json().ToJsonString(J.Options));
            w.Pages.SelectedIndex = 0; w.Cards["Geometria"].IsExpanded = true;
            Render(w, "input-1050", 1050, 1000);
            Check(w.Pages.Items.Count == 2, "Due schede conservate");
            using (var designer = new RetainingWallWorkspace(RetainingWall.Defaults()))
            {
                string original = designer.Data["reinforcement"]!.ToJsonString();
                await designer.CalculateAsync(); await designer.DesignRebarAsync();
                Check(designer.Data["reinforcement"]!.ToJsonString() == original, "Proposta UI separata prima di Applica");
                var apply = Ui.Descendants<Button>((FrameworkElement)designer.designPreview.Content).Single(b => b.Content as string == "Applica proposta");
                apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(designer.Data["reinforcement"]!.ToJsonString() != original && designer.Data["detailing"].B("enabled"), "Applica trasferisce le barre proposte e abilita i dettagli");
                await designer.CalculateAsync();
                Check(designer.Calculation is not null && designer.ViewMode.SelectedItem as string == "Armature", "Proposta applicata ricalcolata e visualizzata");
                var designed = Archivio.Documento(RetainingWall.Module); designed["dati"] = designer.Data.DeepClone();
                Archivio.Scrivi(Path.Combine(directory, "predimensionamento.anthea"), designed);
            }
            Check(Application.Current.Windows.Count == 0, "Nessuna interazione con il desktop");
            log.Add("PASS " + log.Count + " controlli interfaccia avanzata");
        }
        finally { File.WriteAllLines(Path.Combine(directory, "test.txt"), log); }
    }
}
