using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Anthea.Reports;

/// <summary>Pure document composition: all numbers, equations and references come from the GPC result.</summary>
public static class ClimateReport
{
    private const int TableWidthTwips=9600;
    public static DocxDocument Create(string title,JsonObject input,JsonObject result,IReadOnlyList<string[]> inputRows)
    {
        XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var body=new XElement(w+"body");var package=new DocxDocument();
        XElement P(string text,bool heading=false)=>new(w+"p",new XElement(w+"pPr",new XElement(w+"spacing",new XAttribute(w+"after",100)),heading?new XElement(w+"keepNext"):null),new XElement(w+"r",new XElement(w+"rPr",new XElement(w+"rFonts",new XAttribute(w+"ascii","Arial"),new XAttribute(w+"hAnsi","Arial")),new XElement(w+"sz",new XAttribute(w+"val",heading?25:18)),heading?new XElement(w+"b"):null),new XElement(w+"t",text)));
        string Text(JsonNode? n)=>n is JsonValue v&&v.TryGetValue<double>(out var d)?d.ToString("G10",CultureInfo.GetCultureInfo("it-IT")):n?.ToString()??"";
        void Table(string[] headers,IEnumerable<string[]> rows)
        {
            int width=TableWidthTwips/headers.Length;
            XElement Row(string[] cells,bool header=false)=>new(w+"tr",new XElement(w+"trPr",new XElement(w+"cantSplit"),header?new XElement(w+"tblHeader"):null),cells.Select(s=>new XElement(w+"tc",new XElement(w+"tcPr",new XElement(w+"tcW",new XAttribute(w+"w",width),new XAttribute(w+"type","dxa")),header?new XElement(w+"shd",new XAttribute(w+"fill","E8EEF4")):null),P(s))));
            body.Add(new XElement(w+"tbl",new XElement(w+"tblPr",new XElement(w+"tblW",new XAttribute(w+"w",9600),new XAttribute(w+"type","dxa")),new XElement(w+"tblBorders",new[]{"top","left","bottom","right","insideH","insideV"}.Select(s=>new XElement(w+s,new XAttribute(w+"val","single"),new XAttribute(w+"sz",4),new XAttribute(w+"color","BCC7D3"))))),new XElement(w+"tblGrid",headers.Select(_=>new XElement(w+"gridCol",new XAttribute(w+"w",width)))),Row(headers,true),rows.Select(row=>Row(row))),P(""));
        }
        void Values(JsonArray? values)
        {
            Table(["Grandezza / descrizione","Valore / unità","Formula / riferimento"],(values??[]).Select(v=>new[]{Text(v!["Symbol"])+" · "+Text(v["Description"]),Text(v["Value"])+" "+Text(v["Unit"]),Text(v["Formula"])+" · "+Text(v["Reference"])}));
        }
        body.Add(P(title,true),P("ANTHEA · Azioni"),P(Text(result["Standard"])),P(Text(result["Scope"])));
        ClimateFigures.Append(package,body,input);
        body.Add(P("Dati di ingresso",true));
        Table(["Parametro","Valore / unità"],inputRows);
        body.Add(P("Parametri e formule",true));Values(result["Parameters"]?.AsArray());
        foreach(var c in result["Cases"]?.AsArray()??[]){body.Add(P(Text(c!["Name"]),true),P(Text(c["Description"])));Values(c["Values"]?.AsArray());}
        body.Add(P("Ipotesi e campo di applicazione",true));foreach(var note in result["Notes"]?.AsArray()??[])body.Add(P(Text(note)));
        body.Add(new XElement(w+"sectPr",new XElement(w+"pgSz",new XAttribute(w+"w",11906),new XAttribute(w+"h",16838)),new XElement(w+"pgMar",new XAttribute(w+"top",1134),new XAttribute(w+"bottom",1134),new XAttribute(w+"left",1134),new XAttribute(w+"right",1134))));
        package.SetXml("[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"png\" ContentType=\"image/png\"/><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        package.SetXml("_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
        package.SetXml("word/document.xml",new XDocument(new XElement(w+"document",new XAttribute(XNamespace.Xmlns+"w",w),body)).ToString());return package;
    }
}
