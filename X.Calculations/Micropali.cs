using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Piles;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Data.Sections;
using Bd = GPC.Checkers.Geotechnics.Piles.BustamanteDoix;

namespace Anthea.Calculations;

/// <summary>
/// Bustamante–Doix in GPCChecker.Geotechnics (Viggiani §13.1.6, pp. 392–396): tabelle e abachi della libreria; qui i nomi del foglio, le unità
/// (m, kN, kPa ↔ mm, N, MPa) e i messaggi. Pressione MPa, aderenza kPa.
/// </summary>
public static class BustamanteDoix
{
    public const string Versione = Bd.Version;
    public const string Fonte = Bd.Source;
    /// <summary>Terreni della tabella 13.12: famiglia degli abachi e intervalli di α IRS e IGU, dalla libreria.</summary>
    public static readonly Dictionary<string,(string Family,double[] IRS,double[] IGU)> Terreni = Enum.GetValues<BustamanteDoixSoil>().ToDictionary(Bd.Label,
        s => (Bd.Family(s), Range(s, MicropileInjection.IRS), Range(s, MicropileInjection.IGU)));
    /// <summary>Abachi digitalizzati (p_l MPa, s MPa) della libreria: SG, AL, MC, R; 1 = IRS, 2 = IGU.</summary>
    public static readonly Dictionary<string,(double X,double Y)[]> Curve = new[] { "SG", "AL", "MC", "R" }.SelectMany(f => new[] { f + "1", f + "2" })
        .ToDictionary(c => c, c => Bd.Chart(c).Select(p => (p.LimitPressure, p.UnitResistance)).ToArray());
    static double[] Range(BustamanteDoixSoil soil, MicropileInjection injection) { var r = Bd.AlphaRange(soil, injection); return [r.Min, r.Max]; }
    internal static BustamanteDoixSoil Soil(string terreno) => Enum.GetValues<BustamanteDoixSoil>().Where(s => Bd.Label(s) == terreno).Select(s => (BustamanteDoixSoil?)s).FirstOrDefault()
        ?? throw new ArgumentException("Scegliere un terreno della tabella Viggiani 13.12.");
    internal static MicropileInjection Injection(string iniezione) => iniezione switch
    {
        "IGU" => MicropileInjection.IGU, "IRS" => MicropileInjection.IRS, _ => throw new ArgumentException("Scegliere il tipo di iniezione IGU o IRS.")
    };
    public static double[] IntervalloAlpha(string terreno,string iniezione) => Range(Soil(terreno), Injection(iniezione));
    /// <summary>Aderenza unitaria dall'abaco della libreria, con i controlli del foglio: α positivo, p_l entro l'abaco.</summary>
    public static JsonObject Parametro(string terreno,string iniezione,double pressione,double alpha)
    {
        var soil = Soil(terreno); var injection = Injection(iniezione);
        if (!double.IsFinite(alpha) || alpha<=0) throw new ArgumentException($"α {iniezione}: inserire un valore maggiore di zero.");
        string code = Bd.Family(soil) + (injection == MicropileInjection.IGU ? "2" : "1"); var points = Bd.Chart(code);
        if (!double.IsFinite(pressione) || pressione<points[0].LimitPressure || pressione>points[^1].LimitPressure)
            throw new ArgumentException($"p_l fuori abaco {code}: usare {points[0].LimitPressure:g}–{points[^1].LimitPressure:g} MPa; nessuna estrapolazione.");
        return Json(Bd.UnitShaftResistance(soil, injection, pressione, alpha));
    }
    static JsonObject Json(BustamanteDoixShaft s) => J.Obj(("curva",s.Curve),("pl",s.LimitPressure),("alpha",s.Alpha),("alpha_consigliato",new[] { s.RecommendedAlpha.Min, s.RecommendedAlpha.Max }),
        ("s",1000*s.UnitResistance),("iniezione",s.Injection.ToString()));
    /// <summary>
    /// Tratti di aderenza fra l'inizio dell'iniezione e la quota (m lungo l'asse), con il diametro di perforazione (m): segmenti della libreria
    /// (Ds = α D, π Ds L s) nelle unità del foglio. I dati degli strati attivi sono controllati prima, con i messaggi del foglio.
    /// </summary>
    public static List<JsonObject> Tratti(List<Strato> strati,double quota,double inizio,double diametro,string iniezione,double pressione)
    {
        var injection = Injection(iniezione); var layers = new List<MicropileLayer>();
        for(int i=0;i<strati.Count;i++)
        {
            var st=strati[i]; bool active=st.Valori.B("laterale_attiva",true); var soil=BustamanteDoixSoil.Gravel; double alpha=double.NaN;
            if(active && Math.Min(st.Fondo,quota)>Math.Max(st.Cielo,inizio))
                try { alpha=st.Valori.Required("alpha",strict:true); _=Parametro(st.Valori.S("terreno"),iniezione,pressione,alpha); soil=Soil(st.Valori.S("terreno")); }
                catch(ArgumentException ex) { throw new ArgumentException($"Strato {i+1}: {ex.Message}"); }
            layers.Add(new MicropileLayer(st.Cielo*1000,st.Fondo*1000,soil,alpha,active));
        }
        return Bd.Segments(layers,quota*1000,inizio*1000,diametro*1000,injection,pressione).Select(s =>
        {
            if(s.Shaft is null) return J.Obj(("strato",s.Layer),("cielo",s.Top/1000),("fondo",s.Bottom/1000),("iniezione",iniezione),("alpha",null),("ds",null),("pl",null),("s",0.0),("curva",null),("laterale",0.0),("laterale_attiva",false));
            var p=Json(s.Shaft); p["strato"]=s.Layer; p["cielo"]=s.Top/1000; p["fondo"]=s.Bottom/1000; p["ds"]=s.DrillDiameter!.Value/1000; p["laterale"]=s.Lateral/1000; return p;
        }).ToList();
    }
}

/// <summary>Tubi CHS del micropalo: profili di ModelData (EN 10210, Celsius) e peso della libreria (MicropileTube.Weight).</summary>
public static class Chs
{
    // Taglie proposte nel foglio; dimensioni e sezione dai cataloghi di ModelData.
    static readonly Dictionary<double,double[]> Taglie=new() { [60.3]=[3.2,3.6,4,5,6.3,8],[76.1]=[3.2,3.6,4,5,6.3,8],[88.9]=[3.2,3.6,4,5,6.3,8,10],[114.3]=[3.6,4,5,6.3,8,10],[139.7]=[3.6,4,5,6.3,8],[168.3]=[5,6.3,8,10,12.5],[193.7]=[5,6.3,8,10,12.5,14.2,16],[219.1]=[5,6.3,8,10,12.5,14.2,16],[244.5]=[5,6.3,8,10,12.5,14.2,16],[273]=[5,6.3,8,10,12.5,14.2,16,17.5] };
    public static readonly Dictionary<string,(double Diameter,double Thickness)> Catalogo = Build();
    /// <summary>Acciaio del tubo: della sezione serve la densità di Model (7850 kg/m³).</summary>
    internal static readonly SteelMaterial Acciaio = new("S355", 210000, 355, 510);
    private static Dictionary<string,(double,double)> Build()
    {
        var result=new Dictionary<string,(double,double)>();
        foreach(var (d,tt) in Taglie) foreach(var t in tt)
        {
            string name=FormattableString.Invariant($"CHS {d:g} × {t:g}"); var tube=Sezione(name);
            result[name]=(tube.Diameter,tube.Thickness);
        }
        return result;
    }
    /// <summary>La sezione CHS di ModelData della designazione.</summary>
    public static SectionCHS Sezione(string profilo) => SectionMappings.CreateSection(profilo) as SectionCHS ?? throw new ArgumentException("Profilo CHS non presente nei cataloghi di ModelData: " + profilo);
    public static JsonObject Peso(string profilo,double diametro,double gamma=25)
    {
        if(!Catalogo.TryGetValue(profilo,out var v))throw new ArgumentException("Selezionare il profilo CHS dal catalogo.");
        if(!double.IsFinite(diametro)||diametro<=0)throw new ArgumentException("Diametro di perforazione non valido.");
        if(!double.IsFinite(gamma)||gamma<=0)throw new ArgumentException("Peso specifico del calcestruzzo non valido.");
        if(v.Diameter>=diametro*1000)throw new ArgumentException("Il diametro esterno CHS deve essere minore del diametro di perforazione.");
        var w=MicropileTube.Weight(Sezione(profilo),Acciaio,diametro*1000,gamma*1e-6);
        // Aree m², massa kg/m dalla densità del materiale di Model (t/mm³), pesi kN/m (= N/mm).
        return J.Obj(("profilo",profilo),("diametro_chs_mm",v.Diameter),("spessore_mm",v.Thickness),("area_acciaio",w.SteelArea/1e6),("area_cls",w.GroutArea/1e6),("massa_acciaio",Acciaio.Density*w.SteelArea*1e6),
            ("q_acciaio",w.Steel),("q_cls",w.Grout),("q_totale",w.Total),("gamma_cls",gamma));
    }
}

public static class GeometriaMicropalo
{
    public static double Coseno(double theta)
    {
        if(!double.IsFinite(theta)||theta<0||theta>=90)throw new ArgumentException("Inclinazione θ: deve essere compresa tra 0° incluso e 90° escluso.");
        return MicropileTube.AxisCosine(theta*Math.PI/180);
    }
}
