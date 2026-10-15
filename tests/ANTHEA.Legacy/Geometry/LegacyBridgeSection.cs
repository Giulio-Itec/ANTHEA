// Spostato da X.Calculations/BridgeSection.SlabLayout.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: codice morto, senza
// chiamanti nemmeno nelle prove. Codice storico conservato per le prove, fuori dall'installer.
using System.Text.Json.Nodes;
using static Anthea.Calculations.BridgeSection;

namespace Anthea.Calculations;

/// <summary>Parte storica di <see cref="BridgeSection"/> (voce J2 dell'inventario D3): disposizione della soletta prima di Checker.</summary>
public static class LegacyBridgeSection
{
    // Diagnostic source retained until removal is approved.
    public static BridgeSlabLayout LegacySlabLayout(JsonObject data)
    {
        bool enabled = data.B("predalle");
        double height = J.Number(data["h_cls"]) ?? double.NaN;
        double thickness = enabled ? J.Number(data["h_predalle"]) ?? double.NaN : 0;
        if (enabled && (!double.IsFinite(thickness) || thickness <= 0 || !double.IsFinite(height) || thickness >= height))
            throw new ArgumentException("Predalle: inserire uno spessore maggiore di zero e minore dello spessore totale della soletta.");
        string reference = enabled ? data.S("rif_ferri_inf", PredalleTopReference) : SlabBottomReference;
        double bottomDistance = J.Number(data["cover_bottom"]) ?? double.NaN;
        if (enabled && data.B("rebars_bottom"))
        {
            if (!BottomRebarReferences.Contains(reference)) throw new ArgumentException("Selezionare il riferimento geometrico dei ferri inferiori.");
            if (reference == PredalleTopReference && (!double.IsFinite(bottomDistance) || bottomDistance <= 0 || bottomDistance < data.D("d_bottom") / 2))
                throw new ArgumentException("Ferri inferiori: la distanza dall'estradosso predalle all'asse deve essere positiva e almeno pari al raggio della barra.");
        }
        return new(enabled, thickness, height, reference,
            height - (J.Number(data["cover_top"]) ?? double.NaN),
            bottomDistance + (enabled && reference == PredalleTopReference ? thickness : 0));
    }
}
