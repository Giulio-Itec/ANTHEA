// Spostato da X.Calculations/PaloOrizzontale.Stratified.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: aiuto usato solo dalla
// prova supporto/test/X.Verifiche/HorizontalStratifiedChecks.cs (T8). Codice conservato per le prove, fuori dall'installer.
using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static class LegacyPaloOrizzontale
{
    /// <summary>Uses a copy of the input and selects the distributed, possibly mixed-soil model.</summary>
    public static JsonObject CalculateStratified(JsonObject data)
    {
        var copy = (JsonObject)data.DeepClone();
        if (copy["generali"] is not JsonObject general) return J.Error("Dati generali mancanti.");
        general["metodo_calcolo"] = PaloOrizzontale.StratifiedMethod;
        return PaloOrizzontale.Calculate(copy);
    }
}
