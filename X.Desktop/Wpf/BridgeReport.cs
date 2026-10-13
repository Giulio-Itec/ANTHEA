using Anthea.Reports;
using System.Windows;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    internal byte[] BuildReport(string title, HashSet<string> options, bool projectReport = false) => DocxWriter.Write(BuildReportDocument(title, options, projectReport));
    internal DocxDocument BuildReportDocument(string title, HashSet<string> options, bool projectReport = false)
    {
        Commit();
        if (Busy || Calculation is not { } result) throw new InvalidOperationException("Attendere l’aggiornamento automatico e correggere i dati non validi prima di esportare il report.");
        if (!projectReport) { viewSettings["report_sezioni"] = J.Node(options.OrderBy(k => k).ToArray()); Modified?.Invoke(); }
        var images = ModuleFigureRenderer.BridgeFigures(result, viewSettings, options).Select(f => f.Image()).ToList();
        // ProjectReportPlan does not consolidate the bridge's flat inputs. Preserve them in its chapter.
        return ReportBridge.CreateDocument(title, result, options, images);
    }
}
