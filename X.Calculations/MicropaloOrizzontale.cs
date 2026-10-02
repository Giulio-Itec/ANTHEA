using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Piles;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Standards;

namespace Anthea.Calculations;

/// <summary>
/// Micropalo sotto azione orizzontale: tubo CHS di Model (catalogo di ModelData o D, t assegnati), proprietà della sezione di Model e momento
/// resistente della libreria (MicropileTube.LateralResistance, classe 1, interazione lineare N–M). Unità del foglio: mm, MPa, kN, kNm.
/// </summary>
public static class MicropaloOrizzontale
{
    public const string Module = "geo_micropalo_orizzontale";
    public static JsonObject Defaults()
    {
        var data = PaloOrizzontale.Defaults(); data["tipo_sezione"] = "CHS";
        data["generali"]!["diametro"] = "0.24"; data["generali"]!["origine_momento"] = "Sezione CHS";
        data["sezione"] = J.Obj(("modo_chs", "Catalogo"), ("profilo_chs", "CHS 139.7 × 8"),
            ("diametro_chs_mm", "139.7"), ("spessore_chs_mm", "8"), ("fy_chs_mpa", "355"), ("gamma_m0", "1.05"));
        return data;
    }

    /// <summary>Il tubo, l'acciaio (fy) e γM0 della sezione del foglio, con i controlli del foglio.</summary>
    private static (SectionCHS Tube, SteelMaterial Steel, StandardNTC2018Steel Standard) Tubo(JsonObject data)
    {
        var s = data["sezione"]!; double d, t; SectionCHS? catalog = null;
        if (s.S("modo_chs") == "Catalogo")
        {
            if (!Chs.Catalogo.TryGetValue(s.S("profilo_chs"), out var profile)) throw new ArgumentException("Selezionare un CHS dal catalogo ANTHEA.");
            (d, t) = profile; catalog = Chs.Sezione(s.S("profilo_chs"));
        }
        else if (s.S("modo_chs") == "Manuale") { d = s.Required("diametro_chs_mm", strict: true); t = s.Required("spessore_chs_mm", strict: true); }
        else throw new ArgumentException("Modalità CHS non riconosciuta.");
        if (2 * t >= d) throw new ArgumentException("CHS: lo spessore deve essere minore di metà diametro.");
        if (d >= data["generali"]!.Required("diametro", strict: true) * 1000) throw new ArgumentException("Il diametro CHS deve essere minore del diametro geotecnico.");
        double fy = s.Required("fy_chs_mpa", strict: true), gamma = s.Required("gamma_m0", 1);
        return (catalog ?? new SectionCHS(d, t), new SteelMaterial("CHS", 210000, fy, fy), new StandardNTC2018Steel { GammaM0 = gamma });
    }

    public static JsonObject Properties(JsonObject data)
    {
        var (tube, steel, standard) = Tubo(data);
        double fyd = steel.CalculateFyd(standard), a = tube.Area;
        // Proprietà di Model (SectionCHS), classe della libreria; massa dalla densità di Model (7850 kg/m³).
        return J.Obj(("diametro_mm", tube.Diameter), ("spessore_mm", tube.Thickness), ("diametro_interno_mm", tube.DiameterInternal), ("area_mm2", a),
            ("inerzia_mm4", tube.J11), ("wel_mm3", tube.Wel1), ("wpl_mm3", tube.Wpl1), ("massa_kg_m", Chs.Acciaio.Density * a * 1e6),
            ("fyd_mpa", fyd), ("classe", MicropileTube.SectionClass(tube, steel.Fyk)), ("npl_kn", a * fyd / 1000), ("mpl_knm", tube.Wpl1 * fyd / 1e6));
    }

    public static JsonObject Section(JsonObject data)
    {
        var p = Properties(data);
        if (p.D("classe") != 1) throw new ArgumentException("CHS non di classe 1: momento automatico per il meccanismo plastico non disponibile. Occorre una valutazione specifica; non viene attribuita automaticamente duttilità.");
        double? axial = J.Number(data["generali"]!["azione_assiale"]);
        if (axial is null || !double.IsFinite(axial.Value)) throw new ArgumentException("Forza assiale non valida.");
        if (Math.Abs(axial.Value) / p.D("npl_kn") >= 1) throw new ArgumentException("La forza assiale raggiunge o supera la resistenza del CHS.");
        var (tube, steel, standard) = Tubo(data);
        var r = MicropileTube.LateralResistance(tube, steel, standard, axial.Value * 1000, data["generali"]!.Required("diametro", strict: true) * 1000);
        p["momento_knm"] = r.ResistingMoment / 1e6; p["n_kn"] = axial.Value;
        p["tipo"] = "CHS";
        p["modello"] = MicropileTubeResistance.Model;
        return p;
    }
}
