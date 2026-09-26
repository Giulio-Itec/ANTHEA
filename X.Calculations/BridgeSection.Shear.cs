using System.Text.Json.Nodes;
namespace Anthea.Calculations;
public static partial class BridgeSection
{
    public static JsonObject AccessoryDefaults() => J.Obj(("gamma_m1", 1.1), ("eta_taglio", 1.2),
        ("irrigidimenti", false), ("a_irr", 3000), ("b_irr", 150), ("t_irr", 15), ("N_irr", 0),
        ("pioli", false), ("n_pioli", 2), ("d_pioli", 22), ("h_pioli", 150), ("passo_pioli", 200),
        ("passo_trasv_pioli", 150), ("fu_pioli", 450), ("gamma_v", 1.25), ("d_testa_pioli", 35), ("t_testa_pioli", 12),
        ("pioli_fatica", true), ("copriferro_pioli", 35));
    public static void EnsureAccessoryDefaults(JsonObject data)
    {
        foreach (var pair in AccessoryDefaults()) if (!data.ContainsKey(pair.Key))
            data[pair.Key] = pair.Value?.DeepClone();
        foreach (var pair in DetailDefaults()) if (!data.ContainsKey(pair.Key))
            data[pair.Key] = pair.Value?.DeepClone();
    }

}
