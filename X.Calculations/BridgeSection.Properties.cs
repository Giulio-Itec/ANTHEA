using System.Text.Json.Nodes;
namespace Anthea.Calculations;
public static partial class BridgeSection
{
    public static BridgeSectionProperties RectangleProperties(string name, double width, double height, double bottom, double x) => HBridgeSection.RectangleProperties(name, width, height, bottom, x);
    public static BridgeSectionProperties CombineProperties(string name, IEnumerable<BridgeSectionProperties> source) => HBridgeSection.CombineProperties(name, source);
    public static List<BridgeSectionProperties> SteelPartProperties(BridgeGeometry g, bool equivalent = false) => HBridgeSection.SteelPartProperties(g, equivalent);
    public static BridgeSectionProperties GrossPhaseProperties(JsonObject data, JsonObject phase, bool slabOnly = false) => HBridgeSection.GrossPhaseProperties(GeometryInput(data), ToCheckerPhase(phase), slabOnly);
}
