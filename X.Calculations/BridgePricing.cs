using System.Text.Json;
using System.Text.Json.Nodes;
using GPC.Design.Costs;
using NativeBridge = GPC.Design.Bridges.BridgeConcept;
namespace Anthea.Calculations;

public sealed record BridgePriceRate(string WorkCode, string Unit, decimal? Price, string Reference, string[] Includes);
public sealed record BridgePriceSnapshot(string Revision, string Currency, BridgePriceRate[] Rates);
public sealed record BridgePriceLine(string Id, string WorkCode, decimal Quantity, string Unit, decimal? UnitPrice,
    decimal? Cost, string Issue, string? IncludedBy);
public sealed record BridgePriceEstimate(string Revision, bool Complete, decimal? Direct, decimal? Total, BridgePriceLine[] Lines);

/// <summary>Typed pricing adapter. Arithmetic and inclusion policies are exclusively GPC.Design operations.</summary>
public static class BridgePricing
{
    public sealed class Context
    {
        internal readonly NativeBridge.Result Result;
        internal readonly GPC.Design.Bridges.BridgeAssumptions Assumptions;
        internal Context(NativeBridge.Result result, GPC.Design.Bridges.BridgeAssumptions assumptions) { Result = result; Assumptions = assumptions; }
        public BridgePriceEstimate Evaluate(BridgePriceSnapshot snapshot)
        {
            var e = NativeBridge.Reprice(Result, Assumptions, Native(snapshot));
            return new(e.PriceRevision, e.Complete, e.DirectCost, e.TotalCost, e.Lines.Select(l => new BridgePriceLine(l.Quantity.Id,
                l.Quantity.WorkCode, l.Quantity.Amount, Unit(l.Quantity.Unit), l.UnitPrice, l.Amount, l.Issue.ToString(), l.IncludedBy)).ToArray());
        }
    }
    public static Context Prepare(JsonObject data)
    {
        var model = BridgeConcept.NativeInput(data) with { Prices = null };
        return new(NativeBridge.Calculate(model), model.Assumptions);
    }
    public static decimal ConvertPrice(decimal value, string sourceUnit, string targetUnit) => CostEstimator.ConvertPrice(value, Unit(sourceUnit), Unit(targetUnit));
    internal static ResolvedPriceSnapshot? Read(JsonObject data)
    {
        if (data["pricing"] is null) return null;
        if (data["pricing"] is not JsonObject pricing || (pricing["schema"] is not JsonValue schema || !schema.TryGetValue<int>(out int version) || version != 1) || pricing["snapshot"] is null)
            throw new ArgumentException("Documento prezzario non valido o versione non supportata.");
        try { return Native(pricing["snapshot"]!.Deserialize<BridgePriceSnapshot>() ?? throw new ArgumentException("Snapshot prezzi mancante.")); }
        catch (JsonException e) { throw new ArgumentException("Snapshot prezzi non valido.", e); }
    }
    internal static ResolvedPriceSnapshot Native(BridgePriceSnapshot snapshot)
    {
        if (snapshot.Rates is null) throw new ArgumentException("Tariffe risolte mancanti.");
        return new(snapshot.Revision, snapshot.Currency, snapshot.Rates.Select(r => new ResolvedCostRate(r.WorkCode, Unit(r.Unit), r.Price, r.Reference, r.Includes)));
    }
    private static QuantityUnit Unit(string value) => value.Trim().ToLowerInvariant() switch
    {
        "m" => QuantityUnit.Metre, "m2" or "m²" => QuantityUnit.SquareMetre, "m3" or "m³" => QuantityUnit.CubicMetre,
        "kg" => QuantityUnit.Kilogram, "t" => QuantityUnit.Tonne, "cad" or "nr" or "n" => QuantityUnit.Item, "h" or "ora" => QuantityUnit.Hour,
        _ => throw new ArgumentException("Unità del prezzo non supportata: " + value)
    };
    private static string Unit(QuantityUnit value) => value switch
    {
        QuantityUnit.Metre => "m", QuantityUnit.SquareMetre => "m²", QuantityUnit.CubicMetre => "m³",
        QuantityUnit.Kilogram => "kg", QuantityUnit.Tonne => "t", QuantityUnit.Item => "cad", QuantityUnit.Hour => "h", _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    /// <summary>
    /// Price analyses of the catalogue (decision of the user of 14/10/2026): arithmetic, nesting and refusals are GPC.Design operations; this
    /// adapter maps units and translates the refusals. Items are the priced entries of the catalogue (code, unit, price).
    /// </summary>
    public static IReadOnlyDictionary<string, PriceAnalysisResult> ResolveAnalyses(IEnumerable<(string Code, string Unit, decimal? Price)> items,
        IEnumerable<PriceAnalysisEntry> analyses, decimal generalExpensesPercent, decimal profitPercent)
    {
        try
        {
            var resolved = PriceAnalyses.Resolve(items.Select(i => new ResolvedCostRate(i.Code, Unit(i.Unit), i.Price, "voce/" + i.Code)),
                analyses.Select(a => new PriceAnalysis(a.Code, Unit(a.Unit), (a.Components ?? []).Select(c => new AnalysisComponent(c.Code, c.Quantity)).ToArray(),
                    a.GeneralExpensesPercent, a.ProfitPercent)), generalExpensesPercent, profitPercent);
            return resolved.ToDictionary(p => p.Key, p => new PriceAnalysisResult(p.Value.Code, Unit(p.Value.Unit),
                p.Value.Lines.Select(l => new PriceAnalysisLine(l.Code, l.Quantity, Unit(l.Unit), l.UnitPrice, l.Amount)).ToArray(), p.Value.Subtotal,
                p.Value.GeneralExpensesPercent, p.Value.GeneralExpenses, p.Value.ProfitPercent, p.Value.Profit, p.Value.UnitPrice,
                p.Value.GeneralExpensesOverridden, p.Value.ProfitOverridden), StringComparer.Ordinal);
        }
        catch (ArgumentException e) { throw new ArgumentException(AnalysisMessage(e.Message), e); }
    }

    static readonly (System.Text.RegularExpressions.Regex Pattern, string Text)[] AnalysisMessages =
    [
        (new(@"^Missing component: (?<c>.*) in (?<a>.*)$"), "Analisi ${a}: componente ${c} assente dal prezzario."),
        (new(@"^Component without price: (?<c>.*) in (?<a>.*)$"), "Analisi ${a}: la voce ${c} non ha prezzo."),
        (new(@"^Circular reference between analyses: (?<a>.*)$"), "Analisi ${a}: riferimento circolare fra analisi."),
        (new(@"^Code used twice: (?<a>.*)$"), "Codice ${a} usato due volte fra voci e analisi."),
        (new(@"^Invalid or repeated component in (?<a>.*): (?<c>.*)$"), "Analisi ${a}: componente ${c} non valido o ripetuto (quantità maggiore di zero)."),
        (new(@"^Analysis without code, unit or components: (?<a>.*)$"), "Analisi ${a}: servono codice, unità e almeno un componente."),
        (new(@"^Percentage of (?<p>general expenses|profit)(?: of (?<a>.*))? outside 0–100\.$"), "Percentuale fuori dall'intervallo 0–100."),
        (new(@"^Invalid or duplicate priced item: (?<c>.*)$"), "Voce ${c} non valida o duplicata."),
    ];

    // Library refusals of the analyses in Italian; a text without translation is a programming error, as in the other mappings
    static string AnalysisMessage(string message)
    {
        foreach (var (pattern, text) in AnalysisMessages) { var m = pattern.Match(message); if (m.Success) return m.Result(text); }
        if (message.StartsWith("Unità del prezzo", StringComparison.Ordinal)) return message;
        throw new InvalidOperationException("Rifiuto delle analisi prezzi senza traduzione: " + message);
    }
}

public sealed record PriceAnalysisComponent(string Code, decimal Quantity);
public sealed record PriceAnalysisEntry(string Code, string Description, string Unit, PriceAnalysisComponent[] Components,
    decimal? GeneralExpensesPercent, decimal? ProfitPercent);
public sealed record PriceAnalysisLine(string Code, decimal Quantity, string Unit, decimal UnitPrice, decimal Amount);
public sealed record PriceAnalysisResult(string Code, string Unit, PriceAnalysisLine[] Lines, decimal Subtotal, decimal GeneralExpensesPercent,
    decimal GeneralExpenses, decimal ProfitPercent, decimal Profit, decimal UnitPrice, bool GeneralExpensesOverridden, bool ProfitOverridden);
