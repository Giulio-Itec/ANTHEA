using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The side of the sheet that the shear and torsion (sub-step S5, <see cref="ConcreteShearViewModel"/>) uses: the panel, the links shown
/// read-only in the tab of the detailing (until the ViewModel of S6), the results of a row and the change of an option of the shear. The rows
/// of the shear, their calculation on the UI thread (C2), the automatic parameters and the invalidations stay in
/// ConcreteSheetViewModel.Checks.cs, where S1 put them.
/// </summary>
public sealed partial class ConcreteSheetViewModel
{
    /// <summary>The main tab of the shear and torsion («Taglio e torsione»).</summary>
    public const int ShearTab = 4;

    /// <summary>Shear and torsion (tab 05, S5).</summary>
    public ConcreteShearViewModel Shear { get; private set; } = null!;

    /// <summary>
    /// The links shown read-only in the tab 06 «Dettagli costruttivi» (BuildStirrups(readOnly: true) of BuildDetailingPanel): built with the
    /// tab 05 because the part «[taglio]» of the traces writes them with those of the shear; S6 takes them in its ViewModel.
    /// </summary>
    public ConcreteStirrupsViewModel DetailingStirrups { get; private set; } = null!;

    /// <summary>The last automatic parameters of the shear could not be computed from the section (they are empty).</summary>
    internal bool AutomaticShearFailed { get; private set; }

    /// <summary>The shear checks of the row <paramref name="id"/>, when calculated.</summary>
    internal ConcreteResults.ShearResult[]? ShearChecks(string id) => shearResults.GetValueOrDefault(id);

    /// <summary>The torsion check of the row <paramref name="id"/>, when calculated.</summary>
    internal ConcreteResults.TorsionResult? TorsionCheck(string id) => torsionResults.GetValueOrDefault(id);

    // The tabs 05 and 06 (their read-only links), built after the families of the serviceability: no write.
    private void CreateShearPanel()
    {
        Shear = new ConcreteShearViewModel(this, Actions.Shear);
        DetailingStirrups = new ConcreteStirrupsViewModel(this);
        OnPropertyChanged(nameof(Shear)); OnPropertyChanged(nameof(DetailingStirrups));
    }

    // Every form that shows the links read-only, in the order of the historic view (the tab 05, then the tab 06).
    private IEnumerable<FormViewModel> ReadOnlyStirrupForms() => Shear is null ? [] : Shear.Stirrups.Forms.Concat(DetailingStirrups?.Forms ?? []);

    /// <summary>
    /// An option of the shear changed (the callback of its form, BuildShearPanel): the model with links brings the links back in the inputs
    /// (the sheet is invalidated), then the fields, the automatic parameters, the links of the drawings and the shear calculated again.
    /// </summary>
    internal void ShearOptionChanged(string key)
    {
        if (key == "modello" && ShearOptions.S("modello") == "Con staffe" && Input.S("staffe_presenti") == "No") { Input["staffe_presenti"] = "Sì"; Invalidate(); }
        Shear.EnableFields(); RefreshAutomaticShear(); SynchronizeStirrups(); InvalidateActions("Taglio");
    }
}
