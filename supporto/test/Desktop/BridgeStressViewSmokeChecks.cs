using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private async Task CheckBridgeStressView(BridgeWorkspace bridge, Func<Task> wait, string directory)
    {
        int count = 0;
        void Check(bool value, string message) { count++; if (!value) throw new Exception(message); }
        async Task Layout() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        async Task SetScale(string text)
        {
            bridge.ConcreteScaleEditor.Focus(); bridge.ConcreteScaleEditor.Text = text;
            bridge.Drawing.Focus(); await Layout();
        }
        int page = bridge.Pages.SelectedIndex, view = bridge.DisplayChoice.SelectedIndex, stageIndex = bridge.StageChoice.SelectedIndex;
        var settings = bridge.Data["ui_mista"]!.AsObject();
        bridge.Pages.SelectedIndex = 1; bridge.DisplayChoice.SelectedIndex = 0; await Layout();
        var result = bridge.Calculation!;
        double n = result.Stages.Last().Contributions.Last().HomogenizationN;
        Check(bridge.ConcreteScaleAuto.IsChecked == true && Math.Abs(bridge.Drawing.ConcreteAmplification - n) < 1e-12, "Scala CLS non inizializzata a n della fase variabile.");
        Check(bridge.ConcreteScaleAuto.ToolTip?.ToString()?.Contains("Q · variabili") == true, "Sorgente della scala automatica non identificabile.");
        var physical = result.Stages.Last().Points.Select(p => p.Stress).ToArray();
        var rawLabel = "σc,sup " + result.Stages.Last().Points.First(p => p.Material == "CLS").Stress.ToString("0.##", CultureInfo.GetCultureInfo("it-IT"));
        Check(bridge.Drawing.StressLabels.Any(p => p.Text == rawLabel), "Valori CLS del diagramma non corrispondono alle tensioni reali.");
        File.WriteAllBytes(Path.Combine(directory, "tensioni_campiture_auto.png"), Ui.Snapshot(this));
        File.WriteAllBytes(Path.Combine(directory, "diagramma_campiture_auto.png"), Ui.Snapshot(bridge.Drawing));
        await SetScale("15,5");
        Check(bridge.ConcreteScaleAuto.IsChecked == false && bridge.Drawing.ConcreteAmplification == 15.5, "Fattore manuale con virgola non acquisito.");
        Check(ReferenceEquals(result, bridge.Calculation) && !bridge.ResultsAreStale && physical.SequenceEqual(bridge.Calculation!.Stages.Last().Points.Select(p => p.Stress)), "Scala grafica modifica il calcolo o invalida le verifiche.");
        Check(bridge.Drawing.StressLabels.Any(p => p.Text == rawLabel), "Amplificazione applicata anche alle etichette numeriche.");
        Check(settings.D("amplificazione_cls") == 15.5 && !settings.B("amplificazione_cls_auto", true), "Scala manuale non salvata nelle preferenze.");
        using (var reopened = new BridgeWorkspace((JsonObject)bridge.Data.DeepClone()))
        {
            await reopened.CalculateAsync();
            Check(reopened.Drawing.ConcreteAmplification == 15.5 && reopened.ConcreteScaleAuto.IsChecked == false, "Riapertura dell’archivio perde la scala manuale.");
        }
        File.WriteAllBytes(Path.Combine(directory, "diagramma_campiture_manuale.png"), Ui.Snapshot(bridge.Drawing));
        foreach (string invalid in new[] { "", "incompleto", "0", "-1", "NaN", "Infinity", "1001" })
        {
            await SetScale(invalid);
            Check(bridge.Drawing.ConcreteAmplification == 15.5 && ReferenceEquals(bridge.Calculation, result), "Scala non valida altera vista o calcolo.");
            Check(bridge.ConcreteScaleEditor.ToolTip?.ToString()?.Contains("ultima scala valida") == true, "Scala non valida priva di indicazione.");
        }
        await SetScale("15,5");
        bridge.StageChoice.SelectedIndex = 1; await Layout();
        Check(bridge.Drawing.ConcreteAmplification == 15.5, "Cambio situazione perde il fattore manuale.");
        bridge.ConcreteScaleAuto.IsChecked = true; await Layout();
        Check(Math.Abs(bridge.Drawing.ConcreteAmplification - n) < 1e-12, "Auto n dipende dalla situazione visualizzata anziché dalla fase variabile.");
        var phases = bridge.InputForms.Where(f => f.Editors.ContainsKey("Mx")).ToArray();
        var lastPhase = phases.Last(); var lastHomogenization = bridge.InputForms.Last(f => f.Editors.ContainsKey("n"));
        lastHomogenization.Set("n", "12"); await wait();
        Check(Math.Abs(bridge.Drawing.ConcreteAmplification - 12) < 1e-10, "Auto n non segue l’aggiornamento dell’omogeneizzazione.");
        await SetScale("8");
        lastHomogenization.Set("n", "13"); await wait();
        Check(bridge.Drawing.ConcreteAmplification == 8, "Ricalcolo sovrascrive la scala manuale.");
        bridge.ConcreteScaleAuto.IsChecked = true; await Layout();
        ((CheckBox)lastPhase.Editors["attiva"]).IsChecked = false; await wait();
        Check(bridge.Calculation!.Stages.Count == 2, "Prova della scala senza Q non ha disattivato la fase.");
        Check(Math.Abs(bridge.Drawing.ConcreteAmplification - bridge.Calculation!.Stages.Last().Contributions.Last().HomogenizationN) < 1e-10, "Auto n usa una fase disattivata.");
        ((CheckBox)phases[1].Editors["attiva"]).IsChecked = false; await wait();
        Check(bridge.Drawing.ConcreteAmplification == 1 && bridge.Calculation!.Stages.Count == 1, "Scala senza fasi composte deve essere unitaria.");
        ((CheckBox)phases[1].Editors["attiva"]).IsChecked = true;
        ((CheckBox)lastPhase.Editors["attiva"]).IsChecked = true; lastHomogenization.Set("phi", "0"); await wait();
        bridge.StageChoice.SelectedIndex = bridge.Calculation!.Stages.Count - 1;
        foreach (int mode in new[] { 0, 1 })
        {
            bridge.DisplayChoice.SelectedIndex = mode;
            foreach (var size in new[] { (1600d, 990d), (1366d, 768d) })
            {
                Width = size.Item1; Height = size.Item2; await Layout();
                var labels = bridge.Drawing.StressLabels;
                Check(labels.All(t => t.Bounds.Left >= 0 && t.Bounds.Top >= 0 && t.Bounds.Right <= bridge.Drawing.ActualWidth && t.Bounds.Bottom <= bridge.Drawing.ActualHeight), "Etichette delle tensioni fuori dal disegno.");
                for (int i = 0; i < labels.Count; i++) for (int j = 0; j < i; j++) Check(!labels[i].Bounds.IntersectsWith(labels[j].Bounds), "Etichette delle tensioni sovrapposte.");
                File.WriteAllBytes(Path.Combine(directory, $"tensioni_{mode}_{size.Item1:0}.png"), Ui.Snapshot(this));
            }
        }
        Width = 1600; Height = 990; bridge.DisplayChoice.SelectedIndex = 2; await Layout();
        Check(!bridge.ConcreteScaleEditor.IsVisible, "Controllo tensioni visibile in modalità geometrica.");
        bridge.Pages.SelectedIndex = 0; await Layout();
        Check(!bridge.ConcreteScaleEditor.IsVisible, "Controllo tensioni presente nel pannello geometrico.");
        var materialForm = bridge.InputForms.Single(f => f.Editors.ContainsKey("fy_override"));
        Check(((CheckBox)materialForm.Editors["fy_override"]).Content?.ToString() == "Sovrascrivi fy per tutta la carpenteria" &&
            materialForm.Editors["fy_override"].ToolTip?.ToString()?.Contains("Non modifica le armature") == true, "Dicitura o tooltip fy poco chiari.");
        // Exercise the renderer with exact stress fields, independently of the solver.
        IEnumerable<GeometryDrawing> Areas(Drawing drawing)
        {
            if (drawing is GeometryDrawing area) yield return area;
            if (drawing is DrawingGroup group)
                foreach (Drawing child in group.Children) foreach (var item in Areas(child)) yield return item;
        }
        foreach (var sample in new (string Name, double Stress, double Slope, bool Blue, bool Red)[] { ("nulle", 0d, 0d, false, false),
            ("compressione", -10d, 0d, true, false), ("trazione", 10d, 0d, false, true), ("cambio_segno", 0d, .1, true, true) })
        {
            var source = result.Stages.Last();
            var c = source.Contributions.Last() with { UniformStress = sample.Stress, StressSlope = sample.Slope, Centroid = result.Geometry.SlabHeight / 2 };
            var points = source.Points.Select(p => p with { Stress = c.Stress(p.Material, p.Y), Contributions = [c.Stress(p.Material, p.Y)], Active = true }).ToList();
            var stage = source with { Contributions = [c], Points = points };
            var drawing = new BridgeDrawing { Geometry = result.Geometry, Stage = stage, ConcreteAmplification = n, Width = 1200, Height = 640 };
            drawing.Measure(new Size(1200, 640)); drawing.Arrange(new Rect(0, 0, 1200, 640)); drawing.UpdateLayout();
            File.WriteAllBytes(Path.Combine(directory, "diagramma_" + sample.Name + ".png"), Ui.Snapshot(drawing));
            var colors = Areas(VisualTreeHelper.GetDrawing(drawing)!).Where(p => p.Geometry is StreamGeometry && p.Brush is SolidColorBrush)
                .Select(p => ((SolidColorBrush)p.Brush).Color.ToString()).ToArray();
            Check(colors.Contains("#FF226293") == sample.Blue && colors.Contains("#FFB44D43") == sample.Red, "Campiture con segno errato: " + sample.Name);
        }
        settings.Remove("amplificazione_cls");
        bridge.Pages.SelectedIndex = page; bridge.DisplayChoice.SelectedIndex = view; bridge.StageChoice.SelectedIndex = stageIndex; await Layout();
        File.WriteAllText(Path.Combine(directory, "tensioni-grafiche-smoke.txt"), $"OK: {count} controlli su scala CLS automatica/manuale, input non validi, invariabilità dei risultati, etichette reali e leggibili, navigazione e nota fy.");
    }
}
