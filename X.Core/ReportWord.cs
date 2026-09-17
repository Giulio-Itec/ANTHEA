using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace X.Core;

public sealed record ImmagineReport(string Titolo,byte[] Png,string Categoria);
/// <summary>Pacchetto DOCX Open XML nativo, senza automazione Office. Nessun ricalcolo nei report.</summary>
public static class ReportWord
{
    private static readonly XNamespace W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public static readonly (string Key,string Label)[] Sezioni=[("generali","Dati generali"),("efficienza","Efficienza del gruppo"),("coefficienti","Coefficienti normativi"),("stratigrafia","Tabelle stratigrafiche"),("nq","Metodo Nq / abachi"),("risultati","Risultati e verifiche"),("criteri","Criteri di calcolo"),("dettagli","Dettagli ogni 0,50 m"),("grafico_profilo","Profilo stratigrafico"),("grafico_nq","Curve Nq / abachi"),("grafico_capacita","Capacità portante")];
    public static void Esporta(string path,string title,string module,JsonObject data,JsonObject result,HashSet<string>? options=null,IReadOnlyList<ImmagineReport>? images=null)
    {
        if(result.S("errore")!="")throw new ArgumentException("Calcolo non disponibile: "+result.S("errore"));
        if(module=="str_palo")throw new ArgumentException("Il report Word della sezione non è disponibile, come nel programma sorgente. Esportare i risultati JSON.");
        options??=Sezioni.Where(s=>!s.Key.StartsWith("grafico_")).Select(s=>s.Key).ToHashSet();if(options.Count==0)throw new ArgumentException("Selezionare almeno un contenuto per il report.");
        bool micro=module=="geo_micropalo_verticale";var body=new XElement(W+"body");
        XElement P(string text,bool bold=false)=>new(W+"p",new XElement(W+"pPr",new XElement(W+"spacing",new XAttribute(W+"after",bold?160:80))),new XElement(W+"r",new XElement(W+"rPr",new XElement(W+"rFonts",new XAttribute(W+"ascii","Calibri"),new XAttribute(W+"hAnsi","Calibri")),new XElement(W+"sz",new XAttribute(W+"val",bold?25:19)),bold?new XElement(W+"b"):null),new XElement(W+"t",new XAttribute(XNamespace.Xml+"space","preserve"),text)));
        void Table(Tabella table)
        {
            body.Add(P(table.Titolo,true));var t=new XElement(W+"tbl");int width=9360/table.Colonne.Length;
            t.Add(new XElement(W+"tblPr",new XElement(W+"tblW",new XAttribute(W+"w",9360),new XAttribute(W+"type","dxa")),new XElement(W+"tblBorders",new[]{"top","left","bottom","right","insideH","insideV"}.Select(k=>new XElement(W+k,new XAttribute(W+"val","single"),new XAttribute(W+"sz",4),new XAttribute(W+"color","CBD5E1"))))));
            t.Add(new XElement(W+"tblGrid",table.Colonne.Select(_=>new XElement(W+"gridCol",new XAttribute(W+"w",width)))));
            foreach(var (cells,index) in new[]{table.Colonne}.Concat(table.Righe).Select((row,i)=>(row,i)))
            {
                var row=new XElement(W+"tr",index==0?new XElement(W+"trPr",new XElement(W+"tblHeader")):null);
                foreach(var text in cells)row.Add(new XElement(W+"tc",new XElement(W+"tcPr",new XElement(W+"tcW",new XAttribute(W+"w",width),new XAttribute(W+"type","dxa")),index==0?new XElement(W+"shd",new XAttribute(W+"fill","E8EFF7")):null),P(text,index==0)));
                t.Add(row);
            }
            body.Add(t);body.Add(P(""));
        }
        body.Add(P("ANTHEA — "+title,true));body.Add(P((micro?"Micropalo — Bustamante–Doix":"Palo — capacità portante")+" · "+DateTime.Now.ToString("dd/MM/yyyy HH:mm")));
        if(options.Contains("generali"))Table(Tabelle.Parametri("Dati generali — m, kN, kPa, gradi; p_i in MPa",data["generali"]!.AsObject(),"generali"));
        if(options.Contains("efficienza")){Table(Tabelle.Parametri("Efficienza — dati",data["efficienza"] as JsonObject??new(),"efficienza"));Table(Tabelle.Parametri("Efficienza — risultati",result["efficienza"]!.AsObject(),"efficienza"));}
        if(options.Contains("coefficienti"))
        {
            var g=data["generali"]!;var (xi3,xi4)=Calcolo.Verticali[g.S("verticali_indagate","1")];
            Table(Tabelle.Parametri("Coefficienti applicati",J.Obj(("ξ3",xi3),("ξ4",xi4),("γs,c",g.D("sicurezza_laterale_compressione",1.15)),("γs,t",g.D("sicurezza_laterale_trazione",1.25)),("γb",g.D("sicurezza_base",1.35)),("γG sfavorevole",g.D("peso_palo_sfavorevole",1.3)),("γG favorevole",g.D("peso_palo_favorevole",1))),"coefficienti"));
        }
        if(options.Contains("stratigrafia"))for(int i=0;i<data.Array("stratigrafie").Count;i++)for(int j=0;j<data.Array("stratigrafie")[i]!.AsArray().Count;j++)Table(Tabelle.Parametri($"Sondaggio {i+1} — strato {j+1}",data.Array("stratigrafie")[i]![j]!.AsObject(),"stratigrafia"));
        if(options.Contains("nq")){body.Add(P("Metodo",true));body.Add(P(micro?BustamanteDoix.Fonte+". p_l = p_i; α maggiora il diametro. Interpolazione lineare degli abachi, senza estrapolazione.":Nq.Descrizione));}
        if(options.Contains("criteri"))
        {
            body.Add(P("Criteri e unità",true));body.Add(P("Lunghezze m; forze kN; tensioni e aderenze kPa; pesi specifici kN/m³. I valori arrotondati sono solo di presentazione. I minimi dei contributi laterale e di punta sono valutati separatamente."));
            body.Add(P("Rd,c = ηc min[Rb,media/(ξ3 γb) + Rs,media/(ξ3 γs,c); Rb,min/(ξ4 γb) + Rs,min/(ξ4 γs,c)]. Rd,t = ηt min[Rs,media/(ξ3 γs,t); Rs,min/(ξ4 γs,t)]."));
            body.Add(P(micro?"Rs = Σπ (Db α) Δs s_aderenza. Coordinate lungo asse s; z=s cosθ. Il peso proprio è proiettato sull'asse. Rb è la percentuale selezionata di Rs.":"Rd drenata: Rb,calc = Ab σ′v Nq; Rs = ΣπD Δz (c′ + k μ σ′media). Non drenata coesiva sotto falda: τ=α Cu e Rb,calc = Ab (Nc Cu + σv,L totale), formulazione lorda richiesta il 16/09/2026. Sopra falda si usa il ramo drenato. La sottospinta agisce separatamente sul peso nelle azioni."));
        }
        if(options.Contains("risultati"))
        {
            body.Add(P("Avvisi",true));foreach(var warning in result.Array("avvisi"))body.Add(P(warning!.ToString()));if(!result.B("copertura_completa"))body.Add(P("ATTENZIONE: la stratigrafia non copre tutta la lunghezza richiesta; risultati limitati alla quota disponibile."));
            var rows=new List<string[]>();foreach(var (name,c) in result["curve"]!.AsObject())
            {
                var last=c!.Array("progetto")[^1]!;var action=result["azioni"]!.Array(name.Contains("trazione")?"trazione":"compressione").LastOrDefault();double rd=last[1]!.GetValue<double>();double? ed=J.Number(action?[1]);
                rows.Add([name.Replace('_',' '),Tabelle.F(last[0]),Tabelle.F(rd),ed.HasValue?Tabelle.F(ed.Value):"—",ed.HasValue?(ed<=rd?"Verificato alla quota disponibile":"Non verificato"):"Azione non inserita"]);
            }
            Table(new("Resistenze di progetto",[micro?"Verifica (asse s)":"Verifica","Quota [m]","Rd [kN]","Ed [kN]","Esito"],rows,"risultati"));
        }
        if(options.Contains("dettagli")){body.Add(P("Dettagli ogni 0,50 m e quota finale",true));foreach(var t in Tabelle.Crea(result,micro))Table(t);}
        var selected=(images??[]).Where(i=>options.Contains(i.Categoria)).ToArray();
        XNamespace wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing",a="http://schemas.openxmlformats.org/drawingml/2006/main",pic="http://schemas.openxmlformats.org/drawingml/2006/picture";
        for(int i=0;i<selected.Length;i++)
        {
            body.Add(P(selected[i].Titolo,true));long cx=5800000,cy=3600000;
            body.Add(new XElement(W+"p",new XElement(W+"r",new XElement(W+"drawing",new XElement(wp+"inline",new XElement(wp+"extent",new XAttribute("cx",cx),new XAttribute("cy",cy)),new XElement(wp+"docPr",new XAttribute("id",i+1),new XAttribute("name",selected[i].Titolo)),new XElement(a+"graphic",new XElement(a+"graphicData",new XAttribute("uri",pic.NamespaceName),new XElement(pic+"pic",new XElement(pic+"nvPicPr",new XElement(pic+"cNvPr",new XAttribute("id",0),new XAttribute("name","image.png")),new XElement(pic+"cNvPicPr")),new XElement(pic+"blipFill",new XElement(a+"blip",new XAttribute(R+"embed","img"+i)),new XElement(a+"stretch",new XElement(a+"fillRect"))),new XElement(pic+"spPr",new XElement(a+"xfrm",new XElement(a+"off",new XAttribute("x",0),new XAttribute("y",0)),new XElement(a+"ext",new XAttribute("cx",cx),new XAttribute("cy",cy))),new XElement(a+"prstGeom",new XAttribute("prst","rect"),new XElement(a+"avLst")))))))))));
        }
        body.Add(new XElement(W+"sectPr",new XElement(W+"pgSz",new XAttribute(W+"w",11906),new XAttribute(W+"h",16838)),new XElement(W+"pgMar",new XAttribute(W+"top",1134),new XAttribute(W+"bottom",1134),new XAttribute(W+"left",1273),new XAttribute(W+"right",1273))));
        using var memory=new MemoryStream();
        using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true))
        {
            void Entry(string name,string content){using var writer=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false));writer.Write(content);}
            Entry("[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            Entry("_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"doc\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            var relationships=new XElement(XName.Get("Relationships","http://schemas.openxmlformats.org/package/2006/relationships"));
            for(int i=0;i<selected.Length;i++){relationships.Add(new XElement(relationships.Name.Namespace+"Relationship",new XAttribute("Id","img"+i),new XAttribute("Type",R+"image"),new XAttribute("Target",$"media/image{i}.png")));using var stream=zip.CreateEntry($"word/media/image{i}.png").Open();stream.Write(selected[i].Png);}
            // Il tipo relazione è un URI, non il nome XML espanso.
            foreach(var rel in relationships.Elements())rel.SetAttributeValue("Type",R.NamespaceName+"/image");
            Entry("word/_rels/document.xml.rels",relationships.ToString());Entry("word/document.xml",new XDocument(new XDeclaration("1.0","utf-8","yes"),new XElement(W+"document",new XAttribute(XNamespace.Xmlns+"w",W),new XAttribute(XNamespace.Xmlns+"r",R),body)).ToString());
        }
        Archivio.ScriviAtomico(path,memory.ToArray());
    }
}
