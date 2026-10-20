using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.Reports;
using X.Core;

internal static class ConcreteStadiumReportChecks
{
    internal static int Run(string output, Action<string, DocxDocument> compare)
    {
        var document = DocumentService.CreateDocument("str_palo");
        var data = document["dati"]!.AsObject();
        ConcreteSheetPreparation.PrepareReport(data);
        var input = data["input"]!.AsObject();
        var draft = ConcreteTemplateWizard.Draft(input);
        ConcreteTemplateWizard.Select(draft, ConcreteTemplateWizard.Choices[7]);
        foreach (var (key, value) in new[] {
            ("width_mm", "5000"), ("height_mm", "2500"), ("cover_mm", "50"),
            ("transverse_bar_diameter_mm", "12"), ("transverse_spacing_mm", "150"),
            ("wizard_stadium_curve_count", "17"), ("wizard_stadium_curve_diameter_mm", "32"),
            ("wizard_stadium_straight_count", "8"), ("wizard_stadium_straight_diameter_mm", "25") })
            draft[key] = value;
        ConcreteTemplateWizard.Apply(data, draft);
        var result = J.Obj(("motore", "GPCChecker.Concrete.dll"), ("normativa_riferimento", "NTC 2018"),
            ("dati", data.DeepClone()));
        var report = ReportConcrete.CreateDocument("Capsula 5000 × 2500", data, result, ["geometria"]);
        string text = report.GetXml("word/document.xml").Root!.Value;
        if (!text.Contains("Barre per arco, tangenti incluse") || !text.Contains("Barre interne per rettilineo") ||
            !text.Contains("Ø barre curve [mm]") || !text.Contains("Ø barre rettilinei [mm]"))
            throw new InvalidOperationException("Capsula: ricetta dei due tratti assente dalla relazione.");
        string target = Path.Combine(output, "capsula-5000x2500-geometria.docx");
        File.WriteAllBytes(target, DocxWriter.Write(report));
        compare("capsula-5000x2500-geometria", report);
        return 1;
    }
}
