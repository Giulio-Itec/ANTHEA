using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class BridgeSection
{
    public static IEnumerable<string[]> DetailInputRows(JsonObject d)
    {
        var rows = new List<string[]>();
        void Add(string key, string name, string unit = "")
        { rows.Add([name, J.Number(d[key]) is {} n ? EngineeringFormat.Number(n) : d[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b ? "Sì" : "No" : d.S(key), unit]); }
        void Plates(string suffix, string name)
        {
            Add("lati_" + suffix, name + " · disposizione");
            Add("b_" + suffix, name + " · sporgenza sinistra / unico", "mm"); Add("t_" + suffix, name + " · spessore sinistro / unico", "mm");
            if (d.S("lati_" + suffix) == StiffenerSides[3]) { Add("b_" + suffix + "_dx", name + " · sporgenza destra", "mm"); Add("t_" + suffix + "_dx", name + " · spessore destro", "mm"); }
            Add("betaL_" + suffix, name + " · Lcr / distanza fra flange");
            Add("saldature_" + suffix, name + " · verifica saldature");
            if (d.B("saldature_" + suffix)) Add("aw_" + suffix, name + " · gola cordoni", "mm");
        }
        if (d.B("irrigidimenti"))
        {
            Plates("irr", "Intermedio"); Add("pannelli_uguali", "Intermedio · pannelli uguali"); Add("a_irr", "Intermedio · aL / passo", "mm");
            if (!d.B("pannelli_uguali")) Add("a_irr_dx", "Intermedio · aR", "mm");
            Add("N_irr", "Intermedio · compressione esterna SLU", "kN"); Add("x_irr", "Intermedio · posizione compressione esterna x", "mm");
        }
        if (d.B("appoggio"))
        {
            Add("pos_app", "Appoggio · posizione"); Plates("app", "Appoggio");
            if (d.S("pos_app") != SupportLocations[1]) Add("a_app_sx", "Appoggio · pannello sinistro", "mm");
            if (d.S("pos_app") != SupportLocations[2]) Add("a_app_dx", "Appoggio · pannello destro", "mm");
            if (d.S("pos_app") != SupportLocations[0])
            {
                Add("c_app", "Appoggio · distanza dall’estremità", "mm"); Add("terminale_rigido", "Terminale · richiesta curva rigida");
                if (d.B("terminale_rigido")) Add("e_term", "Terminale · interasse due coppie", "mm");
            }
            Add("R_app", "Appoggio · R inviluppo SLU", "kN"); Add("x_app", "Appoggio · posizione reazione x", "mm"); Add("z_app", "Appoggio · eccentricità longitudinale z", "mm");
            Add("s_app", "Appoggio · lunghezza impronta", "mm"); Add("B_app", "Appoggio · larghezza impronta", "mm");
        }
        if (d.B("pioli"))
        {
            Add("armatura_trasv", "Soletta · verifica armatura trasversale"); Add("fatica_pioli", "Pioli · verifica resistente a fatica");
            if (d.B("armatura_trasv"))
            {
                foreach (string side in new[] { "sup", "inf" })
                { Add("d_trasv_" + side, "Barre trasversali " + side + " · diametro", "mm"); if (d.D("d_trasv_" + side) > 0) Add("s_trasv_" + side, "Barre trasversali " + side + " · passo", "mm"); }
                Add("cot_trasv", "Soletta · cot θ"); Add("quota_q_sx", "Soletta · frazione q a sinistra"); Add("As_m_trasv", "Soletta · As per flessione trasversale", "mm²/m");
                Add("l_anc_trasv", "Soletta · ancoraggio disponibile", "mm"); Add("buona_aderenza_trasv", "Soletta · buona aderenza");
                Add("bordo_cls_sx", "Piolo esterno · distanza bordo reale SX", "mm"); Add("bordo_cls_dx", "Piolo esterno · distanza bordo reale DX", "mm");
                Add("forcine_bordo", "Splitting · forcine ancorate a U"); if (d.B("forcine_bordo")) Add("d_forcine", "Splitting · diametro forcine", "mm");
            }
            if (d.B("fatica_pioli"))
            {
                Add("q_fat_min", "Fatica · q minimo", "kN/m"); Add("q_fat_max", "Fatica · q massimo", "kN/m");
                Add("lambda_v", "Fatica · equivalenza λv"); Add("phi_fat", "Fatica · amplificazione φfat"); Add("flangia_fat_tesa", "Fatica · flangia anche tesa");
                if (d.B("flangia_fat_tesa")) { Add("dsigma_fat", "Fatica · ΔσE,2 flangia", "MPa"); Add("gamma_mf_flangia", "Fatica · γMf flangia"); }
                Add("gamma_ff", "Fatica · γFf"); Add("gamma_mf_pioli", "Fatica · γMf,s pioli");
            }
        }
        return rows;
    }
}
