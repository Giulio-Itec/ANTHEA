using System.Text.Json.Nodes;

namespace Anthea.Application.Concrete;

/// <summary>Report-only projection of values omitted by the historical JSON result contract.</summary>
public static class ConcreteReportCurvature
{
    public static void Append(JsonObject reportResult, ConcreteResults.CurvatureResult? curvature)
    {
        if (curvature is null || reportResult["momento_curvatura"] is not JsonObject curve) return;
        if (curvature.Ductility is { } ductility) curve["Ductility"] = System.Text.Json.JsonSerializer.SerializeToNode(ductility);
        if (curvature.DuctilityMessage is { } message) curve["DuctilityMessage"] = message;
    }
}
