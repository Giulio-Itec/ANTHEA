using System.Reflection;
using System.Text.Json.Nodes;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Sections.Steel;

namespace X.Core;

/// <summary>Bridge N–Mx section, mm/N/MPa internally. Model owns geometry and materials;
/// Checker solves composite elastic stresses; ANTHEA iterates EN 1993-1-5 effective widths and superposes stage contributions.</summary>
public static partial class BridgeSection
{
    public const string Module = "str_mista_ponte";
    public const string Method = "Sezione composta N–Mx · larghezze efficaci EN 1993-1-5:2006 · v1";
    public static readonly string[] Standards = ["NTC 2018 / EN 1994-2:2005", "EN 1994-2:2005"];
    public static readonly string[] ConcreteNames = Catalog<ConcreteMaterialEN1992>(typeof(ConcreteMaterialEN1992Data)).Keys.ToArray();
    public static readonly string[] SteelNames = ["S235", "S275", "S355", "S420"];
    public static readonly string[] RebarNames = ["B450A", "B450C", "B500A", "B500B", "B500C"];
    public const string ShrinkageKind = "Ritiro";
    public static readonly string[] PhaseKinds = ["Solo acciaio", "Composta", "Soletta esclusa", ShrinkageKind];
    public static bool HasConcrete(string kind) => kind is "Composta" or ShrinkageKind;
    public static readonly string[] HomoModes = ["Da φ", "Da n"];
    public const string CommonLoadReference = "Quota comune", GrossLoadReference = "Baricentro omogeneizzato lordo", EffectiveLoadReference = "Baricentro efficace · iterativo";
    public static readonly string[] LoadReferences = [GrossLoadReference, EffectiveLoadReference, CommonLoadReference];
    public const string Scope = "Analisi elastica N–Mx con connessione completa, sezione simmetrica e anima senza irrigidimenti longitudinali. " +
        "b_eff della soletta è un dato di ingresso. Le fasi sono incrementi di carico già combinati; per ogni situazione la sezione efficace è comune ai contributi sommati. " +
        "Il ritiro uniforme assegnato al CLS produce effetti primari locali. Non è un'analisi evolutiva con redistribuzione viscosa. Taglio e pioli hanno controlli locali dedicati. Irrigidimenti, appoggi, saldature continue, soletta trasversale e fatica dei pioli hanno verifiche opzionali con campo dichiarato. Torsione e instabilità globale del ponte restano escluse.";
    public static JsonObject Defaults() => J.Obj(("versione_mista", 1), ("nome", "Sezione composta da ponte"),
        ("normativa", Standards[0]), ("gamma_m0", "1.05"), ("gamma_c", "1.5"), ("alpha_cc", "0.85"), ("gamma_s", "1.15"),
        ("stato", "SLU"), ("classe4", true), ("classe_cls", "C35/45"), ("acciaio", "S355"), ("armatura", "B450C"),
        ("fy_override", false), ("fy", "355"), ("b_cls", "3000"), ("h_cls", "250"), ("h_web", "1800"), ("t_web", "14"),
        ("b_top", "500"), ("t_top", "25"), ("b_bottom", "700"), ("t_bottom", "30"), ("plate2", false), ("b_bottom2", "500"), ("t_bottom2", "20"),
        ("rebars_top", true), ("d_top", "16"), ("pitch_top", "150"), ("cover_top", "45"),
        ("rebars_bottom", true), ("d_bottom", "16"), ("pitch_bottom", "150"), ("cover_bottom", "45"), ("y_ref", "0"),
        ("fasi", new JsonArray(Phase("G1 · getto e carpenteria", "Solo acciaio", 0, 1500, 0, 1, GrossLoadReference),
            Phase("G2 · permanenti portati", "Composta", 0, 2000, 2, 1.1, GrossLoadReference), Phase("Q · variabili", "Composta", 0, 3000, 0, 1, GrossLoadReference))));
    public static JsonObject Phase(string name = "Nuova fase", string kind = "Composta", double n = 0, double m = 0, double phi = 0, double psi = 1, string reference = CommonLoadReference) =>
        J.Obj(("nome", name), ("tipo", kind), ("attiva", true), ("N", n), ("Mx", m), ("V", 0), ("q_conn", 0), ("epsilon_cs", 0), ("modo", "Da φ"), ("phi", phi), ("psi", psi), ("n", "18"), ("riferimento_N", reference));
    public static JsonObject ShrinkagePhase() => Phase("Ritiro soletta", ShrinkageKind, phi: 2, psi: .55, reference: GrossLoadReference);
    public static string LoadReference(JsonObject phase) => phase.S("riferimento_N", CommonLoadReference);
    /// <summary>Fixed application point on the gross section, using the material ratios of this phase.</summary>
    public static double GrossPhaseCentroid(JsonObject data, JsonObject phase)
    {
        var g = Geometry(data);
        if (HasConcrete(phase.S("tipo"))) return NativeSection(data).GetHomogeneizedMechanicalProperties(Homogenization(data, phase).PhiEffective).centroidH.Y;
        if (phase.S("tipo") == "Solo acciaio") return g.SteelCentroid;
        if (phase.S("tipo") != "Soletta esclusa") throw new ArgumentException("Tipo di fase sconosciuto.");
        var m = Materials(data); double ratio = m.Rebar.ElasticModulusTension / m.Steel.ElasticModulusTension;
        return (g.SteelArea * g.SteelCentroid + g.Bars.Sum(b => b.Area * ratio * b.Y)) / (g.SteelArea + g.Bars.Sum(b => b.Area * ratio));
    }
    public static void ValidateShape(JsonObject d)
    {
        if (d.D("versione_mista") != 1 || d["fasi"] is not JsonArray p || p.Count is < 1 or > 20 || p.Any(x => x is not JsonObject))
            throw new ArgumentException("Formato della sezione composta non valido (versione 1, da 1 a 20 fasi).");
    }
    private static Dictionary<string, T> Catalog<T>(Type type) where T : Material => type.GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(p => typeof(T).IsAssignableFrom(p.PropertyType)).Select(p => (T)p.GetValue(null)!).ToDictionary(m => m.Name);
    public static (ConcreteMaterialEN1992 Concrete, SteelMaterial Steel, SteelMaterial Rebar) Materials(JsonObject d)
    {
        var c = Catalog<ConcreteMaterialEN1992>(typeof(ConcreteMaterialEN1992Data));
        var s = Catalog<SteelMaterialEN1993>(typeof(SteelMaterialEN1993Data));
        var r = Catalog<SteelMaterialEN1992>(typeof(SteelMaterialEN1992Data));
        if (!c.TryGetValue(d.S("classe_cls"), out var concrete) || !s.TryGetValue(d.S("acciaio"), out var steel) || !r.TryGetValue(d.S("armatura"), out var rebar))
            throw new ArgumentException("Selezionare materiali presenti nei cataloghi Model.");
        SteelMaterial a = steel;
        if (d.B("fy_override"))
        {
            double fy = d.Required("fy", strict: true);
            if (fy > steel.Fu) throw new ArgumentException("fy adottato non può superare fu del materiale.");
            a = new SteelMaterial(steel.Name, steel.ElasticModulusTension, fy, steel.Fu, .15, SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic, SteelMaterial.SteelTypes.Structural);
        }
        return (concrete, a, rebar);
    }
    public static BridgeGeometry Geometry(JsonObject d)
    {
        ValidateShape(d);
        double P(string k) => d.Required(k, strict: true);
        double b = P("b_cls"), tc = P("h_cls"), hw = P("h_web"), tw = P("t_web"), bt = P("b_top"), tt = P("t_top"), b1 = P("b_bottom"), t1 = P("t_bottom");
        double b2 = d.B("plate2") ? P("b_bottom2") : 0, t2 = d.B("plate2") ? P("t_bottom2") : 0;
        if (Math.Min(bt, b1) <= tw || b2 > b1 || b2 != 0 && b2 <= tw || hw <= tw)
            throw new ArgumentException("Controllare anima e piattabande: b > tw; la seconda piastra inferiore deve essere contenuta nella prima.");
        var mat = Materials(d);
        double tb = t1 + t2, bb = (b1 * t1 + b2 * t2) / tb, h = hw + tt + tb;
        IRebarSection? Bar(string side)
        {
            if (!d.B("rebars_" + side)) return null;
            double diameter = P("d_" + side), pitch = P("pitch_" + side), cover = P("cover_" + side);
            if (pitch < diameter || cover < diameter / 2 || cover + diameter / 2 > tc || diameter >= b || b / pitch > 500)
                throw new ArgumentException("Armatura " + side + ": controllare diametro, passo e distanza dell'asse dalla faccia (massimo 501 barre/fila).");
            return new RebarSectionCircular(diameter, mat.Rebar);
        }
        var top = Bar("top"); var bottom = Bar("bottom");
        if (top is not null && bottom is not null && tc - d.D("cover_top") - d.D("cover_bottom") < (top.Diameter + bottom.Diameter) / 2)
            throw new ArgumentException("Le file di armature devono avere assi distinti e distanza almeno pari alla somma dei raggi.");
        var shape = new SectionH(h, tw, bt, tt, bb, tb, "H saldato · piattabanda equivalente");
        var section = new ReinforcedConcreteSection(b, tc, mat.Concrete, top!, d.D("pitch_top"), d.D("cover_top"),
            bottom!, d.D("pitch_bottom"), shape, mat.Steel, d.D("cover_bottom"));
        var bars = section.Rebars.Select((r, i) => new BridgeBar("B" + (i + 1), r.Position.X, r.Position.Y, r.RebarSection.Diameter, r.Area)).ToArray();
        for (int i = 0; i < bars.Length; i++) for (int j = 0; j < i; j++)
            if (double.Hypot(bars[i].X - bars[j].X, bars[i].Y - bars[j].Y) < (bars[i].Diameter + bars[j].Diameter) / 2 - 1e-8)
                throw new ArgumentException("Le due file di armature si sovrappongono: correggere le quote degli assi.");
        // Resultant rectangle preserves area and overall thickness; exact two-plate inertia is reported separately.
        double y1 = -tt - hw - t1 / 2, y2 = -h + t2 / 2;
        double ab = b1 * t1 + b2 * t2, yb = (b1 * t1 * y1 + b2 * t2 * y2) / ab;
        double ib = b1 * Math.Pow(t1, 3) / 12 + b1 * t1 * Math.Pow(y1 - yb, 2) + b2 * Math.Pow(t2, 3) / 12 + b2 * t2 * Math.Pow(y2 - yb, 2);
        return new(b, tc, hw, tw, bt, tt, b1, t1, b2, t2, bb, tb, h, ab, yb, ib, shape.Area, shape.Jxx, shape.Centroid.Y - h, bars);
    }
    public static ReinforcedConcreteSection NativeSection(JsonObject d)
    {
        var g = Geometry(d); var m = Materials(d);
        return new ReinforcedConcreteSection(g.Width, g.SlabHeight, m.Concrete,
            d.B("rebars_top") ? new RebarSectionCircular(d.D("d_top"), m.Rebar) : null!, d.D("pitch_top"), d.D("cover_top"),
            d.B("rebars_bottom") ? new RebarSectionCircular(d.D("d_bottom"), m.Rebar) : null!, d.D("pitch_bottom"),
            new SectionH(g.Height, g.WebThickness, g.TopWidth, g.TopThickness, g.BottomEquivalentWidth, g.BottomEquivalentThickness, "H"), m.Steel, d.D("cover_bottom"));
    }
    public static (double N0, double N, double PhiEffective, double Phi) Homogenization(JsonObject data, JsonObject phase)
    {
        var m = Materials(data); double n0 = m.Steel.ElasticModulusTension / m.Concrete.ElasticModulusCompression;
        if (!HomoModes.Contains(phase.S("modo"))) throw new ArgumentException("Modo di omogeneizzazione sconosciuto.");
        double psi = phase.Required("psi", strict: true);
        if (phase.S("modo") == "Da n")
        {
            double n = phase.Required("n", strict: true);
            double effective = ReinforcedConcreteSection.CalculateHomogenizedFactorPhi(n, m.Steel, m.Concrete);
            if (effective < -1e-9) throw new ArgumentException("n deve essere almeno n₀ = Ea/Ecm per una viscosità non negativa.");
            return (n0, n, Math.Max(0, effective), Math.Max(0, effective) / psi);
        }
        double phi = phase.Required("phi"); return (n0, n0 * (1 + psi * phi), psi * phi, phi);
    }
}
