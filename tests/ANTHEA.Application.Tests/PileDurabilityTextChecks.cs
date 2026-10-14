using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

/// <summary>F2.9-U3 (decision of 14/10/2026): cover reference and refusals of the elastic pile in Italian, values unchanged.</summary>
internal static class PileDurabilityTextChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        JsonObject Input(string exposure) => J.Obj(("esposizione", exposure), ("fck_mpa", 30), ("vita_durabilita", 50),
            ("longitudinal_bar_diameter_mm", 20), ("qualita_copriferro", false));
        var details = J.Obj(("aggregato", 20), ("delta_c", 10), ("copriferro_override", false));
        var xc2 = ElasticHorizontalPile.PileDurability(Input("XC2"), details);
        Check(xc2.S("fonte") == "NTC 2018 §4.1.6.1.3 e Circolare 2019 Tab. C4.1.IV", "Riferimento del copriferro non tradotto: " + xc2.S("fonte"));
        Check(xc2.D("cmin_dur") == 25 && !xc2.ContainsKey("errore"), "Copriferro di durabilità XC2 C30 cambiato");
        var unknown = ElasticHorizontalPile.PileDurability(Input("XZ9"), details);
        Check(unknown.S("errore").EndsWith(ConcreteLibraryMapping.DurabilityUnknownExposureMessage), "Esposizione sconosciuta non tradotta: " + unknown.S("errore"));
        return checks;
    }
}
