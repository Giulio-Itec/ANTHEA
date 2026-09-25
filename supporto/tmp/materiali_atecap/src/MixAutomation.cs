using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Materiali;

// ATECAP 2020 p. 19, prospetto 5 UNI 11104; celle unite trascritte per ogni esposizione.
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

public sealed partial class MaterialWindow
{
    readonly Dictionary<string,TextBox> compositionValues=new();
    readonly TextBlock compositionSource=Text("",12),compositionAirNote=Text("",12),compositionChlorideNote=Text("Cl 0,40: proposta per armatura ordinaria dagli esempi ATECAP, pp. 43–50; non è una classe dedotta dall’esposizione e va confermata nella prescrizione.",12);
    readonly TextBlock compositionRequirements=Text("",12);
    UIElement CompositionValue(string key,string label,string unit="")
    {
        var box=new TextBox{IsReadOnly=true,IsReadOnlyCaretVisible=false,Background=Brush("#EDF5FF"),Text="—"};
        compositionValues.Add(key,box);
        return Field(label,box,unit);
    }
    UIElement BuildComposition()
    {
        return Stack(
            Paper(Stack(Text("Prescrizione del calcestruzzo",15,true),
                Text("Valori aggiornati dalle esposizioni selezionate · ATECAP 2020",12),
                CompositionValue("chloride","Classe di contenuto in ioni cloruro"),
                compositionChlorideNote,
                CompositionValue("ratio","Rapporto A/C massimo"),
                CompositionValue("cement","Dosaggio minimo di cemento","kg/m³"),
                CompositionValue("air","Contenuto minimo di aria","%"),
                compositionAirNote,
                CompositionValue("dmax","Dmax aggregati (condiviso)","mm"),
                Text("Dmax ripreso dai dati del copriferro. Nella prescrizione corrisponde a Dupper (ATECAP, p. 21).",12),
                Select("consistency","Consistenza al getto",["Da definire","S1","S2","S3","S4","S5"]),consistencyInfo)),
            Paper(Stack(Text("Definizione del cemento",15,true),
                Select("cement","Famiglia del cemento",["CEM I","CEM II","CEM III","CEM IV","CEM V","Altro / da specificare"]),
                Numeric("cementName","Designazione / produttore","",""),
                Select("cementClass","Classe del cemento",["32,5","42,5","52,5"]),
                Select("cementEarly","Resistenza iniziale",["N","R"]),compositionRequirements)),
            Paper(Stack(Text("Riferimenti e criteri",15,true),compositionSource)));
    }
    void RefreshMix(Exposure[] active,double fck)
    {
        foreach(var box in compositionValues.Values)box.Text="Da definire";
        compositionValues["chloride"].Text="Cl 0,40";
        compositionAirNote.Text="";compositionRequirements.Text="";
        compositionSource.Text="ATECAP 2020, p. 19, prospetto 5 UNI 11104. Per esposizioni concomitanti: il minore A/C massimo e il maggiore dosaggio minimo. Valori senza applicazione del concetto k per le aggiunte (nota d).";
        if(choices["life"].SelectedIndex==1)compositionSource.Text+="\n\nVita utile 100 anni: i valori del documento sono riferimenti per 50 anni (p. 9); la prescrizione per 100 anni richiede una valutazione specifica.";
        string[] slump=["Da scegliere secondo elemento e modalità di getto (p. 21).","S1: 10–40 mm","S2: 50–90 mm","S3: 100–150 mm","S4: 160–210 mm","S5: > 220 mm (tabella ATECAP, p. 22)."];
        consistencyInfo.Text=slump[choices["consistency"].SelectedIndex];
        double? dmax=null;
        try{dmax=Read("aggregate","Dmax",.1,1000);compositionValues["dmax"].Text=dmax.Value.ToString("0.##",CultureInfo.GetCultureInfo("it-IT"));}
        catch(ArgumentException){compositionAirNote.Text="Correggere il Dmax nei dati del copriferro.";}
        try
        {
            var limits=AtecapMix.Required(active);
            compositionValues["ratio"].Text=limits.Ratio?.ToString("0.00",CultureInfo.GetCultureInfo("it-IT"))??"Non prescritto";
            compositionValues["cement"].Text=limits.Cement?.ToString()??"Non prescritto";
            bool air=active.Any(e=>e.Code is "XF2" or "XF3" or "XF4");
            if(!air)compositionValues["air"].Text="Non prescritto";
            else if(dmax is double d)
            {
                var minimum=AtecapMix.Air(active,d);
                compositionValues["air"].Text=minimum?.ToString("0.0",CultureInfo.GetCultureInfo("it-IT"))??"Da definire";
                compositionAirNote.Text=d>20?"Nota a: minimo 4% per Dupper > 20 mm.":d>=12&&d<=16?"Nota a: 5% è l’esempio indicato per Dupper da 12 a 16 mm; da confermare nella specifica.":"Nota a: il 4% è indicato per Dupper > 20 mm. Per questo Dmax il documento non fornisce un minimo numerico univoco: definire il contenuto d’aria nella specifica.";
                compositionAirNote.Text+=" L’alternativa senza aria inglobata richiede prove prestazionali di resistenza al gelo/disgelo.";
            }
            if(active.Any(e=>e.Code=="XF1"))compositionRequirements.Text+="XF1: tabella per calcestruzzo senza aria aggiunta; per la variante aerata valgono le specifiche XF2/XF3 (nota b).\n";
            if(active.Any(e=>e.Code.StartsWith("XF")))compositionRequirements.Text+="Aggregati di adeguata resistenza al gelo/disgelo secondo UNI EN 12620.\n";
            if(active.Any(e=>e.Code.StartsWith("XS")))compositionRequirements.Text+="Cemento resistente all’acqua di mare secondo UNI 9156.\n";
            if(active.Any(e=>e.Code.StartsWith("XA")))compositionRequirements.Text+="In presenza di solfati: cemento resistente ai solfati, classe da scegliere secondo UNI 11417-1 (nota c).\n";
            compositionSource.Text+="\n\nEsposizioni: "+string.Join(" + ",active.Select(e=>e.Code))+". Classe minima: "+MinimumConcrete.Label(MinimumConcrete.Required(active))+".";
            if(fck<MinimumConcrete.Required(active))compositionSource.Text+="\nLa classe di calcestruzzo selezionata è inferiore al minimo richiesto.";
        }
        catch(ArgumentException ex){compositionSource.Text+="\n\n"+ex.Message;}
    }
    void CheckAutomaticMix()
    {
        void Expect(string key,string expected){if(compositionValues[key].Text!=expected)throw new Exception($"ATECAP {key}: {compositionValues[key].Text}, atteso {expected}");}
        numbers["aggregate"].Text="32";
        foreach(var (code,ratio,cement) in new[]{("X0","Non prescritto","Non prescritto"),("XC1","0,60","300"),("XC2","0,60","300"),("XC3","0,55","320"),("XC4","0,50","340"),("XS1","0,50","340"),("XS2","0,45","360"),("XS3","0,45","360"),("XD1","0,55","320"),("XD2","0,50","340"),("XD3","0,45","360"),("XA1","0,55","320"),("XA2","0,50","340"),("XA3","0,45","360"),("XF1","0,50","320"),("XF2","0,50","340"),("XF4","0,45","360"),("XF3","0,50","340")})
        {exposureSelector.SelectedItem=code;Expect("ratio",ratio);Expect("cement",cement);}
        Expect("air","4,0");numbers["aggregate"].Text="16";Expect("air","5,0");
        foreach(var d in new[]{"20","18","8"}){numbers["aggregate"].Text=d;Expect("air","Da definire");}
        numbers["aggregate"].Text="NaN";Expect("dmax","Da definire");Expect("air","Da definire");
        numbers["aggregate"].Text="32";exposureChecks["XS3"].IsChecked=true;Expect("ratio","0,45");Expect("cement","360");Expect("air","4,0");
        exposureSelector.SelectedItem="X0";Expect("ratio","Non prescritto");Expect("cement","Non prescritto");Expect("air","Non prescritto");
        exposureChecks["XC4"].IsChecked=true;Expect("ratio","Da definire");Expect("cement","Da definire");
        exposureSelector.SelectedItem="XC4";choices["life"].SelectedIndex=1;
        if(!compositionSource.Text.Contains("100 anni"))throw new Exception("Nota vita utile assente.");
        choices["cement"].SelectedIndex=1;Expect("cement","340");Expect("chloride","Cl 0,40");
        if(compositionValues.Values.Any(x=>!x.IsReadOnly))throw new Exception("Campo automatico modificabile.");
        choices["cement"].SelectedIndex=0;choices["life"].SelectedIndex=0;numbers["aggregate"].Text="20";exposureSelector.SelectedItem="XC1";
    }
}
