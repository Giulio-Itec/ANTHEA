using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Reports;
using X.Core;
namespace Anthea.Application.Reporting;

public sealed record ReportFigure(string Caption, byte[] Png, string Category, int? Index = null, double AspectRatio = 1.6)
{
    public ImmagineReport Image() => new(Caption, Png, Category, Index);
}
public sealed record ModuleReportRequest(string Title, ModuleCalculation Calculation, IReadOnlySet<string> Options, bool ProjectReport, object? Context = null);

/// <summary>The application requests figures; a renderer owns only graphics and never creates an editor or calculates a sheet.</summary>
public interface IFigureRenderer
{
    Task<IReadOnlyList<ReportFigure>> RenderAsync(ModuleReportRequest request, CancellationToken cancellation);
}

public sealed class ModuleReportService(IFigureRenderer renderer) : IProjectReportChapterBuilder
{
    public static bool Supports(string module) => module is EmbeddedWalls.EmbeddedWallDocument.Module or Stratigraphy.StratigraphyDocument.Module or "geo_palo_verticale" or "geo_micropalo_verticale" or BridgeConcept.Module or HorizontalPileGroup.Module or Anthea.FoundationWorkspace.FoundationDocument.Module or RebarMaterial.Module or "mat_calcestruzzo" or RetainingWall.Module or BridgeSection.Module or PaloOrizzontale.Module or MicropaloOrizzontale.Module or "str_palo";
    public static HashSet<string> DefaultSections(string module) => module == BridgeSection.Module ? ReportBridge.DefaultSections()
        : module == "str_palo" ? ReportConcrete.Sections.Where(s => s.Key is not ("dettagli" or "sle_tutte")).Select(s => s.Key).ToHashSet()
        : ReportWord.Sezioni.Where(s => s.Key != "dettagli").Select(s => s.Key).ToHashSet();

    public async Task<ReportProject.SheetContent> BuildAsync(JsonObject sheet, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        string module = sheet.S("modulo_id");
        if (module == EmbeddedWalls.EmbeddedWallDocument.Module)
        {
            var input = sheet["dati"] as JsonObject ?? throw new ArgumentException("Dati della paratia assenti.");
            var result = EmbeddedWalls.EmbeddedWallDocument.ReadCurrentResult(input)
                ?? throw new ArgumentException("Risultati della paratia assenti o superati. Calcolare e salvare il foglio prima della relazione; nessun ricalcolo nascosto.");
            return new(Document: EmbeddedWallReport.Create(sheet.S("nome"), result));
        }
        if (module == Stratigraphy.StratigraphyDocument.Module)
        {
            var profile = sheet["dati"] as JsonObject ?? Stratigraphy.StratigraphyDocument.Create();
            Stratigraphy.StratigraphyDocument.Validate(profile);
            return new(Document: StratigraphyReport.Create(sheet.S("nome"), profile));
        }
        if (!Supports(module)) throw new NotSupportedException("Capitolo autonomo non ancora disponibile: " + module);
        var source = sheet["dati"] as JsonObject ?? ModuleCatalog.CreateData(module);
        var data = (JsonObject)source.DeepClone();
        if (module == "mat_calcestruzzo") data = Materiali.MaterialSheetInput.NormalizeState(data);
        else if (module == "str_palo") Anthea.Application.Concrete.ConcreteSheetPreparation.PrepareReport(data);
        else if (module is PaloOrizzontale.Module or MicropaloOrizzontale.Module) Anthea.Application.Presentation.HorizontalSheetPresentation.PrepareReport(data);
        else if (module == BridgeConcept.Module) ModuleSheetPreparation.PrepareBridgeDesign(data);
        else if (module == BridgeSection.Module) Anthea.Application.Presentation.BridgeSheetPresentation.PrepareReport(data);
        else if (module == RetainingWall.Module) Anthea.Application.Presentation.WallSheetPresentation.Prepare(data);
        else if (module == HorizontalPileGroup.Module) ModuleSheetPreparation.PreparePileGroup(data);
        else if (module == Anthea.FoundationWorkspace.FoundationDocument.Module)
        { Anthea.FoundationWorkspace.FoundationDocument.ValidateShape(data); Anthea.FoundationWorkspace.FoundationMaterials.Complete(data); }
        else if (module is "geo_palo_verticale" or "geo_micropalo_verticale") ModuleSheetPreparation.PrepareVertical(data, module);
        sheet["dati"] = data;
        if (module is RebarMaterial.Module or "mat_calcestruzzo") return Materiali.MaterialReportChapter.Create(module, data);
        ModuleCalculation calculation;
        try
        {
            if (module == "str_palo")
            {
                var concrete = await Anthea.Application.Concrete.ConcreteReportAnalysis.CalculateAsync(data, cancellation);
                calculation = new(module, concrete.Data, concrete.Result, concrete);
                sheet["dati"] = concrete.Data;
            }
            else if (module is PaloOrizzontale.Module or MicropaloOrizzontale.Module && data.S("vista_orizzontale") == "elastico")
            {
                using var analysis = new ElasticPileAnalysisSession();
                var result = await analysis.CalculateAsync(data, _ => { }, cancellation: cancellation);
                calculation = new(module, (JsonObject)data.DeepClone(), result);
            }
            else calculation = await Task.Run(() => ModuleServices.Get(module).Calculate(data, cancellation), cancellation);
        }
        catch (Exception error) when (error is not OperationCanceledException && error is not IOException && error is not UnauthorizedAccessException)
        { throw new ArgumentException("Dati incompleti o calcolo non riuscito. Aprire la scheda per correggere gli input.", error); }
        if (calculation.HasError) throw new ArgumentException("Dati incompleti o calcolo non riuscito. Aprire la scheda per correggere gli input.");
        if (module == HorizontalPileGroup.Module) ModuleSheetPreparation.ApplyPileGroupSelection(data, calculation.Values);
        if (module == BridgeSection.Module) Anthea.Application.Presentation.BridgeSheetPresentation.ApplyReportResult(data, calculation.NativeAs<BridgeResult>());
        object? context = null;
        if (module == RetainingWall.Module)
        {
            var wall = calculation.NativeAs<RetainingWall.Result>();
            Anthea.Application.Presentation.WallSheetPresentation.ApplyResult(data, wall);
            context = await Task.Run(() => WallReportPresentation.ForProject(wall), cancellation);
        }
        var request = new ModuleReportRequest(sheet.S("nome"), calculation, DefaultSections(module), true, context);
        IReadOnlyList<ReportFigure> figures = (module is HorizontalPileGroup.Module or Anthea.FoundationWorkspace.FoundationDocument.Module) || calculation.Values.S("tipo_risultato") == "palo_elastico"
            ? Array.Empty<ReportFigure>() : await renderer.RenderAsync(request, cancellation);
        cancellation.ThrowIfCancellationRequested();
        string? chapterError = calculation.Values["errori_calcolo"] is JsonObject errors && errors.Count > 0
            ? string.Join("; ", errors.Select(p => p.Key + ": " + p.Value)) : null;
        return new(Error: chapterError, Document: CreateDocument(request, figures));
    }
    public static DocxDocument CreateDocument(ModuleReportRequest request, IReadOnlyList<ReportFigure> figures)
    {
        var c = request.Calculation;
        if (c.ModuleId == EmbeddedWalls.EmbeddedWallDocument.Module) return EmbeddedWallReport.Create(request.Title, c.NativeAs<EmbeddedWallReportData>());
        if (c.ModuleId == "str_palo") return ReportConcrete.CreateDocument(request.Title, Anthea.Application.Concrete.SectionActionInputs.CalculationCopy(c.Data), c.Values, request.Options.ToHashSet(), figures.Select(f => f.Image()).ToList(), !request.ProjectReport);
        if (c.ModuleId is PaloOrizzontale.Module or MicropaloOrizzontale.Module)
            return c.Values.S("tipo_risultato") == "palo_elastico" ? ReportElasticPile.CreateDocument(request.Title, c.Values)
                : ReportOrizzontale.CreateDocument(request.Title, c.Values, !request.ProjectReport,
                    figures.Select(f => new ReportOrizzontale.Figure(f.Index!.Value, f.Caption, f.Png, f.AspectRatio)).ToList());
        if (c.ModuleId == BridgeSection.Module) return ReportBridge.CreateDocument(request.Title, c.NativeAs<BridgeResult>(), request.Options.ToHashSet(), figures.Select(f => f.Image()).ToList());
        if (c.ModuleId == RetainingWall.Module) return ReportRetainingWall.CreateDocument(request.Title, c.NativeAs<RetainingWall.Result>(),
            figures.Select(f => new ReportRetainingWall.Figure(f.Caption, f.Png, f.AspectRatio)).ToList());
        if (c.ModuleId == HorizontalPileGroup.Module) return ReportPileGroup.CreateDocument(request.Title, c.Values);
        if (c.ModuleId == Anthea.FoundationWorkspace.FoundationDocument.Module) return Anthea.FoundationWorkspace.FoundationReport.WordDocument(request.Title, c.Values);
        if (c.ModuleId == BridgeConcept.Module)
            return BridgeConceptExport.ReportDocument(request.Title, c.Data, c.NativeAs<BridgeConcept.Result>(),
                figures.Single(f => f.Category == "prospetto").Png, figures.Single(f => f.Category == "sezione").Png);
        return ReportWord.CreateDocument(request.Title, c.ModuleId, c.Data, c.Values, request.Options.ToHashSet(), figures.Select(f => f.Image()).ToList(), !request.ProjectReport);
    }
}
