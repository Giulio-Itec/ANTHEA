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

    private static async Task FixturesAndExports(string root, string output)
    {
        string folder = Path.Combine(output, "esportazioni");
        Directory.CreateDirectory(folder);
        var normalizer = new TextNormalizer();
        var services = new Services();
        var editors = Editors(services);
        var fixtures = BridgeDesignScenarios.Fixtures(root);
        Check(fixtures.Count == 10, "Fixture F3 di Bridge Design: " + fixtures.Count);
        int reports = 0;
        byte[] bom = Encoding.UTF8.GetPreamble();
        foreach (string id in fixtures)
        {
            string name = Module + "--" + id;
            var data = BridgeDesignScenarios.FixtureData(root, id);
            using var mvvm = Idle(() => new Hosted(editors.Open(Module, data)));
            using var historic = Idle(() => new Hosted(new SheetEditor(Module, data)));
            Check(mvvm.Editor is MvvmSheetEditor { Sheet: BridgeDesignSheetViewModel }, name + ": editor MVVM con la vista XAML");
            // The calculation of the opening first, on both views.
            await Idle(() => mvvm.Pending).WaitAsync(TimeSpan.FromSeconds(180)); await Idle(() => historic.Pending).WaitAsync(TimeSpan.FromSeconds(180));
            await mvvm.Settle(); await historic.Settle();
            mvvm.Editor.Commit(); historic.Editor.Commit();
            Check(Same(mvvm, historic), name + ": dati all'apertura, dopo il calcolo automatico, uguali a quelli della vista storica");
            // The visit of the F3 capture writes what the historic visit writes (nothing: pages and tabs are state of the view).
            Idle(() => { Visit(mvvm.Root); return 0; }); Idle(() => { Visit(historic.Root); return 0; });
            await mvvm.Settle(); await historic.Settle();
            mvvm.Editor.Commit(); historic.Editor.Commit();
            Check(Same(mvvm, historic) && mvvm.Modified == historic.Modified, $"{name}: la visita della cattura F3 scrive come la vista storica ({mvvm.Modified} Modified)");
            // The three appearances write nothing.
            string visited = Normalized(mvvm.Editor.Data); int modified = mvvm.Modified;
            foreach (var mode in new[] { AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light }) { Appearance.Set(mode, false); await mvvm.Settle(); }
            mvvm.Editor.Commit();
            Check(Normalized(mvvm.Editor.Data) == visited && mvvm.Modified == modified, name + ": i tre aspetti non scrivono e non sollevano Modified");
            await Idle(() => mvvm.Editor.CalculateAsync()).WaitAsync(TimeSpan.FromSeconds(180));
            await Idle(() => historic.Editor.CalculateAsync()).WaitAsync(TimeSpan.FromSeconds(180));
            for (int i = 0; i < 50 && mvvm.Editor.Busy; i++) await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            await mvvm.Settle(); await historic.Settle();
            mvvm.Editor.Commit(); historic.Editor.Commit();
            Dump(folder, name + "-calcolo", mvvm, historic);
            Check(Same(mvvm, historic) && mvvm.Editor.HasResults == historic.Editor.HasResults && Normalized(mvvm.Editor.Result) == Normalized(historic.Editor.Result),
                name + ": dati e risultato dopo il calcolo uguali a quelli della vista storica");
            var view = mvvm.View; var past = historic.Historic;
            if (past.Calculation is null)
            {
                // Without a result: the exports of the card do nothing, the JSON and the report are refused as by the historic sheet.
                services.Saves.Clear();
                Idle(() => { Press(Caption(view.DesignScroll, "Esporta quantità CSV")); Press(Caption(view.DesignScroll, "Immagine PNG")); return 0; });
                Check(services.Saves.Count == 0, name + ": senza risultato le esportazioni della scheda non chiedono il file, come la vista storica");
                string Refusal(ISheetEditor editor) { try { editor.ExportResult(Path.Combine(folder, name + "-rifiutato.json")); return "-"; } catch (InvalidOperationException e) { return e.Message; } }
                Check(Refusal(mvvm.Editor) == Refusal(historic.Editor), name + ": senza risultato il JSON è rifiutato con il messaggio della vista storica");
                continue;
            }
            // The JSON of the result with the data (BD7).
            mvvm.Editor.ExportResult(Path.Combine(folder, name + "-mvvm.json"));
            historic.Editor.ExportResult(Path.Combine(folder, name + "-storica.json"));
            string jsonMvvm = BridgeDesignTrace.Normalize(File.ReadAllText(Path.Combine(folder, name + "-mvvm.json")));
            Check(jsonMvvm == BridgeDesignTrace.Normalize(File.ReadAllText(Path.Combine(folder, name + "-storica.json"))) && JsonNode.Parse(jsonMvvm)!["dati"] is JsonObject,
                name + ": JSON del risultato uguale a quello della vista storica, con i dati (BD7)");
            // Report Word and the PDF beside it.
            string title = "Fixture F3 · " + Module + " · " + id;
            mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, []);
            historic.Editor.ExportReport(Path.Combine(folder, name + "-storica.docx"), title, []);
            string? part = DifferentPart(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.docx")), File.ReadAllBytes(Path.Combine(folder, name + "-storica.docx")), normalizer);
            Check(part is null, name + ": report Word uguale in ogni parte a quello della vista storica, figure comprese (date normalizzate)" + (part is null ? "" : ", diversa " + part));
            int attempts = 1;
            while (!SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-storica.pdf"))) && attempts < 3)
            {
                attempts++;
                mvvm.Editor.ExportReport(Path.Combine(folder, name + "-mvvm.docx"), title, []);
                historic.Editor.ExportReport(Path.Combine(folder, name + "-storica.docx"), title, []);
            }
            Check(SamePdf(File.ReadAllBytes(Path.Combine(folder, name + "-mvvm.pdf")), File.ReadAllBytes(Path.Combine(folder, name + "-storica.pdf"))),
                name + ": PDF affiancato uguale a quello della vista storica (date normalizzate" + (attempts > 1 ? $", {attempts} esportazioni" : "") + ")");
            reports++;
            // «Esporta quantità CSV», «Immagine PNG» (the view of the drawing) and «Esporta sezioni e quote CSV» (tab «Sezioni e quote»).
            string csv = Path.Combine(folder, name + "-quantita.csv"), png = Path.Combine(folder, name + "-ponte.png"), technical = Path.Combine(folder, name + "-quote.csv");
            services.Saves.Clear();
            services.SavePath = csv; Idle(() => { Press(Caption(view.DesignScroll, "Esporta quantità CSV")); return 0; });
            services.SavePath = png; Idle(() => { Press(Caption(view.DesignScroll, "Immagine PNG")); return 0; });
            Idle(() => { view.OutputTabs.SelectedIndex = 5; return 0; }); await mvvm.Settle();
            services.SavePath = technical; Idle(() => { Press(Caption(view.DesignScroll, BridgeDesignPresentation.TechnicalCsvButton)); return 0; });
            services.SavePath = null;
            Check(services.Saves.Select(s => s.FileName).SequenceEqual(["BridgeDesign_quantita.csv", "BridgeDesign.png", "BridgeDesign_sezioni_quote.csv"])
                && services.Saves.Select(s => FileFilter.Join(s.Filters)).SequenceEqual(["Quantità CSV|*.csv", "Immagine PNG|*.png", "Sezioni e quote CSV|*.csv"]),
                name + ": nomi proposti e filtri delle esportazioni della vista storica");
            Check(File.ReadAllBytes(csv).SequenceEqual(bom.Concat(Encoding.UTF8.GetBytes(BridgeDesignModel.Csv(past.Calculation))))
                && File.ReadAllBytes(technical).SequenceEqual(bom.Concat(Encoding.UTF8.GetBytes(BridgeDesignModel.TechnicalCsv(past.Data, past.Calculation))))
                && CompositeSectionHistoricTrace.Pixels(File.ReadAllBytes(png)) == CompositeSectionHistoricTrace.Pixels(past.Drawing.Png(false)),
                name + ": CSV delle quantità e delle quote e PNG del disegno uguali a quelli della vista storica");
            Check(Same(mvvm, historic) && mvvm.Modified == historic.Modified, name + ": le esportazioni non scrivono nei dati");
        }
        Log.Add($"   esportazioni: {reports} report");
        Check(reports >= 8, $"Report confrontati con la vista storica: {reports}");

        static bool Same(Hosted a, Hosted b) => Normalized(a.Editor.Data) == Normalized(b.Editor.Data);
    }

    /// <summary>The data and the results of the two views beside the exports when they differ, to read the difference.</summary>
    private static void Dump(string folder, string name, Hosted mvvm, Hosted historic)
    {
        string a = Normalized(mvvm.Editor.Data), b = Normalized(historic.Editor.Data);
        string ra = Normalized(mvvm.Editor.Result), rb = Normalized(historic.Editor.Result);
        if (a == b && ra == rb) return;
        File.WriteAllText(Path.Combine(folder, name + "-dati-mvvm.json"), a); File.WriteAllText(Path.Combine(folder, name + "-dati-storica.json"), b);
        File.WriteAllText(Path.Combine(folder, name + "-risultato-mvvm.json"), ra); File.WriteAllText(Path.Combine(folder, name + "-risultato-storica.json"), rb);
    }

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
    /// (BridgePricesView, built and never shown): the caption, the size (1350 × 780 without the borders), the texts, the tabs and the columns of the
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
        // The historic window on the same data, built off screen and never shown.
        var historicWindow = BridgePricesView.Create(host.Root, new PriceCatalogueViewModel((JsonObject)sheet.Data.DeepClone()));
        static string[] Texts(DependencyObject root) => Walk(root, popups: false).OfType<TextBlock>()
            .Where(t => t.TemplatedParent is null && t.Text.Length > 0 && !System.Windows.Data.BindingOperations.IsDataBound(t, TextBlock.TextProperty))
            .Select(t => t.Text).Distinct().Order(StringComparer.Ordinal).ToArray();
        static string[] Columns(DependencyObject root) => Walk(root, popups: false).OfType<DataGrid>()
            .SelectMany(g => g.Columns.Select(c => c.Header is TextBlock header ? header.Text : c.Header as string ?? "")).ToArray();
        static string[] Tabs(DependencyObject root) => Walk(root, popups: false).OfType<TabItem>().Select(t => t.Header as string ?? "").ToArray();
        static string[] Buttons(DependencyObject root) => Walk(root, popups: false).OfType<Button>().Select(b => b.Content as string ?? "").Where(c => c.Length > 0).Order(StringComparer.Ordinal).ToArray();
        string[] historicTexts = Texts((DependencyObject)historicWindow.Content), historicColumns = Columns((DependencyObject)historicWindow.Content),
            historicTabs = Tabs((DependencyObject)historicWindow.Content), historicButtons = Buttons((DependencyObject)historicWindow.Content);
        historicWindow.Content = null; historicWindow.Close();
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
        services.PriceScript = null;
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
        services.PriceScript = null;
        Check(services.PriceWindows == 2 && sheet.Data["pricing"] is JsonObject && host.Modified == modified + 1
            && sheet.Prices.OfType<BridgeDesignText>().Any(t => t.Role == BridgeDesignTextRole.Title), "«Applica prezzario»: il documento dei prezzi nei dati, una modifica, il pannello ricostruito");
        // Read-only (BD4): the editor is refused, its button locked.
        host.Editor.InspectRevision(); await host.Settle();
        open = Caption(view.DesignScroll, BridgeDesignPresentation.ManagePricesButton);
        Check(open is { IsEnabled: false }, "BD4: «Gestisci prezzario…» bloccato in una revisione in sola lettura");
    }

    // ---------------------------------------------------------------- 5. «Stampa scheda»

    private static async Task PrintPage(string root, string output)
    {
        var services = new Services();
        var data = BridgeDesignScenarios.FixtureData(root, "default");
        using var mvvm = Idle(() => new Hosted(Editors(services).Open(Module, data)));
        using var historic = Idle(() => new Hosted(new SheetEditor(Module, data)));
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
        await Idle(() => mvvm.Pending).WaitAsync(TimeSpan.FromSeconds(180)); await Idle(() => historic.Pending).WaitAsync(TimeSpan.FromSeconds(180));
        await mvvm.Settle(); await historic.Settle();
        var page = view.PrintPage(1000, 700); var past = historic.Historic.PrintPage(1000, 700);
        static string[] Texts(FrameworkElement? page) => page is Viewbox { Child: Panel panel } ? panel.Children.OfType<TextBlock>().Select(t => t.Text).ToArray() : [];
        Check(Texts(page).Length > 4 && Texts(page).SequenceEqual(Texts(past)), "La pagina di «Stampa scheda»: i testi della pagina storica (" + Texts(past).Length + ")");
        string a = CompositeSectionHistoricTrace.Pixels(Ui.DocumentSnapshot(page!)), b = CompositeSectionHistoricTrace.Pixels(Ui.DocumentSnapshot(past));
        File.WriteAllBytes(Path.Combine(output, "schermate", "stampa.png"), Ui.DocumentSnapshot(page!));
        Check(a == b, "La pagina di «Stampa scheda» uguale pixel per pixel a quella storica (" + a + ")");
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
            Idle(() => { host.View.Pages.SelectedIndex = 1; return 0; }); await host.Settle();
            await Idle(() => host.Sheet.RunOptimizationAsync(new BridgeDesignModel.OptimizationOptions { KeepPier = true, KeepFoundation = true, MinSpans = 3, MaxSpans = 6,
                DepthPercentMin = 100, DepthPercentMax = 115, DepthPercentStep = 15, PilePercentMin = 100, PilePercentMax = 150, PilePercentStep = 25 })).WaitAsync(TimeSpan.FromSeconds(300));
            await host.Settle();
            File.WriteAllBytes(Path.Combine(folder, $"ottimizzazione-{suffix}.png"), host.Png());
            written += 3;
        }
        Appearance.Set(AppAppearance.Light, false);
        // The historic view at the same size, for the comparison by eye.
        using (var historic = Idle(() => new Hosted(new SheetEditor(Module, BridgeDesignScenarios.FixtureData(root, "confronto-alternativa-a")), false, 1400, 1000)))
        {
            await Idle(() => historic.Pending).WaitAsync(TimeSpan.FromSeconds(180)); await historic.Settle();
            File.WriteAllBytes(Path.Combine(folder, "storica-progetto-chiara.png"), historic.Png());
            written++;
        }
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
        Check(written == 11, "Schermate della vista nei tre aspetti (progetto, confronto, ottimizzazione), della vista storica e della disposizione stretta: " + written + " PNG in " + folder + " (144 DPI su questa macchina)");
    }

    // ---------------------------------------------------------------- 7. help

    /// <summary>F1 opens the guide of the module through the navigator of the services (MvvmSheetEditor, as SheetEditor); no «?» on the fields, as the historic view (BD6).</summary>
    private static async Task Help(string root)
    {
        var services = new Services();
        var data = BridgeDesignScenarios.FixtureData(root, "default");
        using var host = Idle(() => new Hosted(Editors(services).Open(Module, data)));
        using var historic = Idle(() => new Hosted(new SheetEditor(Module, data)));
        await host.Settle(); await historic.Settle();
        static int Questions(DependencyObject root) => Walk(root).OfType<Button>().Count(b => b.IsVisible && b.Content as string == "?");
        Check(Questions(host.Root) == 0 && Questions(historic.Root) == 0, "Nessuna etichetta «?» sui campi, come la vista storica");
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
        foreach (string id in new[] { "default", "cassone-acciaio-prezzario", "strallato-ortotropo-fiume" })
        {
            if (!BridgeDesignScenarios.Fixtures(root).Contains(id)) { Log.Add("   nomi: fixture assente " + id); continue; }
            var data = BridgeDesignScenarios.FixtureData(root, id);
            using var historic = Idle(() => new Hosted(new SheetEditor(Module, data)));
            using var host = Idle(() => new Hosted(editors.Open(Module, data)));
            await Idle(() => historic.Pending).WaitAsync(TimeSpan.FromSeconds(180)); await Idle(() => host.Pending).WaitAsync(TimeSpan.FromSeconds(180));
            await historic.Settle(); await host.Settle();
            var before = new HashSet<string>(StringComparer.Ordinal); var after = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hosted in new[] { historic, host })
            {
                var names = ReferenceEquals(hosted, historic) ? before : after;
                foreach (var (page, input, output) in Places())
                {
                    Select(hosted.Root, page, input, output); await hosted.Settle();
                    names.UnionWith(Names(hosted.Root));
                }
            }
            var missing = before.Except(after).Order(StringComparer.Ordinal).ToArray();
            Log.Add($"   nomi nuovi ({id}): " + string.Join(", ", after.Except(before).Order(StringComparer.Ordinal)));
            Check(missing.Length == 0 && before.Count > 20, $"{id}: nomi di automazione della vista storica conservati ({before.Count})" + (missing.Length == 0 ? "" : "; mancanti: " + string.Join(", ", missing)));
        }
    }

    /// <summary>Every place of the view: the five tabs of the inputs and the six of the results on the page of the project, and the page of the search.</summary>
    private static IEnumerable<(int Page, int Input, int Output)> Places()
    {
        for (int input = 0; input < 5; input++) yield return (0, input, 0);
        for (int output = 0; output < 6; output++) yield return (0, 0, output);
        yield return (1, 0, 0);
    }

    /// <summary>The page and the tabs chosen on either view (the first three tab controls of the tree: pages, inputs, results).</summary>
    private static void Select(DependencyObject root, int page, int input, int output)
    {
        var tabs = Walk(root, popups: false).OfType<TabControl>().Take(3).ToArray();
        tabs[0].SelectedIndex = page;
        if (page == 0) { tabs[1].SelectedIndex = input; tabs[2].SelectedIndex = output; }
    }

    // ---------------------------------------------------------------- 9. read-only

    /// <summary>
    /// The controls that accept the user after InspectRevision, in the new view and in the historic view on the same data, in every place; the only
    /// difference is «Gestisci prezzario…», locked in the new view (BD4).
    /// </summary>
    private static async Task ReadOnly(string root)
    {
        foreach (string id in new[] { "default", "confronto-alternativa-a" })
        {
            var data = BridgeDesignScenarios.FixtureData(root, id);
            using var host = Idle(() => new Hosted(Editors(new Services()).Open(Module, data)));
            using var historic = Idle(() => new Hosted(new SheetEditor(Module, data)));
            await Idle(() => host.Pending).WaitAsync(TimeSpan.FromSeconds(180)); await Idle(() => historic.Pending).WaitAsync(TimeSpan.FromSeconds(180));
            await host.Settle(); await historic.Settle();
            host.Editor.InspectRevision(); historic.Editor.InspectRevision();
            int modified = host.Modified;
            var before = new Dictionary<string, string>(StringComparer.Ordinal); var after = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var hosted in new[] { historic, host })
            {
                var map = ReferenceEquals(hosted, historic) ? before : after;
                foreach (var (page, input, output) in Places())
                {
                    Select(hosted.Root, page, input, output); await hosted.Settle();
                    foreach (var (key, value) in Controls(hosted.Root)) map[key] = map.TryGetValue(key, out var previous) && previous != value ? "misto" : value;
                }
            }
            var common = before.Keys.Intersect(after.Keys).Order(StringComparer.Ordinal).ToArray();
            var different = common.Where(k => before[k] != after[k]).Select(k => k + ": storica " + before[k] + ", nuova " + after[k]).ToArray();
            Log.Add($"   sola lettura ({id}), solo nella storica: " + string.Join(", ", before.Keys.Except(after.Keys).Order(StringComparer.Ordinal)));
            Log.Add($"   sola lettura ({id}), solo nella nuova: " + string.Join(", ", after.Keys.Except(before.Keys).Order(StringComparer.Ordinal)));
            Log.Add($"   sola lettura ({id}), comuni: " + string.Join(", ", common.Select(k => k + " " + after[k])));
            string prices = "pulsante " + BridgeDesignPresentation.ManagePricesButton;
            Check(different.SequenceEqual([prices + ": storica libero, nuova bloccato"]) && common.Length >= 30,
                $"{id}: sola lettura, stessi controlli bloccati e liberi della vista storica ({common.Length} controlli comuni), salvo «Gestisci prezzario…» (BD4)"
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
