// Spostato da X.Calculations (commit 4144fe9e) con la decisione D3 del 15/10/2026: rami «legacy» di Calculate e Optimize
// (BridgeConcept.Library.cs), Validate (BridgeConcept.cs), CostWarnings (BridgeConcept.Pricing.cs) e LegacyReferenceDepth
// (BridgeConcept.AdvancedSchema.cs), usati solo dal motore precedente. Codice storico conservato per le prove, fuori dall'installer.
using System.Text.Json.Nodes;
using static Anthea.Calculations.BridgeConcept;
using TrafficModel = GPC.Design.Bridges.TrafficModel;

namespace Anthea.Calculations;

/// <summary>
/// Motore precedente di Bridge Design (voci I1-I3 dell'inventario D3): le parti storiche della classe partial
/// <see cref="BridgeConcept"/>, che nel prodotto calcola solo con GPC.Design. I tipi restano quelli di <see cref="BridgeConcept"/>.
/// </summary>
public static partial class LegacyBridgeConcept
{
    /// <summary>Calcolo col motore precedente: era <c>BridgeConcept.Calculate(data, legacy: true)</c>.</summary>
    public static Result Calculate(JsonObject data)
    {
        if (ReadTrafficModel(data) != TrafficModel.LegacyUniform) throw new ArgumentException("Il motore diagnostico precedente non supporta Schema 1.");
        return LegacyCalculate(data);
    }
    /// <summary>Ricerca col motore precedente: era <c>BridgeConcept.Optimize(…, legacy: true)</c>.</summary>
    public static OptimizationResult Optimize(JsonObject source, OptimizationOptions options, CancellationToken cancellation = default,
        IProgress<OptimizationProgress>? progress = null)
    {
        if (ReadTrafficModel(source) != TrafficModel.LegacyUniform) throw new ArgumentException("Il motore diagnostico precedente non supporta Schema 1.");
        return LegacyOptimize(source, options, cancellation, progress);
    }
    private static void Validate(JsonObject data)
    {
        ValidateShape(data);
        foreach (var (key, fields) in new[] { ("input", Site.Concat(Layout).Concat(Section).Concat(AdvancedSection).Concat(Substructure)), ("rates", Rates.AsEnumerable()), ("assumptions", Assumptions.Concat(AdvancedAssumptions)) })
            foreach (var p in fields)
            {
                var n = J.Number(data[key]?[p.Key]);
                if (n is null || n < p.Min || n > p.Max || p.Unit == "n." && n != Math.Truncate(n.Value))
                    throw new ArgumentException($"{p.Label}: valore richiesto tra {p.Min} e {p.Max} {p.Unit}.");
            }
        var i = data["input"]!;
        if (!DeckTypes.Contains(i.S("deck_type"))) throw new ArgumentException("Tipo di impalcato non riconosciuto.");
        if (!Families.Any(f => f.Id == i.S("family"))) throw new ArgumentException("Tipologia impalcato non riconosciuta.");
        foreach (var (key, choices) in new[] { ("obstacle", Obstacles), ("soil", Soils), ("pier", Piers), ("foundation", Foundations) })
            if (!choices.Contains(i.S(key))) throw new ArgumentException("Scelta non valida: " + key);
        foreach (string key in new[] { "continuous", "start_pier", "end_pier", "low_carbon", "recycled" })
            if (i[key] is not JsonValue v || !v.TryGetValue<bool>(out _)) throw new ArgumentException("Opzione non valida: " + key);
        if (i.S("obstacle") != "Nessuno" && i.D("obstacle_width") >= i.D("length") - 4) throw new ArgumentException("L'ostacolo deve lasciare almeno 2 m per lato alle estremità del ponte.");
    }
    private static void CostWarnings(List<string> warnings, double length, double bearingForce, double bearingCount, double bearingRate)
    {
        double average = bearingForce / Math.Max(1, bearingCount);
        if (average > 1500 || bearingRate < 2500) warnings.Add($"Appoggi: carico medio equivalente {average:0} kN/dispositivo, prezzo {bearingRate:0} €/cad. Verificare portata e corsa: il listino ANAS usa €/kN; nessun dimensionamento dei dispositivi.");
        if (length > 200) warnings.Add("Giunti: il prezzo fisso non segue corsa termica/sismica e lunghezza dilatabile. Per grandi opere può sottostimare il costo.");
        warnings.Add("Computo parziale: scavi, rinterri, drenaggi, protezioni, impermeabilizzazione, centine alte, accessi e sicurezza specifica non computati analiticamente. Oneri aggiuntivi e imprevisti sono riserve, non un computo completo.");
    }
    public static double LegacyReferenceDepth(Family family, double maxSpan, bool continuous) => Math.Max(family.MinDepth,
        maxSpan / family.Ratio * (HasUpperStructure(family.Id) ? 1 : continuous ? .95 : 1.1));
}
