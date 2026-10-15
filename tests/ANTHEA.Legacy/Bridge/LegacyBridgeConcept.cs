// Spostato da X.Calculations/BridgeConcept.Library.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: ramo «legacy» di Optimize,
// prima scelto con un parametro che nessuno passava. Codice storico conservato per le prove, fuori dall'installer.
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
    /// <summary>Ricerca col motore precedente: era <c>BridgeConcept.Optimize(…, legacy: true)</c>.</summary>
    public static OptimizationResult Optimize(JsonObject source, OptimizationOptions options, CancellationToken cancellation = default,
        IProgress<OptimizationProgress>? progress = null)
    {
        if (ReadTrafficModel(source) != TrafficModel.LegacyUniform) throw new ArgumentException("Il motore diagnostico precedente non supporta Schema 1.");
        return LegacyOptimize(source, options, cancellation, progress);
    }
}
