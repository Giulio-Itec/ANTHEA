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
        bool topBars = data.B("rebars_top"), bottomBars = data.B("rebars_bottom");
        string reference = enabled ? data.S("rif_ferri_inf", PredalleTopReference) : SlabBottomReference;
        var nativeReference = reference switch
        {
            SlabBottomReference => GPC.Checkers.CompositeBridge.SlabBottomReference.SlabSoffit,
            PredalleTopReference => GPC.Checkers.CompositeBridge.SlabBottomReference.PredalleTop,
            _ when !bottomBars => GPC.Checkers.CompositeBridge.SlabBottomReference.SlabSoffit,
            _ => throw new ArgumentException("Selezionare il riferimento geometrico dei ferri inferiori.")
        };
        var value = new GPC.Checkers.CompositeBridge.SlabRebarLayout(enabled, enabled ? data.Required("h_predalle") : 0,
            data.Required("h_cls"), nativeReference, topBars ? data.Required("cover_top") : 0,
            bottomBars ? data.Required("cover_bottom") : 0, bottomBars,
            bottomBars ? data.Required("d_bottom", strict: true) : 0);
        return new(value.HasPredalle, value.PredalleThickness, value.Height, reference, value.TopAxisY, value.BottomAxisY);
    }
}
