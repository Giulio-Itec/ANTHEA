using System.Text.Json.Nodes;

namespace Anthea.Calculations;
/// <summary>Presentation scope of the checks currently implemented; missing/unsupported checks stay visible.</summary>
public static class SleCheckScope
{
    public static bool Stress(string set) => set != "SLE_FREQ";
    /// <summary>Crack check shown for the set: through the serviceability adapter with the default engine (refactoring F2.7b, commit A4),
    /// total as before (excluded standards and invalid data give true).</summary>
    public static bool Cracking(string set, JsonNode workspace) => ConcreteServiceabilityAdapter.CrackingRequired(set, workspace);
}
