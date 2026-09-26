using System.Text.Json.Nodes;
namespace Anthea.Calculations;

// ANTHEA's archive/report envelope. All numerical result types belong to Checker.
public sealed record BridgeResult(string Method, string Scope, JsonObject Input, BridgeGeometry Geometry, BridgeMaterialValues Materials, List<BridgeStage> Stages)
{
    public JsonObject Json()
    {
        var json = J.Node(this)!.AsObject();
        for (int i = 0; i < Stages.Count; i++)
            if (Stages[i].GetHistory() is { } h)
            {
                var settings = new System.Text.Json.JsonSerializerOptions(J.Options) { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
                json["Stages"]![i]!["History"] = System.Text.Json.JsonSerializer.SerializeToNode(h.State, settings);
                json["Stages"]![i]!["Nonlinear"] = h.Nonlinear;
                // Derived material records are deliberately serialized with their runtime type.
                var fibers = json["Stages"]![i]!["History"]!["Fibers"]!.AsArray();
                for (int j = 0; j < h.State.Fibers.Count; j++)
                {
                    fibers[j]!["MaterialState"] = J.Node(h.State.Fibers[j].MaterialState);
                    if (!double.IsFinite(h.State.Fibers[j].Fiber.StripBottom)) fibers[j]!["Fiber"]!["StripBottom"] = null;
                    if (!double.IsFinite(h.State.Fibers[j].Fiber.StripTop)) fibers[j]!["Fiber"]!["StripTop"] = null;
                }
            }
        return json;
    }
}
