using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using System.Collections.Immutable;
namespace Anthea.Application.Presentation;

public sealed record HorizontalModelPreview(string Label, string Hint, IReadOnlyDictionary<string, string> Factors, string Efficiency);
public sealed record HorizontalChsPreview(JsonObject? Geometry, IReadOnlyDictionary<string, string> Fields);
public sealed record HorizontalConcretePreview(ConcreteGeometry Geometry, double LinkRadius, double LinkDiameter);

/// <summary>Sheet preview data from the existing pile and material engines. No WPF or new engineering rules.</summary>
public static class HorizontalPreview
{
    public static string StiffnessDescription(JsonObject data)
    {
        try
        {
            var s = ElasticHorizontalPile.SectionStiffness((JsonObject)data.DeepClone());
            return $"EJ={s.EI:G7} kNm² · E={s.ModulusMpa:G7} MPa · J={s.InertiaMm4:G7} mm⁴\n{s.Assumption}"
                + (s.OverrideReason == "" ? "" : " · OVERRIDE: " + s.OverrideReason);
        }
        catch (ArgumentException ex) { return "EJ da completare nei dati iniziali: " + ex.Message; }
    }
    private static readonly ImmutableArray<string> ChsKeys = ["diametro_mm", "spessore_mm", "area_mm2", "inerzia_mm4", "wel_mm3", "wpl_mm3", "massa_kg_m", "classe", "npl_kn", "mpl_knm"];
    public static HorizontalModelPreview ReadModel(JsonObject data)
    {
        string label, hint, efficiency;
        try
        {
            label = "Modello automatico: " + PaloOrizzontale.ModelloAutomatico(data);
            hint = "Scelta dalle proprietà effettive lungo il palo e dalla falda, non dal numero di righe.";
        }
        catch (ArgumentException ex) { label = "Modello automatico · dati da completare"; hint = ex.Message; }
        var factors = new Dictionary<string, string>(PileInputCatalog.CorrelationFields(data["verifica"], "1"));
        // Preserve the historical displayed value; normative consolidation belongs to F4.
        factors["__gamma_r"] = "1,30";
        try { efficiency = PaloOrizzontale.Efficiency(data).D("eta").ToString("F3"); }
        catch (ArgumentException) { efficiency = "—"; }
        return new(label, hint, factors, efficiency);
    }
    public static string PassivePressure(JsonObject row)
    {
        double? phi = J.Number(row["angolo_attrito"]);
        return row.S("tipologia") == "Granulare" && phi is >= 0 and < 90
            ? PaloOrizzontale.PassivePressureCoefficient(phi.Value).ToString("F1") : "—";
    }
    public static HorizontalConcretePreview ConcreteSection(JsonObject data)
    {
        var input = (JsonObject)data["sezione"]!.DeepClone();
        input["shape"] = "Circolare"; input["diameter_mm"] = data["generali"].Required("diametro", strict: true) * 1000;
        if (input.D("longitudinal_bar_count") > 512) throw new ArgumentException("Numero barre fuori campo");
        var geometry = ConcretePreview.PrepareGeometry(input, 12, 36);
        return new(geometry, GPC.Checkers.Concrete.Piles.PileReinforcement.CircularLinkRadius(input.D("diameter_mm"), input.D("cover_mm"), input.D("transverse_bar_diameter_mm")), input.D("transverse_bar_diameter_mm"));
    }
    public static IReadOnlyDictionary<string, string> ConcreteStrengths(JsonObject input)
    {
        try
        {
            var values = ConcreteMaterials.DesignValues(input, ConcreteStandards.PileWorkspace(input));
            return new Dictionary<string, string> { ["__fcd"] = values.Fcd.ToString("F1"), ["__fyd"] = values.Fyd.ToString("F1") };
        }
        catch (ArgumentException) { return new Dictionary<string, string> { ["__fcd"] = "—", ["__fyd"] = "—" }; }
    }
    public static HorizontalChsPreview ChsSection(JsonObject data)
    {
        var fields = new Dictionary<string, string>();
        try
        {
            var properties = MicropaloOrizzontale.Properties(data);
            foreach (string key in ChsKeys) fields["__" + key] = properties.D(key).ToString(key == "classe" ? "0" : "N1");
            fields["__fyd"] = properties.D("fyd_mpa").ToString("N1");
            return new(properties, fields);
        }
        catch (ArgumentException)
        {
            foreach (string key in ChsKeys) fields["__" + key] = "—";
            fields["__fyd"] = "—";
            return new(null, fields);
        }
    }
}
