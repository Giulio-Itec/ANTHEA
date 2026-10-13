using System.Text.Json.Nodes;
using GPC.Checkers.CompositeBridge;
namespace Anthea.Application.Presentation;

public sealed record BridgePreview(BridgeGeometry Geometry, JsonObject Input, string MaterialText, string GeometryError)
{
    private static string F(double value) => BridgePropertyPresentation.Number(value);
    public static BridgePreview Read(JsonObject data)
    {
        var geometry = BridgeSection.Geometry(data); var snapshot = (JsonObject)data.DeepClone();
        try
        {
            var m = BridgeSection.Materials(data);
            string text = $"{m.Concrete.Name}   fck = {F(Math.Abs(m.Concrete.Fck))} MPa\nEcm = {F(m.Concrete.ElasticModulusCompression)} MPa\n\n{m.Steel.Name}   fy = {F(m.Steel.Fyk)} MPa\nEa = {F(m.Steel.ElasticModulusTension)} MPa\n\n{m.Rebar.Name}   fyk = {F(m.Rebar.Fyk)} MPa\nEs = {F(m.Rebar.ElasticModulusTension)} MPa";
            return new(geometry, snapshot, text, "");
        }
        catch (Exception ex) { return new(geometry, snapshot, ex.Message, ex.Message); }
    }
    public static string SlabDescription(JsonObject data)
    {
        var slab = BridgeSection.SlabLayout(data);
        string lower = data.B("rebars_bottom") ? $"Ferri inferiori: {slab.BottomReference} + {F(data.D("cover_bottom"))} mm → y = {F(slab.BottomAxisY)} mm." : "Ferri inferiori assenti.";
        return (slab.HasPredalle ? $"Predalle: y = 0…{F(slab.PredalleThickness)} mm; getto sovrastante: {F(slab.Height - slab.PredalleThickness)} mm.\n" : "y = 0 all'intradosso della soletta.\n") + lower;
    }
    public static string UpdateSteelHeightDescription(JsonObject data)
    {
        double clear = BridgeSheetPresentation.UpdateWebHeight(data);
        double second = data.B("plate2") && data.S("sezione", BridgeSection.SectionTypes[0]) == BridgeSection.SectionTypes[0] ? data.D("t_bottom2") : 0;
        return $"H = {F(data.D("h_trave"))} mm · h anima = H − t sup − t inf{(second > 0 ? " − t inf.2" : "")} = {F(clear)} mm.";
    }
}
