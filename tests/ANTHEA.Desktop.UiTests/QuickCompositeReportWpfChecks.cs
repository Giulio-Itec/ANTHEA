using System.IO;
using Anthea.Application.Bridge;
using Anthea.Application.Documents;
using Anthea.Reports;
using X.Core;

namespace X.Desktop;

internal static class QuickCompositeReportWpfChecks
{
    internal static void Run(string directory, Action<bool, string> check)
    {
        string folder = Path.Combine(directory, "relazione-composta-rapida"); Directory.CreateDirectory(folder);
        var data = BridgeQuickSection.Create();
        BridgeQuickSection.Replace(data, [new("SLU", 1500, 2000, 3000, 600, -100), new("Rara", 1500, 1600, 2000, 350, -100, "SLE RA"),
            new("QP", 1500, 1600, 800, 100, -100, "SLE QP")]);
        var analysis = BridgeQuickReportAnalysis.Create(data); var renderer = new QuickCompositeFigureRenderer();
        foreach (bool brief in new[] { false, true })
        {
            var options = brief ? new HashSet<string> { BridgeQuickReport.Version, BridgeQuickReport.Brief } : BridgeQuickReport.Defaults();
            var images = renderer.Render(analysis, options);
            check(images.Any(i => i.Category == "sezione") && (brief || images.Count(i => i.Category == "tensioni") == 3), "Composta rapida: figure reali di geometria e tensioni delle combinazioni");
            foreach (var (image, index) in images.Select((image, index) => (image, index))) File.WriteAllBytes(Path.Combine(folder, (brief ? "breve" : "completa") + "-figura-" + index + ".png"), image.Png);
            byte[] word = DocxWriter.Write(BridgeQuickReport.Create("Trave composta a I · tre stati limite", analysis.Report, options, images.Select(i => i.Image()).ToArray()));
            string path = Path.Combine(folder, brief ? "Composta-rapida-breve.docx" : "Composta-rapida-completa.docx");
            ReportFiles.Write(path, word, "Trave composta a I", maximumPages: brief ? ReportFiles.ShortReportPages : null);
            check(File.Exists(path) && File.Exists(ReportFiles.PdfPath(path)), "Composta rapida: Word e PDF " + (brief ? "breve entro due pagine" : "completi"));
        }
        var document = DocumentService.CreateDocument(BridgeQuickSection.Module); document["dati"] = data;
        DocumentService.Save(Path.Combine(folder, "Composta-rapida-esempio.anthea"), document);
        var negative=BridgeQuickSection.Create();
        BridgeQuickSection.Replace(negative,[new("SLU negativa",0,-300,-500,-200,50),new("Rara negativa",0,-180,-250,-120,30,"SLE RA"),new("QP negativa",0,-100,-120,-80,20,"SLE QP")]);
        var negativeAnalysis=BridgeQuickReportAnalysis.Create(negative);
        // Keep all checks and warnings of the negative-moment example in its complete report.
        var negativeOptions=BridgeQuickReport.Defaults();
        var negativeImages=renderer.Render(negativeAnalysis,negativeOptions);
        byte[] negativeWord=DocxWriter.Write(BridgeQuickReport.Create("Trave composta a I · momenti negativi",negativeAnalysis.Report,negativeOptions,negativeImages.Select(i=>i.Image()).ToArray()));
        string negativePath=Path.Combine(folder,"Composta-rapida-negativa.docx");
        ReportFiles.Write(negativePath,negativeWord,"Trave composta a I · momenti negativi");
        var negativeDocument=DocumentService.CreateDocument(BridgeQuickSection.Module);negativeDocument["dati"]=negative;
        DocumentService.Save(Path.Combine(folder,"Composta-rapida-negativa.anthea"),negativeDocument);
        check(File.Exists(ReportFiles.PdfPath(negativePath)) && negativeAnalysis.Report.Cases.Count==3,"Composta rapida: secondo esempio con momenti negativi, tre stati limite e Word/PDF");
    }
}
