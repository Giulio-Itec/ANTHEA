using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace Anthea.FoundationWorkspace;

/// <summary>Both editions share the same validated result text and contain no calculation logic.</summary>
public static class FoundationReport
{
    static string[] Lines(string title,JsonObject result)
    {
        if(result["modulo_id"]?.ToString()!=FoundationDocument.Module)throw new ArgumentException("Risultato non riconosciuto.");
        return [title,"ANTHEA · Relazione di calcolo",..FoundationPresentation.ReportLines(result)];
    }
    public static byte[] Word(string title,JsonObject result)
    {
        XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var body=new XElement(w+"body");
        var lines=Lines(title,result);
        for(int i=0;i<lines.Length;i++)
        {
            bool heading=i is 0 or 2 || lines[i] is "Coefficienti e contributi" or "Dati di ingresso";
            var p=new XElement(w+"p",new XElement(w+"pPr",new XElement(w+"spacing",new XAttribute(w+"after",heading?180:100)),heading?new XElement(w+"keepNext"):null));
            if(lines[i]=="Dati di ingresso")p.Element(w+"pPr")!.Add(new XElement(w+"pageBreakBefore"));
            p.Add(new XElement(w+"r",new XElement(w+"rPr",new XElement(w+"rFonts",new XAttribute(w+"ascii","Arial"),new XAttribute(w+"hAnsi","Arial")),new XElement(w+"sz",new XAttribute(w+"val",i==0?36:heading?26:21)),heading?new XElement(w+"b"):null),new XElement(w+"t",lines[i])));body.Add(p);
        }
        body.Add(new XElement(w+"sectPr",new XElement(w+"pgSz",new XAttribute(w+"w",11906),new XAttribute(w+"h",16838)),new XElement(w+"pgMar",new XAttribute(w+"top",1134),new XAttribute(w+"right",1134),new XAttribute(w+"bottom",1134),new XAttribute(w+"left",1134))));
        using var stream=new MemoryStream();
        using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
        {
            void Entry(string name,string text) {using var writer=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false));writer.Write(text);}
            Entry("[Content_Types].xml","<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            Entry("_rels/.rels","<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Entry("word/document.xml",new XDocument(new XElement(w+"document",new XAttribute(XNamespace.Xmlns+"w",w),body)).ToString());
        }
        return stream.ToArray();
    }
    public static byte[] Pdf(string title,JsonObject result)
    {
        // On Windows, reuse the installed fonts (or the application's existing resolver).
        GlobalFontSettings.UseWindowsFontsUnderWindows=true;
        var doc=new Document();doc.Info.Title=title;doc.Info.Author="ANTHEA";
        var normal=doc.Styles[StyleNames.Normal]!;normal.Font.Name="Arial";normal.Font.Size=10.5;normal.ParagraphFormat.SpaceAfter=Unit.FromPoint(6);
        var section=doc.AddSection();section.PageSetup.PageFormat=PageFormat.A4;section.PageSetup.TopMargin=section.PageSetup.BottomMargin=section.PageSetup.LeftMargin=section.PageSetup.RightMargin=Unit.FromCentimeter(2);
        var lines=Lines(title,result);
        for(int i=0;i<lines.Length;i++)
        {
            var p=section.AddParagraph(lines[i]);
            if(lines[i]=="Dati di ingresso")p.Format.PageBreakBefore=true;
            if(i is 0 or 2 || lines[i] is "Coefficienti e contributi" or "Dati di ingresso") {p.Format.Font.Size=i==0?18:13;p.Format.Font.Bold=true;p.Format.KeepWithNext=true;p.Format.SpaceBefore=Unit.FromPoint(8);}
        }
        var footer=section.Footers.Primary.AddParagraph("ANTHEA · ");footer.AddPageField();footer.Format.Font.Size=8;
        var renderer=new PdfDocumentRenderer { Document=doc };renderer.RenderDocument();using var output=new MemoryStream();renderer.PdfDocument.Save(output,false);return output.ToArray();
    }
}
