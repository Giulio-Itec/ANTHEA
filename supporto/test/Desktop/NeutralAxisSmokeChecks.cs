using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GPC.Checkers.Concrete.Results;
using GPC.Geometry;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeNeutralAxis(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks.Add("OK: " + message); }
        // Known affine fields, including a translated reference: the oracle is epsilon(x,y)=0.
        foreach (var sample in new[] { ("orizzontale", 0d, -.00001, .001), ("verticale", .00001, 0d, .001),
            ("inclinato", .00002, -.00001, .001), ("passante_riferimento", .00002, -.00001, 0d) })
        {
            var plane = new StrainPlane(sample.Item2, sample.Item3, new Point2d(35, -27), sample.Item4);
            var axis = SectionNeutralAxis.Concrete(plane); var line = axis.Line!;
            Check(line is not null && Math.Abs(plane.GetStrain(line.Start)) < 1e-12 && Math.Abs(plane.GetStrain(line.End)) < 1e-12,
                "CLS: estremi su ε=0, " + sample.Item1);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var display = NeutralAxisOverlay.Draw(dc, axis, (x,y) => new Point(400+x, 300-y), new Rect(150,100,500,400), new Rect(20,60,760,480), 30);
                Check(display.Start.HasValue && display.End.HasValue && (display.Start.Value-display.End.Value).Length > 100, "Tracciamento asse " + sample.Item1);
                foreach (var point in new[] { display.Start!.Value, display.End!.Value })
                    Check(Math.Abs(plane.GetStrain(point.X-400, 300-point.Y)) < 1e-12, "Ritaglio conserva ε=0: " + sample.Item1);
            }
        }
        foreach (double strain in new[] { 0d, .001, -.001 })
            Check(SectionNeutralAxis.Concrete(new StrainPlane(0, 0, new Point2d(0,0), strain)).Line is null, "Campo uniforme: nessun asse inventato " + strain);
        using (var dc = new DrawingVisual().RenderOpen())
        {
            var outside = SectionNeutralAxis.Concrete(new StrainPlane(0, -.00001, new Point2d(0,0), .1));
            var display = NeutralAxisOverlay.Draw(dc, outside, (x,y) => new Point(400+x,300-y), new Rect(150,100,500,400), new Rect(20,60,760,480),30);
            Check(display.Start is null && display.Caption.Contains("esterno"), "Asse esterno segnalato senza falsa linea sulla sezione");
        }
        var data = Archivio.Documento("str_palo");
        var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        var engine = new CheckerSection(input, settings, settings["sle"]!["SLE"]!.AsObject());
        var stress = await Task.Run(() => engine.Stress(new(-100, 80, 35), "TEST"));
        var view = new ConcreteSectionViewport { Section = engine.Geometry, Stress = stress, Labels = false };
        File.WriteAllBytes(Path.Combine(directory,"cls_asse_inclinato.png"),view.Png(900,650));
        Check(view.NeutralAxisDisplay?.Start is not null, "CLS: asse sullo stato reale Checker con flessione deviata");
        view.ShowNeutralAxis = false; _ = view.Png();
        Check(view.NeutralAxisDisplay is null, "CLS: disattivazione elimina linea e legenda");
        view.ShowNeutralAxis = true; view.Stress = null; _ = view.Png();
        Check(view.NeutralAxisDisplay is null, "CLS: nessun asse residuo senza risultato");
        using (var concrete = new ConcreteWorkspace(data)) concrete.VerifyNeutralAxisControls(Check);

        var bridgeData = BridgeSection.Defaults(); bridgeData["classe4"] = false;
        foreach (string method in BridgeSection.CalculationMethods)
        {
            bridgeData["metodo_analisi"] = method;
            var result = await Task.Run(() => BridgeSection.Calculate(bridgeData));
            foreach (var stage in result.Stages)
            {
                var axis = SectionNeutralAxis.Bridge(stage);
                Check(axis.Line is not null, "Ponte: asse disponibile, " + method + " / " + stage.Name);
                var line = axis.Line!;
                double residual = stage.GetHistory() is { } h ? h.State.TotalPlane.At(line.Start.Y) : stage.Contributions.Sum(c => c.SteelStress(line.Start.Y));
                Check(Math.Abs(residual) < 1e-8, "Ponte: zero coerente con risultato della fase, " + method);
            }
            var drawing = new BridgeDrawing { Geometry = result.Geometry, Stage = result.Stages.Last(), Width = 1100, Height = 700 };
            drawing.Measure(new Size(1100,700)); drawing.Arrange(new Rect(0,0,1100,700)); drawing.UpdateLayout();
            File.WriteAllBytes(Path.Combine(directory,"ponte_" + Array.IndexOf(BridgeSection.CalculationMethods,method) + ".png"),Ui.Snapshot(drawing));
            Check(drawing.NeutralAxisDisplay?.Start is not null, "Ponte: asse visibile " + method);
            Check(drawing.NeutralAxisDisplay!.Caption.Contains(stageCaption(method)), "Ponte: legenda distingue tensione e deformazione");
            drawing.ShowNeutralAxis = false; drawing.InvalidateVisual(); _ = Ui.Snapshot(drawing);
            Check(drawing.NeutralAxisDisplay is null, "Ponte: asse disattivabile " + method);
            drawing.ShowNeutralAxis = true; drawing.Stage = null; drawing.InvalidateVisual(); _ = Ui.Snapshot(drawing);
            Check(drawing.NeutralAxisDisplay is null, "Ponte: nessun asse senza fase " + method);
        }
        static string stageCaption(string method) => method == BridgeSection.CalculationMethods[0] ? "σa" : "ε totale";
        bridgeData["metodo_analisi"] = BridgeSection.CalculationMethods[0];
        using var bridge = new BridgeWorkspace(bridgeData);
        var window = new Window { Content = bridge, Width = 1600, Height = 1000, Owner = this };
        try
        {
            window.Show(); bridge.Pages.SelectedIndex = 1; await bridge.CalculateAsync();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            Check(bridge.NeutralAxisToggle.IsChecked == true && bridge.Drawing.NeutralAxisDisplay?.Start is not null, "Ponte: asse attivo per impostazione iniziale");
            var result = bridge.Calculation;
            bridge.StageChoice.SelectedIndex = 0; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            Check(ReferenceEquals(bridge.Drawing.Stage, result!.Stages[0]), "Cambio fase aggiorna il risultato dell’asse");
            bridge.NeutralAxisToggle.IsChecked = false; bridge.NeutralAxisToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Check(ReferenceEquals(result, bridge.Calculation) && !bridge.ResultsAreStale && !bridge.Drawing.ShowNeutralAxis, "Comando grafico non invalida il calcolo");
            using var reopened = new BridgeWorkspace((JsonObject)bridge.Data.DeepClone());
            Check(reopened.NeutralAxisToggle.IsChecked == false, "Preferenza asse salvata alla riapertura");
            bridge.NeutralAxisToggle.IsChecked = true; bridge.NeutralAxisToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            bridge.Drawing.ContourSection = true; bridge.Drawing.ContourDiagram = true; bridge.Drawing.InvalidateVisual();
            bridge.DetachView();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); bridge.detachedWindow!.UpdateLayout();
            Check(bridge.Drawing.NeutralAxisDisplay?.Start is not null && bridge.NeutralAxisToggle.IsVisible, "Vista staccata: asse e comando disponibili");
            File.WriteAllBytes(Path.Combine(directory,"ponte_vista_staccata.png"),Ui.Snapshot(bridge.detachedWindow!));
            bridge.detachedWindow!.Close();
            bridge.Pages.SelectedIndex = 0;
            Check(bridge.NeutralAxisToggle.Visibility == Visibility.Collapsed && bridge.Drawing.Stage is null, "Pannello geometrico senza controlli tensionali");
        }
        finally { window.Close(); }
        File.WriteAllLines(Path.Combine(directory,"smoke.txt"), checks.Append($"OK: {checks.Count} controlli completati."));
    }
}

internal sealed partial class ConcreteWorkspace
{
    internal void VerifyNeutralAxisControls(Action<bool,string> check)
    {
        var options = new JsonObject(); var view = new ConcreteSectionViewport();
        var toggle = NeutralAxisToggle(view, options);
        check(toggle.IsChecked == true && view.ShowNeutralAxis, "CLS: opzione inizialmente attiva");
        toggle.IsChecked = false; toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        var reopened = new ConcreteSectionViewport(); _ = NeutralAxisToggle(reopened, options);
        check(!view.ShowNeutralAxis && !reopened.ShowNeutralAxis, "CLS: preferenza persistita");
    }
}
