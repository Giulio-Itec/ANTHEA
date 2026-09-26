using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public sealed record SectionPropertyValue(string Group, string Name, double Value, string Unit);
public sealed record ConcreteSectionPropertyResult(double Phi, double N, IReadOnlyList<SectionPropertyValue> Values);

/// <summary>Mechanical properties from the same native model used for section analysis; no report or UI dependency.</summary>
public static class ConcreteSectionProperties
{
    public static ConcreteSectionPropertyResult Calculate(CheckerSectionModel model, JsonObject input, JsonObject workspace, JsonObject options)
    {
        double ec = model.Section.ConcreteMaterial.E, es = input.Required("steel_modulus_mpa", strict: true);
        var ratio = Homogenization.Resolve(es, ec, options.S("metodo") == "Da n",
            SectionWorkspace.Number(options.S(options.S("metodo") == "Da n" ? "n" : "phi", "0"), "Omogeneizzazione"));
        var section = model.Section; var values = new List<SectionPropertyValue>();
        string group = model.Geometry.Holes.Count > 0 ? "Solo calcestruzzo · al netto del foro, senza sottrarre le barre" : "Solo calcestruzzo · sezione lorda";
        void Add(string name, double value, string unit) => values.Add(new(group, name, value, unit));
        Add("Area", section.Area / 100, "cm²"); Add("Larghezza", model.Geometry.Width, "mm"); Add("Altezza", model.Geometry.Height, "mm");
        if (model.Geometry.Shape == "Circolare") Add("Lati per contorno circolare", model.Geometry.CircularSides, "");
        Add("Baricentro x", section.Centroid.X, "mm"); Add("Baricentro y", section.Centroid.Y, "mm");
        foreach (string key in new[] { "Jxx", "Jyy", "Jxy", "Jp", "J11", "J22" }) Add(key, (double)section.GetType().GetProperty(key)!.GetValue(section)! / 1e4, "cm⁴");
        foreach (string key in new[] { "WelXMin", "WelXMax", "WelYMin", "WelYMax" }) Add(key, (double)section.GetType().GetProperty(key)!.GetValue(section)! / 1e3, "cm³");
        Add("Raggio giratore x", section.Rxx, "mm"); Add("Raggio giratore y", section.Ryy, "mm");
        group = "Omogeneizzata al CLS · sezione integra (Checker)";
        Add("φ", ratio.Phi, ""); Add("n armature", ratio.N, "");
        foreach (var ep in workspace.Array("trefoli").Select(t => t.D("Ep")).Distinct()) Add($"n trefoli (Ep = {ep:G12} MPa)", Homogenization.Resolve(ep, ec, false, ratio.Phi).N, "");
        var h = section.GetHomogeneizedMechanicalProperties(ratio.Phi);
        Add("Area omogeneizzata", h.areaH / 100, "cm²"); Add("Baricentro x", h.centroidH.X, "mm"); Add("Baricentro y", h.centroidH.Y, "mm");
        Add("Sx", h.SxH / 1e3, "cm³"); Add("Sy", h.SyH / 1e3, "cm³");
        Add("Jxx", h.JxxH / 1e4, "cm⁴"); Add("Jyy", h.JyyH / 1e4, "cm⁴"); Add("Jxy", h.JxyH / 1e4, "cm⁴");
        Add("J11", h.J11H / 1e4, "cm⁴"); Add("J22", h.J22H / 1e4, "cm⁴"); Add("Angolo principale", h.angleX * 180 / Math.PI, "°");
        group = "Armature ordinarie";
        Add("Numero barre", model.Geometry.Bars.Count, ""); Add("As totale", model.Geometry.AreaSteel / 100, "cm²"); Add("As / Ac", 100 * model.Geometry.AreaSteel / section.Area, "%");
        foreach (var bars in model.Geometry.Bars.GroupBy(v => v.Diametro).OrderBy(g => g.Key)) Add($"{bars.Count()} Ø{bars.Key:G12} · As", bars.Sum(v => v.Area) / 100, "cm²");
        group = "Trefoli / cavi";
        Add("Numero cavi", workspace.Array("trefoli").Count, ""); Add("Ap totale", workspace.Array("trefoli").Sum(t => t.D("area")) / 100, "cm²");
        return new(ratio.Phi, ratio.N, values);
    }
}

public static class Homogenization
{
    public static (double Phi, double N) Resolve(double steelModulus, double concreteModulus, bool fromN, double value)
    {
        if (!double.IsFinite(steelModulus) || steelModulus <= 0 || !double.IsFinite(concreteModulus) || concreteModulus <= 0 || !double.IsFinite(value))
            throw new ArgumentException("Omogeneizzazione: inserire moduli elastici finiti positivi e un valore numerico.");
        double phi = fromN ? value * concreteModulus / steelModulus - 1 : value;
        if (!double.IsFinite(phi) || phi < -1e-12) throw new ArgumentException("Inserire φ ≥ 0 oppure n ≥ Es/Ecm.");
        phi = Math.Max(0, phi); double n = steelModulus * (1 + phi) / concreteModulus;
        if (!double.IsFinite(n)) throw new ArgumentException("Omogeneizzazione fuori intervallo numerico.");
        return (phi, n);
    }
}
