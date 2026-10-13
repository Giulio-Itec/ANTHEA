using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Reports;
using X.Core;
namespace Anthea.Application.Reporting;

public sealed record ReportFigure(string Caption, byte[] Png, string Category, int? Index = null, double AspectRatio = 1.6)
{
    public ImmagineReport Image() => new(Caption, Png, Category, Index);
}
public sealed record ModuleReportRequest(string Title, ModuleCalculation Calculation, IReadOnlySet<string> Options, bool ProjectReport);

/// <summary>The application requests figures; a renderer owns only graphics and never creates an editor or calculates a sheet.</summary>
public interface IFigureRenderer
{
    Task<IReadOnlyList<ReportFigure>> RenderAsync(ModuleReportRequest request, CancellationToken cancellation);
}

public sealed class ModuleReportService(IFigureRenderer renderer) : IProjectReportChapterBuilder
{
    public static bool Supports(string module) => module is "geo_palo_verticale" or "geo_micropalo_verticale" or BridgeConcept.Module;
    public static HashSet<string> DefaultSections(string module) => module == BridgeSection.Module ? ReportBridge.DefaultSections()
        : module == "str_palo" ? ReportConcrete.Sections.Where(s => s.Key is not ("dettagli" or "sle_tutte")).Select(s => s.Key).ToHashSet()
        : ReportWord.Sezioni.Where(s => s.Key != "dettagli").Select(s => s.Key).ToHashSet();

    public async Task<ReportProject.SheetContent> BuildAsync(JsonObject sheet, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        string module = sheet.S("modulo_id");
        if (!Supports(module)) throw new NotSupportedException("Capitolo autonomo non ancora disponibile: " + module);
        var source = sheet["dati"] as JsonObject ?? ModuleCatalog.CreateData(module);
        var data = (JsonObject)source.DeepClone();
        if (module == BridgeConcept.Module) ModuleSheetPreparation.PrepareBridgeDesign(data);
        else ModuleSheetPreparation.PrepareVertical(data, module);
        sheet["dati"] = data;
        ModuleCalculation calculation;
        try { calculation = await Task.Run(() => ModuleServices.Get(module).Calculate(data, cancellation), cancellation); }
        catch (Exception error) when (error is not OperationCanceledException && error is not IOException && error is not UnauthorizedAccessException)
        { throw new ArgumentException("Dati incompleti o calcolo non riuscito. Aprire la scheda per correggere gli input.", error); }
        if (calculation.HasError) throw new ArgumentException("Dati incompleti o calcolo non riuscito. Aprire la scheda per correggere gli input.");
        var request = new ModuleReportRequest(sheet.S("nome"), calculation, DefaultSections(module), true);
        var figures = await renderer.RenderAsync(request, cancellation);
        cancellation.ThrowIfCancellationRequested();
        return new(Document: CreateDocument(request, figures));
    }
    public static DocxDocument CreateDocument(ModuleReportRequest request, IReadOnlyList<ReportFigure> figures)
    {
        var c = request.Calculation;
        if (c.ModuleId == BridgeConcept.Module)
            return BridgeConceptExport.ReportDocument(request.Title, c.Data, c.NativeAs<BridgeConcept.Result>(),
                figures.Single(f => f.Category == "prospetto").Png, figures.Single(f => f.Category == "sezione").Png);
        return ReportWord.CreateDocument(request.Title, c.ModuleId, c.Data, c.Values, request.Options.ToHashSet(), figures.Select(f => f.Image()).ToList(), !request.ProjectReport);
    }
}
