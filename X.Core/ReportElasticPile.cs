using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
namespace X.Core;
public static class ReportElasticPile
{
    public static byte[] Create(string title,JsonObject result)
    {
        if(result.S("tipo_risultato")!="palo_elastico"||result["risposta"] is not JsonObject r)throw new ArgumentException("Risposta elastica non disponibile.");
        XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";var body=new XElement(w+"body");
        XElement Paragraph(string s)=>new(w+"p",new XElement(w+"r",new XElement(w+"rPr",new XElement(w+"sz",new XAttribute(w+"val",19))),new XElement(w+"t",s)));
        void P(string s)=>body.Add(Paragraph(s));
        void Table(string[] headers,IEnumerable<string[]> rows)
        {
            if(body.Elements().LastOrDefault()?.Name==w+"tbl")P("");
            int width=9360/headers.Length;
            var table=new XElement(w+"tbl",new XElement(w+"tblPr",new XElement(w+"tblW",new XAttribute(w+"w",9360),new XAttribute(w+"type","dxa")),new XElement(w+"tblLayout",new XAttribute(w+"type","fixed")),new XElement(w+"tblBorders",new[]{"top","left","bottom","right","insideH","insideV"}.Select(side=>new XElement(w+side,new XAttribute(w+"val","single"),new XAttribute(w+"sz",4),new XAttribute(w+"color","C9D2DB"))))),new XElement(w+"tblGrid",headers.Select(_=>new XElement(w+"gridCol",new XAttribute(w+"w",width)))));
            foreach(var (row,i) in new[]{headers}.Concat(rows).Select((row,i)=>(row,i)))table.Add(new XElement(w+"tr",new XElement(w+"trPr",new XElement(w+"cantSplit"),i==0?new XElement(w+"tblHeader"):null),row.Select(c=>new XElement(w+"tc",new XElement(w+"tcPr",new XElement(w+"tcW",new XAttribute(w+"w",width),new XAttribute(w+"type","dxa")),i==0?new XElement(w+"shd",new XAttribute(w+"fill","E8EFF5")):null),Paragraph(c)))));body.Add(table);
        }
        string F(JsonNode? p,string key)=>J.Number(p?[key])?.ToString("G7")??p?[key]?.ToString()??"";
        string Side(JsonNode? p)=>p.S("Side") switch {"Above"=>"sup.","Below"=>"inf.","Interior"=>"int.",var side=>side};
        string C(JsonNode? p,string key)
        {
            var n=J.Number(p?[key]);if(n is null)return p?[key]?.ToString()??"";
            double a=Math.Abs(n.Value);return n.Value.ToString(a>0&&(a<.01||a>=100000)?"0.###E+0":"G4");
        }
        P(title+" · Risposta elastica del palo sotto il carico assegnato");P(result.S("segni"));P(result.S("limiti"));P(result.S("unita"));P(result.S("fonte"));P(result.S("raccordo"));
        var d=result["input"]!;
        P($"Sforzo normale positivo a compressione: N(x)=Ntesta+w x. Ntesta={F(d,"N")} kN; w={F(d,"peso_lineare")} kN/m. Nessun trasferimento assiale al terreno, galleggiamento automatico o effetto del secondo ordine. Il peso del c.a. include già l'armatura; nel CHS acciaio e iniezione occupano aree disgiunte.");
        if(result["dati_condivisi"] is JsonObject common)P($"Dati comuni: stratigrafia {F(common,"stratigrafia")}; geometria {common.S("geometria")}; sezione {common.S("sezione")}; e dal piano campagna {F(common,"eccentricita_da_pc")} m. {common.S("convenzione_carico")}. {common.S("migrazione")}");
        if(r["Section"] is JsonObject section)P($"EJ da sezione: E={F(section,"ModulusMpa")} MPa; J={F(section,"InertiaMm4")} mm⁴; EJ base={F(section,"BaseEI")}, adottato={F(section,"EI")} kNm². {section.S("Material")}. {section.S("Assumption")}. Override: {section.S("OverrideReason")}");
        Table(["Parametro","Valore / unità"],new[]{("diametro","D [m]"),("lunghezza","L totale [m]"),("libero","L libero [m]"),("EI","EI [kNm²]"),("fonte_EI","Origine EI"),("H","H [kN]"),("C","C [kNm]"),("e","e [m]; alternativo a C"),("vincolo","Rotazione testa"),("punta","Vincolo punta"),("passo","Passo iniziale [m]")}.Where(p=>p.Item1!="e"||result["dati_condivisi"] is null).Select(p=>new[]{p.Item2,F(d,p.Item1)}));
        Table(["Strato","Spessore [m]","Modalità","Condizioni dichiarate"],d.Array("strati").Select(p=>new[]{p.S("nome"),F(p,"spessore"),p.S("legge"),p.S("fonte")}));P("kh e nh [kN/m³]; k distribuito [kN/m²]. Valori adottati riportati di seguito per ciascun tratto. z misurata dal piano campagna per tutti gli strati. Matrice di fondazione integrata: nessuna molla nodale indipendente.");
        P(d.B("falda")?$"Falda a z={F(d,"z_falda")} m dal piano campagna; γw={F(d,"gamma_w")} kN/m³.":"Falda non presente nel modello.");
        P("Determinazione dei parametri del terreno per tratto");
        foreach(var p in result.Array("parametri_terreno"))
        {
            P($"Strato {F(p,"Layer")} · {p.S("Name")} · z={F(p,"Top")}–{F(p,"Bottom")} m · {ElasticHorizontalPile.MethodLabel(p.S("Method"))} · {ElasticHorizontalPile.ParameterLabel(p.S("Law"))}: valore di base {F(p,"BaseValue")}, adottato {F(p,"AdoptedValue")} {p.S("Unit")}.");
            P(p.S("Source"));P(p.S("Applicability"));P("Condizioni dichiarate: "+p.S("Conditions"));
            if(p!["TableNhNPerCm3"] is not null)P($"nh tabellato/scelto = {F(p,"TableNhNPerCm3")} N/cm³."+(p["TableMinimumNPerCm3"] is not null?$" Intervallo riga [{F(p,"TableMinimumNPerCm3")}; {F(p,"TableMaximumNPerCm3")}].":"")+" Conversione: 1 N/cm³ = 1000 kN/m³.");
            if(p["A"] is not null)P($"A adottato {F(p,"A")}; intervallo {F(p,"MinimumA")}–{F(p,"MaximumA")}; consigliato {F(p,"RecommendedA")}. γ o γ′ adottato {F(p,"AdoptedUnitWeight")} kN/m³; nh=Aγ/1,35.");
            if(p["InitialMean"] is not null)P($"Media iniziale del software {F(p,"InitialMean")} {p.S("InitialMeanUnit")}; origine valore adottato: {p.S("SelectionOrigin")}. La media non è una raccomandazione attribuita al libro.");
            if(p.B("Overridden"))P("OVERRIDE manuale: "+p.S("OverrideReason"));
        }
        Table(["Grandezza","Min","x [m]","Max","x [m]","|max|","x [m]"],new[]{("y","m"),("theta","rad"),("N","kN"),("V","kN"),("M","kNm"),("q","kN/m")}.Select(p=>{var e=r["Extrema"]![p.Item1];return new[]{p.Item1+" ["+p.Item2+"]",F(e,"Minimum"),F(e,"MinimumDepth"),F(e,"Maximum"),F(e,"MaximumDepth"),F(e,"AbsoluteMaximum"),F(e,"AbsoluteMaximumDepth")};}));
        P($"Testa: y={F(r,"HeadDisplacement")} m; θ={F(r,"HeadRotation")} rad. Reazioni: testa {F(r,"HeadReactionMoment")} kNm; punta {F(r,"TipReactionForce")} kN e {F(r,"TipReactionMoment")} kNm.");
        P($"Equilibrio: ΣF={F(r,"ForceResidual")} kN; ΣMtesta={F(r,"MomentResidual")} kNm. Terreno: risultante {F(r,"SoilForce")} kN e momento {F(r,"SoilMomentAboutHead")} kNm.");
        P($"Mesh: {F(r,"Elements")} elementi, confronto {F(r,"ComparisonElements")}. Variazioni relative y/M/V: {F(r["RelativeMeshChanges"],"y")}/{F(r["RelativeMeshChanges"],"M")}/{F(r["RelativeMeshChanges"],"V")}. Soglia 0,001: "+(r.B("MeshConverged")?"soddisfatta":"NON soddisfatta; raffinare"));
        P("FEM Hermite Euler–Bernoulli, 2 gdl/nodo, matrice di fondazione consistente con quadratura di Gauss. Recupero V e M per equilibrio degli elementi. Estremi dai polinomi degli elementi. Nessuna differenziazione numerica degli spostamenti.");
        P("Fonte FEM consultata: TU Delft, Computational Modelling, §4.1 Euler–Bernoulli beam elements (web, 02/10/2026): https://interactivetextbooks.citg.tudelft.nl/computational-modelling/structural_linear/euler_bernouilli.html . Per il terreno: fonte Viggiani e autori citati riportati sopra; articoli originali non consultati.");
        P("Valori lungo il palo: Above=limite superiore dell'interfaccia; Below=limite inferiore; Interior=interno elemento. x dalla testa, z dal piano campagna. Tabelle dei punti arrotondate a quattro cifre significative per la leggibilità; precisione completa nelle esportazioni CSV e JSON.");
        Table(["x [m]","z [m]","Strato/lato","y [m]","θ [rad]","N [kN]","V [kN]","M [kNm]"],r.Array("Points").Select(p=>new[]{C(p,"Depth"),C(p,"GroundDepth"),F(p,"Layer")+" "+p.S("Side"),C(p,"Displacement"),C(p,"Rotation"),C(p,"AxialForce"),C(p,"Shear"),C(p,"Moment")}));
        P("Rigidezze del terreno e reazione sul palo. nh è riportato soltanto per la legge lineare; z e lato sono definiti nella tabella precedente.");
        Table(["x [m]","Strato/lato","kh [kN/m³]","nh [kN/m³]","k [kN/m²]","q [kN/m]"],r.Array("Points").Select(p=>new[]{F(p,"Depth"),F(p,"Layer")+" "+p.S("Side"),F(p,"Kh"),F(p,"Nh"),F(p,"DistributedStiffness"),F(p,"SoilReaction")}));
        P("Discretizzazione: K* equivale all'integrale di k sul tratto tributario. Valori illustrativi [kN/m], non molle concentrate impiegate dal FEM.");
        Table(["Nodo x [m]","Tributaria da [m]","a [m]","K* [kN/m]"],r.Array("Nodes").Select(p=>new[]{F(p,"Depth"),F(p,"TributaryTop"),F(p,"TributaryBottom"),F(p,"EquivalentSpring")}));
        if(result["armature"] is JsonNode reinforcement)
        {
            P("Armature e verifiche per tratto");P(reinforcement.S("stato"));P(reinforcement.S("ipotesi"));
            var settings=reinforcement["impostazioni"];
            if(settings!=null)
            {
                P("Azioni di progetto: "+(settings.B("azioni_progetto")?"confermate":"da confermare")+". Modello di taglio circolare: "+(settings.B("taglio_confermato")?"confermato":"da confermare")+$"; z/d={F(settings,"z_d")}. Dettagli: {settings.S("elemento")}.");
                P($"Aderenza: {(settings.B("aderenza_buona")?"buona":"altre condizioni")}; barre sovrapposte {F(settings,"percentuale_sovrapposta")}% ; distanza libera {F(settings,"distanza_barre")} mm. Sovrapposizione iniziale: {F(settings,"fattore_sovrapposizione")} diametri, arrotondata al decimetro superiore.");
                P($"Aggregato {F(settings,"aggregato")} mm; tolleranza copriferro {F(settings,"delta_c")} mm. Sviluppo disponibile esterno: testa {F(settings,"ancoraggio_testa")} m, punta {F(settings,"ancoraggio_punta")} m. Lunghezza minima tratti {F(settings,"lunghezza_minima")} m; barra commerciale massima {F(settings,"lunghezza_barra")} m.");
            }
            foreach(var t in reinforcement.Array("tratti"))
            {
                var s=t!["sezione"]!;P($"{t.S("id")}: x={F(t,"inizio")}–{F(t,"fine")} m. "+(t.B("collegato")?"Collegato alla sezione principale":"Armatura personalizzata")+$". {t.S("stato")} {t.S("errore")}");
                P($"Armatura: {F(s,"longitudinal_bar_count")} φ{F(s,"longitudinal_bar_diameter_mm")}; staffe chiuse φ{F(s,"transverse_bar_diameter_mm")}/{F(s,"transverse_spacing_mm")} mm; copriferro {F(s,"cover_mm")} mm.");
                P("Normativa: "+t.S("normativa")+". Dettagli: "+t.S("comportamento_dettagli")+".");
                if(t["durabilita"] is JsonNode durability)P($"Durabilità: esposizione {(string.IsNullOrWhiteSpace(durability.S("esposizione"))?"non assegnata":durability.S("esposizione"))}; cmin,dur {(durability["cmin_dur"]==null?"da determinare":F(durability,"cmin_dur")+" mm")}. {durability.S("origine")}. {durability.S("fonte")}");
                foreach(var pending in t.Array("da_completare"))P(pending?.ToString()??"");
                Table(["Controllo costruttivo","Valore","Limite","Unità","Esito"],t.Array("controlli_costruttivi").Select(c=>new[]{ElasticHorizontalPile.ConstructionLabel(c.S("Key")),F(c,"Actual"),F(c,"Limit"),c.S("Unit"),c?["Passed"]==null?"Da completare":c.B("Passed")?"Soddisfatto":"Non soddisfatto"}));
                var links=t["distinta_staffe"];if(links!=null)P($"Distinta staffe: {F(links,"quantita")} φ{F(links,"diametro_mm")} mm, passo non maggiore di {F(links,"passo_massimo_mm")} mm. {links.S("stato")}");
                var c=t["critica"];var a=c?["Action"];P($"Sezione critica x={F(a,"Depth")} m: N={F(a,"N")} kN, V={F(a,"V")} kN, M={F(a,"M")} kNm. MRd nominale +={F(c,"MRdPositive")}, MRd−={F(c,"MRdNegative")} kNm; VRd={F(c,"VRd")} kN. {c.S("Message")}");
                P("Lato: sup. = superiore, inf. = inferiore, int. = interno all'elemento.");
                Table(["x [m] / lato","N [kN]","M [kNm]","MRd+ util. [kNm]","MRd− util. [kNm]","V / VRd [kN]","Esito"],t.Array("verifiche").Select(v=>new[]{C(v?["Action"],"Depth")+" "+Side(v?["Action"]),F(v?["Action"],"N"),F(v?["Action"],"M"),F(v,"UsableMRdPositive"),F(v,"UsableMRdNegative"),F(v?["Action"],"V")+" / "+F(v,"VRd"),v.S("Status")}));
            }
            P("Distinta preliminare: ogni tratto definisce la fine fisica delle proprie barre; il primo gruppo parte da zero, i successivi dall'inizio tratto meno la sovrapposizione adottata. Gruppi identici restano separati; tagli interni soltanto oltre la lunghezza commerciale disponibile. Ogni pezzo compare una sola volta. Giunti raggruppati, nessun contributo resistente aggiuntivo delle barre sovrapposte; sviluppo, staffe, confinamento e disposizione esecutiva dei giunti da completare.");
            foreach(var run in reinforcement.Array("distinta")){P($"Gruppo {run.S("Id")} · tratti {run.S("Segment")}: {F(run,"Count")} Ø{F(run,"Diameter")}. {run.S("StartKind")} → {run.S("EndKind")}. lbd={F(run,"Anchorage")} m, a_l={F(run,"Shift")} m. {run.S("Status")}");P(run.S("Reference"));Table(["Posizione / n / φ","Da–a x [m]","Taglio [m]","Commerciale [m]","Giunto tra pezzi [m]"],run!.Array("Pieces").Select(b=>new[]{b.S("Id")+" / "+F(b,"Count")+" / "+F(b,"Diameter"),F(b,"Start")+"–"+F(b,"End"),F(b,"CuttingLength"),F(b,"StockLength"),F(b,"LapWithPrevious")}));}
            foreach(var joint in reinforcement.Array("giunti"))
            {
                P($"{joint.S("Id")} · {joint.S("Kind")} · {joint.S("UpperRun")} ↔ {joint.S("LowerRun")} · {F(joint,"MatchedCount")}/{F(joint,"Count")} coppie allineate; x={F(joint,"Start")}–{F(joint,"End")} m. l0 iniziale {F(joint,"InitialLength")}, richiesta {F(joint,"RequiredLength")}, adottata {F(joint,"AdoptedLength")}, effettiva {F(joint,"ActualLength")} m. Lunghezza disponibile: {(joint.B("LengthPassed")?"sì":"NO")}; giunti non interferenti e distanza trasversale: {(joint.B("ArrangementPassed")?"sì":"NO")}. Giunti raggruppati al 100%.");P(joint.S("Note"));
            }
        }
        body.Add(new XElement(w+"sectPr",new XElement(w+"pgSz",new XAttribute(w+"w",11906),new XAttribute(w+"h",16838)),new XElement(w+"pgMar",new XAttribute(w+"top",900),new XAttribute(w+"bottom",900),new XAttribute(w+"left",1000),new XAttribute(w+"right",1000))));
        using var memory=new MemoryStream();using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true))
        {void Entry(string path,string text){using var writer=new StreamWriter(zip.CreateEntry(path).Open(),new UTF8Encoding(false));writer.Write(text);}
        Entry("[Content_Types].xml","<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        Entry("_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"doc\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");Entry("word/document.xml",new XDocument(new XElement(w+"document",body)).ToString());}return memory.ToArray();
    }
}
