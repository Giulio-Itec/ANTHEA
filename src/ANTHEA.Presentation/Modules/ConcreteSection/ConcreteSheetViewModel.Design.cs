using System.Text.Json.Nodes;
using Anthea.Calculations;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The side of the sheet that the reinforcement design (sub-step S8, <see cref="ConcreteDesignViewModel"/>) uses: the panel, its options and
/// what «Applica configurazione» does to the other tabs after writing the proposal into the data.
/// </summary>
public sealed partial class ConcreteSheetViewModel
{
    /// <summary>The main tab of the reinforcement design («Calcola armature»).</summary>
    public const int DesignTab = 7;

    /// <summary>The reinforcement design (tab 08, S8).</summary>
    public ConcreteDesignViewModel Design { get; private set; } = null!;

    /// <summary>The options of the search in the settings (<c>calcola_armature</c>), prepared at opening.</summary>
    internal JsonObject DesignOptions => Settings["calcola_armature"]!.AsObject();

    // The tab 08, built last, after its options are prepared: no write.
    private void CreateDesignPanel()
    {
        Design = new ConcreteDesignViewModel(this);
        OnPropertyChanged(nameof(Design));
    }

    /// <summary>
    /// The end of ApplyDesign of the historic view, after the proposal is written into the data: the geometry, the reinforcement and the second
    /// layers, the detailing, the anchorage, the shear and the torsion show the data again; the bars are cleared and the sheet is invalidated
    /// (the bars of the new section come with the preview).
    /// </summary>
    internal void DesignApplied()
    {
        foreach (var form in new[] { Inputs.Geometry, Inputs.Reinforcement }.Concat(Inputs.Layers.Select(l => l.Form)))
            foreach (var field in form.Fields) if (Input.ContainsKey(field.Key)) form.Set(field.Key, Input.S(field.Key), true);
        Detailing.ReloadFields();
        foreach (var field in Shear.Form.Fields) Shear.Form.Set(field.Key, ShearOptions.S(field.Key), true);
        foreach (var field in Shear.Torsion.Fields) Shear.Torsion.Set(field.Key, ShearOptions.S(field.Key), true);
        Inputs.Bars.Rows.Clear(); Invalidate();
    }
}
