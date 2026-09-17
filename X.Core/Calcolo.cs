using System.Text.Json.Nodes;

namespace X.Core;

public sealed record Strato(JsonObject Valori,double Cielo,double Fondo);

/// <summary>Porting di calcolo.py/core.py. Unità: m, kN, kPa, kN/m³, gradi. Nessuna dipendenza dalla GUI.</summary>
public static class Calcolo
{
    public static readonly Dictionary<string,(double Xi3,double Xi4)> Verticali = new() { ["1"]=(1.70,1.70),["2"]=(1.65,1.55),["3"]=(1.60,1.48),["4"]=(1.55,1.42),["5"]=(1.50,1.34),["7"]=(1.45,1.28),["≥10"]=(1.40,1.21) };
    public static readonly Dictionary<string,(double Sciolto,double Denso,string Mu)> Parametri = new()
    {
        ["Profilato d'acciaio"]=(.7,1,"tan20"),["Tubo d'acciaio chiuso"]=(1,2,"tan20"),["Calcestruzzo prefabbricato"]=(1,2,"tan3phi4"),["Calcestruzzo gettato in opera"]=(1,3,"tanphi"),["Trivellato"]=(.5,.4,"tanphi"),["Elica continua"]=(.7,.9,"tanphi")
    };
    public static (double? K,double? Mu) CoefficientiLaterali(JsonNode g,JsonNode v)
    {
        string tipo=g.S("tipo_palo","Trivellato");if(tipo=="Battuto")tipo=g.S("sottotipo_palo_battuto","Profilato d'acciaio");
        if(!Parametri.TryGetValue(tipo,out var p))return(null,null);
        double? k=v.S("addensamento")=="Sciolto"?p.Sciolto:v.S("addensamento")=="Denso"?p.Denso:null;
        double? phi=J.Number(v["angolo_attrito"]);
        double? mu=p.Mu=="tan20"?Math.Tan(20*Math.PI/180):phi is null?null:Math.Tan((p.Mu=="tan3phi4"?3*phi.Value/4:phi.Value)*Math.PI/180);
        return(k,mu);
    }
    public static double CoefficienteAlfa(JsonNode g,double cu)=>g.S("tipo_palo","Trivellato")=="Battuto"?(cu<=25?1:cu<70?1-.0111*(cu-25):.5):(cu<=25?.7:cu<70?.7-.008*(cu-25):.35);
    public static void ValidaForma(JsonNode? dati)
    {
        if(dati is not JsonObject)throw new ArgumentException("I dati del foglio devono essere un oggetto.");
        foreach(var k in new[]{"generali","efficienza","visibilita_grafici"}) if(dati.AsObject().ContainsKey(k)&&dati[k] is not JsonObject)throw new ArgumentException($"'{k}' deve contenere un oggetto.");
        if(dati.AsObject().ContainsKey("stratigrafie")&&dati["stratigrafie"] is not JsonArray)throw new ArgumentException("Stratigrafie: elenco non valido.");
        foreach(var s in dati.Array("stratigrafie"))
        {
            if(s is not JsonArray rows)throw new ArgumentException("Stratigrafia: elenco non valido.");
            foreach(var r in rows)
            {
                if(r is not JsonObject row||row.Any(p=>p.Value is null or JsonObject or JsonArray))throw new ArgumentException("Strato: dati non validi.");
                if(row.ContainsKey("laterale_attiva")&&!(row["laterale_attiva"] is JsonValue v&&v.TryGetValue<bool>(out _)))throw new ArgumentException("laterale_attiva: atteso vero/falso.");
            }
        }
        if(dati["generali"] is JsonObject g)
        {
            if(g.Any(p=>p.Value is null or JsonObject or JsonArray))throw new ArgumentException("Parametri generali: valore non valido.");
            foreach(var k in new[]{"presenza_falda","considera_sottospinta","considera_punta"})if(g.ContainsKey(k)&&!(g[k] is JsonValue v&&v.TryGetValue<bool>(out _)))throw new ArgumentException($"{k}: atteso vero/falso.");
        }
    }
    public static JsonObject Efficienza(JsonNode dati)
    {
        try
        {
            var eff=dati["efficienza"];string method=eff.S("metodo","Nessuna riduzione");
            double ec=1,et=1;int nx=1,ny=1;
            if(method is "Converse-Labarre" or "Feld")
            {
                int Count(string key) { double? x=eff?[key] is null?1:J.Number(eff[key]);if(x is null||x<1||Math.Abs(x.Value-Math.Round(x.Value))>Math.Max(1e-9,1e-9*Math.Abs(x.Value))||x>int.MaxValue)throw new ArgumentException($"{key} deve essere un intero maggiore o uguale a 1.");return (int)Math.Round(x.Value); }
                nx=Count("numero_pali_x");ny=Count("numero_pali_y");
            }
            if(method=="Converse-Labarre")
            {
                double d=dati["generali"].D("diametro");if((nx>1||ny>1)&&d<=0)throw new ArgumentException("Inserire un diametro D maggiore di zero.");
                double tx=nx>1?Math.Atan(d/eff.Required("interasse_x",strict:true))*180/Math.PI/90*(nx-1)/nx:0;
                double ty=ny>1?Math.Atan(d/eff.Required("interasse_y",strict:true))*180/Math.PI/90*(ny-1)/ny:0;
                ec=1-tx-ty;et=ec;
            }
            else if(method=="Feld") { double pairs=(nx-1.0)*ny+nx*(ny-1.0)+2*(nx-1.0)*(ny-1.0);ec=1-(2*pairs/(nx*(double)ny))/16;et=ec; }
            else if(method=="Definita dall'utente") { ec=eff?["eta_compressione"] is null?1:eff.Required("eta_compressione",strict:true);et=eff?["eta_trazione"] is null?1:eff.Required("eta_trazione",strict:true); }
            else if(method!="Nessuna riduzione")throw new ArgumentException("Metodo di efficienza non riconosciuto.");
            if(ec<=0)throw new ArgumentException("Il metodo selezionato produce ηg non positivo.");
            return J.Obj(("errore",""),("metodo",method),("eta_compressione",ec),("eta_trazione",et),("numero_pali",nx*(double)ny));
        }
        catch(ArgumentException ex) { return J.Error(ex.Message); }
    }
    public static JsonObject Calcola(JsonNode dati,bool micropalo=false)
    {
        try { ValidaForma(dati);return new Motore(dati,micropalo).Esegui(); }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or FormatException or OverflowException) { return J.Error(ex.Message); }
    }
    private sealed class Motore
    {
        private readonly JsonNode dati;private readonly JsonObject g;private readonly bool micro;
        private double d,l,zf,cos=1,sb,pct,pi;private bool falda;private JsonObject? pesoChs;
        public Motore(JsonNode input,bool micropalo) { dati=input;micro=micropalo;g=input["generali"] as JsonObject??new JsonObject(); }
        private void Validate()
        {
            if(micro)
            {
                cos=GeometriaMicropalo.Coseno(J.Number(g["inclinazione"])??(g.ContainsKey("inclinazione")?double.NaN:0));
                if(g.S("metodo_micropalo")!=BustamanteDoix.Versione)throw new ArgumentException("Foglio micropalo precedente (FHWA): selezionare terreno, IGU/IRS, p_l e coefficienti α di Bustamante–Doix. Le aderenze precedenti non sono convertibili.");
                if(g.S("tipo_iniezione") is not ("IGU" or "IRS"))throw new ArgumentException("Scegliere IGU o IRS per Bustamante–Doix.");
            }
            d=g.Required("diametro",strict:true);l=g.Required("lunghezza",strict:true);
            foreach(var k in new[]{"peso_specifico_palo","sicurezza_laterale_compressione","sicurezza_laterale_trazione","sicurezza_base","peso_palo_sfavorevole","peso_palo_favorevole"})if(g.ContainsKey(k))g.Required(k,strict:true);
            foreach(var k in new[]{"azione_compressione","azione_trazione"})if(!string.IsNullOrWhiteSpace(g.S(k)))g.Required(k);
            falda=!micro&&g.B("presenza_falda");zf=falda?g.Required("profondita_falda"):0;
            _=Nq.MetodoPrecedente(g);
            if(!Verticali.ContainsKey(g.S("verticali_indagate","1")))throw new ArgumentException("Numero di verticali indagate non riconosciuto.");
            if(micro)
            {
                pi=g.Required("pressione_iniezione",strict:true);pesoChs=Chs.Peso(g.S("profilo_chs"),d,g.D("peso_specifico_palo",25));
                sb=g.ContainsKey("inizio_aderenza")?g.Required("inizio_aderenza"):0;
                if(sb>=l)throw new ArgumentException("La zona di aderenza deve iniziare prima della punta.");
                pct=g.B("considera_punta")?g.Required("percentuale_punta"):0;if(pct>15)throw new ArgumentException("Il contributo di punta deve essere compreso tra 0% e 15% della resistenza laterale.");
            }
            foreach(var rows in dati.Array("stratigrafie"))
            {
                double bottom=0;
                foreach(var r in rows!.AsArray())
                {
                    bottom+=r.Required("spessore",strict:true);
                    if(micro)
                    {
                        if(r.B("laterale_attiva",true)&&(!r!.AsObject().ContainsKey("terreno")||!r.AsObject().ContainsKey("alpha")))throw new ArgumentException("Strato: completare i parametri Bustamante–Doix.");
                        continue;
                    }
                    if(r.S("tipologia") is not ("Granulare" or "Coesivo"))throw new ArgumentException("Strato: scegliere la tipologia.");
                    if(r.S("addensamento") is not ("Sciolto" or "Denso"))throw new ArgumentException("Strato: scegliere l’addensamento.");
                    r.Required("peso_specifico");if(r.Required("angolo_attrito")>=90)throw new ArgumentException("φ deve essere inferiore a 90°.");
                    foreach(var k in new[]{"peso_specifico_saturo","coesione_efficace","coesione_non_drenata","nc"})if(!string.IsNullOrWhiteSpace(r.S(k)))r.Required(k);
                    if(r.S("tipologia")=="Coesivo"&&falda&&Math.Min(bottom,l)>zf&&string.IsNullOrWhiteSpace(r.S("coesione_non_drenata")))throw new ArgumentException("Inserire Cu per il calcolo non drenato.");
                }
            }
        }
        private double Peso(double z,double area)
        {
            if(micro)return pesoChs!.D("q_totale")*z*cos;
            double weight=g.D("peso_specifico_palo",25)*area*z;
            if(falda&&g.B("considera_sottospinta"))weight-=9.81*area*Math.Max(0,z-zf);
            return Math.Max(weight,0);
        }
        private double Incremento(JsonNode v,double top,double bottom)
        {
            double gamma=v.D("peso_specifico"),sat=v.D("peso_specifico_saturo",gamma);
            if(!falda)return gamma*(bottom-top);
            return gamma*Math.Max(0,Math.Min(bottom,zf)-top)+Math.Max(sat-9.81,0)*Math.Max(0,bottom-Math.Max(top,zf));
        }
        private (double? K,double? Mu) KMu(JsonNode v)
        {
            return CoefficientiLaterali(g,v);
        }
        private double Alfa(double cu)
        {
            return CoefficienteAlfa(g,cu);
        }
        private JsonObject Resistenze(List<Strato> strata,double z,double area,double perimeter)
        {
            double effective=0,total=0,ld=0,lu=0;var tip=strata[0];var segments=new List<JsonObject>();
            foreach(var st in strata)
            {
                if(st.Cielo>=z)break;var v=st.Valori;double bottom=Math.Min(st.Fondo,z),length=bottom-st.Cielo;if(length<=0)continue;
                double sigmaTop=effective,increment=Incremento(v,st.Cielo,bottom),mean=effective+increment/2;
                if(falda&&st.Cielo<zf&&zf<bottom)
                {
                    double ds1=Incremento(v,st.Cielo,zf),ds2=Incremento(v,zf,bottom);
                    mean=effective+(ds1/2*(zf-st.Cielo)+(ds1+ds2/2)*(bottom-zf))/length;
                }
                effective+=increment;
                double gamma=v.D("peso_specifico"),sat=v.D("peso_specifico_saturo",gamma),wet=falda?Math.Max(0,bottom-Math.Max(st.Cielo,zf)):0;
                total+=gamma*(length-wet)+sat*wet;tip=st;
                var (k,mu)=KMu(v);double cohesion=v.D("coesione_efficace"),friction=k is not null&&mu is not null?k.Value*mu.Value*mean:0;
                bool active=v.B("laterale_attiva",true);double td=active?cohesion+friction:0,tu;double? alfa=null;ld+=perimeter*length*td;
                if(v.S("tipologia")=="Coesivo")
                {
                    double cu=v.D("coesione_non_drenata");alfa=Alfa(cu);tu=alfa.Value*cu;
                    double dryBottom=Math.Min(bottom,Math.Max(st.Cielo,falda?zf:bottom)),dryLength=dryBottom-st.Cielo;
                    if(dryLength==length)tu=td;
                    else if(dryLength>0)
                    {
                        double dryMean=sigmaTop+Incremento(v,st.Cielo,dryBottom)/2;
                        double dryTau=cohesion+(k is not null&&mu is not null?k.Value*mu.Value*dryMean:0);
                        tu=(dryTau*dryLength+tu*(length-dryLength))/length;
                    }
                }
                else tu=td;
                if(!active)tu=0;lu+=perimeter*length*tu;
                var segment=J.Obj(("cielo",st.Cielo),("fondo",bottom),("sigma_media",mean),("sigma_fondo",effective),("k",k),("mu",mu),("tau_d",td),("tau_u",tu),("alfa",alfa),("laterale_d",perimeter*length*td),("laterale_u",perimeter*length*tu));
                if(!active)segment["laterale_attiva"]=false;segments.Add(segment);if(bottom>=z)break;
            }
            var vp=tip.Valori;double? phi=J.Number(vp["angolo_attrito"]);JsonObject? nq=z>0&&phi is not null?Nq.Dettaglio(phi.Value,z/d,d>.8):null;
            double bd=area*effective*(nq?.D("nq")??0),bu=bd;double? nc=null;
            if(vp.S("tipologia")=="Coesivo")
            {
                nc=vp.D("nc",9);
                // Formulazione lorda autorizzata 16/09/2026: sigma totale, distinta dalla sottospinta sulle azioni.
                bu=area*(nc.Value*vp.D("coesione_non_drenata")+total);
                if(!falda||z<=zf)bu=bd;
            }
            var result=J.Obj(("tratti",segments),("sigma_punta",effective),("phi_punta",phi),("sigma_totale_punta",total),("nq",nq?["nq"]),("nc",nc),("drenante",J.Obj(("base",bd),("laterale",ld))),("non_drenante",J.Obj(("base",bu),("laterale",lu))));
            if(nq is not null)result["dettaglio_nq"]=nq;return result;
        }
        private static JsonObject Componenti(double lm,double bm,double lmin,double bmin,double xi3,double xi4,double gs,double gb,double eta)
        {
            JsonObject Branch(double lateral,double basal,double xi)=>J.Obj(("calc",new[]{lateral,basal}),("k",new[]{lateral/xi,basal/xi}),("d",new[]{eta*lateral/(xi*gs),eta*basal/(xi*gb)}));
            return J.Obj(("Media",Branch(lm,bm,xi3)),("Minimo",Branch(lmin,bmin,xi4)));
        }
        public JsonObject Esegui()
        {
            Validate();var eff=Efficienza(dati);if(eff.S("errore")!="")throw new ArgumentException("Efficienza: "+eff.S("errore"));
            var strata=new List<List<Strato>>();
            foreach(var rows in dati.Array("stratigrafie"))
            {
                var list=new List<Strato>();double top=0;
                foreach(var r in rows!.AsArray()) { double bottom=top+r!.D("spessore");list.Add(new Strato(r!.AsObject(),top/cos,bottom/cos));top=bottom; }
                if(list.Count==0)throw new ArgumentException("Inserire almeno uno spessore positivo in ogni stratigrafia.");
                if(micro)try { _=BustamanteDoix.Tratti(list,l,sb,d,g.S("tipo_iniezione"),pi); }catch(ArgumentException ex){throw new ArgumentException($"Stratigrafia {strata.Count+1}: {ex.Message}");}
                strata.Add(list);
            }
            if(strata.Count==0)throw new ArgumentException("Aggiungere almeno una stratigrafia.");
            double max=Math.Min(l,strata.Min(s=>s[^1].Fondo));var depths=new SortedSet<double>(Enumerable.Range(0,101).Select(i=>max*i/100));
            foreach(var list in strata)foreach(var s in list)if(s.Fondo<=max)depths.Add(s.Fondo);
            if(falda&&zf<=max)depths.Add(zf);for(int i=0;i<=(int)(max*2);i++)depths.Add(i/2.0);if(micro&&sb<=max)depths.Add(sb);
            double area=Math.PI*d*d/4,perimeter=Math.PI*d;var (xi3,xi4)=Verticali[g.S("verticali_indagate","1")];
            double gs=g.D("sicurezza_laterale_compressione",1.15),gt=g.D("sicurezza_laterale_trazione",1.25),gb=g.D("sicurezza_base",1.35),ggc=g.D("peso_palo_sfavorevole",1.30),ggt=g.D("peso_palo_favorevole",1),etaC=eff.D("eta_compressione"),etaT=eff.D("eta_trazione");
            var curves=new JsonObject();var actions=J.Obj(("compressione",new JsonArray()),("trazione",new JsonArray()));var details=new List<JsonObject>();
            string[] conditions=micro?["compressione"]:["drenante","non_drenante"];
            foreach(var c in conditions)foreach(var dir in new[]{"compressione","trazione"})curves[micro?dir:c+"_"+dir]=J.Obj(("media",new JsonArray()),("minima",new JsonArray()),("progetto",new JsonArray()));
            foreach(double z in depths)
            {
                var surveys=new List<JsonObject>();
                foreach(var list in strata)
                {
                    if(!micro)surveys.Add(Resistenze(list,z,area,perimeter));
                    else
                    {
                        // La fonte ricava Db dal perimetro/pi anche in questo passaggio.
                        var segments=BustamanteDoix.Tratti(list,z,sb,perimeter/Math.PI,g.S("tipo_iniezione"),pi);double lateral=J.Sum(segments.Select(s=>s.D("laterale")));
                        surveys.Add(J.Obj(("compressione",J.Obj(("laterale",lateral),("base",lateral*pct/100))),("tratti",segments)));
                    }
                }
                double weight=Peso(z,area);var components=new JsonObject();
                details.Add(J.Obj(("z",z),("sondaggi",surveys),("peso",weight),("componenti",components)));
                components=details[^1]["componenti"]!.AsObject();
                foreach(var c in conditions)
                {
                    double[] bases=surveys.Select(s=>s[c].D("base")).ToArray(),sides=surveys.Select(s=>s[c].D("laterale")).ToArray();
                    double bm=J.Sum(bases)/bases.Length,lm=J.Sum(sides)/sides.Length,bmin=bases.Min(),lmin=sides.Min();
                    // Micropalo: la fonte deriva le basi dopo la media delle laterali.
                    if(micro){bm=lm*pct/100;bmin=lmin*pct/100;}
                    components[c]=Componenti(lm,bm,lmin,bmin,xi3,xi4,gs,gb,etaC);
                    foreach(var dir in new[]{"compressione","trazione"})
                    {
                        bool compression=dir=="compressione";double mean,min;
                        if(compression){mean=micro?(lm/gs+bm/gb)/xi3:bm/(xi3*gb)+lm/(xi3*gs);min=micro?(lmin/gs+bmin/gb)/xi4:bmin/(xi4*gb)+lmin/(xi4*gs);mean*=etaC;min*=etaC;}
                        else {mean=lm/(xi3*gt)*etaT;min=lmin/(xi4*gt)*etaT;}
                        var curve=curves[micro?dir:c+"_"+dir]!;
                        curve["media"]!.AsArray().Add(new JsonArray(z,mean));curve["minima"]!.AsArray().Add(new JsonArray(z,min));curve["progetto"]!.AsArray().Add(new JsonArray(z,Math.Min(mean,min)));
                    }
                }
                if(J.Number(g["azione_compressione"]) is double nc)actions["compressione"]!.AsArray().Add(new JsonArray(z,nc+ggc*weight));
                if(J.Number(g["azione_trazione"]) is double nt)actions["trazione"]!.AsArray().Add(new JsonArray(z,Math.Max(0,nt-ggt*weight)));
            }
            var result=J.Obj(("errore",""),("curve",curves),("dettagli",details),("azioni",actions),("profondita_massima",max),("lunghezza_palo",l),("copertura_completa",max>=l-1e-9),("numero_stratigrafie",strata.Count),("efficienza",eff));
            var warnings=new List<string>();if(eff.S("metodo") is "Feld" or "Converse-Labarre")warnings.Add("Scelta progettuale 2026-09-15: efficienza geometrica applicata sia a compressione sia a trazione.");
            if(!micro)
            {
                result["metodo_nq"]="Parametrizzata";result["diametro_nq"]=d;result["descrizione_nq"]=Nq.Descrizione;
                var previous=Nq.MetodoPrecedente(g);if(previous!="")warnings.Add("Foglio convertito da Nq "+previous+" a Parametrizzata: i risultati precedenti possono cambiare.");
                warnings.Add("Ipotesi progettuale 2026-09-15: nella verifica non drenata la porzione sopra falda è drenata; senza falda le due verifiche coincidono.");
                warnings.Add("Nq: Parametrizzata NQ-2026-09-09; "+(d>.8?"Nq* per D > 0,80 m.":"Nq per D ≤ 0,80 m.")+" φ non ridotto.");
                var traces=details.SelectMany(dt=>dt.Array("sondaggi")).Select(s=>s?["dettaglio_nq"]).Where(t=>t is not null).ToArray();
                if(traces.Any(t=>t.B("limite_phi")))warnings.Add("ATTENZIONE Nq: φ fuori dal tratto visibile di almeno una curva; adottato il bordo. Vedere le φ adottate nel dettaglio export.");
                if(traces.Any(t=>t.B("limite_rapporto")))warnings.Add("ATTENZIONE Nq: alcune quote hanno z/D fuori dall'intervallo "+(d>.8?"4–32":"5–50")+"; adottata la curva di bordo, senza estrapolare.");
            }
            else
            {
                result["inizio_aderenza"]=sb;result["inclinazione"]=g.D("inclinazione");result["profondita_punta"]=l*cos;result["coordinata_curve"]="Lungo asse s [m]";result["peso_sezione"]=pesoChs!.DeepClone();result["metodo_micropalo"]=BustamanteDoix.Versione;result["pressione_iniezione"]=pi;result["ipotesi_pressione"]="p_l = p_i";
                warnings.Add("Strati orizzontali; θ dalla verticale. L, sb e coordinate delle curve sono lungo l'asse; z = s cos θ. Azioni inserite assiali; peso proprio proiettato sull'asse. Verifiche trasversali non comprese.");
                warnings.Add(BustamanteDoix.Fonte+". Abachi digitalizzati dalla scansione: letture approssimate, senza estrapolazione.");
                if(sb==0&&g.S("tipo_iniezione")=="IRS")warnings.Add("Scelta di progetto: IRS applicato anche nei primi 5 m, in deroga alla raccomandazione IGU superficiale di Viggiani p. 396.");
                if(l-sb<4)warnings.Add("ATTENZIONE: zona iniettata inferiore ai 4 m raccomandati.");
                warnings.Add("Ipotesi progettuale autorizzata: p_l = p_i. La pressione di iniezione generale alimenta gli abachi di tutti gli strati; non è una misura Ménard del terreno.");
                warnings.Add("Verificare le condizioni esecutive di p. 392 e i volumi minimi di miscela della tabella 13.12; il programma non li certifica.");
            }
            result["avvisi"]=J.Node(warnings);return result;
        }
    }
}
