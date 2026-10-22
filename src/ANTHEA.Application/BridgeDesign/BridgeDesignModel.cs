using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Reports;
using X.Core;
using Native = Anthea.Calculations.BridgeConcept;
namespace Anthea.Application.BridgeDesign;

/// <summary>Read-only views and editor queries over the same completed native result.</summary>
public static partial class BridgeDesignModel
{
    /// <summary>The view of the result of <paramref name="calculation"/>, kept by the calculation (F5.13 d): built once, then the same instance.</summary>
    public static Result Read(ModuleCalculation calculation)
    {
        ArgumentNullException.ThrowIfNull(calculation);
        return calculation.View(c => new Result(c.NativeAs<Native.Result>()));
    }
    public const string Module = Native.Module;
    public const string Scope = Native.Scope;
    public const string OptimizationScope = Native.OptimizationScope;
    public const string PriceBasis = Native.PriceBasis;
    public static string[] DeckTypes => Native.DeckTypes.ToArray();
    public static string[] Obstacles => Native.Obstacles.ToArray();
    public static string[] Soils => Native.Soils.ToArray();
    public static string[] Piers => Native.Piers.ToArray();
    public static string[] Foundations => Native.Foundations.ToArray();
    public static Parameter[] Site => Native.Site.Select(p => new Parameter(p)).ToArray();
    public static Parameter[] Layout => Native.Layout.Select(p => new Parameter(p)).ToArray();
    public static Parameter[] Section => Native.Section.Select(p => new Parameter(p)).ToArray();
    public static Parameter[] Substructure => Native.Substructure.Select(p => new Parameter(p)).ToArray();
    public static Parameter[] Rates => Native.Rates.Select(p => new Parameter(p)).ToArray();
    public static Parameter[] Assumptions => Native.Assumptions.Select(p => new Parameter(p)).ToArray();
    public static Parameter[] AdvancedSection => Native.AdvancedSection.Select(p => new Parameter(p)).ToArray();
    public static Parameter[] AdvancedAssumptions => Native.AdvancedAssumptions.Select(p => new Parameter(p)).ToArray();
    public static Family[] Families => Native.Families.Select(f => new Family(f)).ToArray();
    public static IReadOnlyDictionary<string, string> RateNotes => new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(Native.RateNotes.ToDictionary(p => p.Key, p => p.Value));
    public static JsonObject Defaults() => Native.NewDocument();
    public static string[] TrafficModels => Native.TrafficModels.ToArray();
    public const string Schema1 = Native.Schema1;
    public const string UniformLegacy = Native.UniformLegacy;
    public static void ApplyRecommendedTrafficFactor(JsonObject data) => Native.ApplyRecommendedTrafficFactor(data);
    public static bool IsExtended(string family) => Native.IsExtended(family);
    public static bool HasTowers(string family) => Native.HasTowers(family);
    public static bool HasUpperStructure(string family) => Native.HasUpperStructure(family);
    public static TechnicalItem[] TechnicalSchedule(JsonObject data, Result result) => Native.TechnicalSchedule(data, result.Source).Select(x => new TechnicalItem(x)).ToArray();
    public static SpanGeometry[] SpanSchedule(Result result) => Native.SpanSchedule(result.Source).Select(x => new SpanGeometry(x)).ToArray();
    public static SupportGeometry[] SupportSchedule(JsonObject data, Result result) => Native.SupportSchedule(data, result.Source).Select(x => new SupportGeometry(x)).ToArray();
    public static string Csv(Result result) => BridgeConceptExport.Csv(result.Source);
    public static string TechnicalCsv(JsonObject data, Result result) => BridgeConceptExport.TechnicalCsv(data, result.Source);
    public static DocxDocument ReportDocument(string title, JsonObject data, Result result, byte[]? elevation = null, byte[]? section = null)
        => BridgeConceptExport.ReportDocument(title, data, result.Source, elevation, section);
}
