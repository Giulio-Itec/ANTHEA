using System.Windows;
using System.Windows.Controls;

namespace Materiali;

public sealed partial class MaterialView
{
    readonly ComboBox exposureSelector=new(){MinWidth=72,VerticalAlignment=VerticalAlignment.Top};
    readonly TextBlock exposureAggressiveness=Text("",14,true),combinedExposure=Text("",12);
    readonly TextBlock exposureExamples=Text("",12);
    bool updatingExposure;
    // Testi di ANTHEA sulle classi di esposizione di UNI EN 206, prospetto 1 (decisione U5 del 14/10/2026, voce F4-47):
    // descrizione dell'ambiente.
    static readonly Dictionary<string,string> ExposureDescriptions=new()
    {
        ["X0"]="Nessun rischio di corrosione o di attacco. Con armature o inserti metallici: solo ambiente molto asciutto. Senza armature: qualsiasi ambiente privo di gelo, abrasione e attacco chimico.",
        ["XC1"]="Corrosione da carbonatazione: ambiente sempre asciutto oppure sempre bagnato.",
        ["XC2"]="Corrosione da carbonatazione: ambiente bagnato, raramente asciutto.",
        ["XC3"]="Corrosione da carbonatazione: umidità dell’aria moderata o elevata.",
        ["XC4"]="Corrosione da carbonatazione: alternanza di periodi asciutti e bagnati.",
        ["XD1"]="Corrosione da cloruri non marini: umidità dell’aria moderata.",
        ["XD2"]="Corrosione da cloruri non marini: ambiente bagnato, raramente asciutto.",
        ["XD3"]="Corrosione da cloruri non marini: alternanza di periodi asciutti e bagnati.",
        ["XS1"]="Corrosione da cloruri marini: salsedine portata dall’aria, senza contatto con l’acqua di mare.",
        ["XS2"]="Corrosione da cloruri marini: immersione permanente in acqua di mare.",
        ["XS3"]="Corrosione da cloruri marini: zone di marea, di onda e di spruzzi.",
        ["XF1"]="Gelo e disgelo: saturazione d’acqua moderata, senza sali disgelanti.",
        ["XF2"]="Gelo e disgelo: saturazione d’acqua moderata, con sali disgelanti.",
        ["XF3"]="Gelo e disgelo: saturazione d’acqua elevata, senza sali disgelanti.",
        ["XF4"]="Gelo e disgelo: saturazione d’acqua elevata, con sali disgelanti o acqua di mare.",
        ["XA1"]="Attacco chimico debole da terreni o acque.",
        ["XA2"]="Attacco chimico moderato da terreni o acque.",
        ["XA3"]="Attacco chimico forte da terreni o acque."
    };
    // Testi di ANTHEA: esempi di elementi tipici, da confermare sul progetto.
    static readonly Dictionary<string,string> ExposureExamples=new()
    {
        ["X0"]="Elementi armati in interni molto asciutti.\nElementi non armati in interni, interrati o immersi in terreni e acque non aggressivi.",
        ["XC1"]="Elementi in interni con bassa umidità.\nElementi sempre immersi in acqua.",
        ["XC2"]="Fondazioni e superfici a contatto con l’acqua per lunghi periodi.\nVasche e serbatoi d’acqua.",
        ["XC3"]="Elementi in interni con umidità da moderata a elevata.\nSuperfici esterne riparate dalla pioggia.",
        ["XC4"]="Superfici esterne bagnate dalla pioggia che si asciugano fra un evento e l’altro, fuori dai casi XC2.",
        ["XD1"]="Elementi raggiunti dai cloruri portati dall’aria, per esempio vicino a strade trattate con sali disgelanti.",
        ["XD2"]="Vasche di impianti di trattamento delle acque, piscine, elementi a contatto con acque industriali contenenti cloruri.",
        ["XD3"]="Elementi raggiunti da spruzzi di acque con sali disgelanti: parti di ponti, muri di sostegno stradali, pavimentazioni di strade e parcheggi.",
        ["XS1"]="Strutture vicine alla costa senza contatto con l’acqua di mare.",
        ["XS2"]="Parti di opere marittime sempre sommerse.",
        ["XS3"]="Banchine, moli, pontili e opere di difesa costiera nella zona di marea e di spruzzi.",
        ["XF1"]="Superfici verticali esposte alla pioggia e al gelo, senza sali disgelanti.",
        ["XF2"]="Superfici verticali di opere stradali esposte al gelo e ai sali disgelanti portati dall’aria.",
        ["XF3"]="Superfici orizzontali esposte alla pioggia e al gelo dove l’acqua può ristagnare, senza sali disgelanti.",
        ["XF4"]="Impalcati, strade e pavimentazioni esposti al gelo e ai sali disgelanti.\nSuperfici esposte al gelo in zona costiera.",
        ["XA1"]="Elementi a contatto con terreni o acque di falda debolmente aggressivi: grado di attacco dall’analisi chimica, UNI EN 206 prospetto 2.",
        ["XA2"]="Elementi a contatto con terreni o acque di falda moderatamente aggressivi: grado di attacco dall’analisi chimica, UNI EN 206 prospetto 2.",
        ["XA3"]="Elementi a contatto con terreni o acque di falda fortemente aggressivi: grado di attacco dall’analisi chimica, UNI EN 206 prospetto 2."
    };
    static string Aggressiveness(int severity)=>severity switch{0=>"ordinaria",1=>"aggressiva",2=>"molto aggressiva",_=>throw new ArgumentOutOfRangeException(nameof(severity))};
    UIElement BuildExposureSelector(UIElement checks)
    {
        exposureSelector.ItemsSource=MaterialSheetInput.ExposureChoices.Select(e=>e.Code).ToArray();
        exposureSelector.SelectedItem="XC1";
        System.Windows.Automation.AutomationProperties.SetName(exposureSelector,"Classe di esposizione");
        var row=new Grid{Margin=new Thickness(0,5,0,8)};
        row.ColumnDefinitions.Add(new(){Width=new GridLength(80)});row.ColumnDefinitions.Add(new());
        var description=Stack(Text("Descrizione dell’ambiente",12,true),exposureDetails,Text("Esempi informativi",12,true),exposureExamples);
        description.Margin=new Thickness(12,0,0,0);
        row.Children.Add(exposureSelector);Grid.SetColumn(description,1);row.Children.Add(description);
        exposureSelector.SelectionChanged+=(_,_)=>
        {
            if(updatingExposure)return;
            updatingExposure=true;
            try{foreach(var (code,check) in exposureChecks)check.IsChecked=code==exposureSelector.SelectedItem?.ToString();}
            finally{updatingExposure=false;}
            RefreshDetails();
        };
        return Paper(Stack(Text("Classe di esposizione",15,true),row,exposureAggressiveness,combinedExposure,
            Text("Descrizioni ed esempi di ANTHEA secondo la classificazione di UNI EN 206, prospetto 1.\nAggressività: NTC 2018, tab. 4.1.III.",11)));
    }
    void RefreshExposureSelector(MaterialExposureChoice[] active)
    {
        updatingExposure=true;
        try
        {
            string? selected=exposureSelector.SelectedItem?.ToString();
            if(!active.Any(e=>e.Code==selected)){selected=active.FirstOrDefault()?.Code;exposureSelector.SelectedItem=selected;}
            exposureDetails.Text=selected is null?"Selezionare una classe di esposizione.":ExposureDescriptions[selected];
            exposureExamples.Text=selected is null?"":ExposureExamples[selected];
            exposureAggressiveness.Text=selected is null?"Aggressività: da definire":"Aggressività: "+Aggressiveness(MaterialSheetInput.ExposureChoices.Single(e=>e.Code==selected).Severity);
            combinedExposure.Text=active.Length>1?"Classi attive: "+string.Join(" + ",active.Select(e=>e.Code))+
                (active.Any(e=>e.Code=="X0")?" · combinazione non valida: X0 deve essere isolata.":"\nAggressività complessiva: "+Aggressiveness(active.Max(e=>e.Severity))):"";
            if (legacyExposures is not null && active.Length == 1)
                combinedExposure.Text="Archivio precedente con più esposizioni: " + string.Join(" + ", legacyExposures.Select(v=>v?.ToString())) + ". Classe attiva unica: " + selected + ".";
            combinedExposure.Visibility=active.Length>1 || legacyExposures is not null?Visibility.Visible:Visibility.Collapsed;
        }
        finally{updatingExposure=false;}
    }
}

