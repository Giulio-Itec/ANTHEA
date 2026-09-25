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
        void Image(string caption, string category, BridgeStage? stage, int mode)
        {
            var drawing = new BridgeDrawing { Geometry = result.Geometry, Stage = stage, Mode = mode, Width = 1200, Height = 640 };
            drawing.Measure(new Size(1200, 640)); drawing.Arrange(new Rect(0, 0, 1200, 640)); drawing.UpdateLayout();
            images.Add(new(caption, Ui.Snapshot(drawing), category));
        }
        if (options.Contains("grafici"))
        {
            if (options.Contains("geometria")) Image("Geometria della sezione e disposizione delle armature", "geometria", null, 2);
            if (options.Overlaps(["tensioni", "classe4", "omogeneizzazione"]))
                for (int i = 0; i < result.Stages.Count; i++)
                    Image($"Situazione {i + 1} dopo {result.Stages[i].Name}. Contributi numerati e somma in blu; arancio indica la parte inefficace.", "fase_" + i, result.Stages[i], 1);
        }
        // ProjectReportPlan does not consolidate the bridge's flat inputs. Preserve them in its chapter.
        return ReportBridge.Create(title, result, options, images);
    }
}
