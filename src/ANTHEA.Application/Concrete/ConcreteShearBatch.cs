using System.Text.Json.Nodes;
namespace Anthea.Application.Concrete;

public static class ConcreteShearBatch
{
    /// <summary>Preserves row-level input errors; a failure outside that contract stops the batch with already published rows intact.</summary>
    public static void Calculate(JsonObject input, JsonObject settings, IReadOnlyList<JsonObject> rows,
        Action<int, ConcreteShearResult?, string?> completed, CancellationToken cancellation = default)
    {
        var options = settings["taglio"]!.AsObject();
        ConcreteCalculationSettings.ValidateStirrups(input, options);
        for (int i = 0; i < rows.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            ConcreteShearResult? result = null; string? error = null;
            try { result = ConcreteShearAnalysis.Calculate(input, settings, options, rows[i]); }
            catch (ArgumentException ex) { error = ex.Message; }
            completed(i, result, error);
        }
    }
}
