using System.Windows;
using System.Windows.Controls;

namespace Materiali;

public sealed partial class MaterialWindow
{
    readonly ComboBox exposureSelector=new(){MinWidth=72,VerticalAlignment=VerticalAlignment.Top};
    readonly TextBlock exposureAggressiveness=Text("",14,true),combinedExposure=Text("",12);
    readonly TextBlock exposureExamples=Text("",12);
    bool updatingExposure;
    // ATECAP 2020, Vademecum del calcestruzzo, pagina 7, Prospetto 1: descrizione dell'ambiente.
    static readonly Dictionary<string,string> ExposureDescriptions=new()
    {
        ["X0"]="Per calcestruzzo privo di armatura o inserti metallici: tutte le esposizioni eccetto dove c’è gelo e disgelo, abrasione o attacco chimico. Calcestruzzi con armatura o inserti metallici: in ambiente molto asciutto.",
        ["XC1"]="Permanentemente secco, acquoso o saturo d’acqua",
        ["XC2"]="Prevalentemente acquoso o saturo d’acqua, raramente secco",
        ["XC3"]="Moderata o alta umidità dell’aria",
        ["XC4"]="Ciclicamente secco e acquoso o saturo d’acqua",
        ["XD1"]="Moderata umidità dell’aria",
        ["XD2"]="Prevalentemente acquoso o saturo d’acqua, raramente secco",
        ["XD3"]="Ciclicamente secco e acquoso o saturo d’acqua",
        ["XS1"]="Aria che trasporta salsedine marina in assenza di contatto con l’acqua di mare",
        ["XS2"]="Acqua di mare",
        ["XS3"]="Aree soggette a marea, moto ondoso, spruzzi di acqua di mare",
        ["XF1"]="Condizioni che determinano una moderata saturazione del calcestruzzo, in assenza di agente disgelante",
        ["XF2"]="Condizioni che determinano una moderata saturazione del calcestruzzo in presenza di agente disgelante",
        ["XF3"]="Condizioni che determinano una elevata saturazione del calcestruzzo in assenza di agente disgelante",
        ["XF4"]="Condizioni che determinano una elevata saturazione del calcestruzzo con presenza di agente antigelo oppure acqua di mare",
        ["XA1"]="Ambiente chimicamente debolmente aggressivo",
        ["XA2"]="Ambiente chimicamente moderatamente aggressivo",
        ["XA3"]="Ambiente chimicamente fortemente aggressivo"
    };
    // ATECAP 2020, pagina 7, Prospetto 1: esempi informativi.
    static readonly Dictionary<string,string> ExposureExamples=new()
    {
        ["X0"]="Calcestruzzo all’interno di edifici con umidità relativa dell’aria molto bassa. Calcestruzzo non armato all’interno di edifici.\nCalcestruzzo non armato immerso in suolo non aggressivo o in acqua non aggressiva.\nCalcestruzzo non armato soggetto a cicli di bagnato asciutto ma non soggetto ad abrasione, gelo o attacco chimico.",
        ["XC1"]="Calcestruzzo all’interno di edifici con umidità relativa dell’aria bassa.\nCalcestruzzo permanentemente immerso in acqua o esposto a condensa.",
        ["XC2"]="Calcestruzzo a contatto con acqua per lungo tempo.\nCalcestruzzo di strutture di contenimento acqua.\nCalcestruzzo di molte fondazioni.",
        ["XC3"]="Calcestruzzo in esterni con superfici esterne riparate dalla pioggia, o in interni con umidità dell’aria da moderata ad alta.",
        ["XC4"]="Calcestruzzo in esterni con superfici soggette a alternanze di ambiente secco ed acquoso o saturo d’acqua.\nCalcestruzzo ciclicamente esposto all’acqua in condizioni che non ricadono nella classe XC2.",
        ["XD1"]="Calcestruzzo per ponti, viadotti o barriere stradali esposto all’azione aggressiva dei cloruri trasportati dall’aria ad esempio derivanti dall’uso di sali disgelanti.",
        ["XD2"]="Calcestruzzo per impianti di trattamento acque o esposto ad acque contenenti cloruri, ad esempio acque industriali o di piscine.",
        ["XD3"]="Calcestruzzo esposto a spruzzi di soluzioni di cloruri, ad esempio derivanti da sali disgelanti.\nCalcestruzzo di opere accessorie stradali (muri di sostegno), parti di ponti, pavimentazioni stradali o industriali o di parcheggi.",
        ["XS1"]="Calcestruzzo per strutture in zone costiere.",
        ["XS2"]="Calcestruzzo di parti di strutture marine completamente immerse in acqua.",
        ["XS3"]="Calcestruzzo di opere portuali, ad esempio banchine, moli, pontili.\nCalcestruzzo di opere di difesa marittima, ad esempio barriere frangiflutti, dighe foranee.",
        ["XF1"]="Calcestruzzo di facciate, colonne o elementi strutturali verticali o inclinati esposti alla pioggia ed ai cicli di gelo/disgelo.",
        ["XF2"]="Calcestruzzo di facciate, colonne o elementi strutturali verticali o inclinati esposti alla pioggia ed ai cicli di gelo/disgelo in presenza di sali disgelanti, ad esempio opere stradali esposte al gelo in presenza di sali disgelanti trasportati dall’aria.",
        ["XF3"]="Calcestruzzo di elementi orizzontali in edifici dove possono aver luogo accumuli d’acqua.",
        ["XF4"]="Calcestruzzo di elementi orizzontali, di strade o pavimentazioni, esposti al gelo ed ai sali disgelanti oppure esposti al gelo in zone costiere.",
        ["XA1"]="Calcestruzzo esposto a terreno naturale e acqua del terreno con caratteristiche chimiche del prospetto 2 della UNI EN 206.",
        ["XA2"]="Calcestruzzo esposto a terreno naturale e acqua del terreno con caratteristiche chimiche del prospetto 2 della UNI EN 206.",
        ["XA3"]="Calcestruzzo esposto a terreno naturale e acqua del terreno con caratteristiche chimiche del prospetto 2 della UNI EN 206."
    };
    static string Aggressiveness(int severity)=>severity switch{0=>"ordinaria",1=>"aggressiva",2=>"molto aggressiva",_=>throw new ArgumentOutOfRangeException(nameof(severity))};
    UIElement BuildExposureSelector(UIElement checks)
    {
        exposureSelector.ItemsSource=Durability.Exposures.Select(e=>e.Code).ToArray();
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
            Text("Descrizione ed esempi: ATECAP 2020, p. 7, prospetto 1.\nAggressività: NTC 2018, tab. 4.1.III.",11),
            new Expander{Header="Esposizioni concomitanti",Margin=new Thickness(0,6,0,0),Content=Stack(Text("Per più esposizioni sulla stessa superficie. Una nuova scelta nel menu riparte dalla sola classe selezionata.",12),checks)}));
    }
    void RefreshExposureSelector(Exposure[] active)
    {
        updatingExposure=true;
        try
        {
            string? selected=exposureSelector.SelectedItem?.ToString();
            if(!active.Any(e=>e.Code==selected)){selected=active.FirstOrDefault()?.Code;exposureSelector.SelectedItem=selected;}
            exposureDetails.Text=selected is null?"Selezionare una classe di esposizione.":ExposureDescriptions[selected];
            exposureExamples.Text=selected is null?"":ExposureExamples[selected];
            exposureAggressiveness.Text=selected is null?"Aggressività: da definire":"Aggressività: "+Aggressiveness(NtcCover.Severity(selected));
            combinedExposure.Text=active.Length>1?"Classi attive: "+string.Join(" + ",active.Select(e=>e.Code))+
                (active.Any(e=>e.Code=="X0")?" · combinazione non valida: X0 deve essere isolata.":"\nAggressività complessiva: "+Aggressiveness(active.Max(e=>NtcCover.Severity(e.Code)))):"";
            combinedExposure.Visibility=active.Length>1?Visibility.Visible:Visibility.Collapsed;
        }
        finally{updatingExposure=false;}
    }
    void CheckExposureSelector()
    {
        foreach(var (codes,expected) in new[]{("X0 XC1 XC2 XC3 XF1","ordinaria"),("XC4 XD1 XS1 XA1 XA2 XF2 XF3","aggressiva"),("XD2 XD3 XS2 XS3 XA3 XF4","molto aggressiva")})
            foreach(var code in codes.Split(' '))
            {
                exposureSelector.SelectedItem=code;
                if(Active().Length!=1||Active()[0].Code!=code||exposureDetails.Text!=ExposureDescriptions[code]||exposureExamples.Text!=ExposureExamples[code]||string.IsNullOrWhiteSpace(exposureExamples.Text)||exposureAggressiveness.Text!="Aggressività: "+expected)
                    throw new Exception("Menu/descrizione/aggressività non aggiornati: "+code);
            }
        exposureSelector.SelectedItem="XC1";exposureChecks["XS3"].IsChecked=true;
        if(!combinedExposure.Text.Contains("molto aggressiva")||numbers["ntcCmin"].Text!="35")throw new Exception("Combinazione non aggiornata.");
        exposureSelector.SelectedItem="XC4";
        if(Active().Length!=1||numbers["ntcCmin"].Text!="30")throw new Exception("Cambio menu non propagato al calcolo.");
        exposureSelector.SelectedItem="XC1";
    }
}
