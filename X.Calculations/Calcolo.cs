using System.Globalization;
using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Piles;
using GPC.Model.Geotechnics;
using GPC.Model.Standards;

namespace Anthea.Calculations;

public sealed record Strato(JsonObject Valori,double Cielo,double Fondo);

/// <summary>
/// Foglio della portanza assiale di pali e micropali. Il documento (m, kN, kPa, kN/m³, gradi) è letto e controllato qui; la portanza lungo la
/// profondità è GPCChecker.Geotechnics (AxialPileCapacity, mm, N, MPa), con i terreni e i fattori ξ di Model (NTC 2018 Tab. 6.4.IV). I risultati
/// sono riportati nel formato del foglio. Nessuna dipendenza dalla GUI.
/// </summary>
public static class Calcolo
{
    const double M = 1000, KPa = 1e-3, KN3 = 1e-6, Deg = Math.PI / 180;
    static readonly StandardNTC2018Geotechnics Ntc = new();
    static readonly string[] Indagate = ["1","2","3","4","5","7","≥10"];
    static int Profili(string verticali) => verticali == "≥10" ? 10 : int.Parse(verticali, CultureInfo.InvariantCulture);
    /// <summary>ξ3, ξ4 per numero di verticali indagate, da Model (StandardNTC2018Geotechnics.PileCorrelationFactors).</summary>
    public static readonly Dictionary<string,(double Xi3,double Xi4)> Verticali = Indagate.ToDictionary(k => k, k => { var x = Ntc.PileCorrelationFactors(Profili(k)); return (x.Item1, x.Item2); });
    static readonly Dictionary<string,PileInstallation> Tipi = new()
    {
        ["Profilato d'acciaio"]=PileInstallation.DrivenSteelSection,["Tubo d'acciaio chiuso"]=PileInstallation.DrivenClosedSteelTube,["Calcestruzzo prefabbricato"]=PileInstallation.DrivenPrecastConcrete,
        ["Calcestruzzo gettato in opera"]=PileInstallation.DrivenCastInPlace,["Trivellato"]=PileInstallation.Bored,["Elica continua"]=PileInstallation.ContinuousFlightAuger
    };
    /// <summary>K sciolto e denso e regola di μ per tecnologia (Viggiani Tab. 13.2), dalla libreria.</summary>
    public static readonly Dictionary<string,(double Sciolto,double Denso,string Mu)> Parametri = Tipi.ToDictionary(p => p.Key, p => AxialPileCapacity.ShaftCoefficients(p.Value));
    static PileInstallation? Tecnologia(JsonNode? g)
    {
        string tipo=g.S("tipo_palo","Trivellato");if(tipo=="Battuto")tipo=g.S("sottotipo_palo_battuto","Profilato d'acciaio");
        return Tipi.TryGetValue(tipo,out var t)?t:null;
    }
    /// <summary>
    /// γb della Tab. 6.4.II (R3) per la tecnologia del foglio, da Model (StandardNTC2018Geotechnics.PileExecution) tramite
    /// PileResistanceFactors.FromStandard: 1,15 per i pali infissi, 1,35 per i trivellati, 1,30 per l'elica continua. I micropali, perforati
    /// e iniettati, non hanno valori propri nella norma e usano quelli dei trivellati (scheda D7-d).
    /// </summary>
    public static double SicurezzaBaseNormativa(JsonNode? g)
    {
        var execution=g?["metodo_micropalo"] is not null?PileExecution.Bored:Tecnologia(g) switch
        {
            PileInstallation.ContinuousFlightAuger=>PileExecution.ContinuousFlightAuger,
            PileInstallation.Bored or null=>PileExecution.Bored,
            _=>PileExecution.Driven
        };
        return PileResistanceFactors.FromStandard(new StandardNTC2018Geotechnics{PileExecution=execution},1).Base;
    }
    /// <summary>Versione dei fogli del palo e del micropalo verticale con γb della tecnologia come predefinito (D7-d, opzione d2).</summary>
    public const int VersioneFoglio=2;
    static bool Marcato(JsonNode? dati)=>J.Number(dati?["versione"]) is double v&&v>=VersioneFoglio;
    /// <summary>
    /// γb effettivo del foglio (dati completi del palo o del micropalo verticale), unica regola per calcolo, relazione, progetti ed editor:
    /// il valore del foglio; quello della tecnologia (<see cref="SicurezzaBaseNormativa"/>) se il foglio non ha la chiave o se il foglio,
    /// salvato prima della versione <see cref="VersioneFoglio"/>, ha 1,35, il predefinito di allora per ogni tecnologia. Non scrive nei dati:
    /// <see cref="AggiornaFoglio"/> applica la stessa regola al documento.
    /// </summary>
    public static double SicurezzaBase(JsonNode? dati)
    {
        var g=dati?["generali"];var stored=g?["sicurezza_base"];
        double normative=SicurezzaBaseNormativa(g);
        // Un valore non numerico è respinto dal calcolo (Validate): qui vale quello della tecnologia.
        if(J.Number(stored) is not double value)return normative;
        return !Marcato(dati)&&value==1.35?normative:value;
    }
    /// <summary>γb effettivo nel formato dei coefficienti memorizzati (due decimali, punto), per completare un foglio senza la chiave.</summary>
    public static string SicurezzaBaseTesto(JsonNode? dati)=>SicurezzaBase(dati).ToString("0.00",CultureInfo.InvariantCulture);
    /// <summary>
    /// Migrazione una tantum dei fogli del palo e del micropalo verticale salvati prima della versione <see cref="VersioneFoglio"/>: γb 1,35
    /// (anche come testo) o assente diventa quello della tecnologia, cioè cambia solo per i pali battuti e a elica continua; poi il foglio
    /// riceve la versione, così un 1,35 scelto dopo non è più riscritto. Chiamata dall'editor all'apertura e dai progetti prima di scrivere
    /// in un foglio, come RetainingWall.Upgrade; il calcolo non la chiama. Restituisce vero se il foglio cambia.
    /// </summary>
    public static bool AggiornaFoglio(JsonObject dati)
    {
        if(Marcato(dati))return false;
        if(dati["generali"] is JsonObject g)
        {
            double normative=SicurezzaBaseNormativa(g);
            if(normative!=1.35&&(g["sicurezza_base"] is null||J.Number(g["sicurezza_base"])==1.35))g["sicurezza_base"]=SicurezzaBaseTesto(dati);
        }
        dati["versione"]=VersioneFoglio;return true;
    }
    public static (double? K,double? Mu) CoefficientiLaterali(JsonNode g,JsonNode v)
    {
        if(Tecnologia(g) is not PileInstallation tipo)return(null,null);
        var p=AxialPileCapacity.ShaftCoefficients(tipo);
        double? k=v.S("addensamento")=="Sciolto"?p.Loose:v.S("addensamento")=="Denso"?p.Dense:null;
        double? phi=J.Number(v["angolo_attrito"]);
        double? mu=p.Mu=="tan20"||phi is not null?AxialPileCapacity.LateralCoefficients(tipo,SoilDensity.Loose,(phi??0)*Deg).Mu:null;
        return(k,mu);
    }
    /// <summary>α(cu) dell'aderenza non drenata (cu in kPa), Viggiani Tab. 13.3, dalla libreria.</summary>
    public static double CoefficienteAlfa(JsonNode g,double cu)=>AxialPileCapacity.Alpha(g.S("tipo_palo","Trivellato")=="Battuto"?PileInstallation.DrivenSteelSection:PileInstallation.Bored,cu*KPa);
    public static void ValidaForma(JsonNode? dati)
    {
        if(dati is not JsonObject)throw new ArgumentException("I dati del foglio devono essere un oggetto.");
        foreach(var k in new[]{"generali","efficienza","visibilita_grafici"}) if(dati.AsObject().ContainsKey(k)&&dati[k] is not JsonObject)throw new ArgumentException($"'{k}' deve contenere un oggetto.");
        if(dati.AsObject().ContainsKey("stratigrafie")&&dati["stratigrafie"] is not JsonArray)throw new ArgumentException("Stratigrafie: elenco non valido.");
        // Versione del foglio (D7-d, d2): assente nei fogli precedenti, 2 nei fogli nuovi o aggiornati.
        if(dati.AsObject().ContainsKey("versione")&&J.Number(dati["versione"]) is not (1d or (double)VersioneFoglio))throw new ArgumentException("Versione del foglio non supportata.");
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
    /// <summary>Efficienza del gruppo dal foglio, con la libreria (PileGroupEfficiency): Converse-Labarre, Feld, assegnata o nessuna.</summary>
    static (string Metodo,PileGroupEfficiency Efficienza) Gruppo(JsonNode dati)
    {
        var eff=dati["efficienza"];string method=eff.S("metodo","Nessuna riduzione");
        int nx=1,ny=1;
        if(method is "Converse-Labarre" or "Feld")
        {
            int Count(string key) { double? x=eff?[key] is null?1:J.Number(eff[key]);if(x is null||x<1||Math.Abs(x.Value-Math.Round(x.Value))>Math.Max(1e-9,1e-9*Math.Abs(x.Value))||x>int.MaxValue)throw new ArgumentException($"{key} deve essere un intero maggiore o uguale a 1.");return (int)Math.Round(x.Value); }
            nx=Count("numero_pali_x");ny=Count("numero_pali_y");
        }
        if(method=="Converse-Labarre")
        {
            double d=dati["generali"].D("diametro");if((nx>1||ny>1)&&d<=0)throw new ArgumentException("Inserire un diametro D maggiore di zero.");
            double sx=nx>1?eff.Required("interasse_x",strict:true):double.NaN,sy=ny>1?eff.Required("interasse_y",strict:true):double.NaN;
            return (method,Positive(()=>PileGroupEfficiency.ConverseLabarre(nx,ny,sx*M,sy*M,d*M)));
        }
        if(method=="Feld")return (method,Positive(()=>PileGroupEfficiency.Feld(nx,ny)));
        if(method=="Definita dall'utente")return (method,PileGroupEfficiency.UserDefined(eff?["eta_compressione"] is null?1:eff.Required("eta_compressione",strict:true),eff?["eta_trazione"] is null?1:eff.Required("eta_trazione",strict:true)));
        if(method=="Nessuna riduzione")return (method,PileGroupEfficiency.None());
        throw new ArgumentException("Metodo di efficienza non riconosciuto.");
    }
    static PileGroupEfficiency Positive(Func<PileGroupEfficiency> efficiency)
    {
        try { return efficiency(); }
        catch(ArgumentException) { throw new ArgumentException("Il metodo selezionato produce ηg non positivo."); }
    }
    public static JsonObject Efficienza(JsonNode dati)
    {
        try { var (method,e)=Gruppo(dati); return J.Obj(("errore",""),("metodo",method),("eta_compressione",e.Compression),("eta_trazione",e.Tension),("numero_pali",(double)e.Piles)); }
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
                pi=g.Required("pressione_iniezione",strict:true);
                pesoChs=Chs.Peso(g.S("profilo_chs"),d,g.D("peso_specifico_palo",25));
                sb=g.ContainsKey("inizio_aderenza")?g.Required("inizio_aderenza"):0;
                if(sb>=l)throw new ArgumentException("La zona di aderenza deve iniziare prima della punta.");
                pct=g.B("considera_punta")?g.Required("percentuale_punta"):0;if(pct>15)throw new ArgumentException("Il contributo di punta deve essere compreso tra 0% e 15% della resistenza laterale.");
            }
            else if(Tecnologia(g) is null)throw new ArgumentException("Tipo di palo non riconosciuto.");
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
                    // Terreni di Model: γ > 0, γsat ≥ γ dove la falda lo usa, cu > 0 dove serve il calcolo non drenato.
                    double gamma=r.Required("peso_specifico",strict:true);if(r.Required("angolo_attrito")>=90)throw new ArgumentException("φ deve essere inferiore a 90°.");
                    foreach(var k in new[]{"peso_specifico_saturo","coesione_efficace","coesione_non_drenata","nc"})if(!string.IsNullOrWhiteSpace(r.S(k)))r.Required(k);
                    if(falda&&bottom>zf&&bottom-r.D("spessore")<l&&r.D("peso_specifico_saturo",gamma)<gamma)throw new ArgumentException("γsat deve essere almeno pari a γ sotto falda.");
                    if(r.S("tipologia")=="Coesivo"&&falda&&Math.Min(bottom,l)>zf&&string.IsNullOrWhiteSpace(r.S("coesione_non_drenata")))throw new ArgumentException("Inserire Cu per il calcolo non drenato.");
                    if(r.S("tipologia")=="Coesivo"&&falda&&Math.Min(bottom,l)>zf&&r.D("coesione_non_drenata")<=0)throw new ArgumentException("Cu deve essere positiva per il calcolo non drenato.");
                }
            }
        }
        /// <summary>Profilo di Model di una stratigrafia (spessori verticali dal piano campagna a quota 0), falda alla profondità indicata.</summary>
        private static SoilProfile Profilo(JsonArray rows,int index,double? falda,Func<JsonNode,int,Soil> terreno)
        {
            var list=new List<SoilLayer>();double top=0;
            for(int i=0;i<rows.Count;i++)
            {
                double bottom=top+rows[i].D("spessore");Soil soil;
                try { soil=terreno(rows[i]!,i); }
                catch(ArgumentException ex) { throw new ArgumentException($"Stratigrafia {index+1}, strato {i+1}: {ex.Message}"); }
                list.Add(new SoilLayer(soil,-top*M,-bottom*M));top=bottom;
            }
            return new SoilProfile("Stratigrafia "+(index+1),list,"ANTHEA",falda.HasValue?-falda.Value*M:null);
        }
        /// <summary>Terreno di Model di uno strato del palo: γ, γsat (γ se vuoto o non usato sopra falda), φ′, c′, cu (assente se nullo).</summary>
        private static Soil Terreno(JsonNode v,int i)
        {
            double gamma=v.D("peso_specifico"),saturated=v.D("peso_specifico_saturo",gamma),cu=v.D("coesione_non_drenata");
            return new Soil("Strato "+(i+1),gamma*KN3,Math.Max(saturated,gamma)*KN3,v.D("angolo_attrito")*Deg,v.D("coesione_efficace")*KPa,"ANTHEA",undrainedShearStrength:cu>0?cu*KPa:null);
        }
        private static JsonArray Punti(IEnumerable<(double Depth,double Value)> points)=>new(points.Select(p=>(JsonNode)new JsonArray(p.Depth/M,p.Value/1000)).ToArray());
        private static JsonObject Ramo(AxialComponentBranch b)=>J.Obj(("calc",new[]{b.Shaft/1000,b.Base/1000}),("k",new[]{b.CharacteristicShaft/1000,b.CharacteristicBase/1000}),("d",new[]{b.DesignShaft/1000,b.DesignBase/1000}));
        private static JsonObject Componenti(AxialComponents c)=>J.Obj(("Media",Ramo(c.Mean)),("Minimo",Ramo(c.Minimum)));
        private static JsonObject Tratto(AxialShaftSegment s)
        {
            var o=J.Obj(("cielo",s.Top/M),("fondo",s.Bottom/M),("sigma_media",s.MeanStress/KPa),("sigma_fondo",s.BottomStress/KPa),("k",s.K),("mu",s.Mu),("tau_d",s.DrainedShear/KPa),("tau_u",s.UndrainedShear/KPa),
                ("alfa",s.Alpha),("laterale_d",s.DrainedLateral/1000),("laterale_u",s.UndrainedLateral/1000));
            if(!s.ShaftActive)o["laterale_attiva"]=false;return o;
        }
        private static JsonObject Sondaggio(AxialSurveyResistance a)
        {
            var o=J.Obj(("tratti",a.Segments.Select(Tratto).ToList()),("sigma_punta",a.TipEffectiveStress/KPa),("phi_punta",a.TipFrictionAngle/Deg),("sigma_totale_punta",a.TipTotalStress/KPa),("nq",a.Nq?.Nq),("nc",a.Nc),
                ("drenante",J.Obj(("base",a.DrainedBase/1000),("laterale",a.DrainedShaft/1000))),("non_drenante",J.Obj(("base",a.UndrainedBase/1000),("laterale",a.UndrainedShaft/1000))));
            if(a.Nq is not null)o["dettaglio_nq"]=Nq.Json(a.Nq);return o;
        }
        /// <summary>Curve, dettagli e azioni del risultato della libreria nel formato del foglio (z in m, resistenze e azioni in kN).</summary>
        private JsonObject Risultato<T>(AxialCapacityResult<T> r,Func<T,JsonObject> sondaggio,string metodo)
        {
            var keys=micro?new[]{("compressione",AxialCondition.Drained,true),("trazione",AxialCondition.Drained,false)}
                :new[]{("drenante_compressione",AxialCondition.Drained,true),("drenante_trazione",AxialCondition.Drained,false),("non_drenante_compressione",AxialCondition.Undrained,true),("non_drenante_trazione",AxialCondition.Undrained,false)};
            var curves=new JsonObject();
            foreach(var (key,c,dir) in keys){var curve=r.Curves[(c,dir)];curves[key]=J.Obj(("media",Punti(curve.Mean)),("minima",Punti(curve.Minimum)),("progetto",Punti(curve.Design)));}
            var parts=micro?new[]{("compressione",AxialCondition.Drained,true),("trazione",AxialCondition.Drained,false)}
                :new[]{("drenante",AxialCondition.Drained,true),("drenante_trazione",AxialCondition.Drained,false),("non_drenante",AxialCondition.Undrained,true),("non_drenante_trazione",AxialCondition.Undrained,false)};
            var details=r.Depths.Select(x=>
            {
                var components=new JsonObject();foreach(var (key,c,dir) in parts)components[key]=Componenti(x.Components[(c,dir)]);
                return J.Obj(("z",x.Depth/M),("sondaggi",x.Surveys.Select(sondaggio).ToList()),("peso",x.Weight/1000),("componenti",components));
            }).ToList();
            var actions=J.Obj(("compressione",Punti(r.CompressionActions)),("trazione",Punti(r.TensionActions)));
            var e=r.Efficiency;
            return J.Obj(("errore",""),("curve",curves),("dettagli",details),("azioni",actions),("profondita_massima",r.MaximumDepth/M),("lunghezza_palo",r.PileLength/M),("copertura_completa",r.FullCoverage),
                ("numero_stratigrafie",r.SurveyCount),("efficienza",J.Obj(("errore",""),("metodo",metodo),("eta_compressione",e.Compression),("eta_trazione",e.Tension),("numero_pali",(double)e.Piles))));
        }
        public JsonObject Esegui()
        {
            Validate();
            (string Metodo,PileGroupEfficiency Efficienza) gruppo;
            try { gruppo=Gruppo(dati); } catch(ArgumentException ex) { throw new ArgumentException("Efficienza: "+ex.Message); }
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
            var (xi3,xi4)=Verticali[g.S("verticali_indagate","1")];
            double gammaB=SicurezzaBase(dati);
            var factors=new PileResistanceFactors(g.D("sicurezza_laterale_compressione",1.15),g.D("sicurezza_laterale_trazione",1.25),gammaB,g.D("peso_palo_sfavorevole",1.30),
                g.D("peso_palo_favorevole",1),xi3,xi4);
            double? compression=J.Number(g["azione_compressione"])*1000,tension=J.Number(g["azione_trazione"])*1000;
            var rowsList=dati.Array("stratigrafie").Select(s=>s!.AsArray()).ToList();
            JsonObject result;var warnings=new List<string>();
            if(!micro)
            {
                var surveys=rowsList.Select((rows,i)=>new AxialPileSurvey(Profilo(rows,i,falda?zf:null,Terreno),rows.Select(v=>new AxialPileLayer(v.S("tipologia")=="Coesivo"?SoilBehaviour.Cohesive:SoilBehaviour.Granular,
                    v.S("addensamento")=="Sciolto"?SoilDensity.Loose:SoilDensity.Dense,v.D("nc",9),v.B("laterale_attiva",true))))).ToArray();
                var pile=new AxialPile(Tecnologia(g)!.Value,d*M,l*M,g.D("peso_specifico_palo",25)*KN3,g.B("considera_sottospinta"),compression,tension);
                var r=AxialPileCapacity.Calculate(pile,surveys,factors,gruppo.Efficienza);
                result=Risultato(r,Sondaggio,gruppo.Metodo);
                warnings.AddRange(r.Warnings);
                // Dopo l'avviso dell'efficienza: il foglio convertito da un metodo Nq precedente.
                var previous=Nq.MetodoPrecedente(g);if(previous!="")warnings.Insert(gruppo.Efficienza.Method is PileGroupMethod.Feld or PileGroupMethod.ConverseLabarre?1:0,"Foglio convertito da Nq "+previous+" a Parametrizzata: i risultati precedenti possono cambiare.");
                result["metodo_nq"]="Parametrizzata";result["diametro_nq"]=d;result["descrizione_nq"]=Nq.Descrizione;
            }
            else
            {
                var injection=BustamanteDoix.Injection(g.S("tipo_iniezione"));
                BustamanteDoixSoil Bd(JsonNode v){try{return BustamanteDoix.Soil(v.S("terreno"));}catch(ArgumentException){return BustamanteDoixSoil.Gravel;}}
                // Le stratigrafie del micropalo hanno solo spessori e parametri di Bustamante–Doix: terreno di Model di riferimento per le quote.
                var surveys=rowsList.Select((rows,i)=>new MicropileSurvey(Profilo(rows,i,null,(v,k)=>new Soil(v.S("terreno") is {Length:>0} n?n:"Strato "+(k+1),18*KN3,20*KN3,0,0,"ANTHEA, solo quote")),
                    rows.Select(v=>(Bd(v!),J.Number(v!["alpha"])??double.NaN,v.B("laterale_attiva",true))))).ToArray();
                var pile=new Micropile(d*M,l*M,(J.Number(g["inclinazione"])??0)*Deg,injection,pi,sb*M,g.B("considera_punta")?pct:null,Chs.Sezione(g.S("profilo_chs")),Chs.Acciaio,
                    g.D("peso_specifico_palo",25)*KN3,compression,tension);
                var r=AxialPileCapacity.Calculate(pile,surveys,factors,gruppo.Efficienza);
                result=Risultato(r,s=>J.Obj(("compressione",J.Obj(("laterale",s.Shaft/1000),("base",s.Base/1000))),("tratti",s.Segments.Select(t=>Tratto(t,g.S("tipo_iniezione"))).ToList())),gruppo.Metodo);
                warnings.AddRange(r.Warnings);
                result["inizio_aderenza"]=sb;result["inclinazione"]=g.D("inclinazione");result["profondita_punta"]=r.PileLength*cos/M;result["coordinata_curve"]="Lungo asse s [m]";result["peso_sezione"]=pesoChs!.DeepClone();
                result["metodo_micropalo"]=BustamanteDoix.Versione;result["pressione_iniezione"]=pi;result["ipotesi_pressione"]="p_l = p_i";
            }
            // D7-d: γb effettivo del foglio (SicurezzaBase); se differisce da quello della tecnologia il calcolo lo dichiara.
            double normative=SicurezzaBaseNormativa(g);
            if((!micro||g.B("considera_punta"))&&Math.Abs(gammaB-normative)>1e-9)
            {
                var it=CultureInfo.GetCultureInfo("it-IT");
                warnings.Add($"γb = {gammaB.ToString("0.00",it)} diverso dal valore della NTC 2018 Tab. 6.4.II per la tecnologia del palo ({normative.ToString("0.00",it)}): il calcolo usa il valore del foglio.");
            }
            result["avvisi"]=J.Node(warnings);return result;
        }
        /// <summary>Tratto di aderenza del micropalo nel formato del foglio (m, kPa, kN).</summary>
        private static JsonObject Tratto(MicropileShaftSegment s,string iniezione)
        {
            if(s.Shaft is null)return J.Obj(("strato",s.Layer),("cielo",s.Top/M),("fondo",s.Bottom/M),("iniezione",iniezione),("alpha",null),("ds",null),("pl",null),("s",0.0),("curva",null),("laterale",0.0),("laterale_attiva",false));
            return J.Obj(("curva",s.Shaft.Curve),("pl",s.Shaft.LimitPressure),("alpha",s.Shaft.Alpha),("alpha_consigliato",new[]{s.Shaft.RecommendedAlpha.Min,s.Shaft.RecommendedAlpha.Max}),("s",1000*s.UnitResistance),
                ("iniezione",iniezione),("strato",s.Layer),("cielo",s.Top/M),("fondo",s.Bottom/M),("ds",s.DrillDiameter!.Value/M),("laterale",s.Lateral/1000));
        }
    }
}
