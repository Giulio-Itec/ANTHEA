using System.Reflection;
using System.Text.Json.Nodes;
using GPC.Model.Data.Concrete;
using GPC.Model.Data.Steel;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;

namespace Anthea.Calculations;

/// <summary>Application boundary: archive inputs, Model catalogs and calls into Checker. No section solver lives here.</summary>
public static partial class BridgeSection
{
    public const string Module = "str_mista_ponte";
    public const string Method = HBridgeSection.Method;
    public static readonly string[] Standards = ["NTC 2018 / EN 1994-2:2005", "EN 1994-2:2005"];
    public static readonly string[] ConcreteNames = ConcreteMaterialCatalog.NativeConcrete().Select(m => m.Name).ToArray();
    public static readonly string[] SteelNames = ["S235", "S275", "S355", "S420"];
    public static readonly string[] RebarNames = ["B450A", "B450C", "B500A", "B500B", "B500C"];
    public const string ShrinkageKind = "Ritiro";
    public static readonly string[] PhaseKinds = ["Solo acciaio", "Composta", "Soletta esclusa", ShrinkageKind];
    public static bool HasConcrete(string kind) => kind is "Composta" or ShrinkageKind;
    public static readonly string[] HomoModes = ["Da φ", "Da n"];
    public const string CommonLoadReference = "Quota comune", GrossLoadReference = "Baricentro omogeneizzato lordo", EffectiveLoadReference = "Baricentro efficace · iterativo";
    public static readonly string[] LoadReferences = [GrossLoadReference, EffectiveLoadReference, CommonLoadReference];
    public const string Scope = HBridgeSection.Scope;
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
    public static double GrossPhaseCentroid(JsonObject data, JsonObject phase) => HBridgeSection.GrossPhaseCentroid(GeometryInput(data), ToCheckerPhase(phase));
    public static void ValidateShape(JsonObject d)
    {
        if (d.D("versione_mista") != 1 || d["fasi"] is not JsonArray p || p.Count < 1 || p.Any(x => x is not JsonObject))
            throw new ArgumentException("Formato della sezione composta non valido (versione 1, almeno una fase).");
    }
    public static (ConcreteMaterialEN1992 Concrete, SteelMaterial Steel, SteelMaterial Rebar) Materials(JsonObject d)
    {
        var c = ConcreteMaterialCatalog.NativeConcrete().ToDictionary(m => m.Name);
        var s = ConcreteMaterialCatalog.NativeStructuralSteel().ToDictionary(m => m.Name);
        var r = ConcreteMaterialCatalog.NativeReinforcement().ToDictionary(m => m.Name);
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
    public static BridgeGeometry Geometry(JsonObject d) => HBridgeSection.Geometry(GeometryInput(d));
    public static ReinforcedConcreteSection NativeSection(JsonObject d) => HBridgeSection.NativeSection(GeometryInput(d));
    public static (double N0, double N, double PhiEffective, double Phi) Homogenization(JsonObject data, JsonObject phase) => HBridgeSection.Homogenization(GeometryInput(data), ToCheckerPhase(phase));
}
