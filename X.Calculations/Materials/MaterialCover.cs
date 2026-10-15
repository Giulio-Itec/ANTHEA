using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Calculations;

namespace Materiali;

/// <summary>Uses the same durability engines and input conventions as the Materiali sheet.</summary>
/// <remarks>Refactoring F2.9: the durability goes through the facades and <see cref="ConcreteDurabilityAdapter"/>. Il motore
/// facoltativo (F2.9-D2) è uscito dal prodotto con la decisione D3 del 15/10/2026: le prove agganciano il legacy da tests/ANTHEA.Legacy
/// per tutta la chiamata.</remarks>
public static class MaterialCover
{
    private static double Number(string raw) => double.TryParse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v)
        ? v : throw new ArgumentException("Dati del copriferro incompleti o non numerici.");

    // The nominal cover shown in the material sheet, using its reference bar diameter.
    public static double Required(JsonObject state, double fck) => Required(state, fck,
        Number(state["numeri"]?["diameter"]?.ToString() ?? MaterialDefaults.Number("diameter")));

    public static double Required(JsonObject state, double fck, double diameter)
    {
        string Choice(string key) => state["scelte"]?[key]?.ToString() ?? MaterialDefaults.Choice(key);
        string code = state["esposizione_principale"]?.ToString() ?? MaterialDefaults.Exposure;
        var exposure = ConcreteDurabilityAdapter.Exposures().SingleOrDefault(e => e.Code == code) ?? throw new ArgumentException("Definire la classe di esposizione.");
        var input = Input(state, diameter, Number(state["numeri"]?["aggregate"]?.ToString() ?? MaterialDefaults.Number("aggregate")));
        return Choice("coverMethod") == "EC2 2004" ? Durability.Cover([exposure], fck, input).Nominal :
            NtcCover.Calculate([exposure], fck, input, Choice("ntcElement") == "Piastra / soletta / parete", state["opzioni"]?["ntcQuality"]?.GetValue<bool>() == true, MinimumConcrete.Required([exposure])).Cover.Nominal;
    }
    public static CoverInput Input(JsonObject state, double diameter, double aggregate)
    {
        string Choice(string key) => state["scelte"]?[key]?.ToString() ?? MaterialDefaults.Choice(key);
        bool Flag(string key) => state["opzioni"]?[key]?.GetValue<bool>() == true;
        string ground = Choice("ground"), abrasion = Choice("abrasion");
        if (diameter < .1 || diameter > 1000 || aggregate < .1 || aggregate > 1000) throw new ArgumentException("Diametro o aggregato fuori intervallo.");
        return new CoverInput(Choice("life") == "100 anni" ? 100 : 50,
            Flag("highStrength"), Flag("slab"), Flag("quality"), diameter, aggregate,
            Number(Choice("deviationValue").Replace(" mm", "")), Flag("rough"),
            abrasion.StartsWith("XM3") ? 15 : abrasion.StartsWith("XM2") ? 10 : abrasion.StartsWith("XM1") ? 5 : 0,
            ground == "Direttamente su terra" ? 75 : ground == "Terreno preparato" ? 40 : 0);
    }
}
