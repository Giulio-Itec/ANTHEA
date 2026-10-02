using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

internal static class WikiChecks
{
    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory); var checks = new List<string>();
        void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); checks.Add(label); }
        Check(WikiCatalog.Articles.Length >= 60, "Documentazione globale indicizzata senza sorgenti duplicati");
        Check(WikiCatalog.Articles.Select(a => a.Id).Distinct().Count() == WikiCatalog.Articles.Length, "Route univoche");
        void CheckLink(string uri)
        {
            var target = WikiCatalog.Resolve(uri);
            Check(target is not null, "Destinazione valida · " + uri);
            if (uri.Contains('#'))
                Check(Regex.Matches(WikiCatalog.Body(target!), @"^#{3,4} (.+)", RegexOptions.Multiline)
                    .Any(m => WikiCatalog.Slug(m.Groups[1].Value.Trim()) == uri.Split('#', 2)[1]), "Sezione di destinazione esistente · " + uri);
        }
        var formulaErrors = new List<string>();
        foreach (var a in WikiCatalog.Articles)
        {
            var body = WikiCatalog.Body(a);
            foreach (Match block in Regex.Matches(body, @"```math\r?\n(.*?)```", RegexOptions.Singleline))
                foreach (var latex in block.Groups[1].Value.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        var rendered = WikiEditorial.Formula(latex);
                        rendered.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        Check(rendered.DesiredSize.Width > 0 && rendered.DesiredSize.Height > 0, "Formula LaTeX composta · " + latex.Trim());
                    }
                    catch (Exception ex) { formulaErrors.Add(ex.Message); }
                }
            Check(body.StartsWith("## ") && !body.Contains('\ufffd'), "Offset UTF8 valido · " + a.Id);
            foreach (var m in a.Modules) Check(ModuleCatalog.Get(m) is not null, "Modulo esistente · " + m);
            Check(a.Related.Distinct().Count() == a.Related.Length && !a.Related.Contains(a.Id), "Correlati senza duplicati o autoriferimenti · " + a.Title);
            foreach (var related in a.Related) CheckLink(related);
            foreach (Match link in Regex.Matches(body, @"\]\((/wiki/[^)]+)\)")) CheckLink(link.Groups[1].Value);
        }
        Check(formulaErrors.Count == 0, string.Join("\n", formulaErrors));
        using (var glossary = WikiCatalog.Resource("glossary.json"))
            foreach (var entry in JsonSerializer.Deserialize<Dictionary<string, string[]>>(glossary)!) CheckLink(entry.Value[1]);
        var instability = WikiCatalog.Articles.Single(a => a.Title == "Instabilità delle aste compresse");
        Check(!instability.Related.Select(WikiCatalog.Resolve).Any(a => a!.Title.Contains("acciaio per armature")), "Instabilità: nessun collegamento improprio alla scheda armature");
        var materialGuide = WikiCatalog.Articles.Single(a => a.Type == "guide" && a.Title == "Materiali e durabilità");
        Check(materialGuide.Related.All(id => WikiCatalog.Resolve(id)!.Title is "Calcestruzzo armature e copriferro" or "Scheda acciaio per armature"), "Materiali: correlati pertinenti al contenuto");
        Check(WikiCatalog.Articles.Single(a => a.Title == "Capacità orizzontale con Broms").Area == "Fondazioni e geotecnica", "Broms nella geotecnica");
        Check(WikiContextHelp.Description("axial_force_kn", "geo_palo_verticale") is null, "Convenzione N della sezione CA esclusa dall'aiuto dei pali");
        var beam = WikiCatalog.Resolve("/wiki/manuale/fem/elementi-beam")!;
        using var areasStream = WikiCatalog.Resource("areas.json");
        foreach (var (type, areas) in JsonSerializer.Deserialize<Dictionary<string, string[]>>(areasStream)!)
            foreach (var area in areas) Check(WikiCatalog.Articles.Any(a => a.Type == type && a.Area == area), "Percorso iniziale con contenuto reale · " + area);
        Check(Regex.Matches(WikiCatalog.Body(beam), "^### ", RegexOptions.Multiline).Count >= 25, "Beam approfondito con almeno 25 sezioni");
        Check(WikiCatalog.Search("buckling").Any(a => a.Title.Contains("Instabilità")), "Sinonimo inglese buckling");
        Check(WikiCatalog.Search("copriferro").Any(a => a.Type == "guide") && WikiCatalog.Search("copriferro").Any(a => a.Type == "theory"), "Ricerca condivisa fra teoria e software");
        Check(WikiCatalog.Search("FEM beam").Contains(beam), "Ricerca AND su parole e sinonimi");
        Check(WikiCatalog.Search("π").Any(a => a.Title.Contains("Instabilità")), "Ricerca di simboli matematici e concetti associati");
        var data = MainWindow.WikiExamples.Create("str_palo", "beam-ca"); ModuleCatalog.ValidateData("str_palo", data);
        Check(data["combinazioni"]?["SLU"]?[0]?["azioni"]?[1]?.ToString() == "200", "Momento numerico dell'esempio: 25 × 8² / 8 = 200 kNm");
        Check(SectionWorkspace.Sets.Skip(1).All(s => data["combinazioni"]![s]!.AsArray().Count == 0), "Nessuna combinazione di fabbrica residua nell'esempio");
        var other = MainWindow.WikiExamples.Create("str_palo", "beam-ca"); data["input"]!["height_mm"] = "700";
        Check(other["input"]!["height_mm"]!.ToString() == "800", "Esempi e default indipendenti");
        var file = Path.Combine(directory, "progress.json"); var state = new WikiProgress(file);
        state.Update(beam.Id, "taglio", .6, true); state.Save();
        Check(new WikiProgress(file).Entries[beam.Id].Read.Contains("taglio"), "Persistenza sezione letta e posizione");
        File.WriteAllText(file, "invalid"); Check(new WikiProgress(file).Entries.Count == 0, "Recupero progresso corrotto");
        var wiki = new WikiView((_, _) => { }, new WikiProgress(persist: false));
        async Task Render(string name, double width, double height)
        {
            File.WriteAllText(Path.Combine(directory, "render-stage.txt"), name + " · inizio");
            wiki.Measure(new Size(width, height)); wiki.Arrange(new Rect(0, 0, width, height)); wiki.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            wiki.Measure(new Size(width, height)); wiki.Arrange(new Rect(0, 0, width, height)); wiki.UpdateLayout();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), Ui.Snapshot(wiki));
            File.WriteAllText(Path.Combine(directory, "render-stage.txt"), name + " · completato");
        }
        await Render("home-1400", 1400, 900);
        wiki.Navigate(beam.Id); await Render("beam-1400", 1400, 900);
        Check(Ui.Descendants<Button>(wiki).Any(b => b.Content as string == "Apri esempio · trave 8 m, momento 200 kNm"), "CTA esempio renderizzata");
        wiki.Navigate(beam.Id + "#esempio-concettuale"); await Render("beam-example-1400", 1400, 900);
        wiki.Navigate(beam.Id + "#esempio-concettuale");
        await Render("beam-example-480", 480, 800);
        wiki.Navigate(beam.Id + "#rigidezza-assiale"); await Render("beam-formulas-1400", 1400, 900);
        wiki.Navigate("/wiki/guide/moduli/sezione-ca"); await Render("guide-1400", 1400, 900);
        await Render("guide-compact-480", 480, 800);
        wiki.Home(); await Render("home-compact-480", 480, 800);
        using var assetStream = WikiCatalog.Resource("assets.json");
        foreach (var (source, name) in JsonSerializer.Deserialize<Dictionary<string, string>>(assetStream)!)
            Check(Ui.Asset("Wiki/" + name).PixelWidth > 0, "Figura incorporata · " + source);
        File.WriteAllText(Path.Combine(directory, "integration-stage.txt"), "Prima del costruttore MainWindow");
        var main = new MainWindow();
        File.WriteAllText(Path.Combine(directory, "integration-stage.txt"), "Prima delle verifiche di integrazione");
        try { main.CheckWikiIntegration(checks, directory); }
        finally { main.FinishSmoke(); main.Close(); }
        File.WriteAllText(Path.Combine(directory, "integration-stage.txt"), "Integrazione completata");
        File.WriteAllText(Path.Combine(directory, "checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(directory, "completato.txt"), $"PASS {checks.Count} controlli Wiki");
    }
}

public sealed partial class MainWindow
{
    internal void CheckWikiIntegration(List<string> checks, string directory)
    {
        testing = true;
        wiki = new WikiView((module, example) => OpenWikiModule(module, example), new WikiProgress(persist: false), ResumeCalculation);
        void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); checks.Add(label); }
        void RenderRoot(string name)
        {
            var root = (FrameworkElement)Content; root.Measure(new Size(1600, 900)); root.Arrange(new Rect(0, 0, 1600, 900)); root.UpdateLayout();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), Ui.Snapshot(root));
        }
        NewCalculation("str_palo"); var before = editor;
        Check(editor!.Data["input"]!["axial_force_kn"]!.ToString() == "-2500", "Convenzione UI: N negativo a compressione dopo migrazione dei default");
        editor!.Data["input"]!["height_mm"] = "850";
        ShowWiki("/wiki/guide/moduli/sezione-ca");
        RenderRoot("shell-wiki-1600");
        Check(ReferenceEquals(editor, before) && currentSheet!["dati"]!["input"]!["height_mm"]!.ToString() == "850", "Wiki conserva e conferma input del modulo");
        Ui.Descendants<Button>(wiki).Single(b => b.Content as string == "← Torna al lavoro").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(ReferenceEquals(body.Content, moduleView), "Rientro nella scheda tramite il comando Torna al lavoro");
        dirty = false; OpenWikiModule("str_palo", "beam-ca");
        RenderRoot("shell-example-1600");
        Check(editor!.Data["input"]!["moment_x_knm"]!.ToString() == "200" && dirty && path is null, "Esempio aperto come nuovo documento da salvare");
        Check(Ui.Descendants<Button>(editor).Any(b => b.Content as string == "?"), "Help contestuale degli input disponibile");
        dirty = false; NewProjects(); AddProject(); var project = document;
        ShowWiki(); ShowProjects();
        Check(ReferenceEquals(document, project) && document.Array("progetti").Count == 1, "Progetti conservati nel passaggio alla Wiki");
    }
}
