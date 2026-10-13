using Anthea.Reports;
using System.Windows;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    internal byte[] BuildShortReport(string title)
    {
        Commit();
        if (Busy || !HasResults || calculationQueued) throw new InvalidOperationException("Attendere l’aggiornamento delle verifiche prima di esportare.");
        return ReportConcreteShort.Create(title, Data, Result!);
    }
    internal void ExportReport(string filename, string title, HashSet<string> options, bool projectReport = false)
        => Archivio.ScriviAtomico(filename, BuildReport(title, options, projectReport));
    internal byte[] BuildReport(string title, HashSet<string> options, bool projectReport = false) => DocxWriter.Write(BuildReportDocument(title, options, projectReport));
    internal DocxDocument BuildReportDocument(string title, HashSet<string> options, bool projectReport = false)
    {
        Commit();
        if (Busy || !HasResults || calculationQueued) throw new InvalidOperationException("Attendere l’aggiornamento automatico e correggere i dati non validi prima di esportare il report.");
        if (!projectReport) { settings["report_sezioni"] = J.Node(options.OrderBy(k => k).ToArray()); Modified?.Invoke(); }
        RefreshDetailing();var result = Result!;
        result["dati"] = Data.DeepClone();
        var domains = domainPanels.Select(panel =>
        {
            if (options.Contains("grafici") && options.Contains("dominio2d") && !panel.ThreeD && checker2D.ContainsKey(panel.Key)) RefreshDomainPanel(panel, renderHidden: true);
            return new ConcreteDomainGraphic(panel.ThreeD, panel.Options, panel.Grid.Items.OfType<JsonRow>().Select(r => r.Values).ToArray(),
                (panel.Grid.SelectedItem as JsonRow)?.Values.S("id"), checker3D.GetValueOrDefault(panel.Key), checker2D.GetValueOrDefault(panel.Key),
                domainResults.GetValueOrDefault(panel.Prefix + panel.Key), panel.Plot);
        }).ToArray();
        var stresses = stressPanels.ToDictionary(p => p.Key, p => new ConcreteStressGraphic(p.Value.View,
            stressResults.GetValueOrDefault(p.Key), actions[p.Key].Select(r => r.Values).ToArray()));
        var graphics = new ConcreteReportGraphics(Data, result, preview, domains, stresses, curvatureResult is null ? null : curvaturePlot);
        var images = ModuleFigureRenderer.ConcreteFigures(graphics, options).Select(f => f.Image()).ToList();
        return ReportConcrete.CreateDocument(title, Data, result, options, images, !projectReport);
    }
}
