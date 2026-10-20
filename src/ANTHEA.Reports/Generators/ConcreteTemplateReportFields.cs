using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>Labels of the additional editor dimensions; no section calculation.</summary>
internal static class ConcreteTemplateReportFields
{
    internal static (string Key, string Label)[] Geometry(JsonObject input)
    {
        string shape = input.S("shape");
        var fields = new List<(string, string)> { ("shape", "Forma") };
        if (shape == "Circolare") fields.Add(("diameter_mm", "Diametro esterno [mm]"));
        else
        {
            fields.Add(("width_mm", shape switch {
                "Poligonale regolare" => "Diametro circoscritto [mm]",
                "Trapezoidale" => "Base superiore [mm]",
                "A I" or "A C" => "Larghezza ala superiore [mm]",
                _ => "Larghezza [mm]" }));
            if (shape != "Poligonale regolare") fields.Add(("height_mm", "Altezza totale [mm]"));
        }
        if (shape is "A I" or "A L" or "A C")
        {
            fields.Add(("web_width_mm", "Spessore anima [mm]"));
            fields.Add(("flange_thickness_mm", shape == "A L" ? "Spessore ala [mm]" : "Spessore ala superiore [mm]"));
        }
        if (shape is "A I" or "A C" or "Trapezoidale")
            fields.Add(("bottom_flange_width_mm", shape == "Trapezoidale" ? "Base inferiore [mm]" : "Larghezza ala inferiore [mm]"));
        if (shape is "A I" or "A C") fields.Add(("bottom_flange_thickness_mm", "Spessore ala inferiore [mm]"));
        if (shape == "Poligonale regolare") fields.Add(("wizard_polygon_sides", "Numero lati"));
        if (Anthea.ConcreteWorkspace.ConcreteTemplateAdapter.SeparateStadium(input))
        {
            fields.Add(("wizard_stadium_layout", "Disposizione armature"));
            fields.Add(("wizard_stadium_curve_count", "Barre per arco, tangenti incluse"));
            fields.Add(("wizard_stadium_curve_diameter_mm", "Ø barre curve [mm]"));
            fields.Add(("wizard_stadium_straight_count", "Barre interne per rettilineo"));
            fields.Add(("wizard_stadium_straight_diameter_mm", "Ø barre rettilinei [mm]"));
        }
        fields.Add(("cover_mm", "Copriferro netto [mm]"));
        return fields.ToArray();
    }

    internal static string Describe(JsonObject input)
        => string.Join("; ", Geometry(input).Select(f => f.Label + "=" + input.S(f.Key)));
}
