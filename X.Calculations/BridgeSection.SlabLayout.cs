using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Geometric references only; the solver still receives one homogeneous slab and two optional bar rows.</summary>
public sealed record BridgeSlabLayout(bool HasPredalle, double PredalleThickness, double Height,
    string BottomReference, double TopAxisY, double BottomAxisY);

public static partial class BridgeSection
{
    public const string SlabBottomReference = "Intradosso soletta", PredalleTopReference = "Estradosso predalle";
    public static readonly string[] BottomRebarReferences = [PredalleTopReference, SlabBottomReference];
    public const string PredalleScope = "La predalle è un riferimento geometrico compreso nello spessore totale della soletta. " +
        "Il calcolo mantiene un'unica soletta omogenea: nessun materiale, armatura propria, peso, fase o verifica aggiuntivi della predalle.";

    public static BridgeSlabLayout SlabLayout(JsonObject data)
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
