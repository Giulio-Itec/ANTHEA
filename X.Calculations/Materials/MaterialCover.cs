using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Calculations;

namespace Materiali;

/// <summary>Uses the same durability engines and input conventions as the Materiali sheet.</summary>
/// <remarks>Refactoring F2.9: the durability goes through the facades and <see cref="ConcreteDurabilityAdapter"/>; the optional
/// engine (F2.9-D2) selects it for every call of the entry point, default <see cref="ConcreteDurabilityAdapter.Default"/>.</remarks>
public static class MaterialCover
{
    private static double Number(string raw) => double.TryParse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v)
        ? v : throw new ArgumentException("Dati del copriferro incompleti o non numerici.");

    // The nominal cover shown in the material sheet, using its reference bar diameter.
    public static double Required(JsonObject state, double fck, DurabilityEngine? engine = null) => Required(state, fck,
        Number(state["numeri"]?["diameter"]?.ToString() ?? "16"), engine);

    public static double Required(JsonObject state, double fck, double diameter, DurabilityEngine? engine = null)
    {
        string Choice(string key, string fallback) => state["scelte"]?[key]?.ToString() ?? fallback;
        bool Flag(string key) => state["opzioni"]?[key]?.GetValue<bool>() == true;
        string code = state["esposizione_principale"]?.ToString() ?? "XC1";
        var exposure = ConcreteDurabilityAdapter.Exposures(engine).SingleOrDefault(e => e.Code == code) ?? throw new ArgumentException("Definire la classe di esposizione.");
        string ground = Choice("ground", "Casseratura"), abrasion = Choice("abrasion", "Nessuno");
        double aggregate = Number(state["numeri"]?["aggregate"]?.ToString() ?? "20");
        if (diameter < .1 || diameter > 1000 || aggregate < .1 || aggregate > 1000) throw new ArgumentException("Diametro o aggregato fuori intervallo.");
        var input = new CoverInput(Choice("life", "50 anni") == "100 anni" ? 100 : 50,
            Flag("highStrength"), Flag("slab"), Flag("quality"), diameter, aggregate,
            Number(Choice("deviationValue", "10 mm").Replace(" mm", "")), Flag("rough"),
            abrasion.StartsWith("XM3") ? 15 : abrasion.StartsWith("XM2") ? 10 : abrasion.StartsWith("XM1") ? 5 : 0,
            ground == "Direttamente su terra" ? 75 : ground == "Terreno preparato" ? 40 : 0);
        return Choice("coverMethod", "NTC + Circ. 2019") == "EC2 2004" ? Durability.Cover([exposure], fck, input, engine).Nominal :
            NtcCover.Calculate([exposure], fck, input, Choice("ntcElement", "Trave / pilastro") == "Piastra / soletta / parete", Flag("ntcQuality"), MinimumConcrete.Required([exposure], engine), engine).Cover.Nominal;
    }
}
