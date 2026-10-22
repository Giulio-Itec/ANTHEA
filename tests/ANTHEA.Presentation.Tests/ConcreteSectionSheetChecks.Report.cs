using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Concrete;
using Anthea.Application.Modules;
using Anthea.Application.Reporting;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Testing.Normalization;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The reports of the RC section (F5.10, sub-step S9): the step «report» of the traces (report/*) as the historic trace drives the two commands of
/// the window, and the checks beyond the traces: refusals while the calculation waits, the sections saved with Modified (not for the report of a
/// project), the export given the data, the 2D domain drawn while hidden, the drawings handed to the renderer, the short report, the read-only
/// revision, a sheet without a renderer. The report itself (text and structure, also of the short report) is compared with the F3 reference in
/// <see cref="DataFilesAsync"/>; the images are drawn only in the desktop (ui/f3-report-chapters, ui/check-sezione-view).
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    /// <summary>The figures of the report as ModuleFigureRenderer.ConcreteFigures plans them (captions and categories); the images are only a PNG header.</summary>
    private static readonly PlanFigures Figures = new();

    /// <summary>The sections of the report of the F3 capture and of the historic trace (every section except «dettagli» and «sle_tutte»).</summary>
    private static HashSet<string> ReportSections() => ReportConcrete.Sections.Where(s => s.Key is not ("dettagli" or "sle_tutte")).Select(s => s.Key).ToHashSet();

    private sealed partial class Side
    {
        /// <summary>
        /// The two report commands of the window, as the historic trace drives them («Report Word»: the check of MainWindow.ExportReport, then the
        /// report with the sections of the F3 capture; «Report short»): only whether the report is available, or the message shown.
        /// </summary>
        private string Report(string kind)
        {
            try
            {
                if (kind == "breve") { Sheet.Commit(); _ = ((IModuleShortReport)Sheet.Report).BuildShortReport("Traccia F5.10 breve"); return "report breve disponibile"; }
                Sheet.Commit();
                if (!Sheet.HasResults || Sheet.IsBusy) return "report completo: Attendere l’aggiornamento automatico e correggere gli eventuali dati non validi.";
                _ = Sheet.Report.BuildReport("Traccia F5.10", ReportSections());
                return "report completo disponibile";
            }
            catch (Exception error) { return "report " + kind + ": " + error.Message; }
        }
    }

    // ---------------------------------------------------------------- beyond the traces

    private static async Task ReportAsync(string root, Expect expect)
    {
        var host = new StrictHost();
        using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"), host);
        var shortReport = (IModuleShortReport)sheet.Report;
        expect.That(sheet.IsCalculationQueued && !sheet.HasResults, "Report: calcolo dell'apertura in coda");
        expect.Throws<InvalidOperationException>(() => sheet.Report.BuildReport("Report", ReportSections()), "Report rifiutato con il calcolo in coda");
        expect.Throws<InvalidOperationException>(() => shortReport.BuildShortReport("Breve"), "Report breve rifiutato con il calcolo in coda");
        try { sheet.Report.BuildReport("Report", ReportSections()); } catch (InvalidOperationException error) { expect.That(error.Message == ConcreteSheetViewModel.ReportUnavailable, "Messaggio del report rifiutato"); }
        try { shortReport.BuildShortReport("Breve"); } catch (InvalidOperationException error) { expect.That(error.Message == ConcreteSheetViewModel.ShortReportUnavailable, "Messaggio del report breve rifiutato"); }
        await IdleAsync(sheet);
        int modified = 0; sheet.Modified += (_, _) => modified++;
        expect.That(sheet.SelectedTab == 0 && sheet.Domain2D.Chart!.Segments.Count == 0, "Dominio 2D nascosto e non ancora disegnato");
        int requests = Figures.Requests.Count;
        var sections = ReportSections();
        byte[] report = sheet.Report.BuildReport("Report", sections);
        var drawings = Figures.Requests[^1].Drawings;
        expect.That(report.Length > 0 && Figures.Requests.Count == requests + 1 && Figures.Requests[^1].Options.SetEquals(sections), "Report con i disegni chiesti una volta");
        expect.That(sheet.Settings["report_sezioni"]!.ToJsonString() == J.Node(sections.OrderBy(k => k).ToArray())!.ToJsonString() && modified == 1,
            "Sezioni del report salvate in ordine con Modified");
        expect.That(ReferenceEquals(drawings.Result, sheet.Result) && JsonNode.DeepEquals(sheet.Result!["dati"], sheet.Data) && ReferenceEquals(drawings.Data, sheet.Data),
            "Esportazione dei risultati con i dati del foglio");
        expect.That(drawings.Domains.Count == 2 && drawings.Domains[0].ThreeD && !drawings.Domains[1].ThreeD && ReferenceEquals(drawings.Domains[1].Chart, sheet.Domain2D.Chart)
            && drawings.Domains[0].Rows.SequenceEqual(sheet.Domain3D.Table.Table.Shown.Select(r => r.Values)) && drawings.Domains[0].Selected == sheet.Domain3D.Table.Table.Selected?.Values.S("id")
            && drawings.Domains[0].Three is not null && drawings.Domains[1].Two is not null && drawings.Domains[0].Checks is not null,
            "Domini del report: 3D poi 2D, righe mostrate e selezione della tabella, domini e verifiche");
        expect.That(sheet.Domain2D.Chart.Segments.Count > 0, "Dominio 2D disegnato anche nascosto per la sua figura");
        expect.That(drawings.Stresses.Keys.SequenceEqual(["SLE", "SLE_FREQ", "SLE_QP"]) && ReferenceEquals(drawings.Stresses["SLE"].Map, sheet.StressPanel("SLE").Map)
            && drawings.Stresses["SLE"].Checks is not null && ReferenceEquals(drawings.Preview, sheet.Inputs.Drawing) && drawings.Curvature is null,
            "Famiglie SLE, anteprima e curva (assente) del report");
        byte[] project = sheet.Report.BuildReport("Capitolo", sections, projectReport: true);
        expect.That(project.Length > 0 && modified == 1, "Report del progetto: sezioni non salvate, nessun Modified");
        byte[] brief = shortReport.BuildShortReport("Breve");
        expect.That(brief.Length > 0 && DocxText.Extract(brief, new TextNormalizer([])) == DocxText.Extract(ReportConcreteShort.Create("Breve", SectionActionInputs.CalculationCopy(sheet.Data), sheet.Result!), new TextNormalizer([])),
            "Report breve del generatore con i dati e i risultati del foglio");
        expect.That(host.Calls == 0, "Report senza richieste all'ospite");
        // The curve published enters the drawings (and the figure «Momento–curvatura»).
        await sheet.Curvature.CalculateCommand.ExecuteAsync(null);
        await IdleAsync(sheet);
        sheet.Report.BuildReport("Report", new HashSet<string> { "curvatura", "grafici" });
        expect.That(sheet.Curvature.Result is not null && ReferenceEquals(Figures.Requests[^1].Drawings.Curvature, sheet.Curvature.Result), "Curva pubblicata nei disegni del report");
        // Read-only revision: the report is available and saves its sections with Modified, as the historic view (C5).
        sheet.IsReadOnly = true;
        sheet.Report.BuildReport("Report", new HashSet<string> { "geometria" });
        expect.That(sheet.Settings["report_sezioni"]!.ToJsonString() == "[\"geometria\"]" && modified == 3, "Sola lettura: report disponibile, sezioni salvate con Modified (storico, C5)");
        // A sheet without a renderer (only the checks build one): the figures are refused, a report without «grafici» is written.
        using var bare = new ConcreteSheetViewModel(new SheetContext(ConcreteSectionScenarios.FixtureData(root, "default"), false, new Host(), Scripted.Services(),
            ModuleServices.Get(ConcreteSectionSheetModule.Id), new CalculationLimiter(1)), new ConcreteAnalysisSession());
        await IdleAsync(bare);
        expect.Throws<InvalidOperationException>(() => bare.Report.BuildReport("Report", ReportSections()), "Senza disegni: report con i grafici rifiutato");
        expect.That(bare.Report.BuildReport("Report", new HashSet<string> { "geometria", "materiali" }).Length > 0, "Senza disegni: report senza grafici");
        sheet.Dispose();
        expect.Throws<ObjectDisposedException>(() => sheet.Report.BuildReport("Report", ReportSections()), "Report dopo la chiusura rifiutato");
        expect.Throws<ObjectDisposedException>(() => shortReport.BuildShortReport("Breve"), "Report breve dopo la chiusura rifiutato");
    }

    /// <summary>Tables, rows, cells, images and styles of a report, as F3EquivalenceCapture.CaptureReport counts them.</summary>
    private static JsonObject ReportStructure(byte[] docx)
    {
        using var zip = new ZipArchive(new MemoryStream(docx));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        var document = XDocument.Load(stream);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
        return new JsonObject
        {
            ["tables"] = document.Descendants(w + "tbl").Count(),
            ["rows"] = document.Descendants(w + "tr").Count(),
            ["cells"] = document.Descendants(w + "tc").Count(),
            ["images"] = new JsonArray(document.Descendants(wp + "extent").Select(e => (JsonNode)new JsonObject {
                ["cx"] = (string?)e.Attribute("cx"), ["cy"] = (string?)e.Attribute("cy") }).ToArray()),
            ["styles"] = new JsonArray(document.Descendants(w + "pStyle").Select(e => JsonValue.Create((string?)e.Attribute(w + "val"))).ToArray())
        };
    }

    /// <summary>
    /// The figures of the report as ModuleFigureRenderer.ConcreteFigures plans them from the drawings of the sheet: the same order, captions and
    /// categories (the captions enter the text of the report), images of the same size; every request is kept.
    /// </summary>
    private sealed class PlanFigures : IConcreteSectionFigureRenderer
    {
        internal List<(ConcreteReportDrawings Drawings, IReadOnlySet<string> Options)> Requests { get; } = [];

        public IReadOnlyList<ReportFigure> Render(ConcreteReportDrawings drawings, IReadOnlySet<string> options)
        {
            Requests.Add((drawings, options));
            var figures = new List<ReportFigure>();
            if (!options.Contains("grafici")) return figures;
            var settings = drawings.Data["workspace_ca"]!.AsObject();
            if (options.Contains("geometria")) figures.Add(Figure("Sezione e schema indicativo delle staffe", "geometria"));
            foreach (var panel in drawings.Domains)
            {
                string category = panel.ThreeD ? "dominio3d" : "dominio2d", key = panel.Options.S("stato", "SLU");
                if (!options.Contains(category)) continue;
                if (panel.ThreeD && panel.Three is not null)
                    figures.Add(Figure("Dominio 3D " + (panel.Options.ContainsKey("modello_resistente") ? key + " · " + panel.Options.S("modello_resistente") : ConcreteEditor.Label(key))
                        + " in vista isometrica con filtri e livelli correnti", category));
                else if (!panel.ThreeD && panel.Two is not null) figures.Add(Figure(panel.Chart!.Title + " nella configurazione grafica corrente", category));
            }
            foreach (var (key, stress) in drawings.Stresses)
            {
                if (!options.Contains(key) || stress.Checks is not { } outcomes) continue;
                var rows = drawings.Result["tensioni"]?[key] as JsonObject ?? new();
                var governors = new Dictionary<string, string>();
                foreach (bool cracking in new[] { false, true })
                    if ((cracking ? ConcreteEditor.HasCrackCheck(key, settings) : ConcreteEditor.HasStressCheck(key)) && ReportConcrete.Governing(rows, cracking) is { } governing)
                    {
                        string reason = cracking ? "fessurazione" : "tensioni";
                        governors[governing.Key] = governors.TryGetValue(governing.Key, out var previous) ? previous + " e " + reason : reason;
                    }
                foreach (var (id, reason) in governors)
                {
                    if (outcomes.GetValueOrDefault(id)?.State is null) continue;
                    string name = stress.Rows.FirstOrDefault(row => row.S("id") == id)?.S("nome") ?? id;
                    figures.Add(Figure("SLE " + ConcreteEditor.Label(key) + " · governante " + reason + " · " + name + " · " + stress.Map.Contour, key));
                }
            }
            if (options.Contains("taglio")) figures.Add(Figure("Schema indicativo delle staffe · diametro rappresentato in scala", "taglio"));
            if (options.Contains("curvatura") && drawings.Curvature is not null) figures.Add(Figure("Momento–curvatura · percorso assegnato", "curvatura"));
            return figures;
        }

        private static ReportFigure Figure(string caption, string category) => new(caption, Png(1200, 750), category);

        /// <summary>The signature and the header of a PNG of the given size.</summary>
        private static byte[] Png(int width, int height)
        {
            var png = new byte[33];
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52 }.CopyTo(png, 0);
            BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(16, 4), width);
            BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(20, 4), height);
            png[24] = 8; png[25] = 6;
            return png;
        }
    }
}
