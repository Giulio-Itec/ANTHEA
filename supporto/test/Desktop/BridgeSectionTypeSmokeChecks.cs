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

        kind.SelectedItem = BridgeSection.SectionTypes[0]; form.Set("offset_anima", "0");
        steel.Set("b_top", "500"); steel.Set("b_bottom", "700"); steel.Set("t_bottom", "30"); await wait(); await Layout();
        Check(bridge.Calculation is { } h && h.Geometry.SectionType == BridgeSteelSectionType.H && plate.Visibility == Visibility.Visible, "ritorno all'H.");
        bridge.Pages.SelectedIndex = originalPage; await Layout();
        File.WriteAllText(Path.Combine(directory, "tipo-sezione-smoke.txt"), $"OK: {count} controlli su H, H con anima inclinata e cassoncino (campi, calcolo, disegno, relazione).");
    }
}
