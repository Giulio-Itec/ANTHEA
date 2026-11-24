using System.Text.Json.Nodes;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Anthea.Presentation.Shell.Commands;
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
        ServiceFigures(data, folder, check);
        foreach (bool brief in new[] { false, true })
        {
            var options = brief ? new HashSet<string> { QuickSectionReport.Version, QuickSectionReport.Brief } : QuickSectionReport.Defaults();
            var images = renderer.Render(analysis, options);
            check(images.Any(i => i.Category == "sezione") && (!brief || images.Any(i => i.Category == "dominio")), "Relazione sezione rapida: figura della sezione, dominio nella sola breve");
            // The stresses of the bars with the method n (19/11/2026): the figures of the stresses of the SLE combinations have the callouts of the sheet.
            if (!brief)
                check(images.Any(i => i.Category.StartsWith("tensioni_sle:")) && !images.Any(i => i.Category is "dominio" or "tensioni" or "curvatura")
                    && analysis.Results.Values.Where(r => r.Method == ConcreteQuickSection.Methods[2])
                    .All(r => ConcreteQuickSection.StressCalloutLabels(data, r.Preview.Geometry, r.ServiceState).Count == ConcreteQuickSection.Layers(data).Count),
                    "Relazione sezione rapida: figure delle tensioni, con la tensione della barra più sollecitata di ogni strato per il metodo n");
            foreach (var (image, i) in images.Select((im, i) => (im, i))) File.WriteAllBytes(Path.Combine(folder, (brief ? "breve" : "completa") + "-figura-" + i + ".png"), image.Png);
            byte[] word = DocxWriter.Write(QuickSectionReport.Create("Sezione rettangolare 40 × 60 cm", analysis.Report, options, images.Select(i => i.Image()).ToArray()));
            string path = Path.Combine(folder, brief ? "Sezione-rapida-breve.docx" : "Sezione-rapida-completa.docx");
            ReportFiles.Write(path, word, "Sezione rettangolare 40 × 60 cm", maximumPages: brief ? ReportFiles.ShortReportPages : null);
            check(File.Exists(path) && File.Exists(ReportFiles.PdfPath(path)), "Relazione sezione rapida: Word e PDF " + (brief ? "breve entro due pagine" : "completi"));
        }
        var shearData = (JsonObject)data.DeepClone();
        ConcreteQuickShear.CopyCombinations(shearData);
        ConcreteQuickShear.WriteCombination(shearData, ConcreteQuickShear.Combinations(shearData)[0].S("id"), ConcreteQuickShear.ConditionField, ConcreteQuickShear.WithoutLinks);
        ConcreteQuickShear.Write(shearData, "ancoraggio", JsonValue.Create("Confermato"));
        ConcreteQuickShear.Write(shearData, ConcreteQuickShear.EffectiveLink, JsonValue.Create(false));
        ConcreteQuickShear.Write(shearData, ConcreteQuickShear.Diameter, JsonValue.Create("8"));
        ConcreteQuickShear.Write(shearData, ConcreteQuickShear.Spacing, JsonValue.Create("100"));
        ConcreteQuickShear.AddCombination(shearData);
        string shearId = ConcreteQuickShear.Selected(shearData)!;
        ConcreteQuickShear.WriteCombination(shearData, shearId, "nome", "Taglio indipendente");
        ConcreteQuickShear.WriteCombination(shearData, shearId, ConcreteQuickShear.ConditionField, ConcreteQuickShear.WithLinks);
        ConcreteQuickShear.WriteCombination(shearData, shearId, "Vx", "80");
        ConcreteQuickShear.WriteCombination(shearData, shearId, "Vy", "50");
        var shearAnalysis = ConcreteQuickReportAnalysis.Create(shearData);
        ReportMenu(shearData, folder, check);
        check(shearAnalysis.Report.Shear!.Rows.Count == 12 && shearAnalysis.Report.Shear.Rows.All(r => r.Ratio is not null && r.Passed is not null),
            "Taglio rapido: report diretto con geometria modificata, dodici risultati determinati sui due assi");
        foreach (bool brief in new[] { false, true })
        {
            var options = brief ? new HashSet<string> { QuickSectionReport.Version, QuickSectionReport.Brief } : QuickSectionReport.Defaults();
            var images = renderer.Render(shearAnalysis, options);
            byte[] word = DocxWriter.Write(QuickSectionReport.Create("Sezione con taglio indipendente", shearAnalysis.Report, options, images.Select(i => i.Image()).ToArray()));
            string path = Path.Combine(folder, brief ? "Sezione-taglio-breve.docx" : "Sezione-taglio-completa.docx");
            if (brief)
            {
                var preview = Anthea.Reports.Pdf.DocxPdf.Render(DocxWriter.Read(word));
                File.WriteAllBytes(Path.Combine(folder, "taglio-breve-anteprima.pdf"), preview.Bytes);
            }
            ReportFiles.Write(path, word, "Sezione con taglio indipendente", maximumPages: brief ? ReportFiles.ShortReportPages : null);
            check(File.Exists(ReportFiles.PdfPath(path)), "Taglio rapido: Word e PDF " + (brief ? "breve entro due pagine" : "completo") + " con combinazioni indipendenti");
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

    private static void ServiceFigures(JsonObject original, string folder, Action<bool, string> check)
    {
        var data = (JsonObject)original.DeepClone();
        ConcreteQuickSection.ReplaceCombinations(data, [new("Stesso nome", -250, 75, 0, "SLE_QP"),
            new("Stesso nome", -200, 25, 0, "SLE_QP"), new("Rara esclusa", -400, 110, 0, "SLE")]);
        var analysis = ConcreteQuickReportAnalysis.Create(data);
        var cases = analysis.Report.Cases.Where(c => c.State == "SLE_QP").ToArray();
        var options = new HashSet<string> { QuickSectionReport.Version, "risultati_SLE_QP" };
        var renderer = new QuickSectionFigureRenderer();
        var figures = renderer.Render(analysis, options);
        check(figures.Count == 2 && figures.Select(f => f.Category).SequenceEqual(cases.Select(c => QuickSectionReport.ServiceStressCategory(c.Id)))
            && !figures[0].Png.SequenceEqual(figures[1].Png),
            "SLE: figure proprie di entrambe le combinazioni omonime, anche non governanti, senza selezionare l’allegato");
        var document = QuickSectionReport.Create("SLE con nomi uguali", analysis.Report, options, figures.Select(f => f.Image()).ToArray());
        System.Xml.Linq.XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        System.Xml.Linq.XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        System.Xml.Linq.XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var body = document.GetXml("word/document.xml").Root!.Element(w + "body")!;
        var relationships = document.GetXml("word/_rels/document.xml.rels");
        var pictures = body.Descendants(a + "blip").Select(b =>
        {
            string id = (string)b.Attribute(r + "embed")!;
            string target = (string)relationships.Root!.Elements().Single(e => (string?)e.Attribute("Id") == id).Attribute("Target")!;
            return document.GetBytes("word/" + target);
        }).ToArray();
        check(pictures.Length == 2 && pictures.Zip(figures).All(p => p.First.SequenceEqual(p.Second.Png))
            && !body.Value.Contains("Allegato A") && !body.Value.Contains("Rara esclusa"),
            "SLE: immagini nel capitolo selezionato e associate per ID, senza casi esclusi o duplicati in allegato");
        File.WriteAllBytes(Path.Combine(folder, "SLE-combinazioni-omonime.docx"), DocxWriter.Write(document));
        var annexFigures = renderer.Render(analysis, new HashSet<string> { "dominio", "tensioni", "curvatura" });
        check(annexFigures.Count == 0, "Allegato rimosso: le vecchie opzioni non generano figure inutilizzate");
    }

    private static void ReportMenu(JsonObject data, string folder, Action<bool, string> check)
    {
        var menu = new ReportSectionsDialogViewModel(QuickSectionReport.Module, data);
        var window = ShellDraftChecks.Offscreen(new Window { Title = menu.Title, Content = menu, SizeToContent = SizeToContent.WidthAndHeight });
        try
        {
            window.Show(); window.UpdateLayout();
            var boxes = Ui.Descendants<CheckBox>(window).Where(c => c.DataContext is ReportSectionChoice).ToArray();
            check(boxes.Any(c => c.DataContext is ReportSectionChoice { Key: "materiali" })
                && boxes.Any(c => c.DataContext is ReportSectionChoice { Key: "taglio_armata" })
                && boxes.Any(c => c.DataContext is ReportSectionChoice { Key: "taglio_non_armata" }),
                "Menu relazione guida: caselle disponibili a tutti i livelli, incluse le due verifiche a taglio");
            void Capture(string name)
            {
                window.UpdateLayout();
                var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                image.Render(window);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var output = File.Create(Path.Combine(folder, name)); encoder.Save(output);
            }
            Capture("menu-relazione-guida-introduzione.png");
            var scroll = Ui.Descendants<ScrollViewer>(window).Single(s => s.Name == "ReportContents");
            scroll.ScrollToEnd(); window.UpdateLayout();
            Capture("menu-relazione-guida-verifiche.png");
            foreach (var chapter in menu.Sections) chapter.IsChecked = false;
            menu.Sections.Single(c => c.Key == "q_taglio").IsChecked = true;
            window.UpdateLayout();
            check(menu.CanExport && boxes.Where(c => c.DataContext is ReportSectionChoice { Key: "taglio_armata" or "taglio_non_armata" }).All(c => c.IsChecked == true)
                && boxes.Where(c => c.DataContext is ReportSectionChoice { Key: "riepilogo" }).All(c => c.IsChecked == false),
                "Menu relazione guida: selezione del taglio propagata ai figli senza selezionare il sommario della flessione");
        }
        finally { window.Close(); }
    }
}
