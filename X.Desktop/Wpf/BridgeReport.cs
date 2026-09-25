using System.Windows;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    internal byte[] BuildReport(string title, HashSet<string> options, bool projectReport = false)
    {
        Commit();
        if (Busy || Calculation is not { } result) throw new InvalidOperationException("Attendere l’aggiornamento automatico e correggere i dati non validi prima di esportare il report.");
        if (!projectReport) { viewSettings["report_sezioni"] = J.Node(options.OrderBy(k => k).ToArray()); Modified?.Invoke(); }
        var images = new List<ImmagineReport>();
        var concreteScale = StressScale(result, viewSettings);
        void Image(string caption, string category, BridgeStage? stage, int mode)
        {
            var drawing = new BridgeDrawing { Geometry = result.Geometry, Stage = stage, Input = result.Input,
                LoadPoints = stage?.Contributions.Select((c, i) => (c, i)).Where(p => !p.c.IsShrinkage).Select(p => new BridgeLoadPoint(p.i + 1, p.c.Name, p.c.LoadY, p.c.N)).ToArray() ?? [],
                ShowGeometryLabels = mode == 2, ShowRebarLabels = mode == 2, ConcreteAmplification = concreteScale.Factor,
                ContourSection = viewSettings.B("contour_sezione"), ContourDiagram = viewSettings.B("contour_tensioni"), ShowStressLimits = viewSettings.B("limiti_tensioni"),
                Mode = mode, Width = 1200, Height = 640 };
            drawing.Measure(new Size(1200, 640)); drawing.Arrange(new Rect(0, 0, 1200, 640)); drawing.UpdateLayout();
            images.Add(new(caption, Ui.Snapshot(drawing), category));
        }
        if (options.Contains("grafici"))
        {
            if (options.Contains("taglio"))
                foreach (bool support in new[] { false, true })
                    if (result.Input.B(support ? "appoggio" : "irrigidimenti"))
                    {
                        var sketch = new BridgeDetailSketch { Input = result.Input, AtSupport = support, Width = 800 };
                        sketch.Measure(new Size(800, 165)); sketch.Arrange(new Rect(0, 0, 800, 165)); sketch.UpdateLayout();
                        images.Add(new(support ? "Appoggio e montante terminale schema locale" : "Irrigidimento intermedio e pannelli adiacenti schema locale", Ui.Snapshot(sketch), "dettagli"));
                    }
            if (options.Contains("geometria")) Image("Geometria della sezione e disposizione delle armature", "geometria", null, 2);
            if (options.Overlaps(["tensioni", "classe4", "omogeneizzazione", "taglio"]))
                for (int i = 0; i < result.Stages.Count; i++)
                    Image($"Situazione {i + 1} dopo {result.Stages[i].Name}. {(viewSettings.B("contour_tensioni") ? "Campiture della somma secondo η: verde/ambra entro limite, rosso oltre limite, viola CLS teso." : "Campiture della somma: blu compressione, rosso trazione;")} contributi tratteggiati. CLS amplificato ×{F(concreteScale.Factor)} ({concreteScale.Source}); valori indicati in MPa reali. Arancio: parte inefficace.", "fase_" + i, result.Stages[i], 1);
        }
        // ProjectReportPlan does not consolidate the bridge's flat inputs. Preserve them in its chapter.
        return ReportBridge.Create(title, result, options, images);
    }
}
