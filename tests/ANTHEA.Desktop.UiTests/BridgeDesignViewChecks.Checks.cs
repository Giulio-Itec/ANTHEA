using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.BridgeDesign;
using Anthea.Presentation.Services;
using Anthea.Presentation.Tests;
using Anthea.Testing.Normalization;
using X.Core;
using X.Desktop.Views.BridgeDesign;

namespace X.Desktop;

/// <summary>The F3 fixtures and the exports, the price editor, the printed page, the screenshots, the help, the automation names, read-only (F5.12).</summary>
internal static partial class BridgeDesignViewChecks
{
    // ---------------------------------------------------------------- 3. F3 fixtures and exports

    /// <summary>
    /// For the 10 F3 fixtures, on the path of the F3 capture: data and Modified after the calculation of the opening, after the visit, after the
    /// calculation and after the exports, the result, the JSON with the data (BD7), the CSV of the quantities and of the levels, equal to the values of
    /// the historic view (recorded before its archiving in tests/fixtures/f5-bridge-design/vista-storica.json); the three appearances write nothing;
    /// the Word report and the PDF beside it equal to those of the recipe of the historic view (BridgeDesignWorkspace.BuildReportDocument:
    /// BridgeDesignModel.ReportDocument with the two images of the drawing, then ReportFiles as SheetEditor.ExportReport), the images being those of the
    /// figures of the sheet, equal pixel by pixel to the drawing of the historic view (recorded).
    /// </summary>
    private static async Task FixturesAndExports(string root, string output)
    {
        string folder = Path.Combine(output, "esportazioni");
        Directory.CreateDirectory(folder);
        var normalizer = new TextNormalizer();
        var services = new Services();
        var editors = Editors(services);
        var recorded = Recorded(root)["esportazioni"]!.AsObject();
        var fixtures = BridgeDesignScenarios.Fixtures(root);
        Check(fixtures.Count == 10 && fixtures.Order(StringComparer.Ordinal).SequenceEqual(recorded.Select(p => p.Key).Order(StringComparer.Ordinal)),
            "Fixture F3 di Bridge Design: " + fixtures.Count + ", tutte con i valori registrati della vista storica");
        int reports = 0;
        byte[] bom = Encoding.UTF8.GetPreamble();
        var figures = new BridgeDesignFigureRenderer();
        foreach (string id in fixtures)
        {
            string name = Module + "--" + id;
            var historic = recorded[id]!.AsObject();
            var data = BridgeDesignScenarios.FixtureData(root, id);
            using var mvvm = Idle(() => new Hosted(editors.Open(Module, data)));
            Check(mvvm.Editor is MvvmSheetEditor { Sheet: BridgeDesignSheetViewModel }, name + ": editor MVVM con la vista XAML");
            // The calculation of the opening first.
            await Idle(() => mvvm.Pending).WaitAsync(TimeSpan.FromSeconds(180));
            await mvvm.Settle();
            mvvm.Editor.Commit();
            Check(Same(mvvm, historic, "apertura"), name + ": dati all'apertura, dopo il calcolo automatico, uguali a quelli della vista storica (registrati)");
            // The visit of the F3 capture writes what the historic visit wrote (nothing: pages and tabs are state of the view).
            Idle(() => { Visit(mvvm.Root); return 0; });
            await mvvm.Settle();
            mvvm.Editor.Commit();
            Check(Same(mvvm, historic, "visita"), $"{name}: la visita della cattura F3 scrive come la vista storica ({mvvm.Modified} Modified, registrati)");
            // The three appearances write nothing.
            string visited = Normalized(mvvm.Editor.Data); int modified = mvvm.Modified;
            foreach (var mode in new[] { AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light }) { Appearance.Set(mode, false); await mvvm.Settle(); }
            mvvm.Editor.Commit();
            Check(Normalized(mvvm.Editor.Data) == visited && mvvm.Modified == modified, name + ": i tre aspetti non scrivono e non sollevano Modified");
            await Idle(() => mvvm.Editor.CalculateAsync()).WaitAsync(TimeSpan.FromSeconds(180));
            for (int i = 0; i < 50 && mvvm.Editor.Busy; i++) await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            await mvvm.Settle();
            mvvm.Editor.Commit();
            Dump(folder, name + "-calcolo", mvvm, historic);
            string? result = historic["risultato"]?.GetValue<string>();
            Check(Same(mvvm, historic, "calcolo") && mvvm.Editor.HasResults == historic["risultati"]!.GetValue<bool>()
                && (result is null ? mvvm.Editor.Result is null : Sha(Normalized(mvvm.Editor.Result)) == result),
                name + ": dati e risultato dopo il calcolo uguali a quelli della vista storica (registrati)");
            var view = mvvm.View; var sheet = mvvm.Sheet;
            if (result is null)
            {
                // Without a result: the exports of the card do nothing, the JSON and the report are refused as by the historic sheet.
                services.Saves.Clear();
                Idle(() => { Press(Caption(view.DesignScroll, "Esporta quantità CSV")); Press(Caption(view.DesignScroll, "Immagine PNG")); return 0; });
                Check(services.Saves.Count == 0, name + ": senza risultato le esportazioni della scheda non chiedono il file, come la vista storica");
                string refusal; try { mvvm.Editor.ExportResult(Path.Combine(folder, name + "-rifiutato.json")); refusal = "-"; } catch (InvalidOperationException e) { refusal = e.Message; }
                Check(refusal == historic["rifiuto"]!.GetValue<string>(), name + ": senza risultato il JSON è rifiutato con il messaggio della vista storica («" + refusal + "»)");
                continue;
            }
            // The JSON of the result with the data (BD7).
            mvvm.Editor.ExportResult(Path.Combine(folder, name + "-mvvm.json"));
            string jsonMvvm = BridgeDesignTrace.Normalize(File.ReadAllText(Path.Combine(folder, name + "-mvvm.json")));
            Check(Sha(jsonMvvm) == historic["json"]!.GetValue<string>() && JsonNode.Parse(jsonMvvm)!["dati"] is JsonObject,
                name + ": JSON del risultato uguale a quello della vista storica, con i dati (BD7, registrato)");
            // The two images of the report: the figures of the sheet, equal pixel by pixel to the drawing of the historic view.
            var calculation = sheet.Calculation!;
            byte[] elevation = figures.Drawing(sheet.Data, calculation, false), crossSection = figures.Drawing(sheet.Data, calculation, true);
            Check(ViewTraceTools.Pixels(elevation) == historic["prospetto"]!.GetValue<string>() && ViewTraceTools.Pixels(crossSection) == historic["sezione"]!.GetValue<string>(),
                name + ": prospetto e sezione delle figure uguali pixel per pixel al disegno della vista storica (registrati)");
            // Report Word and the PDF beside it, against the recipe of the historic view.
            string title = "Fixture F3 · " + Module + " · " + id;
            void Export()
            {
                mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, []);
                Anthea.Reports.ReportFiles.Write(Path.Combine(folder, name + "-attesa.docx"),
                    Anthea.Reports.DocxWriter.Write(BridgeDesignModel.ReportDocument(title, sheet.Data, calculation, elevation, crossSection)), title);
            }
            Export();
            string? part = DifferentPart(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.docx")), File.ReadAllBytes(Path.Combine(folder, name + "-attesa.docx")), normalizer);
            Check(part is null, name + ": report Word uguale in ogni parte a quello della ricetta della vista storica, figure comprese (date normalizzate)" + (part is null ? "" : ", diversa " + part));
            int attempts = 1;
            while (!SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-attesa.pdf"))) && attempts < 3)
            {
                attempts++;
                Export();
            }
            Check(SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-attesa.pdf"))),
                name + ": PDF affiancato uguale a quello della ricetta della vista storica (date normalizzate" + (attempts > 1 ? $", {attempts} esportazioni" : "") + ")");
            reports++;
            // «Esporta quantità CSV» (the card of the drawing) and «Esporta sezioni e quote CSV» (tab «Sezioni e quote»); «Immagine PNG» in Images.
            string csv = Path.Combine(folder, name + "-quantita.csv"), technical = Path.Combine(folder, name + "-quote.csv");
            services.Saves.Clear();
            services.SavePath = csv; Idle(() => { Press(Caption(view.DesignScroll, "Esporta quantità CSV")); return 0; });
            Idle(() => { view.OutputTabs.SelectedIndex = 5; return 0; }); await mvvm.Settle();
            services.SavePath = technical; Idle(() => { Press(Caption(view.DesignScroll, BridgeDesignPresentation.TechnicalCsvButton)); return 0; });
            services.SavePath = null;
            Check(services.Saves.Select(s => s.FileName).SequenceEqual(["BridgeDesign_quantita.csv", "BridgeDesign_sezioni_quote.csv"])
                && services.Saves.Select(s => FileFilter.Join(s.Filters)).SequenceEqual(["Quantità CSV|*.csv", "Sezioni e quote CSV|*.csv"]),
                name + ": nomi proposti e filtri delle esportazioni CSV della vista storica");
            Check(WithBom(File.ReadAllBytes(csv), bom, historic["csv"]!.GetValue<string>()) && WithBom(File.ReadAllBytes(technical), bom, historic["quote"]!.GetValue<string>()),
                name + ": CSV delle quantità e delle quote uguali a quelli della vista storica (testi registrati)");
            Check(Same(mvvm, historic, "esportazioni"), name + ": le esportazioni non scrivono nei dati (registrati)");
        }
        Log.Add($"   esportazioni: {reports} report");
        Check(reports >= 8, $"Report confrontati con la vista storica: {reports}");
    }

    /// <summary>The data and the Modified of the sheet equal to those of the historic view at <paramref name="step"/> (recorded).</summary>
    private static bool Same(Hosted mvvm, JsonObject historic, string step) =>
        Sha(Normalized(mvvm.Editor.Data)) == historic[step]!.GetValue<string>() && mvvm.Modified == historic[step + "_modified"]!.GetValue<int>();

    /// <summary>A file written with the BOM and the text whose SHA-256 is <paramref name="sha"/>.</summary>
    private static bool WithBom(byte[] bytes, byte[] bom, string sha) => bytes.AsSpan().StartsWith(bom) && Sha(Encoding.UTF8.GetString(bytes, bom.Length, bytes.Length - bom.Length)) == sha;

    /// <summary>
    /// «Immagine PNG» (the card of the drawing) for every F3 fixture with a result, after the same steps as <see cref="FixturesAndExports"/> (the
    /// calculation of the opening, the visit, the calculation): the proposed name and the filter of the historic view, the PNG equal pixel by
    /// pixel to the drawing of the historic view (recorded), nothing written in the data. Last part of the check (22/10 evening): the PNG goes through
    /// DocumentFile.Write, which renames its temporary file into the image, and there the behaviour monitoring of Trend Micro ends the unsigned
    /// ANTHEA.exe of the runner (question S6). The step in progress is written in dettaglio.txt before each image.
    /// </summary>
    private static async Task Images(string root, string output)
    {
        string folder = Path.Combine(output, "esportazioni");
        Directory.CreateDirectory(folder);
        var services = new Services();
        var editors = Editors(services);
        var recorded = Recorded(root)["immagini"]!.AsObject();
        int images = 0;
        foreach (string id in BridgeDesignScenarios.Fixtures(root))
        {
            string name = Module + "--" + id;
            var data = BridgeDesignScenarios.FixtureData(root, id);
            using var mvvm = Idle(() => new Hosted(editors.Open(Module, data)));
            await Idle(() => mvvm.Pending).WaitAsync(TimeSpan.FromSeconds(180));
            await mvvm.Settle();
            Idle(() => { Visit(mvvm.Root); return 0; });
            await mvvm.Settle();
            await Idle(() => mvvm.Editor.CalculateAsync()).WaitAsync(TimeSpan.FromSeconds(180));
            for (int i = 0; i < 50 && mvvm.Editor.Busy; i++) await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            await mvvm.Settle();
            mvvm.Editor.Commit();
            // Without a result «Immagine PNG» asks nothing (FixturesAndExports checks it).
            if (recorded[id] is not JsonObject historic)
            {
                Check(!mvvm.Editor.HasResults, name + ": senza risultato, come la vista storica: nessuna immagine");
                Log.Add("   " + name + ": senza risultato, nessuna immagine");
                continue;
            }
            Check(Sha(Normalized(mvvm.Editor.Data)) == historic["prima"]!.GetValue<string>() && mvvm.Modified == historic["prima_modified"]!.GetValue<int>() && mvvm.Editor.HasResults,
                name + ": dati e risultato prima dell'immagine uguali a quelli della vista storica (registrati)");
            string png = Path.Combine(folder, name + "-ponte.png");
            int modified = mvvm.Modified; string before = Normalized(mvvm.Editor.Data);
            services.Saves.Clear();
            Log.Add($"   in corso: {name}: «Immagine PNG» → {Path.GetFileName(png)} (dal 22/10, S6, DocumentFile.Write copia il file temporaneo .x_*.tmp "
                + "nel PNG invece di rinominarlo; se la prova finisce qui senza esito: antivirus)");
            services.SavePath = png; Idle(() => { Press(Caption(mvvm.View.DesignScroll, "Immagine PNG")); return 0; }); services.SavePath = null;
            Check(services.Saves.Select(s => (s.FileName, FileFilter.Join(s.Filters))).SequenceEqual([("BridgeDesign.png", "Immagine PNG|*.png")]),
                name + ": «Immagine PNG», nome proposto e filtro della vista storica");
            Check(ViewTraceTools.Pixels(File.ReadAllBytes(png)) == historic["png"]!.GetValue<string>(),
                name + ": PNG del disegno uguale pixel per pixel a quello della vista storica (registrato)");
            Check(Normalized(mvvm.Editor.Data) == before && mvvm.Modified == modified, name + ": l'immagine non scrive nei dati");
            images++;
        }
        Check(images >= 8, $"Immagini PNG confrontate con la vista storica: {images}");
    }

    /// <summary>The data and the result of the view beside the exports when they differ from the recorded values, to read the difference.</summary>
    private static void Dump(string folder, string name, Hosted mvvm, JsonObject historic)
    {
        string data = Normalized(mvvm.Editor.Data), result = Normalized(mvvm.Editor.Result);
        if (Sha(data) == historic["calcolo"]?.GetValue<string>() && (mvvm.Editor.Result is null ? historic["risultato"] is null : Sha(result) == historic["risultato"]?.GetValue<string>())) return;
        File.WriteAllText(Path.Combine(folder, name + "-dati-mvvm.json"), data);
        File.WriteAllText(Path.Combine(folder, name + "-risultato-mvvm.json"), result);
    }

    /// <summary>
    /// The values of the historic view recorded by this check before its archiving (passaggio della Release, 23/10/2026):
    /// tests/fixtures/f5-bridge-design/vista-storica.json.
    /// </summary>
    private static JsonObject Recorded(string root) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests", "fixtures", "f5-bridge-design", "vista-storica.json")))!.AsObject();

    /// <summary>SHA-256 of the UTF-8 text, as the values of <see cref="Recorded"/>.</summary>
    private static string Sha(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>First part of two Word documents that differs, with the dates of core.xml and of the text normalized as in F3; null when equal.</summary>
    private static string? DifferentPart(byte[] a, byte[] b, TextNormalizer normalizer)
    {
        using var first = new ZipArchive(new MemoryStream(a)); using var second = new ZipArchive(new MemoryStream(b));
        var names = first.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ToArray();
        if (!names.SequenceEqual(second.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal))) return "elenco delle parti";
        foreach (string name in names)
        {
            byte[] x = Read(first.GetEntry(name)!), y = Read(second.GetEntry(name)!);
            bool equal = name is "docProps/core.xml" or "word/document.xml"
                ? Normalize(name, x, normalizer) == Normalize(name, y, normalizer) : x.SequenceEqual(y);
            if (!equal) return name;
        }
        return null;
        static byte[] Read(ZipArchiveEntry entry) { using var stream = entry.Open(); using var copy = new MemoryStream(); stream.CopyTo(copy); return copy.ToArray(); }
        static string Normalize(string name, byte[] bytes, TextNormalizer normalizer) => name == "docProps/core.xml"
            ? normalizer.Normalize(Encoding.UTF8.GetString(bytes)) : normalizer.NormalizeReportTimestamps(Encoding.UTF8.GetString(bytes));
    }

    /// <summary>Two PDF files equal but for what changes at every rendering (dates, identifiers, tags of the font subsets).</summary>
    private static bool SamePdf(byte[] a, byte[] b)
    {
        static string Text(byte[] bytes)
        {
            string text = Encoding.Latin1.GetString(bytes);
            text = Regex.Replace(text, @"\(D:[0-9+\-Z']+\)", "(D:)");
            text = Regex.Replace(text, @"/ID\s*\[[^\]]*\]", "/ID[]");
            text = Regex.Replace(text, @"(?<=/(?:FontName|BaseFont)/)[A-Z]{6}\+", "SUBSET+");
            text = Regex.Replace(text, @"(<xmp:\w*Date>)[^<]*(<)", "$1$2");
            return Regex.Replace(text, @"uuid:[0-9a-fA-F-]+", "uuid:");
        }
        return a.SequenceEqual(b) || Text(a) == Text(b);
    }

    // ---------------------------------------------------------------- 4. the price editor

    /// <summary>
    /// «Gestisci prezzario…»: the editor shown by the dialog service from its DataTemplate (PriceCatalogueView) against the historic window
    /// (BridgePricesView, archived; its texts, tabs, columns and buttons recorded before the archiving): the caption, the size (1350 × 780 without the borders), the texts, the tabs and the columns of the
    /// three tables; the template CSV, the catalogue exported and imported again through the file dialog service, with the filters and the names
    /// of the historic window; «Annulla» changes nothing; «Applica prezzario» writes the document into the data (one edit, the tabs rebuilt).
    /// </summary>
    private static async Task PriceEditor(string root, string output)
    {
        string folder = Path.Combine(output, "prezzario");
        Directory.CreateDirectory(folder);
        var services = new Services();
        var data = BridgeDesignScenarios.FixtureData(root, "default");
        using var host = Idle(() => new Hosted(Editors(services).Open(Module, data)));
        await Idle(() => host.Pending).WaitAsync(TimeSpan.FromSeconds(180)); await host.Settle();
        var view = host.View; var sheet = host.Sheet;
        Idle(() => { view.OutputTabs.SelectedIndex = 3; return 0; }); await host.Settle();
        var open = Caption(view.DesignScroll, BridgeDesignPresentation.ManagePricesButton);
        Check(open is { IsEnabled: true }, "«Gestisci prezzario…» nella scheda «Prezzario»");
        static string[] Texts(DependencyObject root) => Walk(root, popups: false).OfType<TextBlock>()
            .Where(t => t.TemplatedParent is null && t.Text.Length > 0 && !System.Windows.Data.BindingOperations.IsDataBound(t, TextBlock.TextProperty))
            .Select(t => t.Text).Distinct().Order(StringComparer.Ordinal).ToArray();
        static string[] Columns(DependencyObject root) => Walk(root, popups: false).OfType<DataGrid>()
            .SelectMany(g => g.Columns.Select(c => c.Header is TextBlock header ? header.Text : c.Header as string ?? "")).ToArray();
        static string[] Tabs(DependencyObject root) => Walk(root, popups: false).OfType<TabItem>().Select(t => t.Header as string ?? "").ToArray();
        static string[] Buttons(DependencyObject root) => Walk(root, popups: false).OfType<Button>().Select(b => b.Content as string ?? "").Where(c => c.Length > 0).Order(StringComparer.Ordinal).ToArray();
        // The historic window on the same data (BridgePricesView, built off screen and never shown), recorded before its archiving.
        var recorded = Recorded(root)["prezzario"]!.AsObject();
        static string[] List(JsonNode? node) => node!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
        string[] historicTexts = List(recorded["testi"]), historicColumns = List(recorded["colonne"]), historicTabs = List(recorded["schede"]), historicButtons = List(recorded["pulsanti"]);
        // «Annulla»: the window shown, compared, then closed without changes.
        string before = Normalized(sheet.Data); int modified = host.Modified;
        services.PriceScript = (editor, window) =>
        {
            var editorView = Walk(window).OfType<PriceCatalogueView>().Single();
            File.WriteAllBytes(Path.Combine(output, "schermate", "prezzario.png"), host.Png(editorView));
            Check(window.Title == "Bridge Design · Gestione prezzari" && Math.Abs(editorView.ActualWidth - 1334) < 1 && Math.Abs(editorView.ActualHeight - 741) < 1,
                "Il prezzario nella finestra del servizio dei dialoghi: titolo e misura della finestra storica (1350 × 780 senza i bordi)");
            Check(Tabs(editorView).SequenceEqual(historicTabs), "Le quattro schede della finestra storica: " + string.Join(", ", historicTabs));
            var columns = Columns(editorView);
            Check(columns.SequenceEqual(historicColumns), "Le colonne delle tre tabelle della finestra storica (" + historicColumns.Length + ")"
                + (columns.SequenceEqual(historicColumns) ? "" : ": " + string.Join(" | ", columns)));
            var texts = Texts(editorView);
            Check(historicTexts.All(texts.Contains), "I testi della finestra storica" + (historicTexts.All(texts.Contains) ? "" : ", mancanti: " + string.Join(" | ", historicTexts.Except(texts))));
            Check(Buttons(editorView).SequenceEqual(historicButtons), "I pulsanti della finestra storica: " + string.Join(", ", historicButtons));
            // «Modello CSV» and «Esporta catalogo JSON» through the file dialog service, then the catalogue imported again.
            string template = Path.Combine(folder, "modello.csv"), catalogue = Path.Combine(folder, "catalogo.json");
            services.SavePath = template; Press(Caption(editorView, "Modello CSV"));
            services.SavePath = catalogue; Press(Caption(editorView, "Esporta catalogo JSON"));
            services.SavePath = null;
            Check(File.ReadAllBytes(template).SequenceEqual(Encoding.UTF8.GetBytes(PriceCatalogueImport.Template())) && File.ReadAllBytes(catalogue).SequenceEqual(editor.ExportCatalogue()),
                "«Modello CSV» e «Esporta catalogo JSON» nei file scelti");
            Check(services.Saves.TakeLast(2).Select(s => (s.FileName, FileFilter.Join(s.Filters))).SequenceEqual([("ANTHEA_Prezzario.csv", "Modello CSV|*.csv"), ("ANTHEA_Prezzario.json", "Catalogo ANTHEA|*.json")]),
                "Nomi proposti e filtri dei file della finestra storica");
            services.OpenPath = catalogue; Press(Caption(editorView, "Importa CSV / Excel / JSON")); services.OpenPath = null;
            Check(services.Opens.Count == 1 && FileFilter.Join(services.Opens[0].Filters) == "Prezzari|*.csv;*.xlsx;*.json|CSV|*.csv|Excel|*.xlsx|Catalogo ANTHEA|*.json"
                && editor.ExportCatalogue().SequenceEqual(File.ReadAllBytes(catalogue)), "«Importa CSV / Excel / JSON»: il catalogo esportato si importa uguale, con i filtri della finestra storica");
            Press(editorView.CancelButton);
            Check(!window.IsVisible && editor.Result is null, "«Annulla» chiude la finestra senza applicare");
        };
        Idle(() => { Press(open); return 0; }); await host.Settle();
        services.PriceScript = null; services.ThrowScriptError();
        Check(services.PriceWindows == 1 && Normalized(sheet.Data) == before && host.Modified == modified, "«Annulla»: nessuna modifica del foglio");
        // «Applica prezzario» after a price typed in a cell of the window.
        services.PriceScript = (editor, window) =>
        {
            var editorView = Walk(window).OfType<PriceCatalogueView>().Single();
            var row = editor.Mappings.Single(m => m.WorkCode == "rebar");
            row.Reason = "Offerta aggiornata"; row.OverridePrice = "1750";
            window.UpdateLayout();
            Check(editorView.StatusText.Text == editor.Status && editorView.ApplyButton.IsEnabled == editor.CanApply, "Stato e «Applica prezzario» della finestra seguono l'anteprima");
            Press(editorView.ApplyButton);
            Check(!window.IsVisible && editor.Result is not null, "«Applica prezzario» applica e chiude la finestra");
        };
        Idle(() => { Press(open); return 0; }); await host.Settle();
        services.PriceScript = null; services.ThrowScriptError();
        Check(services.PriceWindows == 2 && sheet.Data["pricing"] is JsonObject && host.Modified == modified + 1
            && sheet.Prices.OfType<BridgeDesignText>().Any(t => t.Role == BridgeDesignTextRole.Title), "«Applica prezzario»: il documento dei prezzi nei dati, una modifica, il pannello ricostruito");
        // Read-only (F5.15, BD4): the editor opens to be read, without «Applica prezzario»; nothing is written.
        host.Editor.InspectRevision(); await host.Settle();
        open = Caption(view.DesignScroll, BridgeDesignPresentation.ManagePricesButton);
        Check(open is { IsEnabled: true }, "BD4: «Gestisci prezzario…» libero in una revisione in sola lettura");
        string applied = Normalized(sheet.Data); modified = host.Modified;
        services.PriceScript = (editor, window) =>
        {
            var editorView = Walk(window).OfType<PriceCatalogueView>().Single();
            window.UpdateLayout();
            Check(editor.IsReadOnly && !editorView.ApplyButton.IsEnabled && editorView.StatusText.Text.StartsWith(PriceCatalogueViewModel.ReadOnlyStatus, StringComparison.Ordinal),
                "BD4: in sola lettura il prezzario si consulta, «Applica prezzario» spento e lo stato lo dice");
            Press(editorView.CancelButton);
        };
        Idle(() => { Press(open); return 0; }); await host.Settle();
        services.PriceScript = null; services.ThrowScriptError();
        Check(services.PriceWindows == 3 && Normalized(sheet.Data) == applied && host.Modified == modified, "BD4: il prezzario aperto in sola lettura non cambia il foglio");
    }

    // ---------------------------------------------------------------- 5. «Stampa scheda»

    private static async Task PrintPage(string root, string output)
    {
        var services = new Services();
        var data = BridgeDesignScenarios.FixtureData(root, "default");
        using var mvvm = Idle(() => new Hosted(Editors(services).Open(Module, data)));
        var view = mvvm.View;
        int asked = 0; FrameworkElement? printed = null;
        view.ShowPrintDialog = _ => { asked++; return false; };
        view.PrintVisual = (_, page) => printed = page;
        // Without a result (the calculation of the opening not published yet) nothing is asked.
        if (!mvvm.Sheet.HasResults)
        {
            Idle(() => { Press(view.PrintButton); return 0; });
            Check(asked == 0 && view.PrintPage(1000, 700) is null, "«Stampa scheda» senza risultato: nessuna finestra, come la vista storica");
        }
        await Idle(() => mvvm.Pending).WaitAsync(TimeSpan.FromSeconds(180));
        await mvvm.Settle();
        var page = view.PrintPage(1000, 700);
        // The page of the historic view on the same data (BridgeDesignWorkspace.PrintPage), recorded before its archiving.
        var recorded = Recorded(root)["stampa"]!.AsObject();
        string[] past = recorded["testi"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray();
        static string[] Texts(FrameworkElement? page) => page is Viewbox { Child: Panel panel } ? panel.Children.OfType<TextBlock>().Select(t => t.Text).ToArray() : [];
        Check(Texts(page).Length > 4 && Texts(page).SequenceEqual(past), "La pagina di «Stampa scheda»: i testi della pagina storica, registrati (" + past.Length + ")");
        string a = ViewTraceTools.Pixels(Ui.DocumentSnapshot(page!)), b = recorded["pixel"]!.GetValue<string>();
        File.WriteAllBytes(Path.Combine(output, "schermate", "stampa.png"), Ui.DocumentSnapshot(page!));
        Check(a == b, "La pagina di «Stampa scheda» uguale pixel per pixel a quella storica, registrata (" + a + ")");
        Idle(() => { Press(view.PrintButton); return 0; });
        Check(asked == 1 && printed is null, "«Stampa scheda»: la finestra della stampante; annullata non stampa");
        view.ShowPrintDialog = _ => { asked++; return true; };
        string? printer = null;
        // The printable area is read from the default printer: a machine without one cannot lay out the page (as the historic view).
        try { Idle(() => { Press(view.PrintButton); return 0; }); }
        catch (Exception error) when (error is System.Printing.PrintSystemException || error.InnerException is System.Printing.PrintSystemException)
        { printer = error.Message; }
        if (printer is null) Check(asked == 2 && printed is Viewbox, "«Stampa scheda»: confermata, la pagina sull'area stampabile");
        else Log.Add("   stampa: area stampabile non disponibile su questa macchina (" + printer + ")");
    }

    // ---------------------------------------------------------------- 6. screenshots

    private static async Task Screenshots(string root, string folder)
    {
        Directory.CreateDirectory(folder);
        var editors = Editors(new Services());
        int written = 0;
        foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
        {
            Appearance.Set(mode, false);
            string suffix = mode switch { AppAppearance.Light => "chiara", AppAppearance.Dark => "scuro", _ => "molto-scuro" };
            using var host = Idle(() => new Hosted(editors.Open(Module, BridgeDesignScenarios.FixtureData(root, "confronto-alternativa-a")), mode != AppAppearance.Light, 1400, 1000));
            await Idle(() => host.Pending).WaitAsync(TimeSpan.FromSeconds(180)); await host.Settle();
            File.WriteAllBytes(Path.Combine(folder, $"progetto-{suffix}.png"), host.Png());
            Idle(() => { host.View.OutputTabs.SelectedIndex = 2; return 0; }); await host.Settle();
            File.WriteAllBytes(Path.Combine(folder, $"confronto-{suffix}.png"), host.Png());
            // The search of the trace «ottimizzazione» (fixture default, pier and foundation kept, 3–6 spans), set through the boxes of the sheet
            // so that the page shows its limits; on confronto-alternativa-a the same search admits no solution and the page stays empty.
            using (var search = Idle(() => new Hosted(editors.Open(Module, BridgeDesignScenarios.FixtureData(root, "default")), mode != AppAppearance.Light, 1400, 1000)))
            {
                await Idle(() => search.Pending).WaitAsync(TimeSpan.FromSeconds(180)); await search.Settle();
                Idle(() => { search.View.Pages.SelectedIndex = 1; return 0; }); await search.Settle();
                var sheet = search.Sheet;
                sheet.SetLock("pier", true); sheet.SetLock("foundation", true); sheet.SetLimit("campate-min", "3"); sheet.SetLimit("campate-max", "6");
                await Idle(() => sheet.RunOptimizationAsync()).WaitAsync(TimeSpan.FromSeconds(300));
                await search.Settle();
                File.WriteAllBytes(Path.Combine(folder, $"ottimizzazione-{suffix}.png"), search.Png());
                Log.Add($"   schermate ({suffix}): ottimizzazione «{sheet.SearchStatus}»");
            }
            written += 3;
        }
        Appearance.Set(AppAppearance.Light, false);
        // The historic view is archived (23/10/2026): its screenshots stay in the artifacts of the runs before the switch.
        // The compact layout (below 1100 the drawing under the inputs, below 850 the metrics in two columns).
        using (var narrow = Idle(() => new Hosted(editors.Open(Module, BridgeDesignScenarios.FixtureData(root, "default")), false, 820, 1000)))
        {
            await Idle(() => narrow.Pending).WaitAsync(TimeSpan.FromSeconds(180)); await narrow.Settle();
            var view = narrow.View;
            Check(Grid.GetRow(view.DrawingCard) == 1 && Grid.GetColumn(view.DrawingCard) == 0 && view.MetricsGrid.Columns == 2 && Grid.GetRow(view.PreviewCard) == 1,
                "Sotto 1100 il disegno sotto gli ingressi, sotto 850 le metriche su due colonne, sotto 1120 l'anteprima della ricerca sotto le scelte (come la vista storica)");
            File.WriteAllBytes(Path.Combine(folder, "progetto-stretta-chiara.png"), narrow.Png());
            written++;
        }
        Check(written == 10, "Schermate della vista nei tre aspetti (progetto, confronto, ottimizzazione) e della disposizione stretta: " + written + " PNG in " + folder + " (144 DPI su questa macchina)");
    }

    // ---------------------------------------------------------------- 7. help

    /// <summary>F1 opens the guide of the module through the navigator of the services (MvvmSheetEditor, as SheetEditor); no «?» on the fields, as the historic view (BD6).</summary>
    private static async Task Help(string root)
    {
        var services = new Services();
        var data = BridgeDesignScenarios.FixtureData(root, "default");
        using var host = Idle(() => new Hosted(Editors(services).Open(Module, data)));
        await host.Settle();
        static int Questions(DependencyObject root) => Walk(root).OfType<Button>().Count(b => b.IsVisible && b.Content as string == "?");
        Check(Questions(host.Root) == 0 && Recorded(root)["aiuto"]!.GetValue<int>() == 0, "Nessuna etichetta «?» sui campi, come la vista storica (registrata)");
        string guide = Anthea.Wiki.WikiCatalog.ForModule(Module)?.Id ?? throw new InvalidOperationException("Guida assente nella Wiki: " + Module);
        var view = host.View;
        var targets = new List<UIElement> { view.Pages, view.InputTabs, view.StatusText };
        targets.AddRange(Walk(view.InputTabs).OfType<TextBox>().Where(t => t.IsVisible).Take(2));
        services.Wiki.Clear();
        foreach (var target in targets) F1(target);
        Check(services.Wiki.Count == targets.Count && services.Wiki.All(u => u == guide), $"F1 sui campi e fuori: la guida del modulo dal navigatore ({guide}, {services.Wiki.Count} pressioni)");
        host.Editor.InspectRevision(); await host.Settle();
        services.Wiki.Clear(); int modified = host.Modified;
        F1(view.Pages);
        Check(services.Wiki.SequenceEqual([guide]) && host.Modified == modified, "F1 anche in sola lettura, senza modificare il foglio");
    }

    // ---------------------------------------------------------------- 8. automation names

    private static async Task AutomationNames(string root)
    {
        static HashSet<string> Names(DependencyObject root) => Walk(root).OfType<UIElement>().Where(e => e.IsVisible)
            .Select(e => AutomationProperties.GetName(e)).Where(n => !string.IsNullOrEmpty(n)).ToHashSet(StringComparer.Ordinal);
        var editors = Editors(new Services());
        var recorded = Recorded(root)["nomi"]!.AsObject();
        foreach (string id in new[] { "default", "cassone-acciaio-prezzario", "strallato-ortotropo-fiume" })
        {
            if (!BridgeDesignScenarios.Fixtures(root).Contains(id)) { Log.Add("   nomi: fixture assente " + id); continue; }
            var data = BridgeDesignScenarios.FixtureData(root, id);
            using var host = Idle(() => new Hosted(editors.Open(Module, data)));
            await Idle(() => host.Pending).WaitAsync(TimeSpan.FromSeconds(180));
            await host.Settle();
            // The names of the historic view in every place, recorded before its archiving.
            var before = recorded[id]!.AsArray().Select(n => n!.GetValue<string>()).ToHashSet(StringComparer.Ordinal); var after = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (page, input, output) in Places())
            {
                Select(host.Root, page, input, output); await host.Settle();
                after.UnionWith(Names(host.Root));
            }
            var missing = before.Except(after).Order(StringComparer.Ordinal).ToArray();
            Log.Add($"   nomi nuovi ({id}): " + string.Join(", ", after.Except(before).Order(StringComparer.Ordinal)));
            Check(missing.Length == 0 && before.Count > 20, $"{id}: nomi di automazione della vista storica conservati ({before.Count}, registrati)" + (missing.Length == 0 ? "" : "; mancanti: " + string.Join(", ", missing)));
        }
    }

    /// <summary>Every place of the view: the five tabs of the inputs and the six of the results on the page of the project, and the page of the search.</summary>
    private static IEnumerable<(int Page, int Input, int Output)> Places()
    {
        for (int input = 0; input < 5; input++) yield return (0, input, 0);
        for (int output = 0; output < 6; output++) yield return (0, 0, output);
        yield return (1, 0, 0);
    }

    /// <summary>The page and the tabs chosen (the first three tab controls of the tree: pages, inputs, results).</summary>
    private static void Select(DependencyObject root, int page, int input, int output)
    {
        var tabs = Walk(root, popups: false).OfType<TabControl>().Take(3).ToArray();
        tabs[0].SelectedIndex = page;
        if (page == 0) { tabs[1].SelectedIndex = input; tabs[2].SelectedIndex = output; }
    }

    // ---------------------------------------------------------------- 9. read-only

    /// <summary>
    /// The controls that accept the user after InspectRevision, in the new view in every place, against those of the historic view on the same data,
    /// recorded before its archiving; the only difference is «Suggerisci campate dalla lunghezza», locked in the new view (F5.15, BD10 point 5;
    /// «Gestisci prezzario…» is free again since BD4 opens it to be read).
    /// </summary>
    private static async Task ReadOnly(string root)
    {
        var recorded = Recorded(root)["sola_lettura"]!.AsObject();
        foreach (string id in new[] { "default", "confronto-alternativa-a" })
        {
            var data = BridgeDesignScenarios.FixtureData(root, id);
            using var host = Idle(() => new Hosted(Editors(new Services()).Open(Module, data)));
            await Idle(() => host.Pending).WaitAsync(TimeSpan.FromSeconds(180));
            await host.Settle();
            host.Editor.InspectRevision();
            int modified = host.Modified;
            var before = recorded[id]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.GetValue<string>(), StringComparer.Ordinal);
            var after = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (page, input, output) in Places())
            {
                Select(host.Root, page, input, output); await host.Settle();
                foreach (var (key, value) in Controls(host.Root)) after[key] = after.TryGetValue(key, out var previous) && previous != value ? "misto" : value;
            }
            var common = before.Keys.Intersect(after.Keys).Order(StringComparer.Ordinal).ToArray();
            var different = common.Where(k => before[k] != after[k]).Select(k => k + ": storica " + before[k] + ", nuova " + after[k]).ToArray();
            Log.Add($"   sola lettura ({id}), solo nella storica: " + string.Join(", ", before.Keys.Except(after.Keys).Order(StringComparer.Ordinal)));
            Log.Add($"   sola lettura ({id}), solo nella nuova: " + string.Join(", ", after.Keys.Except(before.Keys).Order(StringComparer.Ordinal)));
            Log.Add($"   sola lettura ({id}), comuni: " + string.Join(", ", common.Select(k => k + " " + after[k])));
            string spans = "pulsante Suggerisci campate dalla lunghezza";
            Check(different.SequenceEqual([spans + ": storica libero, nuova bloccato"]) && common.Length >= 30,
                $"{id}: sola lettura, stessi controlli bloccati e liberi della vista storica, registrati ({common.Length} controlli comuni), salvo «Suggerisci campate» (BD10)"
                + (different.Length == 1 ? "" : ": " + string.Join("; ", different)));
            Check(host.Editor.HasResults && host.Modified == modified, id + ": sola lettura, il risultato resta e nulla è modificato");
        }
    }

    /// <summary>The state of the visible input controls, keyed by kind and name (automation name or content).</summary>
    private static Dictionary<string, string> Controls(DependencyObject root)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in Walk(root, popups: false).OfType<Control>().Where(e => e.IsVisible))
        {
            string? key = element switch
            {
                TextBox t when t.TemplatedParent is not Control && AutomationProperties.GetName(t) is { Length: > 0 } name => "testo " + name,
                ComboBox c when AutomationProperties.GetName(c) is { Length: > 0 } name => "scelta " + name,
                CheckBox c when c.Content is string text => "spunta " + text,
                Button b when b.Content is string text && b.TemplatedParent is not Control => "pulsante " + text,
                Button b when AutomationProperties.GetName(b) is { Length: > 0 } name && b.TemplatedParent is not Control => "tipologia " + name,
                Slider s when AutomationProperties.GetName(s) is { Length: > 0 } name => "cursore " + name,
                _ => null
            };
            if (key is null) continue;
            string value = element switch
            {
                TextBox t => t.IsReadOnly || !t.IsEnabled ? "bloccato" : "libero",
                _ => element.IsEnabled ? "libero" : "bloccato"
            };
            map[key] = map.TryGetValue(key, out var previous) && previous != value ? "misto" : value;
        }
        return map;
    }
}
