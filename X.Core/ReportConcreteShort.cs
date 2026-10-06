using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace X.Core;

/// <summary>Seven independent governing combinations, using current results without recalculating or mixing envelopes.</summary>
public static class ReportConcreteShort
{
    public sealed record Row(string Check, string Combination, string Actions, string Values, double? Ratio, int Complete, int Total)
    {
        public string Outcome => Total == 0 ? "Non richiesta\nNessuna azione" : Complete < Total || Ratio is null
            ? $"INCOMPLETA\n{Complete}/{Total} esiti" : Ratio <= 1 ? "VERIFICATA" : "NON VERIFICATA";
    }
    private static double? Number(JsonNode? value) => J.Number(value) is double n && double.IsFinite(n) && n >= 0 ? n : null;
    private static string F(JsonNode? value) => EngineeringFormat.Number(J.Number(value));
    private static string N(double? value) => EngineeringFormat.Number(value);
    private static string Actions(JsonNode? row, bool shear = false) => shear
        ? $"N={F(row?["N"])}; Mx={F(row?["Mx"])}; My={F(row?["My"])}\nVx={F(row?["Vx"])}; Vy={F(row?["Vy"])}"
        : $"N={F(row?.Array("azioni").ElementAtOrDefault(0))}; Mx={F(row?.Array("azioni").ElementAtOrDefault(1))}; My={F(row?.Array("azioni").ElementAtOrDefault(2))}";
    public static Row[] Select(JsonObject data, JsonObject result)
    {
        var output = new List<Row>();
        foreach (string family in new[] { "SLU", "SLV" })
        {
            var actions = data["combinazioni"].Array(family); var results = result["domini"]?["3D:" + family];
            var valid = actions.Select(a => (Action:a, Result:results?[a.S("id")])).Where(x => Number(x.Result?["Utilization"]) is not null).ToArray();
            var chosen = valid.OrderByDescending(x => Number(x.Result?["Utilization"])).FirstOrDefault();
            var rd = chosen.Result?["Resistance"];
            output.Add(new(family + " N–Mx–My", chosen.Action.S("nome", "—"), Actions(chosen.Action),
                rd is null ? "Resistenza non disponibile" : $"NRd={F(rd["N"])} kN\nMxRd={F(rd["Mx"])}; MyRd={F(rd["My"])} kNm",
                Number(chosen.Result?["Utilization"]), valid.Length, actions.Count));
        }
        var shearActions = data["workspace_ca"]?["taglio"].Array("azioni") ?? new JsonArray();
        var shear = shearActions.Select(a => (Action:a, Results:result["taglio"]?[a.S("id")] as JsonArray)).ToArray();
        double? ShearRatio(JsonArray? a) => a?.Select(x => Number(x?["Ratio"])).Where(n => n.HasValue).DefaultIfEmpty(null).Max();
        var worst = shear.Where(x => ShearRatio(x.Results) is not null).OrderByDescending(x => ShearRatio(x.Results)).FirstOrDefault();
        string ShearValue(int i, string axis)
        {
            var r = worst.Results?.ElementAtOrDefault(i);
            return $"{axis}: VRd={F(r?["VRd"])} kN; η={F(r?["Ratio"])}; cotθ={F(r?["CotTheta"])}";
        }
        output.Add(new("Taglio Vx e Vy", worst.Action.S("nome", "—"), Actions(worst.Action, true),
            ShearValue(0,"x") + "\n" + ShearValue(1,"y"), ShearRatio(worst.Results),
            shear.Count(x => x.Results?.Count == 2 && x.Results.All(r => Number(r?["Ratio"]) is not null)), shearActions.Count));
        foreach (var (family, cracking, label) in new[] { ("SLE", false, "Tensioni rara"), ("SLE_FREQ", true, "Fessurazione frequente"),
            ("SLE_QP", false, "Tensioni quasi permanente"), ("SLE_QP", true, "Fessurazione quasi permanente") })
        {
            var actions = data["combinazioni"].Array(family); var results = result["tensioni"]?[family];
            double? Ratio(JsonNode? r) => Number(cracking ? r?["CrackResult"]?["Ratio"] : r?["Ratio"]);
            var valid = actions.Select(a => (Action:a,Result:results?[a.S("id")])).Where(x => Ratio(x.Result) is not null).ToArray();
            var worstSle = valid.OrderByDescending(x => Ratio(x.Result)).FirstOrDefault();
            var state = worstSle.Result?["State"]; var crack = worstSle.Result?["CrackResult"];
            string values;
            if (cracking)
            {
                string Trace(string symbol) { var item=crack.Array("Details").LastOrDefault(d=>d.S("Symbol")==symbol); return item is null ? "" : $"; {symbol}={J.Number(item["Value"])?.ToString("G4",System.Globalization.CultureInfo.InvariantCulture)} {item.S("Unit")}"; }
                values = $"wk={F(crack?["Width"])}; wlim={F(crack?["Limit"])} mm" + Trace("sr,max") + Trace("Δsm adottata") + Trace("β apertura") + Trace("εsm − εcm") +
                    $"\nAc,eff={F(crack?["EffectiveArea"])}; As,eff={F(crack?["EffectiveSteel"])} mm²";
                if(J.Number(crack?["EffectiveSteel"])==0 && J.Number(crack?["Width"])>0)values+="\nLimite superiore senza barre efficaci.";
                if(worstSle.Result is null) values="Tasso wk/wlim non disponibile; verificare dati o criterio senza tasso (decompressione/formazione).";
            }
            else values = $"σc,min={F(state?["sigma_cls"])} MPa; limite |σc|={F(state?["ConcreteStressLimit"])} MPa" +
                (family=="SLE" ? $"\n|σs|max={F(state?["sigma_acciaio"])} MPa; limite={F(state?["SteelStressLimit"])} MPa" : "");
            output.Add(new(label, worstSle.Action.S("nome", "—"), Actions(worstSle.Action), values, Ratio(worstSle.Result),valid.Length,actions.Count));
        }
        return output.ToArray();
    }

    public static byte[] Create(string title, JsonObject data, JsonObject result)
    {
        if(result.S("motore")!="GPCChecker.Concrete.dll")throw new ArgumentException("Risultati sezionali aggiornati non disponibili.");
        var input=data["input"]!.AsObject();var ws=data["workspace_ca"]!.AsObject(); var section=new SezioneCA(input);
        var rows=Select(data,result); var sle=ws["sle_comuni"]!;var domain=ws["dominio3d"]!;var shear=(JsonObject)ws["taglio"]!.DeepClone();
        ConcreteCalculationSettings.UpdateAutomaticShear(input,shear);
        double circularLever=shear.S("modello_circolare").StartsWith("Pile") ? (input.B("foro_presente") ? .60 : .75) : shear.D("z_d");
        double modularRatio=input.D("steel_modulus_mpa")*(1+sle.D("phi"))/ConcreteMaterials.Concrete(input).E;
        XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var body=new XElement(w+"body");
        XElement P(string text,string style="TableText")=>new(w+"p",new XElement(w+"pPr",new XElement(w+"pStyle",new XAttribute(w+"val",style))),
            new XElement(w+"r",text.Split('\n').SelectMany((s,i)=>i==0?new[]{new XElement(w+"t",s)}:new[]{new XElement(w+"br"),new XElement(w+"t",s)})));
        void Table(string[] headers,int[] widths,IEnumerable<string[]> values)
        {
            var table=new XElement(w+"tbl",new XElement(w+"tblPr",new XElement(w+"tblW",new XAttribute(w+"w",9360),new XAttribute(w+"type","dxa")),
                new XElement(w+"tblLayout",new XAttribute(w+"type","fixed")),
                new XElement(w+"tblCellMar",new[]{"top","bottom","left","right"}.Select(s=>new XElement(w+s,new XAttribute(w+"w",90),new XAttribute(w+"type","dxa")))),
                new XElement(w+"tblBorders",new[]{"top","bottom","left","right","insideH","insideV"}.Select(s=>new XElement(w+s,new XAttribute(w+"val","single"),new XAttribute(w+"sz",4),new XAttribute(w+"color","CAD3DF"))))),
                new XElement(w+"tblGrid",widths.Select(v=>new XElement(w+"gridCol",new XAttribute(w+"w",v)))));
            foreach(var (cells,i) in new[]{headers}.Concat(values).Select((v,i)=>(v,i)))
                table.Add(new XElement(w+"tr",new XElement(w+"trPr",new XElement(w+"cantSplit"),i==0?new XElement(w+"tblHeader"):null),cells.Select((s,c)=>new XElement(w+"tc",
                    new XElement(w+"tcPr",new XElement(w+"tcW",new XAttribute(w+"w",widths[c]),new XAttribute(w+"type","dxa")),i==0?new XElement(w+"shd",new XAttribute(w+"fill","E8EFF7")):null),P(s,i==0?"TableHeader":"TableText")))));
            body.Add(table);
        }
        body.Add(P("Verifica sintetica della sezione in calcestruzzo armato","Title"),P(title,"Subtitle"));
        string overall=rows.All(r=>r.Total==0)?"Nessuna verifica disponibile":rows.Any(r=>r.Complete<r.Total)?"Esiti incompleti":rows.Any(r=>r.Ratio>1)?"Una o più verifiche non soddisfatte":"Verifiche numeriche riportate soddisfatte";
        body.Add(P($"ANTHEA · {DateTime.Now:dd/MM/yyyy} · {overall}","Normal"));
        string fields(JsonNode? o,params string[] keys)=>string.Join("; ",keys.Where(k=>o?[k] is not null).Select(k=>$"{k}={o.S(k)}"));
        string axes(JsonNode? o)=>o.S("assi")+(o.S("assi")=="Personalizzati"?$"; x0={o.S("origine_x")}; y0={o.S("origine_y")} mm; θ={o.S("rotazione")}°":"");
        var bars=section.Bars.GroupBy(b=>(b.Y,b.Diametro)).OrderByDescending(g=>g.Key.Y).Select(g=>$"y={N(g.Key.Y)}: {g.Count()}Ø{N(g.Key.Diametro)}, x=[{string.Join("; ",g.Select(b=>N(b.X)))}]");
        if(section.Shape=="Circolare")
        {
            var rings=section.Bars.GroupBy(b=>(Radius:Math.Round(Math.Sqrt(b.X*b.X+b.Y*b.Y),6),b.Diametro)).ToArray();
            double[] Angles(IEnumerable<Barra> group)=>group.Select(b=>(Math.Atan2(b.Y,b.X)*180/Math.PI+360)%360).Order().ToArray();
            // Only encode regular rings parametrically; arbitrary arrangements retain explicit coordinates.
            if(rings.All(g=>{var a=Angles(g);return a.Length>2 && Enumerable.Range(0,a.Length).All(i=>Math.Abs((a[(i+1)%a.Length]-a[i]+360)%360-360d/a.Length)<1e-5);}))
                bars=rings.Select(g=>$"{g.Count()}Ø{N(g.Key.Diametro)} su r={N(g.Key.Radius)}; θ iniziale={Angles(g)[0]:0.###}° da +x, antiorario; passo={360d/g.Count():0.###}°.");
        }
        var dataRows=new List<string[]>{
            new[]{"Normativa e convenzioni",ws.S("normativa")+". N negativo a compressione. Geometria mm; N e V kN; M kNm; σ MPa. Azioni già combinate."},
            new[]{"Geometria",section.Shape+$"; b={N(section.Width)}; h={N(section.Height)}; c netto={input.S("cover_mm")}; Ac={N(section.AreaCls)} mm²"+(section.Shape=="Circolare"?$"; {section.CircularSides} lati":"")+(input.B("foro_presente")?"; foro: "+fields(input,"inner_diameter_mm","inner_width_mm","inner_height_mm"):"")+(section.Shape=="A T"?"; "+fields(input,"flange_width_mm","web_width_mm","flange_thickness_mm"):"")},
            new[]{"Armature longitudinali",$"As={N(section.AreaSteel)} mm²; coordinate nel riferimento geometrico della sezione.\n"+string.Join("\n",bars)},
            new[]{"Staffe",input.S("staffe_presenti")+"; Ø"+input.S("transverse_bar_diameter_mm")+" / "+input.S("transverse_spacing_mm")+" mm; "+shear.S("tipo_staffa")+$"; rami x/y={shear.S("rami_x")}/{shear.S("rami_y")}; interni={shear.S("rami_interni")}"},
            new[]{"Materiali",$"CLS {input.S("classe_cls")}: fck={input.S("fck_mpa")}; {input.S("cls_diagramma")}.\nAcciaio {input.S("classe_acciaio")}: fyk={input.S("fyk_mpa")}; Es={input.S("steel_modulus_mpa")}; fu={input.S("steel_fu_mpa")}; εu={input.S("steel_eps_u")} ‰; {input.S("steel_diagramma")}."},
            new[]{"Resistenze NMM",$"SLU plastico; SLV elastico. {domain.S("strategia")}; {domain.S("criterio")}; CLS teso={domain.S("trazione_cls")}; assi {axes(domain)}."},
            new[]{"Tensioni e fessurazione",$"{sle.S("modello")}; "+(sle.S("modello")=="Lineare"?$"n={N(modularRatio)}; φ={F(sle["phi"])}; ":"")+$"CLS teso={sle.S("trazione_cls")}; assi {axes(sle)}.\n{sle.S("esposizione")}; {sle.S("sensibilita")}; durata {sle.S("durata")}; aderenza {sle.S("aderenza")}. c e interasse fessure: {sle.S("copriferro_fessure","auto")}/{sle.S("spaziatura_fessure","auto")} mm (vuoto = automatico)."},
            new[]{"Parametri a taglio",$"{shear.S("modello")}; {shear.S("parametri")}; Asl in mm².\nx: bw={F(shear["bw_x"])}; d={F(shear["d_x"])}; Asl={F(shear["asl_x"])}; α={F(shear["alpha_x"])}°.\ny: bw={F(shear["bw_y"])}; d={F(shear["d_y"])}; Asl={F(shear["asl_y"])}; α={F(shear["alpha_y"])}°."+(section.Shape=="Circolare"?$" {shear.S("modello_circolare")}; z/d={N(circularLever)}":"")}
        };
        var coefficients=ConcreteStandards.Effective(input,ws);
        string CoefficientLabel(string key)=>key switch{"AlphaCC"=>"αcc","GammaC"=>"γc","GammaS"=>"γs","SteelCoefficientStrainTension"=>"εud/εuk",_=>key};
        dataRows.Insert(5,new[]{"Coefficienti",string.Join("; ",ConcreteStandards.Coefficients.Where(c=>c.Key is "AlphaCC" or "GammaC" or "GammaS" or "SteelCoefficientStrainTension" ||
            Math.Abs((double)coefficients.GetType().GetProperty(c.Key)!.GetValue(coefficients)!-ConcreteStandards.Defaults(ws.S("normativa")).D(c.Key))>1e-12).Select(c=>CoefficientLabel(c.Key)+"="+N((double)coefficients.GetType().GetProperty(c.Key)!.GetValue(coefficients)!)))+"; getto sottile="+input.S("gettato_sottile")});
        if(ws.Array("trefoli").Count>0)dataRows.Add(new[]{"Trefoli",string.Join("\n",ws.Array("trefoli").Select(t=>fields(t,"id","x","y","area","Ep","fpyk","fpk","eps_u","sigma0")))+$"; φp={sle.S("phi_trefoli")}"});
        Table(["Dati essenziali","Valori utilizzati"],[2100,7260],dataRows);
        body.Add(new XElement(w+"p",new XElement(w+"r",new XElement(w+"br",new XAttribute(w+"type","page")))));
        body.Add(P("Combinazioni governanti","Heading1"),P("Una combinazione per verifica, scelta sul tasso non arrotondato fra tutte le azioni inserite, incluse quelle nascoste. Gli esiti incompleti impediscono una conclusione complessiva.","Normal"));
        Table(["Verifica e azioni","Risultati e limiti","η","Esito"],[2900,4200,800,1460],rows.Select(r=>new[]{r.Check+"\n"+r.Combination+"\n"+r.Actions,r.Values,r.Ratio?.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)??"—",r.Outcome}));
        body.Add(P("η ≤ 1: verifica numerica soddisfatta. NMM: tasso secondo il criterio indicato; taglio: max(|Vx|/VRd,x; |Vy|/VRd,y); tensioni rara: max(|σc,comp|/σc,lim; |σs|max/σs,lim); quasi permanente: solo CLS; fessurazione: wk/wlim.","Normal"));
        body.Add(P("Ambito: le sette verifiche riportate. Torsione e interazioni, dettagli costruttivi, ancoraggi, domini 2D e momento–curvatura sono nel report completo. Valori arrotondati solo per la stampa.","Normal"));
        if(result["errori_calcolo"] is JsonObject errors && errors.Count>0)body.Add(P($"Sono presenti {errors.Count} segnalazioni di calcolo: consultare il report completo.","Normal"));
        body.Add(new XElement(w+"sectPr",new XElement(w+"pgSz",new XAttribute(w+"w",11906),new XAttribute(w+"h",16838)),new XElement(w+"pgMar",new XAttribute(w+"top",900),new XAttribute(w+"bottom",900),new XAttribute(w+"left",1273),new XAttribute(w+"right",1273))));
        using var memory=new MemoryStream();memory.Write(ReportConcrete.Create(title,data,result,["materiali"]));memory.Position=0;
        using(var zip=new ZipArchive(memory,ZipArchiveMode.Update,true))
        {
            void Replace(string path,string text){zip.GetEntry(path)?.Delete();using var writer=new StreamWriter(zip.CreateEntry(path).Open(),new UTF8Encoding(false));writer.Write(text);}
            Replace("word/document.xml",new XDocument(new XElement(w+"document",new XAttribute(XNamespace.Xmlns+"w",w),body)).ToString());
            using var reader=new StreamReader(zip.GetEntry("word/styles.xml")!.Open());var styles=XDocument.Parse(reader.ReadToEnd());reader.Close();
            foreach(var style in styles.Root!.Elements(w+"style"))
            {string id=style.Attribute(w+"styleId")!.Value;style.Descendants(w+"sz").Single().SetAttributeValue(w+"val",id=="Title"?30:id=="Heading1"?26:id=="Subtitle"?22:20);}
            Replace("word/styles.xml",styles.ToString());
        }
        return memory.ToArray();
    }
}

