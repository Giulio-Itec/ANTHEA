using System.Text.Json.Nodes;
using GPC.Design.Bridges;
namespace Anthea.Calculations;

public static partial class BridgeConcept
{
    public const string Schema1 = "Schema 1 NTC 2018";
    public const string UniformLegacy = "Uniforme equivalente (legacy)";
    public static IReadOnlyList<string> TrafficModels { get; } = Array.AsReadOnly(new[] { Schema1, UniformLegacy });
    /// <summary>
    /// Partial factors of the ultimate envelopes (census of the combinations, D8, decision of the user of 29/10/2026: «d8 e bridge design devono
    /// usare model»): the rows of NTC 2018 Tab. 5.1.V, set A1, from GPC.Model for the new documents; the multipliers γG and γQ of the project
    /// for the archives (a document without the choice), whose results do not change.
    /// </summary>
    public const string ProfileFactors = "NTC 2018 Tab. 5.1.V (Model)", ProjectMultipliers = "Moltiplicatori del progetto";
    public static IReadOnlyList<string> PartialFactorChoices { get; } = Array.AsReadOnly(new[] { ProfileFactors, ProjectMultipliers });
    /// <summary>Factory for new user documents. Defaults() remains the version-1 compatibility template.</summary>
    public static JsonObject NewDocument()
    {
        var data = Defaults(); data["input"]!["traffic_model"] = Schema1; data["input"]!["partial_factors"] = ProfileFactors;
        ApplyRecommendedTrafficFactor(data); return data;
    }
    /// <summary>The choice of the partial factors of a document: the multipliers of the project when the document has none (archives).</summary>
    public static string PartialFactors(JsonObject data) => ReadPartialFactors(data) == PartialFactorSource.Ntc2018RoadBridges ? ProfileFactors : ProjectMultipliers;
    internal static PartialFactorSource ReadPartialFactors(JsonObject data)
    {
        var input = data["input"]!.AsObject();
        if (!input.ContainsKey("partial_factors")) return PartialFactorSource.ProjectMultipliers;
        if (input["partial_factors"] is not JsonValue value || !value.TryGetValue<string>(out var name)) throw new ArgumentException("Coefficienti parziali non validi.");
        return name switch
        {
            ProfileFactors => PartialFactorSource.Ntc2018RoadBridges,
            ProjectMultipliers => PartialFactorSource.ProjectMultipliers,
            _ => throw new ArgumentException("Coefficienti parziali non riconosciuti.")
        };
    }
    public static void ApplyRecommendedTrafficFactor(JsonObject data) => data["assumptions"]!["gamma_q"] = RoadTraffic.NtcUnfavourableGamma;
    internal static TrafficModel ReadTrafficModel(JsonObject data)
    {
        var input = data["input"]!.AsObject();
        if (!input.ContainsKey("traffic_model")) return TrafficModel.LegacyUniform;
        if (input["traffic_model"] is not JsonValue value || !value.TryGetValue<string>(out var name)) throw new ArgumentException("Modello di traffico non valido.");
        return name switch
        {
            Schema1 => TrafficModel.Ntc2018Schema1,
            UniformLegacy => TrafficModel.LegacyUniform,
            _ => throw new ArgumentException("Modello di traffico non riconosciuto.")
        };
    }
    public sealed record TrafficInfo(string Model, string Reference, int Lanes, double UniformKnPerMetre, double TandemKn,
        double GammaG, double GammaQ, string Scope);
    private static TrafficInfo? FromTraffic(BridgeTrafficSummary? t) => t is null ? null : new(Schema1, t.Reference, t.Lanes, t.UniformKnPerMetre, t.TandemKn, t.GammaG, t.GammaQ, t.Scope);
}
