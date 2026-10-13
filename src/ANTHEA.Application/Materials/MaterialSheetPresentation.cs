using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Calculations;
namespace Materiali;

public record PropertyRow(string Nome, string Simbolo, string Valore, string Unita);

/// <summary>Values and explanations shared by the material sheet and nonvisual reports. Calculations delegate to the existing GPC adapters.</summary>
public sealed class MaterialSheetPresentation
{
    private readonly MaterialSheetInput input;
    public IReadOnlyList<PropertyRow> Properties { get; private set; } = [];
    public bool CoverError { get; private set; }
    public Dictionary<string, string> Composition { get; } = new() { ["chloride"]="", ["ratio"]="", ["cement"]="", ["air"]="", ["dmax"]="" };
    public const string CompositionChlorideNote = "Cl 0,40: proposta per armatura ordinaria dagli esempi ATECAP, pp. 43–50; non è una classe dedotta dall’esposizione e va confermata nella prescrizione.";
    public string Strength { get; private set; } = "";
    public string Tension { get; private set; } = "";
    public string Modulus { get; private set; } = "";
    public string Status { get; private set; } = "";
    public string BondValue { get; private set; } = "";
    public string BondDetails { get; private set; } = "";
    public string CoverHeadline { get; private set; } = "";
    public string CoverSteps { get; private set; } = "";
    public string CoverNotes { get; private set; } = "";
    public string MinimumConcreteClass { get; private set; } = "";
    public string MinimumConcreteNote { get; private set; } = "";
    public string CompositionSource { get; private set; } = "";
    public string CompositionAirNote { get; private set; } = "";
    public string CompositionRequirements { get; private set; } = "";
    public string ConsistencyInfo { get; private set; } = "";

    private MaterialSheetPresentation(MaterialSheetInput input)
    {
        this.input = input;
        RefreshProperties(); RefreshBond(); RefreshMinimumConcrete(input.Exposures);
        RefreshCover(input.Exposures,input.Fck); RefreshMix(input.Exposures,input.Fck);
    }
    public static MaterialSheetPresentation Evaluate(JsonObject state, IEnumerable<string>? activeExposures = null)
        => new(new MaterialSheetInput(state, activeExposures));
    private static string Number(double value) => Math.Abs(value).ToString("N2",CultureInfo.CurrentCulture);
    private void RefreshProperties()
    {
        var selected=(Name:input.ClassName,Fck:input.Fck);
        var m=ConcreteMaterialCatalog.Material(selected.Fck);
        Strength=Number(m.Fck); Tension=Number(m.Fctm); Modulus=Number(m.Ecm/1000);
        Properties=new PropertyRow[] {
            new("Resistenza caratteristica cilindrica","fck",Number(m.Fck),"MPa"),
            new("Resistenza caratteristica cubica (classe)","Rck",selected.Name.Split('/')[1],"MPa"),
            new("Resistenza media a compressione","fcm",Number(m.Fcm),"MPa"),
            new("Resistenza media a trazione","fctm",Number(m.Fctm),"MPa"),
            new("Resistenza caratteristica a trazione · 5%","fctk,0.05",Number(m.Fctk05),"MPa"),
            new("Resistenza caratteristica a trazione · 95%","fctk,0.95",Number(m.Fctk95),"MPa"),
            new("Modulo elastico secante","Ecm",Number(m.Ecm),"MPa"),
            new("Deformazione al picco · parabola-rettangolo","εc2",Number(m.StrainYCompression*1000),"‰"),
            new("Deformazione ultima · parabola-rettangolo","εcu2",Number(m.StrainUCompression*1000),"‰"),
            new("Coefficiente di Poisson","ν",Number(m.Ni),"—")
        };
        Status=selected.Name+"  ·  EN 1992-1-1:2004 / UNI EN 206-1:2006";
    }
    private void RefreshBond()
    {
        BondValue="Da completare";BondDetails="";
        try
        {
            double diameter=input.Number("diameter","Diametro",.1,131.999),alpha=input.Number("bondAlpha","αct",.001,1),gamma=input.Number("bondGamma","γc",1,100);
            double fck=input.Fck,eta1=input.Index("bondCondition")==0?1:.7;
            var b=ConcreteBond.Calculate(fck,diameter,eta1,alpha,gamma);
            BondValue=$"fbd = {b.Fbd:0.00} MPa";
            BondDetails=$"φ = {diameter:0.##} mm  ·  η₁ = {eta1:0.00}  ·  η₂ = {b.Eta2:0.00}\nfctk,0.05 per aderenza = {b.Fct:0.00} MPa\nfctd = {b.Fctd:0.00} MPa"+(b.Capped?"\nLimite C60/75 applicato.":"");
        }
        catch(ArgumentException ex) {BondDetails=ex.Message;}
    }
    private void RefreshMinimumConcrete(Exposure[] active)
    {
        try
        {
            MinimumConcreteClass=MinimumConcrete.Label(MinimumConcrete.Required(active));
            MinimumConcreteNote="UNI 11104:2025, prospetto 6. Per più esposizioni si adotta la classe minima più elevata.";
            if(active.Any(e=>e.Code=="XF1"))MinimumConcreteNote+=" XF1: si adotta C32/40 dell'edizione 2016 (prospetto 5); il prospetto 6 della 2025 indica C30/37.";
            if(active.Any(e=>e.Code.StartsWith("XF")))MinimumConcreteNote+=" XF2/XF3/XF4: valori del prospetto con aria inglobata; per l’alternativa senza aria occorrono prove prestazionali (nota a). XF1: valore ordinario senza aria aggiunta; l’alternativa aerata della nota b richiede una specifica dedicata.";
        }
        catch(ArgumentException ex){MinimumConcreteClass="Da definire";MinimumConcreteNote=ex.Message;}
    }
    private void RefreshCover(Exposure[] active,double fck)
    {
        CoverHeadline="Da completare"; CoverSteps="";
        bool ntc=input.Index("coverMethod")==0;
        CoverNotes="EN 1992-1-1:2004, §4.4.1 e tabelle 4.2–4.4N: valori raccomandati, senza annesso nazionale. Δcdur,γ = Δcdur,st = Δcdur,add = 0. Copriferro misurato fino all'armatura più vicina alla superficie, staffe comprese. Verificare ciascuna barra e superficie; requisiti d'incendio secondo EN 1992-1-2 da valutare separatamente.";
        try
        {
            double delta=double.Parse(input.Selected("deviationValue").Replace(" mm",""),CultureInfo.InvariantCulture);
            var p=MaterialCover.Input(input.State, input.Number("diameter","Diametro",.1,1000), input.Number("aggregate","Dmax",.1,1000));
            if(ntc) RenderNtcCover(active,fck,p);
            else
            {
            var c=Durability.Cover(active,fck,p);
            CoverHeadline=$"cnom = {c.Nominal:0.##} mm";
            CoverSteps=string.Join("\n",c.Lines.Select(l=>$"{l.Exposure}: classe strutturale S{l.StructuralClass} → cmin,dur = {l.Durability:0.##} mm"))+
                $"\n\ncmin,b = φ{(p.Aggregate>32?" + 5 mm (Dmax > 32 mm)":"")} = {c.Bond:0.##} mm"+
                $"\ncmin = max(cmin,b; cmin,dur; 10 mm) + superficie + abrasione = {c.Minimum:0.##} mm"+
                $"\ncnom = cmin + Δcdev = {c.Minimum+delta:0.##} mm"+
                (p.Ground>0?$"\nLimite per il getto: {p.Ground} mm → cnom adottato {c.Nominal:0.##} mm":"");
            if(active.Any(e=>e.CoverColumn<0)) CoverSteps+="\nXF/XA: requisiti aggiuntivi della miscela nella colonna Composizione.";
            }
        }
        catch(ArgumentException ex) {CoverSteps=ex.Message;CoverError=true;}
    }
    private void RenderNtcCover(Exposure[] active,double fck,CoverInput p)
    {
        var n=NtcCover.Calculate(active,fck,p,input.Index("ntcElement")==1,input.Flag("ntcQuality"),MinimumConcrete.Required(active));
        var c=n.Cover;
        CoverNotes="NTC 2018 tab. 4.1.III; Circolare 7/2019 §C4.1.6.1.3 e tab. C4.1.IV, barre ordinarie. Requisiti integrativi di aderenza, Dmax, superficie, abrasione e getto su terreno: EC2 §4.4.1. Verifiche al fuoco separate. Copriferro riferito all'armatura più vicina, staffe comprese.";
        CoverHeadline=$"cnom = {c.Nominal:0.##} mm";
        CoverSteps=$"NTC + Circolare · ambiente {n.Environment.ToLower()}"+
            $"\n{string.Join(" + ",active.Select(e=>e.Code))} · {input.Selected("ntcElement")}"+
            $"\nCmin (fck) = {n.Cmin:0.##} MPa · C0 (fck) = {n.C0:0.##} MPa"+
            $"\nMinimo tabellare = {n.TableCover:0.##} mm"+
            $"\nVita utile: +{n.LifeExtra:0.##} mm; classe < Cmin: +{n.LowStrengthExtra:0.##} mm"+
            $"\nControllo dei copriferri: −{n.QualityReduction:0.##} mm"+
            $"\nMinimo per durabilità = {c.Durability:0.##} mm"+
            $"\nMinimo per aderenza = {c.Bond:0.##} mm"+
            $"\ncmin = max(durabilità; aderenza; 10) + superficie/abrasione = {c.Minimum:0.##} mm"+
            $"\ncnom = cmin + Δcdev = {c.Minimum+p.Deviation:0.##} mm"+
            (p.Ground>0?$"\nLimite getto {p.Ground} mm → adottato {c.Nominal:0.##} mm":"");
        if(fck<n.Cmin) CoverSteps+="\nClasse inferiore a Cmin: verificare anche la specifica del calcestruzzo.";
    }
    private void RefreshMix(Exposure[] active,double fck)
    {
        foreach(var key in Composition.Keys.ToArray())Composition[key]="Da definire";
        Composition["chloride"]="Cl 0,40";
        CompositionAirNote="";CompositionRequirements="";
        CompositionSource="UNI 11104:2016, prospetto 5 (riportato in ATECAP 2020, p. 19). Nella UNI 11104:2025 in vigore (prospetto 6) i dosaggi minimi di cemento sono inferiori e per XF1 l’A/C massimo è 0,55: i valori adottati sono più restrittivi. Per esposizioni concomitanti: il minore A/C massimo e il maggiore dosaggio minimo. Valori senza applicazione del concetto k per le aggiunte (nota d).";
        if(input.Index("life")==1)CompositionSource+="\n\nVita utile 100 anni: i valori del prospetto sono riferimenti per 50 anni (ATECAP 2020, p. 9); la prescrizione per 100 anni richiede una valutazione specifica.";
        string[] slump=["Da scegliere secondo elemento e modalità di getto (p. 21).","S1: 10–40 mm","S2: 50–90 mm","S3: 100–150 mm","S4: 160–210 mm","S5: > 220 mm (tabella ATECAP, p. 22)."];
        ConsistencyInfo=slump[input.Index("consistency")];
        double? dmax=null;
        try{dmax=input.Number("aggregate","Dmax",.1,1000);Composition["dmax"]=dmax.Value.ToString("0.##",CultureInfo.GetCultureInfo("it-IT"));}
        catch(ArgumentException){CompositionAirNote="Correggere il Dmax nei dati del copriferro.";}
        try
        {
            var limits=AtecapMix.Required(active);
            Composition["ratio"]=limits.Ratio?.ToString("0.00",CultureInfo.GetCultureInfo("it-IT"))??"Non prescritto";
            Composition["cement"]=limits.Cement?.ToString()??"Non prescritto";
            bool air=active.Any(e=>e.Code is "XF2" or "XF3" or "XF4");
            if(!air)Composition["air"]="Non prescritto";
            else if(dmax is double d)
            {
                var minimum=AtecapMix.Air(active,d);
                Composition["air"]=minimum?.ToString("0.0",CultureInfo.GetCultureInfo("it-IT"))??"Da definire";
                CompositionAirNote=d>20?"Nota a: minimo 4% per Dupper > 20 mm.":d>=12&&d<=16?"Nota a: 5% è l’esempio indicato per Dupper da 12 a 16 mm; da confermare nella specifica.":"Nota a: il 4% è indicato per Dupper > 20 mm. Per questo Dmax il documento non fornisce un minimo numerico univoco: definire il contenuto d’aria nella specifica.";
                CompositionAirNote+=" L’alternativa senza aria inglobata richiede prove prestazionali di resistenza al gelo/disgelo.";
            }
            if(active.Any(e=>e.Code=="XF1"))CompositionRequirements+="XF1: tabella per calcestruzzo senza aria aggiunta; per la variante aerata valgono le specifiche XF2/XF3 (nota b).\n";
            if(active.Any(e=>e.Code.StartsWith("XF")))CompositionRequirements+="Aggregati di adeguata resistenza al gelo/disgelo secondo UNI EN 12620.\n";
            if(active.Any(e=>e.Code.StartsWith("XS")))CompositionRequirements+="Cemento resistente all’acqua di mare secondo UNI 9156.\n";
            if(active.Any(e=>e.Code.StartsWith("XA")))CompositionRequirements+="In presenza di solfati: cemento resistente ai solfati, classe da scegliere secondo UNI 11417-1 (nota c).\n";
            CompositionSource+="\n\nEsposizioni: "+string.Join(" + ",active.Select(e=>e.Code))+". Classe minima: "+MinimumConcrete.Label(MinimumConcrete.Required(active))+".";
            if(fck<MinimumConcrete.Required(active))CompositionSource+="\nLa classe di calcestruzzo selezionata è inferiore al minimo richiesto.";
        }
        catch(ArgumentException ex){CompositionSource+="\n\n"+ex.Message;}
    }
    public IReadOnlyList<PropertyRow> ReportProperties()
    {
        var rows = Properties.Where(r => r.Simbolo != "fck").ToList();
        void Add(string name, string value) { if (!string.IsNullOrWhiteSpace(value)) rows.Add(new(name, "", value, "")); }
        Add("Aderenza", BondValue); Add("Dettagli aderenza", BondDetails);
        Add("Copriferro nominale", CoverHeadline); Add("Dettagli copriferro", CoverSteps); Add("Applicabilità copriferro", CoverNotes);
        foreach (var (key, name, unit) in new[] { ("chloride", "Classe cloruri", ""), ("ratio", "Rapporto A/C massimo", ""),
            ("cement", "Dosaggio minimo di cemento", "kg/m³"), ("air", "Contenuto minimo di aria", "%") })
            rows.Add(new(name, "", Composition[key], unit));
        Add("Requisiti della composizione", CompositionRequirements); Add("Indicazioni aria", CompositionAirNote);
        Add("Indicazioni cloruri", CompositionChlorideNote); Add("Riferimenti della composizione", CompositionSource);
        return rows;
    }
}
