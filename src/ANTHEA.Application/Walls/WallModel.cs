using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Native = Anthea.Calculations.RetainingWall;
namespace Anthea.Application.Walls;

public static partial class WallModel
{
    /// <summary>The view of the result of <paramref name="calculation"/>, kept by the calculation (F5.13 d): built once, then the same instance.</summary>
    public static Result Read(ModuleCalculation calculation)
    {
        ArgumentNullException.ThrowIfNull(calculation);
        return calculation.View(c => new Result(c.NativeAs<Native.Result>()));
    }
    public static string RebarLabel(JsonObject data, string key) => Native.RebarLabel(data, key);
    public const string AmplificationCalculated = Native.AmplificationCalculated;
    public const string ApproachHelp = Native.ApproachHelp;
    public const string FrictionHelp = Native.FrictionHelp;
    public const string GlobalHelp = Native.GlobalHelp;
    public const string Limits = Native.Limits;
    public const string Method = Native.Method;
    public const string Module = Native.Module;
    public const string Scope = Native.Scope;
    public const string SeismicHelp = Native.SeismicHelp;
    public const string SeismicManual = Native.SeismicManual;
    public const string SoilHelp = Native.SoilHelp;
    public const string TopographyFlat = Native.TopographyFlat;
    public const string SeismicSite = Native.SeismicSite;
    public const string AmplificationAssigned = Native.AmplificationAssigned;
    public const string TopographySlope = Native.TopographySlope;
    public const string TopographyRidge = Native.TopographyRidge;
    public static string[] ActionTypes => Native.ActionTypes.Select(item0 => item0).ToArray();
    public static string[] ActionCategories => Native.ActionCategories.Select(item0 => item0).ToArray();
    public static string[] States => Native.States.Select(item0 => item0).ToArray();
    public static WallModel.Family[] Families => Native.Families.Select(item0 => new WallModel.Family(item0)).ToArray();
    public static WallModel.Parameter[] GeometryFields => Native.GeometryFields.Select(item0 => new WallModel.Parameter(item0)).ToArray();
    public static WallModel.Parameter[] FoundationFields => Native.FoundationFields.Select(item0 => new WallModel.Parameter(item0)).ToArray();
    public static WallModel.Parameter[] MaterialFields => Native.MaterialFields.Select(item0 => new WallModel.Parameter(item0)).ToArray();
    public static string[] FrictionModes => Native.FrictionModes.Select(item0 => item0).ToArray();
    public static string ActionUnit(string type) => Native.ActionUnit(type);
    public static JsonObject NewAction(JsonObject d, string type = "Sovraccarico uniforme") => (JsonObject)Native.NewAction(d, type).DeepClone();
    public static void Upgrade(JsonObject d) => Native.Upgrade(d);
    public static string CombinationSignature(JsonObject d) => Native.CombinationSignature(d);
    public static bool HeelSurcharge(JsonNode? row) => Native.HeelSurcharge(row);
    public static double DefaultPassiveFactor(string state) => Native.DefaultPassiveFactor(state);
    public static JsonArray GenerateCombinations(JsonObject d) => Native.GenerateCombinations(d);
    public static void ValidateActions(JsonObject d, bool matrix = true) => Native.ValidateActions(d, matrix);
    public static WallModel.BarSchedule CalculateBarSchedule(JsonObject input) => new WallModel.BarSchedule(Native.CalculateBarSchedule(input));
    public static WallModel.BarSchedule CalculateBarSchedule(WallModel.Result result) => new WallModel.BarSchedule(Native.CalculateBarSchedule(result.Source));
    public static JsonObject Layer() => (JsonObject)Native.Layer().DeepClone();
    public static JsonObject Example(string family) => (JsonObject)Native.Example(family).DeepClone();
    public static double[][] Outline(JsonObject d) => Native.Outline(d).Select(item0 => item0.Select(item1 => item1).ToArray()).ToArray();
    public static void PrepareGlobalProfile(JsonObject d) => Native.PrepareGlobalProfile(d);
    public static void ProposeGlobalSearch(JsonObject d) => Native.ProposeGlobalSearch(d);
    public static string GlobalSignature(JsonObject d) => Native.GlobalSignature(d);
    public static JsonArray GenerateGlobalCombinations(JsonObject input) => Native.GenerateGlobalCombinations(input);
    public static double Pressure(WallModel.Contact c, double x) => Native.Pressure(c.Source, x);
    public static (double Force, double Moment) Integrate(IEnumerable<WallModel.PressureSegment> pieces, double left, double right, double root) => Native.Integrate(pieces.Select(item => item.Source).ToArray(), left, right, root);
    public static bool UsesConcrete(JsonObject d) => Native.UsesConcrete(d);
    public static void ApplyMaterialPreset(JsonObject d, JsonObject preset) => Native.ApplyMaterialPreset(d, preset);
    /// <summary>
    /// Acciaio del muro salvato con il nome di un materiale del catalogo NTC 2018 ma con valori diversi da quelli attuali (DET-3, per esempio B450A
    /// con εu = 30 ‰): lo stesso testo della scheda Acciaio per armature, della sezione c.a. e del palo orizzontale
    /// (RebarMaterial.CatalogueDifference), oppure null. I materiali del muro passano dalla mappatura del verificatore delle sezioni
    /// (MaterialSectionInput: fyk → fyk_mpa e i completamenti dei muri precedenti, che restano «Personalizzato»).
    /// </summary>
    public static string? SteelDifference(JsonObject materials)
        => RebarMaterial.CatalogueDifference(Native.MaterialSectionInput(J.Obj(("materials", materials.DeepClone()))), "NTC 2018");
    public static double RequiredCover(JsonObject d) => Native.RequiredCover(d);
    public static string RebarZoneName(string key) => Native.RebarZoneName(key);
    public static WallModel.ReinforcementDetails CalculateReinforcementDetails(JsonObject input) => new WallModel.ReinforcementDetails(Native.CalculateReinforcementDetails(input));
    public static JsonObject ExportSection(WallModel.Result result, string member, double position, string selectedCombination) => (JsonObject)Native.ExportSection(result.Source, member, position, selectedCombination).DeepClone();
    public static WallModel.SeismicDerivation? DeriveSeismic(JsonObject d)
        => Native.DeriveSeismic(d) is { } result ? new WallModel.SeismicDerivation(result) : null;
    public static SlopePoint[] GlobalSurface(JsonObject d, SlopePoint[] valley, SlopePoint[] uphill) => Native.GlobalSurface(d, valley.Select(item => item.Source).ToArray(), uphill.Select(item => item.Source).ToArray()).Select(item0 => new SlopePoint(item0)).ToArray();
    public static (string Label, string Unit, string Origin) AuditMeaning(string key) => Native.AuditMeaning(key);
    public static void CompleteSoilInput(JsonObject d) => Native.CompleteSoilInput(d);
    public static double ValleyHeight(JsonObject d) => Native.ValleyHeight(d);
    public static JsonArray ValleyLayers(JsonObject d) => Native.ValleyLayers(d);
    public static string ReinforcementKey(JsonObject d, string member, double? position = null) => Native.ReinforcementKey(d, member, position);
}

public static class Slope
{
    public const string Formula = Anthea.Calculations.Geotechnics.Slope.Formula;
    public static double Height(IReadOnlyList<SlopePoint> line, double x)
        => Anthea.Calculations.Geotechnics.Slope.Height(line.Select(p => p.Source).ToArray(), x);
}
