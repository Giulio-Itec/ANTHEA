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
        "kg" => QuantityUnit.Kilogram, "t" => QuantityUnit.Tonne, "cad" or "nr" or "n" => QuantityUnit.Item,
        _ => throw new ArgumentException("Unità del prezzo non supportata: " + value)
    };
    private static string Unit(QuantityUnit value) => value switch
    {
        QuantityUnit.Metre => "m", QuantityUnit.SquareMetre => "m²", QuantityUnit.CubicMetre => "m³",
        QuantityUnit.Kilogram => "kg", QuantityUnit.Tonne => "t", QuantityUnit.Item => "cad", _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
