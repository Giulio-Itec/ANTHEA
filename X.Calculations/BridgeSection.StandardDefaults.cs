using System.Text.Json.Nodes;
namespace Anthea.Calculations;
public static partial class BridgeSection
{
    // Existing UI transition, moved unchanged in F3. Normative consolidation belongs to F4.
    public static void ApplyStandardDefaults(JsonObject data)
    {
        data["gamma_m0"] = data.S("normativa").StartsWith("NTC") ? "1.05" : "1.0";
        data["gamma_m1"] = "1.1";
        data["alpha_cc"] = data.S("normativa").StartsWith("NTC") ? "0.85" : "1.0";
    }
}
