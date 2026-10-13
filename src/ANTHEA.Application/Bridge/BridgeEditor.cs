using System.Text.Json.Nodes;
using Native = Anthea.Calculations.BridgeSection;
namespace Anthea.Application.Bridge;

/// <summary>Editor operations and selections reuse the existing archive adapter. No copied coefficients or calculation rules.</summary>
public static class BridgeEditor
{
    public const string Module = Native.Module;
    public const string Scope = Native.Scope;
    public const string PredalleScope = Native.PredalleScope;
    public const string ShrinkageKind = Native.ShrinkageKind;
    public const string CommonLoadReference = Native.CommonLoadReference;
    public const string GrossLoadReference = Native.GrossLoadReference;
    public const string EffectiveLoadReference = Native.EffectiveLoadReference;
    public static string[] BottomRebarReferences => Native.BottomRebarReferences.ToArray();
    public static string[] CalculationMethods => Native.CalculationMethods.ToArray();
    public static string[] ConcreteNames => Native.ConcreteNames.ToArray();
    public static string[] DiaphragmKinds => Native.DiaphragmKinds.ToArray();
    public static string[] LoadReferences => Native.LoadReferences.ToArray();
    public static string[] NonlinearCreepModes => Native.NonlinearCreepModes.ToArray();
    public static string[] PhaseKinds => Native.PhaseKinds.ToArray();
    public static string[] RebarNames => Native.RebarNames.ToArray();
    public static string[] ResponseModes => Native.ResponseModes.ToArray();
    public static string[] ResponseOrigins => Native.ResponseOrigins.ToArray();
    public static string[] SectionTypes => Native.SectionTypes.ToArray();
    public static string[] Standards => Native.Standards.ToArray();
    public static string[] SteelNames => Native.SteelNames.ToArray();
    public static string[] StiffenerSides => Native.StiffenerSides.ToArray();
    public static string[] SupportLocations => Native.SupportLocations.ToArray();
    public static void EnsureAccessoryDefaults(JsonObject data) => Native.EnsureAccessoryDefaults(data);
    public static void ValidateShape(JsonObject data) => Native.ValidateShape(data);
    public static bool IsHistory(JsonObject data) => Native.IsHistory(data);
    public static bool IsNonlinear(JsonObject data) => Native.IsNonlinear(data);
    public static bool TorsionEnabled(JsonObject data) => Native.TorsionEnabled(data);
    public static string LoadReference(JsonObject phase) => Native.LoadReference(phase);
    public static JsonObject ShrinkagePhase() => Native.ShrinkagePhase();
    public static JsonObject Phase(string name = "Nuova fase", string kind = "Composta", double n = 0, double m = 0, double phi = 0, double psi = 1, string reference = CommonLoadReference)
        => Native.Phase(name, kind, n, m, phi, psi, reference);
    public static (double LeftWidth, double LeftThickness, double RightWidth, double RightThickness) StiffenerPlates(JsonObject data, string suffix)
        => Native.StiffenerPlates(data, suffix);
    public static SlabLayoutData SlabLayout(JsonObject data)
    {
        var s = Native.SlabLayout(data);
        return new(s.HasPredalle, s.PredalleThickness, s.Height, s.BottomReference, s.TopAxisY, s.BottomAxisY);
    }
    public static string ResponseCsv(SectionResponseResult result) => Native.ResponseCsv(result.Native);
    public static double EffectiveConcreteModulus(double steelModulus, double ratio) => GPC.Checkers.CompositeBridge.BridgeDerivedResults.EffectiveConcreteModulus(steelModulus, ratio);
    public static double EquivalentBottomInertia(BridgeGeometry geometry) => GPC.Checkers.CompositeBridge.BridgeDerivedResults.EquivalentBottomPlate(geometry.Native).Ix;
    public static string ComponentName(string id) => id switch
    {
        GPC.Checkers.CompositeBridge.History.HBridgeHistoryAnalysis.Concrete => "Calcestruzzo",
        GPC.Checkers.CompositeBridge.History.HBridgeHistoryAnalysis.Rebars => "Armatura",
        GPC.Checkers.CompositeBridge.History.HBridgeHistoryAnalysis.Top => "Piattabanda superiore",
        GPC.Checkers.CompositeBridge.History.HBridgeHistoryAnalysis.Web => "Anima",
        GPC.Checkers.CompositeBridge.History.HBridgeHistoryAnalysis.Bottom => "Piattabanda inferiore", _ => id
    };
    public const string ConcreteComponent = GPC.Checkers.CompositeBridge.History.HBridgeHistoryAnalysis.Concrete;
}
public sealed record SlabLayoutData(bool HasPredalle, double PredalleThickness, double Height,
    string BottomReference, double TopAxisY, double BottomAxisY);
