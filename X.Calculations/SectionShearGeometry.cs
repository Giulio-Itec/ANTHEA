using GPC.Design.Sections;
namespace Anthea.Calculations;

/// <summary>Geometric suggestions only: does not infer anchorage, load direction or detailing compliance.</summary>
public static class SectionShearGeometry
{
    public sealed record Direction(double Bw, double Depth, double SteelArea);
    public static Direction Derive(SezioneCA section, bool alongX)
    {
        var shape = section.Shape switch
        {
            "Rettangolare" => ShearSectionShape.Rectangle, "Circolare" => ShearSectionShape.Circle, "A T" => ShearSectionShape.Tee,
            _ => throw new ArgumentException("Parametri automatici disponibili per rettangolare, circolare e T.")
        };
        int[] negative = [], positive = [];
        var input = section.Input;
        if (!alongX && shape == ShearSectionShape.Rectangle && !input.ContainsKey("barre_manuali"))
        {
            int offset = (int)(input.Required("top_bar_count") + input.Required("bottom_bar_count"));
            if (input.B("second_top_enabled"))
            {
                int count = (int)input.Required("second_top_count"); positive = Enumerable.Range(offset, count).ToArray(); offset += count;
            }
            if (input.B("second_bottom_enabled")) negative = Enumerable.Range(offset, (int)input.Required("second_bottom_count")).ToArray();
        }
        bool hollow = input.B("foro_presente");
        var result = ShearGeometryProposal.Derive(shape, alongX, section.Outline.Select(p => (p[0], p[1])).ToArray(), ConcreteDesignMapping.Bars(section),
            section.Width, section.Height, section.Radius,
            hollow ? input.Required(shape == ShearSectionShape.Circle ? "inner_diameter_mm" : "inner_width_mm", strict: true) : 0,
            hollow && shape == ShearSectionShape.Rectangle ? input.Required("inner_height_mm", strict: true) : 0,
            shape == ShearSectionShape.Tee ? input.Required("flange_thickness_mm", strict: true) : 0,
            shape == ShearSectionShape.Tee ? input.Required("web_width_mm", strict: true) : 0, negative, positive);
        return new(result.Bw, result.Depth, result.SteelArea);
    }
}
