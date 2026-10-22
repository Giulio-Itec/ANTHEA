using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.BridgeDesign;
using Anthea.Presentation.Modules.Materials;
using Anthea.Presentation.Modules.Stratigraphy;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Reports;
using Anthea.Testing.Normalization;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// F5.13 c: the files of the sheets written by the shell (<see cref="SheetFileExports"/>, the code of MvvmSheetEditor shared with it), without
/// WPF, on sheets opened by the coordinator: the JSON of the result (refused without results; the data of Bridge Design beside it, BD7), the
/// three ways of the report (material chapter, PDF of the generator of the module, PDF rendered from the DOCX), the short report refused with the
/// text of MvvmSheetEditor and its writing within two pages. The window of today runs the same members (MvvmSheetEditor), checked through the
/// WPF suites of the views at night.
/// </summary>
internal static class SheetExportsChecks
{
    private const string PdfModule = "geo_fondazioni_superficiali";

    internal static int Run(string output) => SingleThreadContext.Run(_ => RunAsync(output));

    private static async Task<int> RunAsync(string output)
    {
        var expect = new Expect();
        string folder = Path.Combine(output, "shell-esportazioni");
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        var services = Scripted.Services();
        var registry = new ModuleRegistry([new ProbeSheetModule(ModuleDirectory.Get("geo_palo_verticale")), new ConcreteMaterialSheetModule(),
            new StratigraphySheetModule(new(Anthea.Application.Stratigraphy.StratigraphyImport.OpenAi(), Anthea.Application.Stratigraphy.StratigraphyImport.EnvironmentDefaults)),
            new BridgeDesignSheetModule(new PlainFigures()), new PdfProbeModule(ModuleDirectory.Get(PdfModule))]);
        var factory = new RegistrySheetFactory(registry, services, new CalculationLimiter(CalculationLimiter.DefaultMaximum),
            m => m is "geo_palo_verticale" or PdfModule ? new ShellBench.EchoService(m) : ModuleServices.Get(m));
        using var workspace = new WorkspaceSession(services, factory, new DocumentFiles(Path.Combine(folder, "Esempi")));
        ISheetExports exports = new SheetFileExports();
        string File(string name) => Path.Combine(folder, name);

        // Results: refused without results with the text of MvvmSheetEditor; then the JSON of the result, UTF-8 without BOM, J.Options.
        var probe = workspace.NewCalculation("geo_palo_verticale");
        var refused = Refusal(() => exports.ExportResult(probe, File("probe.json")));
        expect.That(refused == SheetFileExports.CalculateFirst && !System.IO.File.Exists(File("probe.json")), "Risultati senza calcolo rifiutati, nessun file");
        await Expect.Within(probe.ViewModel.CalculateAsync());
        exports.ExportResult(probe, File("probe.json"));
        expect.That(System.IO.File.ReadAllBytes(File("probe.json")).SequenceEqual(Encoding.UTF8.GetBytes(probe.ViewModel.Result!.ToJsonString(J.Options))),
            "JSON del risultato uguale al risultato del foglio");
        expect.That(JsonNode.Parse(System.IO.File.ReadAllText(File("probe.json")))!["dati"] is null, "Senza i dati per gli altri moduli");

        // BD7: the JSON of Bridge Design carries the data of the sheet, as SheetEditor and MvvmSheetEditor.
        var bridge = workspace.NewCalculation(BridgeDesignModel.Module);
        await Expect.Within(bridge.ViewModel.CalculateAsync());
        expect.That(bridge.ViewModel.HasResults, "Bridge Design calcolato sui dati iniziali");
        exports.ExportResult(bridge, File("bridge.json"));
        var exported = JsonNode.Parse(System.IO.File.ReadAllText(File("bridge.json")))!.AsObject();
        expect.That(exported["dati"] is JsonObject data && JsonNode.DeepEquals(data, bridge.ViewModel.Data), "BD7: il JSON di Bridge Design contiene i dati del foglio");
        exported.Remove("dati");
        var result = bridge.ViewModel.Result!;
        result.Remove("dati");
        expect.That(JsonNode.DeepEquals(exported, result), "BD7: per il resto il JSON è il risultato del foglio");

        // Report of a sheet with its own PDF generator (T8): the DOCX of the report and the PDF of the generator beside it, byte for byte.
        var pdf = workspace.NewCalculation(PdfModule);
        var options = new HashSet<string> { "verifiche" };
        exports.ExportReport(pdf, File("fondazioni.docx"), "Titolo", options);
        expect.That(System.IO.File.ReadAllText(File("fondazioni.docx")) == "docx|Titolo|verifiche|False" && System.IO.File.ReadAllText(File("fondazioni.pdf")) == "pdf|Titolo|verifiche|False",
            "Report con il PDF del generatore del modulo (T8)");
        SheetFileExports.WriteReport(pdf.ViewModel, File("fondazioni-progetto.docx"), "Titolo", options, projectReport: true);
        expect.That(System.IO.File.ReadAllText(File("fondazioni-progetto.pdf")) == "pdf|Titolo|verifiche|True", "Report dentro il report di progetto");

        // Report of the other sheets: the DOCX of the report source and the PDF rendered from it (ReportFiles.Write).
        var normalizer = new TextNormalizer();
        var stratigraphy = workspace.NewCalculation("geo_stratigrafia");
        exports.ExportReport(stratigraphy, File("stratigrafia.docx"), "Stratigrafia di prova", new HashSet<string>());
        string expected = DocxText.Extract(stratigraphy.ViewModel.Report.BuildReport("Stratigrafia di prova", new HashSet<string>()), normalizer);
        expect.That(DocxText.Extract(System.IO.File.ReadAllBytes(File("stratigrafia.docx")), normalizer) == expected, "Report Word dal generatore del foglio");
        expect.That(Pdf(File("stratigrafia.pdf")), "PDF reso dal DOCX accanto al report");

        // Report of a material sheet: the chapter of the material, Word and PDF from the same document (MaterialReportChapter.Write).
        var material = workspace.NewCalculation("mat_calcestruzzo");
        exports.ExportReport(material, File("calcestruzzo.docx"), "Calcestruzzo di prova", new HashSet<string>());
        Materiali.MaterialReportChapter.Write(File("calcestruzzo-diretto.docx"), "mat_calcestruzzo", "Calcestruzzo di prova", material.ViewModel.Data);
        expect.That(DocxText.Extract(System.IO.File.ReadAllBytes(File("calcestruzzo.docx")), normalizer)
            == DocxText.Extract(System.IO.File.ReadAllBytes(File("calcestruzzo-diretto.docx")), normalizer) && Pdf(File("calcestruzzo.pdf")),
            "Report della scheda materiale come il capitolo del materiale");

        // Short report: refused for the hosted sheets with the text of MvvmSheetEditor; the writing keeps the limit of two pages.
        expect.That(Refusal(() => exports.BuildShortReport(stratigraphy, "Breve")) == SheetFileExports.ShortReportUnavailable, "Report short rifiutato");
        byte[] stratigraphyReport = stratigraphy.ViewModel.Report.BuildReport("Stratigrafia di prova", new HashSet<string>());
        var pages = ReportFiles.Write(File("pagine.docx"), stratigraphyReport, "Stratigrafia di prova").Pages;
        string? failure = null;
        try { await Expect.Within(exports.WriteShortReportAsync(File("short.docx"), stratigraphyReport, "Stratigrafia di prova")); }
        catch (InvalidOperationException error) { failure = error.Message; }
        expect.That(pages > ReportFiles.ShortReportPages
            ? failure?.StartsWith("Il contenuto richiede " + pages + " pagine", StringComparison.Ordinal) == true && !System.IO.File.Exists(File("short.docx"))
            : failure is null && System.IO.File.Exists(File("short.docx")) && Pdf(File("short.pdf")),
            "Report short entro due pagine, altrimenti nessun file");
        expect.Throws<ArgumentNullException>(() => exports.ExportResult(null!, File("x.json")), "Foglio assente");
        return expect.Count;
    }

    private static string? Refusal(Action action)
    {
        try { action(); return null; }
        catch (InvalidOperationException error) { return error.Message; }
    }

    private static bool Pdf(string path) => System.IO.File.Exists(path) && System.IO.File.ReadAllBytes(path).Take(4).SequenceEqual("%PDF"u8.ToArray());

    /// <summary>Drawings of Bridge Design as bytes: the JSON of the result does not read them.</summary>
    private sealed class PlainFigures : IBridgeDesignFigureRenderer
    {
        public byte[] Drawing(JsonObject data, BridgeDesignModel.Result result, bool section) => Encoding.UTF8.GetBytes(section ? "sezione" : "prospetto");
    }

    /// <summary>A module whose report source gives its own PDF (IModuleReportPdf), as the shallow foundations: the bytes name the call.</summary>
    private sealed class PdfProbeModule(ModuleInfo info) : ISheetModule
    {
        public ModuleInfo Info { get; } = info;
        public ModuleHelp Help { get; } = new(null);
        public ModulePersistence Persistence { get; } = new([]);
        public ModuleReportCapabilities Reports { get; } = new(false);
        public ISheetViewModel CreateSheet(SheetContext context) => new PdfProbeSheet(Info.Id, context);
    }

    private sealed class PdfProbeSheet(string module, SheetContext context) : SheetViewModelBase(module, context)
    {
        public override IModuleReportSource Report { get; } = new Edition();

        private sealed class Edition : IModuleReportSource, IModuleReportPdf
        {
            public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false) => Text("docx", title, options, projectReport);
            public byte[] BuildPdf(string title, IReadOnlySet<string> options, bool projectReport = false) => Text("pdf", title, options, projectReport);
            private static byte[] Text(string kind, string title, IReadOnlySet<string> options, bool projectReport) =>
                Encoding.UTF8.GetBytes(kind + "|" + title + "|" + string.Join(",", options.Order(StringComparer.Ordinal)) + "|" + projectReport);
        }
    }
}
