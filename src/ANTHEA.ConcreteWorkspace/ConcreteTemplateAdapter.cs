using System.Text.Json.Nodes;
using Anthea.Calculations;
using GPC.Design.Sections;

namespace Anthea.ConcreteWorkspace;

/// <summary>Editor names and units mapped to the GPC template generator; no geometric calculation.</summary>
public static class ConcreteTemplateAdapter
{
    public const string StadiumSeparate = "Curve e rettilinei separati";
    public const string StadiumLegacy = "Radiale dal centro (precedente)";
    public static bool SeparateStadium(JsonObject input)
        => input.S("shape") == "Capsula" && input.S("wizard_stadium_layout") == StadiumSeparate;
    public static bool CustomShape(string shape) => shape is "A I" or "A L" or "A C" or "Ellittica" or "Capsula" or "Trapezoidale" or "Poligonale regolare";
    public static bool Uses(JsonObject input) => CustomShape(input.S("shape")) ||
        input.B("foro_presente") && input.S("wizard_layout") == "Doppio contorno";
    public static ConcreteTemplateGeometry Create(JsonObject input)
    {
        string shape = input.S("shape");
        bool circle = shape == "Circolare";
        var kind = shape switch {
            "A I" => ConcreteTemplateKind.I, "A L" => ConcreteTemplateKind.L, "A C" => ConcreteTemplateKind.C,
            "Ellittica" => ConcreteTemplateKind.Ellipse, "Capsula" => ConcreteTemplateKind.Stadium,
            "Trapezoidale" => ConcreteTemplateKind.Trapezoid, "Poligonale regolare" => ConcreteTemplateKind.RegularPolygon,
            "Rettangolare" when input.B("foro_presente") => ConcreteTemplateKind.HollowRectangle,
            "Circolare" when input.B("foro_presente") => ConcreteTemplateKind.HollowCircle,
            _ => throw new ArgumentException("Tipologia del wizard non riconosciuta.")
        };
        double N(string key) => J.Number(input[key]) ?? throw new ArgumentException("Valore numerico richiesto: " + key);
        int Count(string key) => J.Number(input[key]) is double n && n == Math.Truncate(n) && n >= 0 && n <= 1000
            ? (int)n : throw new ArgumentException("Numero intero richiesto: " + key);
        bool profile = shape is "A I" or "A C" or "A L";
        var template = new ConcreteTemplateInput(kind,
            N(circle ? "diameter_mm" : "width_mm"), N(circle ? "diameter_mm" : "height_mm"),
            profile ? N("web_width_mm") : 0, profile ? N("flange_thickness_mm") : 0,
            shape is "A I" or "A C" or "Trapezoidale" ? N("bottom_flange_width_mm") : 0,
            shape is "A I" or "A C" ? N("bottom_flange_thickness_mm") : 0,
            input.B("foro_presente") ? N(circle ? "inner_diameter_mm" : "inner_width_mm") : 0,
            input.B("foro_presente") ? N(circle ? "inner_diameter_mm" : "inner_height_mm") : 0,
            N("cover_mm"), N("wizard_bar_diameter_mm"), N("wizard_bar_spacing_mm"),
            Count("wizard_radial_count"), Count("wizard_inner_count"),
            input.S("staffe_presenti", "Sì") == "No" ? 0 : N("transverse_bar_diameter_mm"),
            N("transverse_spacing_mm"), circle ? Count("circular_sides") : 96,
            shape == "Poligonale regolare" ? Count("wizard_polygon_sides") : 8);
        if (shape == "Capsula")
        {
            string layout = input.S("wizard_stadium_layout");
            if (layout == StadiumSeparate)
                template = template with { Stadium = new(Count("wizard_stadium_curve_count"),
                    N("wizard_stadium_curve_diameter_mm"), Count("wizard_stadium_straight_count"),
                    N("wizard_stadium_straight_diameter_mm")) };
            else if (layout.Length > 0 && layout != StadiumLegacy)
                throw new ArgumentException("Disposizione delle armature della capsula non riconosciuta.");
        }
        return ConcreteSectionTemplates.Create(template);
    }
}
