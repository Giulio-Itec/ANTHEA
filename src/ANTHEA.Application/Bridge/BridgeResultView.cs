using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Reports;
using X.Core;
namespace Anthea.Application.Bridge;

/// <summary>One owned view of a calculation; reports and queries reuse its original result without recalculation.</summary>
public sealed class BridgeResultView
{
    private readonly BridgeResult source;
    public string Method { get; }
    public string Scope { get; }
    public JsonObject Input { get; }
    public BridgeGeometry Geometry { get; }
    public BridgeMaterialValues Materials { get; }
    public List<BridgeStage> Stages { get; }
    private BridgeResultView(BridgeResult result)
    {
        source = result; var map = new BridgeProjection();
        Method = result.Method; Scope = result.Scope; Input = (JsonObject)result.Input.DeepClone();
        Geometry = map.Read(result.Geometry)!; Materials = map.Read(result.Materials)!;
        Stages = result.Stages.Select(s => map.Read(s)!).ToList();
    }
    /// <summary>The view of <paramref name="calculation"/>, kept by the calculation (F5.13 d): built once, then the same instance.</summary>
    public static BridgeResultView Read(ModuleCalculation calculation)
    {
        ArgumentNullException.ThrowIfNull(calculation);
        return calculation.View(c => new BridgeResultView(c.NativeAs<BridgeResult>()));
    }
    public JsonObject Json() => source.Json();
    public (double Factor, string Source) StressScale(JsonObject settings) => BridgeSheetPresentation.StressScale(source, settings);
    public DocxDocument Report(string title, HashSet<string> options, IReadOnlyList<ImmagineReport> images)
        => ReportBridge.CreateDocument(title, source, options, images);
}
