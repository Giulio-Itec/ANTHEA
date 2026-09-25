using System.Text.Json.Nodes;
using X.Core;

internal static class HorizontalChsChecks
{
    internal static void Run()
    {
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        void Near(double a, double b) => Check(Math.Abs(a-b) <= 1e-8 * Math.Max(1, Math.Abs(b)), $"CHS: {a} != {b}");
        var d = MicropaloOrizzontale.Defaults(); d.Array("stratigrafie")[0]!.AsArray().Add(PaloOrizzontale.Layer());
        var p = MicropaloOrizzontale.Properties(d); var s = d["sezione"]!;
        s["modo_chs"] = "Manuale";
        Check(JsonNode.DeepEquals(p, MicropaloOrizzontale.Properties(d)), "CHS catalogo e manuale discordanti");
        s["diametro_chs_mm"] = 100; s["spessore_chs_mm"] = 10;
        p = MicropaloOrizzontale.Properties(d);
        Near(p.D("area_mm2"), 900 * Math.PI); Near(p.D("inerzia_mm4"), Math.PI / 64 * (1e8 - 40960000));
        Near(p.D("wpl_mm3"), (1e6 - 512000) / 6); Near(p.D("wel_mm3"), p.D("inerzia_mm4") / 50);
        var section = MicropaloOrizzontale.Section(d); Near(section.D("momento_knm"), p.D("mpl_knm"));
        d["generali"]!["azione_assiale"] = p.D("npl_kn") / 2;
        Near(MicropaloOrizzontale.Section(d).D("momento_knm"), p.D("mpl_knm") / 2);
        d["generali"]!["azione_assiale"] = -p.D("npl_kn") / 2;
        Near(MicropaloOrizzontale.Section(d).D("momento_knm"), p.D("mpl_knm") / 2);
        d["generali"]!["azione_assiale"] = 0;
        var result = PaloOrizzontale.Calculate(d); Check(result.S("errore") == "", result.S("errore"));
        Check(result["sezione"].S("tipo") == "CHS", "Motore CA usato per CHS");
        var equivalent = (JsonObject)d.DeepClone(); equivalent["generali"]!["origine_momento"] = "Manuale";
        equivalent["generali"]!["momento_resistente"] = result.D("momento_resistente_knm"); equivalent["generali"]!["provenienza_momento"] = "Test CHS";
        Near(PaloOrizzontale.Calculate(equivalent).D("capacita_kn"), result.D("capacita_kn"));
        foreach (var bad in new Action<JsonObject>[] {
            a => a["sezione"]!["spessore_chs_mm"] = 0,
            a => a["sezione"]!["spessore_chs_mm"] = 50,
            a => a["sezione"]!["spessore_chs_mm"] = 1,
            a => a["sezione"]!["diametro_chs_mm"] = 300,
            a => a["sezione"]!["gamma_m0"] = .5,
            a => a["generali"]!["azione_assiale"] = 1e9,
            a => { a["sezione"]!["modo_chs"] = "Catalogo"; a["sezione"]!["profilo_chs"] = "inesistente"; }
        }) { var invalid = (JsonObject)d.DeepClone(); bad(invalid); Check(PaloOrizzontale.Calculate(invalid).S("errore") != "", "CHS invalido accettato"); }
        Console.WriteLine("Micropalo orizzontale CHS: catalogo/manuale, geometria, N–M, errori e collegamento Broms OK.");
    }
}
