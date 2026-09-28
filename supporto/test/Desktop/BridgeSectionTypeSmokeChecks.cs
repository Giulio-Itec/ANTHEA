using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The type of section of the bridge: H, H with an inclined web and box girder from the input panel, with the fields shown for each type, the
    /// calculation, the tags of the drawing with the real plates and the report; the H is restored at the end
    /// </summary>
    private async Task CheckBridgeSectionTypes(BridgeWorkspace bridge, Func<Task> wait, string directory)
    {
        int count = 0;
        void Check(bool value, string message) { count++; if (!value) throw new Exception("Tipo di sezione: " + message); }
        async Task Layout() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        int originalPage = bridge.Pages.SelectedIndex;
        bridge.Pages.SelectedIndex = 0; await Layout();
        var form = bridge.InputForms.Single(f => f.Editors.ContainsKey("sezione"));
        var steel = bridge.InputForms.Single(f => f.Editors.ContainsKey("t_web"));
        var plate = bridge.InputForms.Single(f => f.Editors.ContainsKey("plate2"));
        var kind = (ComboBox)form.Editors["sezione"];
        Check(kind.SelectedItem?.ToString() == BridgeSection.SectionTypes[0], "la sezione iniziale deve essere l'H.");
        Check(form.Editors["offset_anima"].Visibility != Visibility.Visible && form.Editors["interasse_anime"].Visibility != Visibility.Visible, "campi di anima inclinata e cassoncino visibili per l'H.");
        Check(plate.Visibility == Visibility.Visible, "seconda piastra nascosta per l'H.");

        kind.SelectedItem = BridgeSection.SectionTypes[1]; form.Set("offset_anima", "300"); await wait(); await Layout();
        Check(form.Editors["offset_anima"].Visibility == Visibility.Visible && form.Editors["interasse_anime"].Visibility != Visibility.Visible, "campi dell'anima inclinata.");
        Check(plate.Visibility != Visibility.Visible, "seconda piastra visibile con l'anima inclinata.");
        Check(bridge.Calculation is { } inclined && inclined.Geometry.SectionType == BridgeSteelSectionType.InclinedWebH && inclined.Geometry.WebAngle > 0, "calcolo con l'anima inclinata.");
        Check(bridge.Drawing.VisibleTags.Any(t => t.Contains("α") && t.Contains("lunghezza")), "tag dell'anima inclinata nel disegno.");
        File.WriteAllBytes(Path.Combine(directory, "sezione_anima_inclinata.png"), Ui.Snapshot(this));

        kind.SelectedItem = BridgeSection.SectionTypes[2]; form.Set("interasse_anime", "1800"); form.Set("offset_anima", "250");
        steel.Set("b_top", "450"); steel.Set("b_bottom", "1400"); steel.Set("t_bottom", "25"); await wait(); await Layout();
        Check(form.Editors["interasse_anime"].Visibility == Visibility.Visible, "interasse delle anime nascosto per il cassoncino.");
        Check(bridge.Calculation is { } box && box.Geometry.WebCount == 2 && box.Geometry.TopFlangeCount == 2 && Math.Abs(box.Geometry.TopWidth - 900) < 1e-9, "calcolo del cassoncino.");
        Check(bridge.Drawing.VisibleTags.Any(t => t.Contains("2 × 450")), "tag delle due piattabande superiori.");
        Check(bridge.Calculation!.Stages[^1].Warnings.Any(w => w.StartsWith("Cassoncino")), "ipotesi del cassoncino non dichiarate.");
        Check(bridge.BuildReport("Cassoncino", ReportBridge.DefaultSections()).Length > 1000, "relazione del cassoncino.");
        File.WriteAllBytes(Path.Combine(directory, "sezione_cassoncino.png"), Ui.Snapshot(this));
        bridge.Pages.SelectedIndex = 1; await Layout();
        File.WriteAllBytes(Path.Combine(directory, "sezione_cassoncino_tensioni.png"), Ui.Snapshot(this));
        bridge.Pages.SelectedIndex = 0; await Layout();

        // box torsion: options only for the box, ΔT editable with the checks, results, drawing unchanged and report
        Check(bridge.BoxInputsVisible && bridge.TorqueColumnVisible, "opzioni di torsione e colonna ΔT del cassoncino nascoste.");
        Check(bridge.Calculation!.Stages.All(s => s.Torsion is null) && !bridge.TorsionResultsVisible, "torsione calcolata senza attivarla.");
        var torsionForm = bridge.InputForms.Single(f => f.Editors.ContainsKey("torsione_cassoncino"));
        ((CheckBox)torsionForm.Editors["torsione_cassoncino"]).IsChecked = true; await Layout();
        torsionForm.Set("t_controvento", "4"); torsionForm.Set("L_campata", "40000"); await Layout();
        Check(torsionForm.Editors["passo_diaframmi"].Visibility == Visibility.Visible && torsionForm.Editors["t_diaframma"].Visibility != Visibility.Visible, "campi della distorsione.");
        torsionForm.Set("passo_diaframmi", "5000"); torsionForm.Set("m_t_dist", "60"); torsionForm.Set("T_c_dist", "600"); await Layout();
        Check(torsionForm.Editors["t_diaframma"].Visibility == Visibility.Visible && torsionForm.Editors["A_diagonale"].Visibility != Visibility.Visible, "campi del diaframma a piastra.");
        var phaseForms = bridge.InputForms.Where(f => f.Editors.ContainsKey("T") && f.Editors.ContainsKey("Mx")).ToArray();
        Check(phaseForms.Length >= 2 && phaseForms.All(f => f.Editors["T"].Visibility == Visibility.Visible), "ΔT nelle fasi del cassoncino con la torsione.");
        // the example of the guides: 200, 300 and 1000 kNm on the three default phases
        phaseForms[0].Set("T", "200"); if (phaseForms.Length == 3) phaseForms[1].Set("T", "300"); phaseForms[^1].Set("T", "1000"); await wait(); await Layout();
        var torsion = bridge.Calculation!.Stages[^1].Torsion;
        Check(torsion is not null && torsion.WebFlow > 0 && torsion.SlabFlow > 0 && torsion.Distortion is { Diaphragms: 7 }, "calcolo della torsione e della distorsione.");
        Check(bridge.Calculation.Stages[0].Torsion!.Flows[0].Closed, "fase di solo acciaio chiusa dal controvento.");
        bridge.RevealTorsion(false); await Layout(); bridge.RevealTorsion(false); await Layout();
        File.WriteAllBytes(Path.Combine(directory, "sezione_cassoncino_torsione_ingressi.png"), Ui.Snapshot(this));
        bridge.Pages.SelectedIndex = 1; await Layout();
        var resultsTab = bridge.Results.SelectedIndex;
        bridge.RevealTorsion(true); await Layout();
        Check(bridge.TorsionResultsVisible, "risultati della torsione non mostrati.");
        File.WriteAllBytes(Path.Combine(directory, "sezione_cassoncino_torsione.png"), Ui.Snapshot(this));
        bridge.Results.SelectedIndex = resultsTab; bridge.Pages.SelectedIndex = 0; await Layout();
        Check(bridge.BuildReport("Cassoncino con torsione", ReportBridge.DefaultSections()).Length > 1000, "relazione con la torsione.");
        foreach (var phase in phaseForms) phase.Set("T", "0");
        ((CheckBox)torsionForm.Editors["torsione_cassoncino"]).IsChecked = false; await wait(); await Layout();
        Check(bridge.Calculation!.Stages.All(s => s.Torsion is null), "torsione disattivata.");

        kind.SelectedItem = BridgeSection.SectionTypes[0]; form.Set("offset_anima", "0");
        steel.Set("b_top", "500"); steel.Set("b_bottom", "700"); steel.Set("t_bottom", "30"); await wait(); await Layout();
        Check(bridge.Calculation is { } h && h.Geometry.SectionType == BridgeSteelSectionType.H && plate.Visibility == Visibility.Visible, "ritorno all'H.");
        Check(!bridge.BoxInputsVisible && !bridge.TorqueColumnVisible, "opzioni di torsione visibili per l'H.");
        bridge.Pages.SelectedIndex = originalPage; await Layout();
        File.WriteAllText(Path.Combine(directory, "tipo-sezione-smoke.txt"), $"OK: {count} controlli su H, H con anima inclinata e cassoncino (campi, calcolo, disegno, torsione, relazione).");
    }
}
