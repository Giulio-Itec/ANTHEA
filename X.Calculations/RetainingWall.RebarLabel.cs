using System.Text.Json.Nodes;
namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public static string RebarLabel(JsonObject d, string key)
    {
        if (d.S("family") != "cantilever") return "non armato"; var r = d["reinforcement"]?[key];
        var bar = new GPC.Model.Sections.SectionCircular(r.D("diameter"));
        return $"{r.D("count"):0} Ø{r.D("diameter"):0}/m/faccia · As={r.D("count") * bar.Area:0} mm²/m";
    }
}
