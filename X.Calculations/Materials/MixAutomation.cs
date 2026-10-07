namespace Materiali;

public static class AtecapMix
{
    // A/C massimo e dosaggio minimo di cemento: UNI 11104:2016, prospetto 5 (riportato in ATECAP 2020, p. 19), celle unite trascritte
    // per ogni esposizione. Il prospetto 6 della UNI 11104:2025 in vigore ha dosaggi minimi inferiori in tutte le classi e per XF1
    // A/C 0,55 e 300 kg/m³: i valori qui sono più restrittivi (registro D7-e, da decidere).
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
