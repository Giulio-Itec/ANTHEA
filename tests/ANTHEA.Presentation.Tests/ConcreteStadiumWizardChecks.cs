using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.ConcreteWorkspace;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

internal static partial class ConcreteSectionSheetChecks
{
    private static void StadiumWizardChecks(Expect e)
    {
        var data = SezioneCA.DefaultData(); SectionWorkspace.Prepare(data);
        string original = data.ToJsonString();
        var wizard = new ConcreteTemplateWizardViewModel(data);
        wizard.Form["wizard_type"].SelectedChoice = ConcreteTemplateWizard.Choices[7];
        foreach (var (key, value) in new[] {
            ("width_mm", "5000"), ("height_mm", "2500"), ("cover_mm", "50"),
            ("transverse_bar_diameter_mm", "12"), ("wizard_stadium_curve_count", "17"),
            ("wizard_stadium_curve_diameter_mm", "32"), ("wizard_stadium_straight_count", "8"),
            ("wizard_stadium_straight_diameter_mm", "25") }) wizard.Form[key].EditText = value;
        wizard.Form.Commit();
        e.That(wizard.CanApply && wizard.Geometry!.Bars.Count == 50, "Capsula: anteprima 5000 × 2500 non disponibile: " + wizard.Error);
        e.That(wizard.Form["wizard_stadium_curve_count"].IsVisible && wizard.Form["wizard_stadium_straight_count"].IsVisible &&
            !wizard.Form["wizard_radial_count"].IsVisible && !wizard.Form["wizard_bar_diameter_mm"].IsVisible,
            "Capsula: campi delle due disposizioni mescolati.");
        e.That(data.ToJsonString() == original, "Capsula: modificare la bozza cambia il documento.");
        wizard.Form["wizard_stadium_straight_count"].EditText = "10"; wizard.Form.Commit();
        e.That(wizard.CanApply && wizard.Geometry!.Bars.Count == 54, "Capsula: conteggio rettilineo non applicato all'anteprima.");
        wizard.Form["wizard_stadium_layout"].SelectedChoice = ConcreteTemplateAdapter.StadiumLegacy;
        e.That(wizard.CanApply && wizard.Geometry!.Bars.Count == 24 && wizard.Form["wizard_radial_count"].IsVisible &&
            !wizard.Form["wizard_stadium_curve_count"].IsVisible, "Capsula: modalità radiale precedente non disponibile.");
        wizard.Form["wizard_stadium_layout"].SelectedChoice = ConcreteTemplateAdapter.StadiumSeparate;
        e.That(wizard.CanApply && wizard.Geometry!.Bars.Count == 54, "Capsula: cambio modalità perde i conteggi.");
        wizard.Form["wizard_stadium_curve_count"].EditText = "1"; wizard.Form.Commit();
        e.That(!wizard.CanApply && !wizard.Apply() && data.ToJsonString() == original, "Capsula: conteggio non valido applicato.");
        wizard.Form["wizard_stadium_curve_count"].EditText = "17"; wizard.Form.Commit();
        e.That(wizard.Apply() && new SezioneCA(data["input"]!.AsObject()).Bars.Count == 54, "Capsula: conferma non applicata.");
    }
}
