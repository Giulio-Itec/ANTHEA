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
        QuickCompositeReportWpfChecks.Run(directory, check);
        var dxfData=ConcreteDxfWpfChecks.Data();
        dxfData["input"]!["inner_width_mm"]="123"; dxfData["input"]!["inner_height_mm"]="321";
        var dxfAnalysis=ConcreteQuickReportAnalysis.Create(dxfData);
        var dimensioned=new ConcreteSectionViewport {Section=dxfAnalysis.Results.Values.First().Preview.Geometry,Dimensions=true,CoverDimensions=true};
        _=dimensioned.ReportPng();
        check(dimensioned.DimensionLabels.Any(s=>s.StartsWith("b =")) && dimensioned.DimensionLabels.All(s=>!s.StartsWith("c =") && !s.StartsWith("bi ") && !s.StartsWith("hi ")),"DXF: quote di ingombro presenti, copriferro e foro parametrico assenti anche se richiesti");
        string dxfFolder=Path.Combine(directory,"relazione-dxf"); Directory.CreateDirectory(dxfFolder);
        foreach(bool brief in new[]{false,true})
        {
            var options=brief ? new HashSet<string>{QuickSectionReport.Version,QuickSectionReport.Brief} : QuickSectionReport.Defaults();
            var images=new QuickSectionFigureRenderer().Render(dxfAnalysis,options);
            string name=brief ? "DXF-breve" : "DXF-completa";
            foreach(var (im,i) in images.Select((im,i)=>(im,i))) File.WriteAllBytes(Path.Combine(dxfFolder,name+"-"+i+".png"),im.Png);
            byte[] word=DocxWriter.Write(QuickSectionReport.Create("Sezione forata da DXF",dxfAnalysis.Report,options,images.Select(i=>i.Image()).ToArray()));
            if(brief)
            {
                using var zip=new System.IO.Compression.ZipArchive(new MemoryStream(word));
                using var xml=zip.GetEntry("word/document.xml")!.Open();
                check(System.Xml.Linq.XDocument.Load(xml).Root!.Value.Contains("staffe non importate dal DXF"),"DXF: relazione breve dichiara le staffe non importate");
            }
            string file=Path.Combine(dxfFolder,name+".docx");
            ReportFiles.Write(file,word,"Sezione forata da DXF",maximumPages:brief ? ReportFiles.ShortReportPages : null);
            check(File.Exists(ReportFiles.PdfPath(file)) && images.Any(i=>i.Category=="sezione"),"DXF: relazione Word/PDF e figure reali "+name);
        }
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
        // 19/11/2026: a circular section with the second ring and its link, as the figure of the report draws it.
        var pile = ConcreteQuickSection.Create(); var pileInput = pile["input"]!.AsObject();
        pileInput["shape"] = ConcreteQuickSection.Circular; pileInput["diameter_mm"] = "1000"; pileInput["longitudinal_bar_count"] = "16"; pileInput["longitudinal_bar_diameter_mm"] = "24";
        pileInput["second_inner_enabled"] = true; pileInput["second_inner_count"] = "10"; pileInput["second_inner_diameter"] = "20";
        ConcreteQuickSection.View(pile)[ConcreteQuickSection.SecondRingLinkKey] = "12"; ConcreteQuickSection.ApplyLayers(pile);
        var pileFigure = renderer.Render(ConcreteQuickReportAnalysis.Create(pile), new HashSet<string> { QuickSectionReport.Version, "sezione" }).Single(f => f.Category == "sezione");
        File.WriteAllBytes(Path.Combine(folder, "circolare-secondo-anello.png"), pileFigure.Png);
        check(pile["input"].S("second_inner_gap") == "12" && pileFigure.Png.Length > 0, "Relazione sezione rapida: sezione circolare con la staffa del secondo anello, s = 12 mm");
        var document = DocumentService.CreateDocument(ConcreteQuickSection.Module); document["dati"] = data;
        DocumentService.Save(Path.Combine(folder, "Sezione-rapida-esempio.anthea"), document);
    }
}
