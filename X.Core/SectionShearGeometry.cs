namespace X.Core;

/// <summary>Geometric suggestions only: does not infer anchorage, load direction or detailing compliance.</summary>
public static class SectionShearGeometry
{
    public sealed record Direction(double Bw, double Depth, double SteelArea);
    public static Direction Derive(SezioneCA section, bool alongX)
    {
        if (section.Shape=="Circolare")
        {
            double Coordinate(Barra b)=>alongX?b.X:b.Y;
            var lowerRing=section.Bars.Where(b=>Coordinate(b)<-1e-8).ToArray();var upperRing=section.Bars.Where(b=>Coordinate(b)>1e-8).ToArray();
            if(lowerRing.Length==0||upperRing.Length==0)throw new ArgumentException("Circolare: armatura sui due semicerchi non determinabile.");
            double an=lowerRing.Sum(b=>b.Area),ap=upperRing.Sum(b=>b.Area);
            double d=section.Radius+Math.Min(-lowerRing.Sum(b=>b.Area*Coordinate(b))/an,upperRing.Sum(b=>b.Area*Coordinate(b))/ap);
            // Hollow-circle web width is the sum of the two wall thicknesses at the diameter.
            double ringWidth=section.Input.B("foro_presente")?section.Width-section.Input.D("inner_diameter_mm"):section.Width;
            return new(ringWidth,d,Math.Min(an,ap));
        }
        if (section.Shape is not ("Rettangolare" or "A T")) throw new ArgumentException("Parametri automatici disponibili per rettangolare, circolare e T.");
        int axis = alongX ? 0 : 1;
        double min = section.Outline.Min(p => p[axis]), max = section.Outline.Max(p => p[axis]);
        double C(Barra b) => alongX ? b.X : b.Y;
        // Wizard face layers can have slightly different centres because of different diameters.
        double rim = section.Bars.Max(b => b.Diametro);
        double low = section.Bars.Min(C), high = section.Bars.Max(C);
        var negative = section.Bars.Where(b => C(b) <= low + rim && C(b) < (min + max) / 2).ToArray();
        var positive = section.Bars.Where(b => C(b) >= high - rim && C(b) > (min + max) / 2).ToArray();
        if (!alongX && section.Shape == "Rettangolare" && !section.Input.ContainsKey("barre_manuali"))
        {
            int offset = (int)(section.Input.D("top_bar_count") + section.Input.D("bottom_bar_count"));
            if (section.Input.B("second_top_enabled"))
            {
                int count = (int)section.Input.D("second_top_count");
                positive = positive.Concat(section.Bars.Skip(offset).Take(count)).Distinct().ToArray(); offset += count;
            }
            if (section.Input.B("second_bottom_enabled")) negative = negative.Concat(section.Bars.Skip(offset).Take((int)section.Input.D("second_bottom_count"))).Distinct().ToArray();
        }
        if (negative.Length == 0 || positive.Length == 0) throw new ArgumentException("Armature sui due lembi non determinabili: usare parametri manuali.");
        double negArea = negative.Sum(b => b.Area), posArea = positive.Sum(b => b.Area);
        double depth = Math.Min(max - negative.Sum(b => b.Area * C(b)) / negArea, positive.Sum(b => b.Area * C(b)) / posArea - min);
        double bw = section.Shape == "Rettangolare" ? alongX ? section.Height : section.Width : section.Input.Required(alongX ? "flange_thickness_mm" : "web_width_mm", strict: true);
        if(section.Shape=="Rettangolare"&&section.Input.B("foro_presente"))bw-=section.Input.D(alongX?"inner_height_mm":"inner_width_mm");
        return new(bw, depth, Math.Min(negArea, posArea));
    }
}
