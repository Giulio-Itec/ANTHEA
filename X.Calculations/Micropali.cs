using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Viggiani §13.1.6, pp.392–396; abachi digitalizzati dalla fonte Python. Pressione MPa, aderenza kPa.</summary>
public static class BustamanteDoix
{
    public const string Versione = "BD-VIGGIANI-2026-09-10";
    public const string Fonte = "C. Viggiani, Fondazioni, §13.1.6, pp. 392–396; eq. 13.21, tab. 13.12–13.13, fig. 13.16–13.19";
    public static readonly Dictionary<string,(string Family,double[] IRS,double[] IGU)> Terreni = new()
    {
        ["Ghiaia"]=("SG",[1.8,1.8],[1.3,1.4]),["Ghiaia sabbiosa"]=("SG",[1.6,1.8],[1.2,1.4]),["Sabbia ghiaiosa"]=("SG",[1.5,1.6],[1.2,1.3]),
        ["Sabbia grossa"]=("SG",[1.4,1.5],[1.1,1.2]),["Sabbia media"]=("SG",[1.4,1.5],[1.1,1.2]),["Sabbia fine"]=("SG",[1.4,1.5],[1.1,1.2]),["Sabbia limosa"]=("SG",[1.4,1.5],[1.1,1.2]),
        ["Limo"]=("AL",[1.4,1.6],[1.1,1.2]),["Argilla"]=("AL",[1.8,2],[1.2,1.2]),["Marne"]=("MC",[1.8,1.8],[1.1,1.2]),["Calcari marnosi"]=("MC",[1.8,1.8],[1.1,1.2]),["Calcari alterati o fratturati"]=("MC",[1.8,1.8],[1.1,1.2]),["Roccia alterata e/o fratturata"]=("R",[1.2,1.2],[1.1,1.1])
    };
    public static readonly Dictionary<string,(double X,double Y)[]> Curve = new()
    {
        ["SG1"]=[(.25,.075),(1,.15),(2,.25),(3,.35),(4,.45),(5,.55),(6,.65),(6.7,.72)],
        ["SG2"]=[(.25,.025),(1,.10),(2,.20),(3,.30),(4,.40),(5,.50),(6,.60),(6.7,.67)],
        ["AL1"]=[(.30,.085),(.50,.125),(.75,.155),(1,.180),(1.5,.220),(2,.260),(2.4,.295)],
        ["AL2"]=[(.30,.040),(.50,.065),(.75,.083),(1,.100),(1.5,.130),(2,.160),(2.4,.184)],
        ["MC1"]=[(1,.20),(2,.27),(3,.34),(4,.41),(5,.48),(6,.55),(7,.62),(8,.69)],
        ["MC2"]=[(1,.15),(2,.20),(3,.25),(4,.30),(5,.35),(6,.40),(7,.45),(8,.50)],
        ["R1"]=[(1.3,.20),(2,.30),(3,.43),(4,.55),(5,.68),(6,.81),(7,.94),(8,1.07),(9,1.20),(9.4,1.25)],
        ["R2"]=[(1.3,.17),(2,.25),(3,.34),(4,.44),(5,.54),(6,.64),(7,.74),(8,.85),(9,.95),(9.4,.99)]
    };
    public static double[] IntervalloAlpha(string terreno,string iniezione)
    {
        if (!Terreni.TryGetValue(terreno,out var t)) throw new ArgumentException("Scegliere un terreno della tabella Viggiani 13.12.");
        if (iniezione is not ("IGU" or "IRS")) throw new ArgumentException("Scegliere il tipo di iniezione IGU o IRS.");
        return iniezione=="IGU" ? t.IGU : t.IRS;
    }
    public static JsonObject Parametro(string terreno,string iniezione,double pressione,double alpha)
    {
        var interval=IntervalloAlpha(terreno,iniezione);
        if (!double.IsFinite(alpha) || alpha<=0) throw new ArgumentException($"α {iniezione}: inserire un valore maggiore di zero.");
        var code=Terreni[terreno].Family+(iniezione=="IGU"?"2":"1"); var points=Curve[code];
        if (!double.IsFinite(pressione) || pressione<points[0].X || pressione>points[^1].X) throw new ArgumentException($"p_l fuori abaco {code}: usare {points[0].X:g}–{points[^1].X:g} MPa; nessuna estrapolazione.");
        for (int i=1;i<points.Length;i++) if (pressione<=points[i].X)
        {
            var (x1,y1)=points[i-1];var (x2,y2)=points[i];double s=y1+(y2-y1)*(pressione-x1)/(x2-x1);
            return J.Obj(("curva",code),("pl",pressione),("alpha",alpha),("alpha_consigliato",interval),("s",1000*s),("iniezione",iniezione));
        }
        throw new ArgumentException("Abaco non valutabile.");
    }
    public static List<JsonObject> Tratti(List<Strato> strati,double quota,double inizio,double diametro,string iniezione,double pressione)
    {
        var output=new List<JsonObject>();
        for(int i=0;i<strati.Count;i++)
        {
            var st=strati[i]; double top=Math.Max(st.Cielo,inizio),bottom=Math.Min(st.Fondo,quota);
            if(bottom<=top) continue;
            if(!st.Valori.B("laterale_attiva",true))
            {
                output.Add(J.Obj(("strato",i+1),("cielo",top),("fondo",bottom),("iniezione",iniezione),("alpha",null),("ds",null),("pl",null),("s",0.0),("curva",null),("laterale",0.0),("laterale_attiva",false)));continue;
            }
            JsonObject p;
            try { p=Parametro(st.Valori.S("terreno"),iniezione,pressione,st.Valori.Required("alpha",strict:true)); }
            catch(ArgumentException ex) { throw new ArgumentException($"Strato {i+1}: {ex.Message}"); }
            double ds=diametro*p.D("alpha");p["strato"]=i+1;p["cielo"]=top;p["fondo"]=bottom;p["ds"]=ds;p["laterale"]=Math.PI*ds*(bottom-top)*p.D("s");output.Add(p);
        }
        return output;
    }
}

public static class Chs
{
    public static readonly Dictionary<string,(double Diameter,double Thickness)> Catalogo = Build();
    private static Dictionary<string,(double,double)> Build()
    {
        var dimensions=new Dictionary<double,double[]> { [60.3]=[3.2,3.6,4,5,6.3,8],[76.1]=[3.2,3.6,4,5,6.3,8],[88.9]=[3.2,3.6,4,5,6.3,8,10],[114.3]=[3.6,4,5,6.3,8,10],[139.7]=[3.6,4,5,6.3,8],[168.3]=[5,6.3,8,10,12.5],[193.7]=[5,6.3,8,10,12.5,14.2,16],[219.1]=[5,6.3,8,10,12.5,14.2,16],[244.5]=[5,6.3,8,10,12.5,14.2,16],[273]=[5,6.3,8,10,12.5,14.2,16,17.5] };
        var result=new Dictionary<string,(double,double)>();
        foreach(var (d,tt) in dimensions) foreach(var t in tt) result[FormattableString.Invariant($"CHS {d:g} × {t:g}")]=(d,t);
        return result;
    }
    public static JsonObject Peso(string profilo,double diametro,double gamma=25)
    {
        if(!Catalogo.TryGetValue(profilo,out var v))throw new ArgumentException("Selezionare il profilo CHS dal catalogo.");
        if(!double.IsFinite(diametro)||diametro<=0)throw new ArgumentException("Diametro di perforazione non valido.");
        if(!double.IsFinite(gamma)||gamma<=0)throw new ArgumentException("Peso specifico del calcestruzzo non valido.");
        const double MmToM=.001; double de=v.Diameter*MmToM,sp=v.Thickness*MmToM;
        if(de>=diametro)throw new ArgumentException("Il diametro esterno CHS deve essere minore del diametro di perforazione.");
        double steel=Math.PI*(de*de-Math.Pow(de-2*sp,2))/4,cls=Math.PI*diametro*diametro/4-steel,mass=7850*steel,qs=mass*9.81/1000,qc=gamma*cls;
        return J.Obj(("profilo",profilo),("diametro_chs_mm",v.Diameter),("spessore_mm",v.Thickness),("area_acciaio",steel),("area_cls",cls),("massa_acciaio",mass),("q_acciaio",qs),("q_cls",qc),("q_totale",qs+qc),("gamma_cls",gamma));
    }
}

public static class GeometriaMicropalo
{
    public static double Coseno(double theta)
    {
        if(!double.IsFinite(theta)||theta<0||theta>=90)throw new ArgumentException("Inclinazione θ: deve essere compresa tra 0° incluso e 90° escluso.");
        return Math.Cos(theta*Math.PI/180);
    }
}
