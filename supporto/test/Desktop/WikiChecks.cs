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
        foreach (var pilot in new[] { "bridge", "euler", "cracking", "bearing-capacity", "beam", "guida-sezione-ca" })
            Check(WikiCatalog.Resolve(pilot)?.Status == "reviewed", "Pilota revisionato · " + pilot);
        Check(WikiCatalog.Articles.Select(a => a.Id).Distinct().Count() == WikiCatalog.Articles.Length, "Route univoche");
        // Only content written for ANTHEA (decision of 6/10/2026): no adapted articles, reading lists or external links.
        string[] external = ["Approfondimento ·", "Letture tecniche", "De Pisapia", "CC BY-NC", "marcodepisapia", "geostru", "simonecaffe", "amazon.", "Madosoft", "MAX 16", "TheBridgeEng", "http://", "https://"];
        Check(WikiCatalog.Articles.All(a => !a.Key.StartsWith("mdp-") && !a.Key.StartsWith("letture-")), "Nessun articolo del corpus esterno");
        void CheckLink(string uri)
        {
            uri = WikiCatalog.CanonicalUri(uri);
            var target = WikiCatalog.Resolve(uri);
            Check(target is not null, "Destinazione valida · " + uri);
            if (uri.Contains('#'))
                Check(Regex.Matches(WikiCatalog.Body(target!), @"^#{3,4} (.+)", RegexOptions.Multiline)
                    .Any(m => WikiCatalog.Slug(m.Groups[1].Value.Trim()) == uri.Split('#', 2)[1]), "Sezione di destinazione esistente · " + uri);
        }
        foreach (var alias in WikiCatalog.Aliases)
        {
            Check(!WikiCatalog.Aliases.ContainsKey(alias.Value), "Alias diretto senza catene · " + alias.Key);
            CheckLink(alias.Key);
        }
        foreach (var module in ModuleCatalog.All)
        {
            Check(WikiCatalog.ForModule(module.Id) is not null, "Guida per ogni modulo · " + module.Id);
            Check(WikiContextHelp.ArticlesFor(module.Id).Any(a => a.Type == "theory"), "Approfondimenti tecnici · " + module.Id);
        }
        foreach (var topic in WikiContextHelp.Topics) CheckLink(topic.Uri);
        Check(WikiContextHelp.ForField("cover_mm", "str_palo")?.Uri.EndsWith("#copriferro-minimo-e-nominale") == true, "Copriferro rinvia alla durabilità");
        Check(WikiContextHelp.ForField("copriferro_fessure", "str_palo")?.Uri.EndsWith("#apertura-delle-fessure") == true, "Copriferro SLE distinto dal nominale");
        Check(WikiContextHelp.ForField("N", "geo_palo_orizzontale") is null, "N del palo non eredita la convenzione della sezione CA");
        Check(WikiContextHelp.ForField("spessore_chs_mm", "geo_micropalo_orizzontale") is not null, "Aiuto sulla sezione CHS");
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
            foreach (Match formula in Regex.Matches(body, @"^\$\$ (.+)$", RegexOptions.Multiline))
                try { WikiEditorial.Formula(formula.Groups[1].Value); } catch (Exception ex) { formulaErrors.Add(ex.Message); }
            Check(body.StartsWith("## ") && !body.Contains('\ufffd'), "Offset UTF8 valido · " + a.Id);
            Check(!external.Any(term => (a.Title + "\n" + a.Summary + "\n" + body).Contains(term, StringComparison.OrdinalIgnoreCase)), "Solo contenuti ANTHEA, senza rimandi esterni · " + a.Key);
            var allBlocks = new StackPanel();
            WikiEditorial.Render(allBlocks, body, _ => { }, (_, label, _) => Ui.Text(label));
            allBlocks.Measure(new Size(760, double.PositiveInfinity));
            Check(allBlocks.DesiredSize.Height > 0, "Tutti i blocchi e le formule inline renderizzati · " + a.Key);
            foreach (var m in a.Modules) Check(ModuleCatalog.Get(m) is not null, "Modulo esistente · " + m);
            Check(a.Related.Distinct().Count() == a.Related.Length && !a.Related.Contains(a.Id), "Correlati senza duplicati o autoriferimenti · " + a.Title);
            foreach (var related in a.Related) CheckLink(related);
            foreach (Match link in Regex.Matches(body, @"\]\(((?:/wiki/|wiki:)[^)]+)\)")) CheckLink(link.Groups[1].Value);
            foreach (var prerequisite in a.Prerequisites ?? []) CheckLink(prerequisite);
            Check(WikiCatalog.Chapters.Any(c => c.Id == a.ChapterId), "Capitolo esistente · " + a.Key);
        }
        Check(formulaErrors.Count == 0, string.Join("\n", formulaErrors));
        var delimiterProbe = new StackPanel();
        WikiEditorial.Render(delimiterProbe, "## Test\n\\(E\\)\n\n\\[\\frac{N}{A}\\]\n\n$$\n\\frac{M}{W}\n$$", _ => { }, (_, label, _) => Ui.Text(label));
        Check(delimiterProbe.Children.Count == 3, "Delimitatori LaTeX inline, display e doppio dollaro");
        using (var glossary = WikiCatalog.Resource("glossary.json"))
            foreach (var entry in JsonSerializer.Deserialize<Dictionary<string, string[]>>(glossary)!) CheckLink(entry.Value[1]);
        var instability = WikiCatalog.Articles.Single(a => a.Title == "Instabilità delle aste compresse");
        Check(!instability.Related.Select(WikiCatalog.Resolve).Any(a => a!.Title.Contains("acciaio per armature")), "Instabilità: nessun collegamento improprio alla scheda armature");
        var materialGuide = WikiCatalog.Articles.Single(a => a.Type == "guide" && a.Title == "Materiali e durabilità");
        Check(materialGuide.Related.All(id => WikiCatalog.Resolve(id)!.Title is "Calcestruzzo armature e copriferro" or "Acciaio per armature: proprietà e diagrammi"), "Materiali: correlati pertinenti al contenuto");
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
        Check(WikiCatalog.Search("LTB").Any(a => a.Key == "euler"), "Ricerca acronimo LTB e distinzione da Euler");
        Check(WikiCatalog.Search("pressoflessione").Any(a => a.Key == "guida-sezione-ca"), "Ricerca glossario dominio M-N");
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
        var chapterEntry = Ui.Descendants<Button>(wiki).Single(b => b.Content is TextBlock t && t.Text == "Fondamenti →");
        chapterEntry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Render("chapter-1400", 1400, 900);
        Check(Ui.Descendants<TextBlock>(wiki).Any(t => t.Text == "PERCORSO DI LETTURA"), "Copertina apre il percorso del capitolo");
        var chapterArticle = WikiCatalog.InChapter("fondamenti")[0];
        Ui.Descendants<Button>(wiki).First(b => b.Content is TextBlock t && t.Text == chapterArticle.Title + " →").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(wiki.CurrentId == chapterArticle.Id, "Percorso del capitolo apre l'articolo corretto");
        wiki.Home();
        foreach (var dark in new[] { false, true })
        {
            wiki.SetDark(dark);
            foreach (var id in new[] { "bridge", "euler", "cracking", "bearing-capacity", "beam", "guida-sezione-ca" })
            {
                wiki.Navigate(id); await Render($"pilot-{id}-{(dark ? "dark" : "light")}", 1400, 900);
            }
            wiki.Navigate("cracking#esempio-numerico"); await Render($"cracking-480-{dark}", 480, 800);
            wiki.Navigate("euler#snellezza-e-tensione-critica"); await Render($"euler-chart-{dark}", 1400, 900);
            foreach (var id in new[] { "biblioteca-tecnica", "taglio-traliccio", "taglio-traliccio#esempio-numerico-con-due-quantita-di-staffe", "muri-metodi-perimetro", "esempi-controlli-indipendenti" })
            {
                wiki.Navigate(id); await Render($"technical-{id.Replace('#', '-')}-{dark}", 1400, 900);
                Check(wiki.CurrentId == WikiCatalog.Resolve(id)?.Id && wiki.MissingRoute is null, "Lezione tecnica aperta · " + id);
            }
            // Removed external corpus (6/10/2026): old routes and keys open the Handbook cover with a notice, never an error.
            foreach (var id in new[] { "mdp-89", "/wiki/manuale/calcestruzzo/mdp-265", "letture-geotecnica", "/wiki/manuale/fondamenti/letture-fondamenti#geostru", "wiki:pagina-inesistente" })
            {
                wiki.Navigate(id); await Render($"removed-{WikiCatalog.Slug(id)}-{dark}", 1400, 900);
                Check(wiki.CurrentId is null && wiki.MissingRoute is not null
                    && Ui.Descendants<TextBlock>(wiki).Any(t => t.Text == "PAGINA NON DISPONIBILE")
                    && Ui.Descendants<Button>(wiki).Any(b => b.Content is TextBlock t && t.Text == "Fondamenti →"), "Route eliminata apre la copertina con un avviso · " + id);
            }
            foreach (var id in new[] { "guida-progetti-e-gestione-del-lavoro", "bridge-design", "profili-calcestruzzo", "acciaio-armature", "guida-muri-di-sostegno-con-stratigrafie-di-monte-e-valle" })
            {
                wiki.Navigate(id); await Render($"integrated-{id}-{dark}", 1400, 900);
            }
            wiki.Navigate("guida-sezione-ca#importare-ed-esportare-le-azioni"); await Render($"import-480-{dark}", 480, 800);
            wiki.Home(); await Render($"cover-480-{dark}", 480, 800);
        }
        wiki.SetDark(false);
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
            if (name.EndsWith(".svg"))
            {
                var vector = new WikiVector(name);vector.Measure(new Size(720, 400));
                Check(vector.DesiredSize.Width > 0, "Figura vettoriale incorporata · " + source);
            }
            else Check(Ui.Asset("Wiki/" + name).PixelWidth > 0, "Figura incorporata · " + source);
        File.WriteAllText(Path.Combine(directory, "integration-stage.txt"), "Prima del costruttore MainWindow");
        var main = new MainWindow(TestServices.Create);
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
        Check(Ui.Descendants<Button>(editor).Any(b => b.Content as string == "?" && b.Visibility == Visibility.Visible), "Help contestuale visibile dopo il collegamento all'editor");
        var helpRow = WikiContextHelp.Label(Ui.Text("Copriferro di prova"), "cover_mm", null);
        var probe = new StackPanel(); probe.SetValue(WikiContextHelp.ModuleProperty, "str_palo"); probe.Children.Add(helpRow);
        helpRow.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        var helpButton = Ui.Descendants<Button>(helpRow).Single();
        Check(helpButton.Visibility == Visibility.Visible, "Contesto Wiki ereditato dalle schede");
        var release = RevisionInspection.Protect(probe);
        Check(helpButton.IsEnabled, "Wiki consultabile nelle revisioni in sola lettura"); release();
        var exampleEditor = editor;
        wikiHelp.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(ReferenceEquals(body.Content, dashboard) && ReferenceEquals(editor, exampleEditor)
            && editor!.Data["input"]!["moment_x_knm"]!.ToString() == "200", "Pulsante Wiki apre la guida e conserva editor e azioni dell'esempio");
        ResumeCalculation();
        dirty = false; NewProjects(); AddProject(); var project = document;
        ShowWiki(); ShowProjects();
        Check(ReferenceEquals(document, project) && document.Array("progetti").Count == 1, "Progetti conservati nel passaggio alla Wiki");
    }
}
