using System.Windows;
using System.Windows.Controls;

namespace Materiali;

public static class MinimumConcrete
{
    // ATECAP 2020, p. 19, Prospetto 5 UNI 11104: riga Minima classe di resistenza.
    public static int Fck(string code)=>code switch
    {
        "X0"=>12,
        "XC1" or "XC2" or "XF2" or "XF3"=>25,
        "XC3" or "XD1" or "XF4" or "XA1"=>30,
        "XC4" or "XS1" or "XD2" or "XF1" or "XA2"=>32,
        "XS2" or "XS3" or "XD3" or "XA3"=>35,
        _=>throw new ArgumentException("Classe di esposizione non riconosciuta.")
    };
    public static int Required(Exposure[] exposures)
    {
        Durability.ValidateExposure(exposures);return exposures.Max(e=>Fck(e.Code));
    }
    public static string Label(int fck)=>fck switch {12=>"C12/15",25=>"C25/30",30=>"C30/37",32=>"C32/40",35=>"C35/45",_=>throw new ArgumentException("Classe minima non riconosciuta.")};
}

public sealed partial class MaterialWindow
{
    readonly TextBox minimumConcreteClass=new(){IsReadOnly=true,IsReadOnlyCaretVisible=false,Background=Brush("#EDF5FF"),FontWeight=FontWeights.SemiBold};
    readonly TextBlock minimumConcreteNote=Text("",12);
    void RefreshMinimumConcrete(Exposure[] active)
    {
        try
        {
            minimumConcreteClass.Text=MinimumConcrete.Label(MinimumConcrete.Required(active));
            minimumConcreteNote.Text="ATECAP 2020, p. 19, prospetto 5 UNI 11104. Per più esposizioni si adotta la classe minima più elevata.";
            if(active.Any(e=>e.Code.StartsWith("XF")))minimumConcreteNote.Text+=" XF2/XF3/XF4: valori del prospetto con aria inglobata; per l’alternativa senza aria occorrono prove prestazionali (nota a). XF1: valore ordinario senza aria aggiunta; l’alternativa aerata della nota b richiede una specifica dedicata.";
        }
        catch(ArgumentException ex){minimumConcreteClass.Text="Da definire";minimumConcreteNote.Text=ex.Message;}
    }
}
