using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Materiali;

public sealed partial class MaterialWindow
{
    readonly Dictionary<string,CheckBox> mixAuto=new();
    readonly TextBlock mixAutoNotes=Text("",12);
    readonly TextBlock mixAutoState=Text("",12);
    bool updatingAutoMix;

    UIElement AutoNumeric(string key,string label,string unit)
    {
        var box=new TextBox { Text="",MinWidth=48 };
        var automatic=new CheckBox { Content="Auto",IsChecked=true,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(5,0,0,0),FontSize=11,
            ToolTip="Proposta aggiornata automaticamente. Scrivi nel campo per mantenere un valore manuale; riattiva Auto per ricalcolare." };
        numbers.Add(key,box);mixAuto.Add(key,automatic);
        var panel=new DockPanel();DockPanel.SetDock(automatic,Dock.Right);panel.Children.Add(automatic);panel.Children.Add(box);
        void ModeChanged()
        {
            box.Background=automatic.IsChecked==true?Brush("#EDF5FF"):Brushes.White;
            box.ToolTip=automatic.IsChecked==true?(key=="ntcCmin"?"Cmin tabellare per le esposizioni selezionate, usato nel calcolo del copriferro.":"Proposta automatica, modificabile. Non è un dosaggio qualificato né un dato misurato."):"Valore manuale: conservato al cambio delle altre scelte.";
            RefreshDetails();
        }
        automatic.Checked+=(_,_)=>ModeChanged();automatic.Unchecked+=(_,_)=>ModeChanged();
        box.TextChanged+=(_,_)=>
        {
            if(updatingAutoMix)return;
            updatingAutoMix=true;
            try{automatic.IsChecked=false;}finally{updatingAutoMix=false;}
            RefreshDetails();
        };
        ModeChanged();return Field(label,panel,unit);
    }

    void UpdateAutomaticMix(Exposure[] active)
    {
        updatingAutoMix=true;
        try
        {
            void Set(string key,double? number)
            {
                if(mixAuto[key].IsChecked!=true)return;
                numbers[key].Text=number?.ToString("0.###",CultureInfo.GetCultureInfo("it-IT"))??"";
            }
            double? Value(string key,double min=0)
            {
                // A missing or invalid prerequisite clears only its automatic dependants.
                var raw=numbers[key].Text.Trim().Replace(',','.');
                return double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)&&double.IsFinite(value)&&value>=min?value:null;
            }
            int consistency=choices["consistency"].SelectedIndex;
            Set("slump",consistency switch {1=>25,2=>70,3=>125,4=>185,5=>220,_=>null});
            string slumpNote=consistency==5?" S5: 220 mm è la soglia inferiore, non un valore medio.":" Abbassamento: centro dell’intervallo della classe selezionata (S1–S4).";
            bool validExposure=active.Length>0 && !(active.Length>1&&active.Any(e=>e.Code=="X0"));
            Set("ntcCmin",validExposure?NtcCover.DefaultCmin(active):null);
            var dmax=Value("aggregate",.1);
            bool applicable=validExposure && Selected("cement")=="CEM I" && dmax is >=20 and <=32 && choices["life"].SelectedIndex==0;
            if(!applicable)
            {
                Set("cementMass",null);Set("totalWater",null);Set("air",null);
                mixAutoNotes.Text="Proposte cemento/acqua/aria sospese: servono esposizioni compatibili, CEM I, Dmax 20–32 mm e vita utile 50 anni. I valori manuali restano invariati."+slumpNote;
                mixAutoState.Text="Proposte automatiche sospese: controllare le esposizioni e le ipotesi F.1. Valori manuali conservati.";
                return;
            }
            double? ratio=active.Where(e=>e.MaxRatio.HasValue).Select(e=>e.MaxRatio).Min();
            int minimum=active.Max(e=>e.MinCement);double minAir=active.Max(e=>e.MinAir);
            double? absorbed=Value("absorbedWater");
            if(mixAuto["cementMass"].IsChecked==true)
            {
                double? cement=minimum>0?minimum:null;
                if(mixAuto["totalWater"].IsChecked!=true && ratio is double limit)
                {
                    double? total=Value("totalWater");
                    cement=total is double t && absorbed is double a && t>=a
                        ?Math.Max(minimum,Math.Ceiling((t-a)/limit)):null;
                }
                Set("cementMass",cement);
            }
            double? actualCement=Value("cementMass",.001);
            // Round the water limit down, so the displayed proposal cannot exceed a/c max.
            Set("totalWater",ratio is double r && actualCement is double c && absorbed is double aw?Math.Floor((c*r+aw)*1000+1e-8)/1000:null);
            Set("air",minAir>0?minAir:null);
            mixAutoState.Text="Cemento minimo e acqua al limite a/c: proposte da confermare con il produttore. La consistenza non determina il fabbisogno d’acqua.";
            mixAutoNotes.Text="Proposte dai limiti informativi F.1: cemento minimo; acqua al rapporto a/c massimo. Con acqua manuale, cemento ≥ (atot − aass)/(a/c max), arrotondato in eccesso al kg/m³. Aria compilata solo dove esiste un minimo."+
                " Non sono una ricetta di produzione: resistenza e lavorabilità vanno confermate. Aggregati, prodotti commerciali e cloruri richiedono dati specifici."+slumpNote;
        }
        finally{updatingAutoMix=false;}
    }

    void CheckAutomaticMix()
    {
        void Equal(string key,double expected)
        {
            if(Math.Abs(Read(key,key)-expected)>1e-8)throw new Exception("Composizione automatica: "+key+" atteso "+expected+", trovato "+numbers[key].Text);
        }
        void Blank(string key){if(numbers[key].Text!="")throw new Exception("Composizione: atteso vuoto "+key);}
        foreach(var cb in exposureChecks.Values)cb.IsChecked=false;exposureChecks["XC1"].IsChecked=true;
        choices["cement"].SelectedIndex=0;choices["life"].SelectedIndex=0;numbers["aggregate"].Text="20";numbers["absorbedWater"].Text="0";
        foreach(var cb in mixAuto.Values)cb.IsChecked=true;RefreshDetails();
        Equal("cementMass",260);Equal("totalWater",169);Blank("air");Equal("ntcCmin",25);
        exposureChecks["XC4"].IsChecked=true;Equal("cementMass",300);Equal("totalWater",150);Equal("ntcCmin",30);
        numbers["cementMass"].Text="360";Equal("totalWater",180);
        exposureChecks["XS3"].IsChecked=true;Equal("cementMass",360);Equal("totalWater",162);Equal("ntcCmin",35);
        numbers["ntcCmin"].Text="28";exposureChecks["XC4"].IsChecked=false;Equal("ntcCmin",28);
        mixAuto["ntcCmin"].IsChecked=true;Equal("ntcCmin",35);
        if(mixAuto["cementMass"].IsChecked==true)throw new Exception("Modifica manuale non conservata.");
        numbers["totalWater"].Text="180";mixAuto["cementMass"].IsChecked=true;Equal("cementMass",400);Equal("totalWater",180);
        numbers["absorbedWater"].Text="9";Equal("cementMass",380);
        numbers["totalWater"].Text="";Blank("cementMass");
        numbers["totalWater"].Text="5";Blank("cementMass");
        mixAuto["totalWater"].IsChecked=true;Equal("cementMass",340);Equal("totalWater",162);
        numbers["cementMass"].Text="NaN";Blank("totalWater");mixAuto["cementMass"].IsChecked=true;
        numbers["cementMass"].Text="333,333";
        if((Read("totalWater","Acqua")-9)/333.333>.45)throw new Exception("Arrotondamento automatico superiore al limite a/c.");
        mixAuto["cementMass"].IsChecked=true;
        exposureChecks["XF4"].IsChecked=true;Equal("air",4);
        choices["consistency"].SelectedIndex=4;Equal("slump",185);
        numbers["slump"].Text="190";choices["consistency"].SelectedIndex=3;Equal("slump",190);
        mixAuto["slump"].IsChecked=true;Equal("slump",125);
        choices["consistency"].SelectedIndex=5;Equal("slump",220);
        choices["consistency"].SelectedIndex=0;Blank("slump");
        choices["cement"].SelectedIndex=1;Blank("cementMass");Blank("totalWater");Blank("air");Equal("ntcCmin",35);
        numbers["cementMass"].Text="355";choices["life"].SelectedIndex=1;Equal("cementMass",355);
        choices["cement"].SelectedIndex=0;choices["life"].SelectedIndex=0;mixAuto["cementMass"].IsChecked=true;
        numbers["aggregate"].Text="40";Blank("cementMass");numbers["aggregate"].Text="20";
        exposureChecks["X0"].IsChecked=true;Blank("cementMass");Blank("totalWater");Blank("ntcCmin");
        foreach(var cb in exposureChecks.Values)cb.IsChecked=false;exposureChecks["X0"].IsChecked=true;
        Blank("cementMass");Blank("totalWater");Blank("air");
        exposureChecks["X0"].IsChecked=false;exposureChecks["XC1"].IsChecked=true;numbers["absorbedWater"].Text="0";
        choices["consistency"].SelectedIndex=0;
        Equal("cementMass",260);Equal("totalWater",169);
    }
}
