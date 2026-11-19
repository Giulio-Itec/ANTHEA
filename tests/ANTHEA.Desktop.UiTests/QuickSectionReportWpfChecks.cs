using System.Text.Json.Nodes;
using System.IO;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.Reports;
using X.Core;

namespace X.Desktop;

internal static class QuickSectionReportWpfChecks
{
    internal static void Run(string directory, Action<bool, string> check)
    {
        var folder = Path.Combine(directory, "relazione-sezione-rapida"); Directory.CreateDirectory(folder);
        var data = ConcreteQuickSection.Create();
        data["input"]!["width_mm"] = "400"; data["input"]!["height_mm"] = "600";
        ConcreteQuickSection.WriteLayer(data, 0, "n", "4"); ConcreteQuickSection.WriteLayer(data, 0, "phi", "20");
        ConcreteQuickSection.WriteLayer(data, 1, "n", "4"); ConcreteQuickSection.WriteLayer(data, 1, "phi", "20");
        ConcreteQuickSection.ReplaceCombinations(data, [new("SLU positiva", -600, 180, 0), new("SLU negativa", -450, -140, 0),
            new("Rara", -400, 110, 0, "SLE"), new("Frequente", -300, 90, 0, "SLE_FREQ"), new("Quasi permanente", -250, 75, 0, "SLE_QP")]);
        ConcreteQuickSection.WriteCreep(data, "2"); ConcreteQuickSection.WriteServiceOption(data, ConcreteQuickSection.ExposureKey, "XC3");
        var analysis = ConcreteQuickReportAnalysis.Create(data);
        var renderer = new QuickSectionFigureRenderer();
        foreach (bool brief in new[] { false, true })
        {
            var options = brief ? new HashSet<string> { QuickSectionReport.Version, QuickSectionReport.Brief } : QuickSectionReport.Defaults();
            var images = renderer.Render(analysis, options);
            check(images.Any(i => i.Category == "sezione") && images.Any(i => i.Category == "dominio"), "Relazione sezione rapida: figure reali di sezione e dominio");
            // The stresses of the bars with the method n (19/11/2026): the figures of the stresses of the SLE combinations have the callouts of the sheet.
            if (!brief)
                check(images.Any(i => i.Category == "tensioni") && analysis.Results.Values.Where(r => r.Method == ConcreteQuickSection.Methods[2])
                    .All(r => ConcreteQuickSection.StressCalloutLabels(data, r.Preview.Geometry, r.ServiceState).Count == ConcreteQuickSection.Layers(data).Count),
                    "Relazione sezione rapida: figure delle tensioni, con la tensione della barra più sollecitata di ogni strato per il metodo n");
            foreach (var (image, i) in images.Select((im, i) => (im, i))) File.WriteAllBytes(Path.Combine(folder, (brief ? "breve" : "completa") + "-figura-" + i + ".png"), image.Png);
            byte[] word = DocxWriter.Write(QuickSectionReport.Create("Sezione rettangolare 40 × 60 cm", analysis.Report, options, images.Select(i => i.Image()).ToArray()));
            string path = Path.Combine(folder, brief ? "Sezione-rapida-breve.docx" : "Sezione-rapida-completa.docx");
            ReportFiles.Write(path, word, "Sezione rettangolare 40 × 60 cm", maximumPages: brief ? ReportFiles.ShortReportPages : null);
            check(File.Exists(path) && File.Exists(ReportFiles.PdfPath(path)), "Relazione sezione rapida: Word e PDF " + (brief ? "breve entro due pagine" : "completi"));
        }
        var document = DocumentService.CreateDocument(ConcreteQuickSection.Module); document["dati"] = data;
        DocumentService.Save(Path.Combine(folder, "Sezione-rapida-esempio.anthea"), document);
    }
}
