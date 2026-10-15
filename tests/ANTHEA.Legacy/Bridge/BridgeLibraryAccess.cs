// Spostato da X.Calculations/BridgeSection.cs e BridgeConcept.Library.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026 (voce T7):
// involucri verso GPC usati solo dalle prove. Stesse chiamate GPC, fuori dall'installer.
using System.Text.Json.Nodes;
using GPC.Checkers.CompositeBridge;
using GPC.Model.Sections.Concrete;
using NativeBridge = GPC.Design.Bridges.BridgeConcept;

namespace Anthea.Calculations;

/// <summary>
/// Accesso delle prove alle API GPC che ricevono gli archivi ANTHEA già convertiti: la conversione resta quella del prodotto
/// (membri internal, visibili tramite InternalsVisibleTo). Le API GPC che non richiedono conversioni si chiamano direttamente.
/// </summary>
public static class BridgeLibraryAccess
{
    /// <summary>Era <c>BridgeSection.NativeSection</c>: la sezione Model della sola geometria.</summary>
    public static ReinforcedConcreteSection NativeSection(JsonObject d) => HBridgeSection.NativeSection(BridgeSection.GeometryInput(d));
    /// <summary>Era <c>BridgeConcept.OptimizationExclusions</c>: le esclusioni della ricerca GPC.Design per un risultato.</summary>
    public static string[] OptimizationExclusions(JsonObject data, BridgeConcept.Result result, BridgeConcept.OptimizationOptions options) =>
        NativeBridge.OptimizationExclusions(BridgeConcept.NativeInput(data), BridgeConcept.ToNative(result), BridgeConcept.ToNative(options));
}
