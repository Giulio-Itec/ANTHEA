namespace Materiali;

public record NtcCoverResult(string Environment,int Severity,double Cmin,double C0,double TableCover,double LifeExtra,double LowStrengthExtra,double QualityReduction,CoverResult Cover);
public static class NtcCover
{
    public static int Severity(string code)=>code switch
    {
        "X0" or "XC1" or "XC2" or "XC3" or "XF1"=>0,
        "XC4" or "XD1" or "XS1" or "XA1" or "XA2" or "XF2" or "XF3"=>1,
        "XD2" or "XD3" or "XS2" or "XS3" or "XA3" or "XF4"=>2,
        _=>throw new ArgumentException("Esposizione non riconosciuta.")
    };
    public static double DefaultCmin(Exposure[] values)
    {
        Durability.ValidateExposure(values);
        return 25+5*values.Max(e=>Severity(e.Code));
    }
    public static NtcCoverResult Calculate(Exposure[] values,double fck,CoverInput p,bool plate,bool coverQuality,double? pertinentCmin=null)
    {
        Durability.ValidateExposure(values);
        // Convalida geometria e parametri tramite il ramo EC2, senza usarne la durabilità.
        _=Durability.Cover([Durability.Exposures[0]],fck,p);
        int severity=values.Max(e=>Severity(e.Code));
        double c0=35+5*severity,cmin=pertinentCmin??DefaultCmin(values);
        if(!double.IsFinite(cmin)||cmin<12||cmin>c0) throw new ArgumentException("Cmin pertinente: indicare fck tra 12 MPa e C0 = "+c0+" MPa.");
        double table=(plate?15:20)+10*severity+(fck<c0?5:0);
        double life=p.Life==100?10:0,low=fck<cmin?5:0,quality=coverQuality?5:0;
        double dur=table+life+low-quality;
        double bond=p.Diameter+(p.Aggregate>32?5:0);
        double minimum=Math.Max(10,Math.Max(bond,dur))+(p.Rough?5:0)+p.Abrasion;
        var result=new CoverResult(bond,dur,minimum,Math.Max(minimum+p.Deviation,p.Ground),[]);
        return new(new[]{"Ordinario","Aggressivo","Molto aggressivo"}[severity],severity,cmin,c0,table,life,low,quality,result);
    }
}
