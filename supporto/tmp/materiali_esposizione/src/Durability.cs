namespace Materiali;

public record Exposure(string Code, string Description, double? MaxRatio, int MinStrength, int MinCement, double MinAir, int CoverColumn);
public record CoverInput(int Life, bool StrengthReduction, bool Slab, bool Quality, double Diameter, double Aggregate, double Deviation, bool Rough, int Abrasion, int Ground);
public record CoverLine(string Exposure, int StructuralClass, double Durability);
public record CoverResult(double Bond, double Durability, double Minimum, double Nominal, CoverLine[] Lines);

public static class Durability
{
    // UNI EN 206-1:2006, prospetti 1 e F.1. F.1 è informativo.
    public static readonly Exposure[] Exposures =
    [
        new("X0", "Assenza di rischio; per armature: ambiente molto asciutto", null,12,0,0,0),
        new("XC1", "Carbonatazione · asciutto o sempre bagnato", .65,20,260,0,1),
        new("XC2", "Carbonatazione · bagnato, raramente asciutto", .60,25,280,0,2),
        new("XC3", "Carbonatazione · umidità moderata", .55,30,280,0,2),
        new("XC4", "Carbonatazione · alternanza bagnato/asciutto", .50,30,300,0,3),
        new("XD1", "Cloruri non marini · umidità moderata", .55,30,300,0,4),
        new("XD2", "Cloruri non marini · bagnato, raramente asciutto", .55,30,300,0,5),
        new("XD3", "Cloruri non marini · alternanza bagnato/asciutto", .45,35,320,0,6),
        new("XS1", "Cloruri marini · aerosol, senza contatto diretto", .50,30,300,0,4),
        new("XS2", "Cloruri marini · immersione permanente", .45,35,320,0,5),
        new("XS3", "Cloruri marini · maree, spruzzi e onde", .45,35,340,0,6),
        new("XF1", "Gelo · saturazione moderata, senza disgelanti", .55,30,300,0,-1),
        new("XF2", "Gelo · saturazione moderata, con disgelanti", .55,25,300,4,-1),
        new("XF3", "Gelo · saturazione elevata, senza disgelanti", .50,30,320,4,-1),
        new("XF4", "Gelo · saturazione elevata, disgelanti o mare", .45,30,340,4,-1),
        new("XA1", "Attacco chimico · debole", .55,30,300,0,-1),
        new("XA2", "Attacco chimico · moderato", .50,30,320,0,-1),
        new("XA3", "Attacco chimico · forte", .45,35,360,0,-1)
    ];
    // EN 1992-1-1:2004, prospetto 4.4N: armatura ordinaria.
    static readonly int[,] Covers = {
        {10,10,10,15,20,25,30}, {10,10,15,20,25,30,35},
        {10,10,20,25,30,35,40}, {10,15,25,30,35,40,45},
        {15,20,30,35,40,45,50}, {20,25,35,40,45,50,55}
    };
    public static string Strength(int fck) => fck switch {12=>"C12/15",20=>"C20/25",25=>"C25/30",30=>"C30/37",35=>"C35/45",_=>"C"+fck};
    public static void ValidateExposure(Exposure[] values)
    {
        if(values.Length==0) throw new ArgumentException("Selezionare almeno una classe di esposizione.");
        if(values.Length>1 && values.Any(x=>x.Code=="X0")) throw new ArgumentException("X0 non è combinabile con altre esposizioni.");
    }
    public static int StructuralClass(Exposure e, double fck, CoverInput p)
    {
        // La soglia XD2 è C40/50, XS2 C45/55 (tabella 4.3N).
        int threshold=e.Code switch {"X0" or "XC1"=>30,"XC2" or "XC3"=>35,"XC4" or "XD1" or "XD2" or "XS1"=>40,_=>45};
        return Math.Max(1,4+(p.Life==100?2:0)-(p.StrengthReduction && fck>=threshold?1:0)-(p.Slab?1:0)-(p.Quality?1:0));
    }
    public static CoverResult Cover(Exposure[] values, double fck, CoverInput p)
    {
        ValidateExposure(values);
        if(!double.IsFinite(fck) || fck<12 || fck>90) throw new ArgumentException("Resistenza del calcestruzzo non valida.");
        if(p.Life is not (50 or 100) || !double.IsFinite(p.Diameter) || p.Diameter<=0 || !double.IsFinite(p.Aggregate) || p.Aggregate<=0 || !double.IsFinite(p.Deviation) || p.Deviation<0 || p.Abrasion is not (0 or 5 or 10 or 15) || p.Ground is not (0 or 40 or 75))
            throw new ArgumentException("Controllare diametro, Dmax, tolleranza e condizioni di getto.");
        var lines=values.Where(e=>e.CoverColumn>=0).Select(e=>new CoverLine(e.Code,StructuralClass(e,fck,p),Covers[StructuralClass(e,fck,p)-1,e.CoverColumn])).ToArray();
        if(lines.Length==0) throw new ArgumentException("Per il copriferro aggiungere l'esposizione alla corrosione (XC, XD o XS); XF/XA da sole non definiscono cmin,dur.");
        double bond=p.Diameter+(p.Aggregate>32?5:0),dur=lines.Max(x=>x.Durability);
        double min=Math.Max(10,Math.Max(bond,dur))+(p.Rough?5:0)+p.Abrasion;
        return new(bond,dur,min,Math.Max(min+p.Deviation,p.Ground),lines);
    }
    public static double EffectiveWater(double total,double absorbed)
    {
        if(!double.IsFinite(total)||!double.IsFinite(absorbed)||total<0||absorbed<0||absorbed>total) throw new ArgumentException("Acqua: richiedere 0 ≤ acqua assorbita ≤ acqua totale.");
        return total-absorbed;
    }
    public static void Check()
    {
        static void Equal(double a,double b) { if(Math.Abs(a-b)>1e-8) throw new Exception($"Atteso {b}, ottenuto {a}"); }
        static void Reject(Action action) { try { action(); } catch(ArgumentException) {return;} throw new Exception("Input non valido accettato."); }
        Exposure E(string code)=>Exposures.Single(x=>x.Code==code);
        var p=new CoverInput(50,false,false,false,16,20,10,false,0,0);
        Equal(Cover([E("XC1")],30,p).Nominal,26);
        Equal(Cover([E("XC4")],30,p).Nominal,40);
        Equal(Cover([E("XC4"),E("XS3"),E("XF4")],35,p).Nominal,55);
        Equal(Cover([E("XC1")],30,p with{Diameter=32,Aggregate=32}).Bond,32);
        Equal(Cover([E("XC1")],30,p with{Diameter=32,Aggregate=40}).Bond,37);
        Equal(Cover([E("XC1")],30,p with{Ground=75}).Nominal,75);
        Equal(Cover([E("XC4")],30,p with{Life=100}).Nominal,50);
        Equal(Cover([E("XC4")],30,p with{Rough=true,Abrasion=10}).Nominal,55);
        Equal(StructuralClass(E("XD2"),40,p with{StrengthReduction=true}),3);
        Equal(StructuralClass(E("XS2"),40,p with{StrengthReduction=true}),4);
        Equal(StructuralClass(E("XC1"),30,p with{StrengthReduction=true,Slab=true,Quality=true}),1);
        Equal(EffectiveWater(190,10)/360,.5);
        Reject(()=>Cover([E("XF4")],30,p)); Reject(()=>Cover([E("X0"),E("XC1")],30,p));
        Reject(()=>Cover([],30,p)); Reject(()=>EffectiveWater(10,20)); Reject(()=>EffectiveWater(double.NaN,0));
        Reject(()=>Cover([E("XC1")],30,p with{Diameter=-1}));
        Equal(Exposures.Length,18); Equal(E("XS3").MinCement,340); Equal(E("XD2").MaxRatio!.Value,.55); Equal(E("XA3").MinCement,360);
    }
}
