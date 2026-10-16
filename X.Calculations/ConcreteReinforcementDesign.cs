using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Discrete reinforcement search over the existing section services. Never mutates its source.
/// No domain mesh is generated. Candidates retain the full trace of every applicable combination.</summary>
public static partial class ConcreteReinforcementDesign
{
    internal sealed record Layout(int Top, int Bottom, int Side, double TopPhi, double BottomPhi, double SidePhi,
        bool Stirrups, double StirrupPhi, double Spacing);
    public static ConcreteDesignResult Optimize(JsonObject source, ConcreteDesignOptions options,
        CancellationToken token = default, IProgress<ConcreteDesignProgress>? progress = null)
        => OptimizeNative(source,options,token,progress);

    private static void ValidateScope(JsonObject data, JsonObject workspace)
    {
        if (workspace.S("normativa") != "NTC 2018") throw new ArgumentException("Calcola armature: dettagli costruttivi disponibili per NTC 2018 con integrazioni EC2.");
        if (workspace.Array("trefoli").Count > 0) throw new ArgumentException("Calcola armature: attualmente disponibile per armatura ordinaria senza trefoli.");
        if (data["input"].S("shape") is not ("Rettangolare" or "Circolare" or "A T")) throw new ArgumentException("Ricerca disponibile per rettangoli, sezioni a T e circolari, anche con foro centrale.");
        if (workspace["dettagli_costruttivi"].S("elemento") == "Da scegliere") throw new ArgumentException("Scegliere trave, pilastro, soletta o parete nei dettagli costruttivi.");
        if (workspace["dettagli_costruttivi"].S("zona_sovrapposizione") == "Sì") throw new ArgumentException("Ricerca sulla sezione fuori dalle giunzioni: le barre sovrapposte richiedono una disposizione locale esplicita.");
        if (workspace["sle_comuni"].S("esposizione", "Da scegliere") == "Da scegliere") throw new ArgumentException("Scegliere l’esposizione SLE per il copriferro di durabilità.");
        if (!SectionWorkspace.Sets.Any(k => data["combinazioni"]!.Array(k).Count > 0) && workspace["taglio"]!.Array("azioni").Count == 0)
            throw new ArgumentException("Inserire almeno una combinazione di sollecitazioni nel foglio.");
    }

    internal static JsonObject CandidateInput(JsonObject source, Layout p)
    {
        var i = (JsonObject)source.DeepClone(); i.Remove("barre_manuali");
        foreach (string k in new[] { "top", "bottom", "inner" }) i["second_" + k + "_enabled"] = false;
        i["flange_bottom_count"] = "0";
        i["top_bar_count"] = p.Top; i["bottom_bar_count"] = p.Bottom; i["side_bar_count_per_side"] = p.Side;
        i["top_bar_diameter_mm"] = p.TopPhi; i["bottom_bar_diameter_mm"] = p.BottomPhi; i["side_bar_diameter_mm"] = p.SidePhi;
        i["longitudinal_bar_count"] = p.Top; i["longitudinal_bar_diameter_mm"] = p.TopPhi;
        i["staffe_presenti"] = p.Stirrups ? "Sì" : "No";
        i["transverse_bar_diameter_mm"] = p.Stirrups ? p.StirrupPhi : source.D("transverse_bar_diameter_mm", 8);
        i["transverse_spacing_mm"] = p.Stirrups ? p.Spacing : source.D("transverse_spacing_mm", 200);
        return i;
    }
    public static double Score(ConcreteDesignMetrics m, ConcreteDesignOptions o) => ScoreNative(m, o);
    public static ConcreteDesignCandidate[] Rank(IEnumerable<ConcreteDesignCandidate> source, ConcreteDesignOptions o) => RankNative(source, o);
    internal static string Describe(JsonObject i, JsonObject d) => (i.S("shape") == "Circolare" ? $"{i.S("longitudinal_bar_count")}Ø{i.S("longitudinal_bar_diameter_mm")}" :
        $"Sup. {i.S("top_bar_count")}Ø{i.S("top_bar_diameter_mm")} · inf. {i.S("bottom_bar_count")}Ø{i.S("bottom_bar_diameter_mm")} · laterali {i.S("side_bar_count_per_side")}Ø{i.S("side_bar_diameter_mm")}/lato") +
        (i.S("staffe_presenti") == "Sì" ? $" · staffe Ø{i.S("transverse_bar_diameter_mm")}/{i.S("transverse_spacing_mm")}" : " · senza staffe") +
        (d.S("elemento") is "Soletta piena" or "Parete" ? $" · ortogonali Ø{d.S("diametro_secondaria")}/{d.S("passo_secondaria")} per faccia" : "");
}
