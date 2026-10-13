using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Materiali;

// Limiti di composizione di AtecapMix: UNI 11104:2016, prospetto 5 (riportato in ATECAP 2020, p. 19); differenze rispetto alla
// UNI 11104:2025 in vigore (prospetto 6) nel commento di AtecapMix e nel testo di compositionSource.
public sealed partial class MaterialView
{
    readonly Dictionary<string,TextBox> compositionValues=new();
    readonly TextBlock compositionSource=Text("",12),compositionAirNote=Text("",12),compositionChlorideNote=Text(MaterialSheetPresentation.CompositionChlorideNote,12);
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
                Select("consistency","Consistenza al getto"),consistencyInfo)),
            Paper(Stack(Text("Definizione del cemento",15,true),
                Select("cement","Famiglia del cemento"),
                Numeric("cementName","Designazione / produttore",""),
                Select("cementClass","Classe del cemento"),
                Select("cementEarly","Resistenza iniziale"),compositionRequirements)),
            Paper(Stack(Text("Riferimenti e criteri",15,true),compositionSource)));
    }
}
