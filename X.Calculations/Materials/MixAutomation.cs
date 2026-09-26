namespace Materiali;

public static class AtecapMix
{
    public static (double? Ratio,int? Cement) Limits(string code)=>code switch
    {
        "X0" => (null,null),
        "XC1" or "XC2" => (.60,300),
        "XC3" or "XD1" or "XA1" => (.55,320),
        "XC4" or "XS1" or "XD2" or "XA2" or "XF2" or "XF3" => (.50,340),
        "XF1" => (.50,320),
        "XS2" or "XS3" or "XD3" or "XF4" or "XA3" => (.45,360),
        _ => throw new ArgumentException("Esposizione non riconosciuta.")
    };
    public static (double? Ratio,int? Cement) Required(Exposure[] active)
    {
        Durability.ValidateExposure(active);
        return (active.Min(e=>Limits(e.Code).Ratio),active.Max(e=>Limits(e.Code).Cement));
    }
    public static double? Air(Exposure[] active,double dmax)
    {
        Durability.ValidateExposure(active);
        if(!double.IsFinite(dmax)||dmax<=0)throw new ArgumentException("Dmax non valido.");
        if(!active.Any(e=>e.Code is "XF2" or "XF3" or "XF4"))return null;
        return dmax>20?4:dmax>=12&&dmax<=16?5:null;
    }
}
