using System.Text.Json.Nodes;
using GPC.Design.Bridges;
namespace Anthea.Calculations;

public static partial class BridgeConcept
{
    public const string Schema1 = "Schema 1 NTC 2018";
    public const string UniformLegacy = "Uniforme equivalente (legacy)";
    public static IReadOnlyList<string> TrafficModels { get; } = Array.AsReadOnly(new[] { Schema1, UniformLegacy });
    /// <summary>Factory for new user documents. Defaults() remains the version-1 compatibility template.</summary>
    public static JsonObject NewDocument()
    {
        var data = Defaults(); data["input"]!["traffic_model"] = Schema1;
        ApplyRecommendedTrafficFactor(data); return data;
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
