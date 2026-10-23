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
        if (Busy || Calculation is not { } result) throw new InvalidOperationException(Anthea.Application.Presentation.BridgeInputPresentation.ReportUnavailable);
        if (!projectReport) { viewSettings["report_sezioni"] = J.Node(options.OrderBy(k => k).ToArray()); Modified?.Invoke(); }
        var images = ModuleFigureRenderer.BridgeFigures(result, viewSettings, options).Select(f => f.Image()).ToList();
        // ProjectReportPlan does not consolidate the bridge's flat inputs. Preserve them in its chapter.
        return result.Report(title, options, images);
    }
}
