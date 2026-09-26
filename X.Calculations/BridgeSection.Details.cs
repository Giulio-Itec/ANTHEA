using System.Text.Json.Nodes;
namespace Anthea.Calculations;
public static partial class BridgeSection
{
    public static readonly string[] StiffenerSides = ["Bilaterali simmetrici", "Solo sinistra", "Solo destra", "Bilaterali diversi"];
    public static readonly string[] SupportLocations = ["Appoggio interno", "Estremità sinistra", "Estremità destra"];
    public static JsonObject DetailDefaults() => J.Obj(
        ("lati_irr", StiffenerSides[0]), ("pannelli_uguali", true), ("a_irr_dx", 3000),
        ("b_irr_dx", 150), ("t_irr_dx", 15), ("x_irr", 0), ("betaL_irr", 1), ("saldature_irr", false), ("aw_irr", 6),
        ("appoggio", false), ("pos_app", SupportLocations[0]), ("lati_app", StiffenerSides[0]),
        ("b_app", 180), ("t_app", 25), ("b_app_dx", 180), ("t_app_dx", 25),
        ("a_app_sx", 3000), ("a_app_dx", 3000), ("c_app", 150), ("R_app", ""), ("x_app", 0), ("z_app", 0),
        ("betaL_app", 1), ("s_app", 300), ("B_app", 400), ("terminale_rigido", false), ("e_term", 300),
        ("saldature_app", true), ("aw_app", 8), ("gamma_m2", 1.25),
        ("armatura_trasv", false), ("d_trasv_sup", 16), ("s_trasv_sup", 150), ("d_trasv_inf", 16), ("s_trasv_inf", 150),
        ("cot_trasv", 1), ("quota_q_sx", .5), ("As_m_trasv", 0), ("l_anc_trasv", 700), ("buona_aderenza_trasv", true),
        ("bordo_cls_sx", 500), ("bordo_cls_dx", 500), ("forcine_bordo", false), ("d_forcine", 12),
        ("fatica_pioli", false), ("q_fat_min", ""), ("q_fat_max", ""), ("lambda_v", 1), ("phi_fat", 1),
        ("flangia_fat_tesa", false), ("dsigma_fat", ""), ("gamma_ff", 1), ("gamma_mf_pioli", 1.25), ("gamma_mf_flangia", 1.35));

    public static (double LeftWidth, double LeftThickness, double RightWidth, double RightThickness) StiffenerPlates(JsonObject data, string suffix) =>
        HBridgeSection.StiffenerPlates(ToCheckerInput(data), suffix == "irr" ? BridgeStiffenerRole.Intermediate : BridgeStiffenerRole.Support);
}
