using System.Text.Json.Nodes;
using Native = Anthea.Calculations.ElasticHorizontalPile;
namespace Anthea.Application.Modules;

/// <summary>Existing document operations and captions used by pile editors.</summary>
public static class ElasticPileEditor
{
    public const string Limits = Native.Limits;
    public const string Signs = Native.Signs;
    public const string SeismicSourceUrl = Native.SeismicSourceUrl;
    public static string[] ReinforcementKeys => Native.ReinforcementKeys.ToArray();
    public static string[] InnerRingKeys => Native.InnerRingKeys.ToArray();
    public static string ParameterLabel(string law) => Native.ParameterLabel(law);
    public static string MethodLabel(string method) => Native.MethodLabel(method);
    public static string ConstructionLabel(string key) => Native.ConstructionLabel(key);
    public static void PrepareShared(JsonObject root) => Native.PrepareShared(root);
    public static bool NeedsMigration(JsonObject root) => Native.NeedsMigration(root);
    public static void ResolveMigration(JsonObject root, bool useLegacy) => Native.ResolveMigration(root, useLegacy);
    public static string Csv(JsonObject result) => Native.Csv(result);
    public static void PrepareReinforcement(JsonObject root) => Native.PrepareReinforcement(root);
    public static JsonObject SegmentSection(JsonObject root, JsonObject segment) => Native.SegmentSection(root, segment);
    public static void ApproveReinforcementAssumptions(JsonObject root, bool seismic) => Native.ApproveReinforcementAssumptions(root, seismic);
    public static string TransverseKind(JsonObject root, JsonNode? segment = null) => Native.TransverseKind(root, segment);
    public static JsonObject ReinforcementSummary(JsonObject record) => Native.ReinforcementSummary(record);
    public static JsonObject CreateConcreteSheet(JsonObject root, JsonObject? result, int index) => Native.CreateConcreteSheet(root, result, index);
    public static void ApplyConcreteSheetReinforcement(JsonObject root, JsonObject sheet, int index) => Native.ApplyConcreteSheetReinforcement(root, sheet, index);
}
