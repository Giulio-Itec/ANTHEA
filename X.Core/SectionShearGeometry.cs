namespace X.Core;

/// <summary>Geometric suggestions only: does not infer anchorage, load direction or detailing compliance.</summary>
public static class SectionShearGeometry
{
    public sealed record Direction(double Bw, double Depth, double SteelArea);
    public static Direction Derive(SezioneCA section, bool alongX)
    {
        if (section.Shape is not ("Rettangolare" or "A T")) throw new ArgumentException("Parametri automatici disponibili per rettangolare e T; modello circolare da validare.");
        int axis = alongX ? 0 : 1;
        double min = section.Outline.Min(p => p[axis]), max = section.Outline.Max(p => p[axis]);
        double C(Barra b) => alongX ? b.X : b.Y;
        // Wizard face layers can have slightly different centres because of different diameters.
        double rim = section.Bars.Max(b => b.Diametro);
        double low = section.Bars.Min(C), high = section.Bars.Max(C);
        var negative = section.Bars.Where(b => C(b) <= low + rim && C(b) < (min + max) / 2).ToArray();
        var positive = section.Bars.Where(b => C(b) >= high - rim && C(b) > (min + max) / 2).ToArray();
        if (negative.Length == 0 || positive.Length == 0) throw new ArgumentException("Armature sui due lembi non determinabili: usare parametri manuali.");
        double negArea = negative.Sum(b => b.Area), posArea = positive.Sum(b => b.Area);
        double depth = Math.Min(max - negative.Sum(b => b.Area * C(b)) / negArea, positive.Sum(b => b.Area * C(b)) / posArea - min);
        double bw = section.Shape == "Rettangolare" ? alongX ? section.Height : section.Width : section.Input.Required(alongX ? "flange_thickness_mm" : "web_width_mm", strict: true);
        return new(bw, depth, Math.Min(negArea, posArea));
    }
}
