using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Anthea.Muro;

public record Field(string Key, string Label, string Symbol, string Unit, string Group, double Default, double Min, double Max);
public sealed record Input
{
    public string Format { get; init; } = "ANTHEA.Muro";
    public int Version { get; init; } = 1;
    public string Title { get; init; } = "Nuovo muro";
    public Dictionary<string, string> Values { get; init; } = Fields.ToDictionary(f => f.Key, _ => "");
    public static readonly Field[] Fields = [
        new("H", "Altezza sopra la fondazione", "H", "m", "Geometria", 3, .1, 20),
        new("T", "Spessore del fusto costante", "s", "m", "Geometria", .3, .1, 3),
        new("D", "Spessore della fondazione", "t", "m", "Geometria", .4, .1, 3),
        new("Toe", "Mensola a valle", "a", "m", "Geometria", .8, .05, 15),
        new("Heel", "Mensola a monte", "b", "m", "Geometria", 1.9, .05, 15),
        new("Gc", "Peso specifico calcestruzzo", "γc", "kN/m³", "Terreno e carichi", 25, 15, 30),
        new("Gs", "Peso specifico del riempimento", "γ", "kN/m³", "Terreno e carichi", 18, 10, 25),
        new("Phi", "Attrito del riempimento", "φ′", "°", "Terreno e carichi", 30, 10, 45),
        new("Q", "Sovraccarico uniforme variabile", "qk", "kPa", "Terreno e carichi", 10, 0, 200),
        new("Gf", "Peso specifico terreno di posa", "γf", "kN/m³", "Fondazione", 19, 10, 25),
        new("Phif", "Attrito del terreno di posa", "φ′f", "°", "Fondazione", 34, 10, 45),
        new("Delta", "Attrito alla base", "δb", "°", "Fondazione", 26, 0, 45)
    ];
    public static Input Example() => new() { Title = "Esempio dimostrativo · H = 3 m", Values = Fields.ToDictionary(f => f.Key, f => f.Default.ToString(CultureInfo.InvariantCulture)) };
    public Dictionary<string, double> Parse()
    {
        if (Format != "ANTHEA.Muro" || Version != 1 || Values is null) throw new ArgumentException("Formato o versione del file non supportati.");
        var result = new Dictionary<string, double>();
        foreach (var f in Fields)
        {
            if (!Values.TryGetValue(f.Key, out var text) || string.IsNullOrWhiteSpace(text)) throw new ArgumentException($"Compilare: {f.Label}.");
            // Both Italian decimal commas and invariant decimal points; thousands separators are disallowed.
            if (!double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !double.IsFinite(v)) throw new ArgumentException($"Valore numerico non valido: {f.Label}.");
            if (v < f.Min || v > f.Max) throw new ArgumentException($"{f.Label}: intervallo del modello {f.Min}–{f.Max} {f.Unit}.");
            result[f.Key] = v;
        }
        if (result["Delta"] > result["Phif"]) throw new ArgumentException("L’attrito alla base non può superare quello del terreno di posa.");
        if (result["Delta"] < result["Phif"] / 2) throw new ArgumentException("Il modello di portanza richiede base ruvida: δb ≥ φ′f / 2.");
        return result;
    }
}

public sealed record LoadCase(string Name, double ConcreteFactor, double SoilFactor, double LiveFactor,
    double Horizontal, double Vertical, double Stabilizing, double Overturning, double X, double Eccentricity,
    double EffectiveWidth, double ContactWidth, double PressureToe, double PressureHeel, double MaxPressure,
    double SlidingResistance, double OverturningResistance, double BearingResistance, double StemMoment,
    double StemShear, double ToeMoment, double ToeShear, double HeelMoment, double HeelShear)
{
    public double? SlidingRatio => Ratio(Horizontal, SlidingResistance);
    public double? OverturningRatio => Ratio(Overturning, OverturningResistance);
    public double? BearingRatio => Ratio(Vertical, BearingResistance);
    private static double? Ratio(double e, double r) => r > 0 ? e / r : null;
}
public record Check(string Name, string Case, double Demand, double Resistance, string Unit, double? Ratio, string Status);
public record Result(Input Input, double Width, double Ka, double Nq, double Ngamma, LoadCase Characteristic,
    List<LoadCase> Cases, List<Check> Checks, List<string> Notes);

public static class Engine
{
    public const string Scope = "Modello statico per metro di muro: fusto verticale a spessore costante, riempimento orizzontale granulare asciutto, c′ = 0; fondazione nastriforme orizzontale su terreno omogeneo, senza incasso né terreno sulla mensola a valle. Spinta attiva mobilitabile. Nessuna resistenza passiva.";
    public const string Exclusions = "Non comprende sisma, falda e sottospinta, stratificazioni, stabilità globale, cedimenti, verifiche SLE o resistenza e armature del c.a. Le sollecitazioni di fusto e mensole sono fornite per il successivo dimensionamento strutturale. Gli esiti locali non attestano la verifica completa dell’opera.";
    public const string Factors = "Inviluppo di 8 combinazioni A1 + M1 + R3: γG,c e γG,terra = 1,00 / 1,30; γQ = 0 / 1,50. Lo stesso coefficiente del terreno agisce sul peso e sulla spinta; q agisce anche sulla mensola a monte. γR: scorrimento 1,10; ribaltamento 1,15; portanza 1,40.";
    public static Result Calculate(Input input)
    {
        var v = input.Parse();
        double h=v["H"], t=v["T"], d=v["D"], a=v["Toe"], b=v["Heel"], gc=v["Gc"], gs=v["Gs"], q=v["Q"], width=a+t+b;
        double rad=Math.PI/180, ka=Math.Pow(Math.Tan(Math.PI/4-v["Phi"]*rad/2),2), phif=v["Phif"]*rad;
        double nq=Math.Exp(Math.PI*Math.Tan(phif))*Math.Pow(Math.Tan(Math.PI/4+phif/2),2), ng=2*(nq-1)*Math.Tan(phif);
        LoadCase CalculateCase(string name, double fc, double fs, double fq, bool design)
        {
            double ht=h+d, earth=.5*ka*gs*ht*ht*fs, surcharge=ka*q*ht*fq, horizontal=earth+surcharge;
            double slab=width*d*gc*fc, stem=t*h*gc*fc, soil=b*h*gs*fs, live=b*q*fq;
            double n=slab+stem+soil+live, ms=slab*width/2+stem*(a+t/2)+(soil+live)*(a+t+b/2), mo=earth*ht/3+surcharge*ht/2;
            double x=(ms-mo)/n, e=width/2-x, be=Math.Max(0,width-2*Math.Abs(e));
            var contact=Contact(width,n,x);
            double sliding=n*Math.Tan(v["Delta"]*rad)/(design?1.1:1);
            double ig=Math.Pow(Math.Max(0,1-horizontal/n),3);
            double bearing=.5*v["Gf"]*be*be*ng*ig/(design?1.4:1);
            // Exact integration of each linear piece of the compression-only contact law.
            (double Force,double Moment) Integral(double left,double right,double root,double down)
            {
                var breaks=new List<double>{left,right};
                if(contact.Start>left && contact.Start<right)breaks.Add(contact.Start);
                if(contact.End>left && contact.End<right)breaks.Add(contact.End);
                breaks.Sort(); double f=0,m=0;
                for(int i=0;i<breaks.Count-1;i++)
                {
                    double l=breaks[i],r=breaks[i+1],len=r-l;
                    double p0=Pressure(width,n,x,l)-down,p1=Pressure(width,n,x,r)-down;
                    f+=(p0+p1)*len/2;
                    m+=(l-root)*(p0+p1)*len/2+len*len*(p0+2*p1)/6;
                }
                return(f,m);
            }
            var toe=Integral(0,a,a,d*gc*fc); var heel=Integral(a+t,width,a+t,d*gc*fc+h*gs*fs+q*fq);
            return new(name,fc,fs,fq,horizontal,n,ms,mo,x,e,be,contact.End-contact.Start,
                Pressure(width,n,x,0),Pressure(width,n,x,width),contact.Peak,sliding,ms/(design?1.15:1),bearing,
                ka*gs*fs*h*h*h/6+ka*q*fq*h*h/2, .5*ka*gs*fs*h*h+ka*q*fq*h,
                -toe.Moment,toe.Force,heel.Moment,-heel.Force);
        }
        var cases=new List<LoadCase>();
        foreach(double fc in new[]{1.0,1.3}) foreach(double fs in new[]{1.0,1.3}) foreach(double fq in new[]{0.0,1.5})
            cases.Add(CalculateCase($"C{cases.Count+1}",fc,fs,fq,true));
        var checks=new List<Check>();
        void Add(string name,Func<LoadCase,double> demand,Func<LoadCase,double> resistance,string unit)
        {
            var c=cases.MaxBy(c=>resistance(c)>0?demand(c)/resistance(c):double.PositiveInfinity)!;
            double r=resistance(c), ed=demand(c); double? ratio=r>0?ed/r:null;
            checks.Add(new(name,c.Name,ed,r,unit,ratio,ratio is <=1?"Soddisfatta":"Non soddisfatta"));
        }
        Add("Scorrimento",c=>c.Horizontal,c=>c.SlidingResistance,"kN/m");
        Add("Ribaltamento",c=>c.Overturning,c=>c.OverturningResistance,"kNm/m");
        Add("Capacità portante",c=>c.Vertical,c=>c.BearingResistance,"kN/m");
        var notes=new List<string>();
        if(cases.Any(c=>Math.Abs(c.Eccentricity)>width/3))
        {
            notes.Add("Eccentricità superiore a B/3 in almeno una combinazione: portanza fuori dal campo adottato, richiede analisi specifica.");
            checks[2]=checks[2] with {Status="Fuori campo"};
        }
        if(cases.Any(c=>c.ContactWidth<=0)) notes.Add("Risultante esterna alla base: equilibrio di contatto assente. Sollecitazioni delle mensole non utilizzabili.");
        else if(cases.Any(c=>c.ContactWidth<width-1e-9)) notes.Add("Contatto parziale in almeno una combinazione: pressioni triangolari sulla sola zona compressa.");
        notes.Add("La portanza non considera il confinamento laterale (q′ = 0). Il terreno a monte arriva fino al piano di posa sul piano virtuale al bordo del tallone: altezza della spinta esterna H + t.");
        return new(input,width,ka,nq,ng,CalculateCase("Caratteristica",1,1,1,false),cases,checks,notes);
    }
    public static (double Start,double End,double Peak) Contact(double b,double n,double x)
    {
        if(x<=0 || x>=b)return(0,0,0);
        if(x<b/3)return(0,3*x,2*n/(3*x));
        if(x>2*b/3)return(b-3*(b-x),b,2*n/(3*(b-x)));
        return(0,b,n/b*(1+6*Math.Abs(b/2-x)/b));
    }
    public static double Pressure(double b,double n,double x,double at)
    {
        if(x<=0 || x>=b)return 0;
        if(x<b/3)return at<=3*x?Math.Max(0,2*n/(3*x)*(1-at/(3*x))):0;
        if(x>2*b/3){double len=3*(b-x);return at>=b-len?Math.Max(0,2*n/len*(at-(b-len))/len):0;}
        return Math.Max(0,n/b*(1+6*(b/2-x)/b*(1-2*at/b)));
    }
    public static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true};
    public static void Save(string path,Input input)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(input,JsonOptions);
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllBytes(temp,bytes);File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public static Input Open(string path)
    {
        var i=JsonSerializer.Deserialize<Input>(File.ReadAllText(path),JsonOptions)??throw new ArgumentException("File vuoto.");
        if(i.Format!="ANTHEA.Muro"||i.Version!=1||i.Values is null)throw new ArgumentException("File non compatibile con il modulo muro.");
        return i;
    }
}
