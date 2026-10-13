using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Materiali;

public sealed partial class MaterialView
{
    readonly Grid sections=new() { MinWidth=1140, Margin=new Thickness(10) };
    readonly List<ScrollViewer> columnScrolls=new();
    readonly Dictionary<string,CheckBox> exposureChecks=new();
    readonly Dictionary<string,TextBox> numbers=new();
    readonly Dictionary<string,ComboBox> choices=new();
    readonly TextBlock coverHeadline=Text("",23,true),coverSteps=Text(""),exposureDetails=Text(""),coverNotes=Text("",12),consistencyInfo=Text("",12);
    readonly CheckBox highStrength=new(){Content="Riduzione per classe di resistenza (tab. 4.3N)",Margin=new Thickness(0,3,0,3)},
        slab=new(){Content="Geometria a piastra; armature non influenzate dal getto",Margin=new Thickness(0,3,0,3)},
        quality=new(){Content="Controllo speciale della produzione del calcestruzzo",Margin=new Thickness(0,3,0,3)},
        rough=new(){Content="Superficie irregolare / aggregato a vista (+5 mm)",Margin=new Thickness(0,3,0,3)};
    readonly CheckBox ntcQuality=new(){Content="Controllo qualità con verifica dei copriferri"};
    FrameworkElement? ntcOptions,ec2Options;
    readonly TextBlock deviationNote=Text("",12);
    bool updatingDeviation;
    int lastDeviationControl=-1;
    bool detailsReady;
    static ScrollViewer Scroller(UIElement element)=>new(){Content=element,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    UIElement Field(string label,FrameworkElement control,string unit="",string key="")
    {
        var g=new Grid { Margin=new Thickness(0,2,0,2) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(136) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(44) });
        var t=Text(label,12); t.Margin=new Thickness(0,0,12,0); g.Children.Add(wikiLabel?.Invoke(t,key) ?? t);
        control.Margin=new Thickness(0,0,8,0); control.MinHeight=25; Grid.SetColumn(control,1); g.Children.Add(control);
        var u=Text(unit,12); Grid.SetColumn(u,2); g.Children.Add(u); return g;
    }
    UIElement Numeric(string key,string label,string unit)
    {
        var box=new TextBox { Text=MaterialSheetInput.InitialNumber(key) }; numbers.Add(key,box); box.TextChanged+=(_,_)=>RefreshDetails(); return Field(label,box,unit,key);
    }
    UIElement Select(string key,string label)
    {
        var box=new ComboBox {ItemsSource=MaterialSheetInput.Options(key),SelectedIndex=MaterialSheetInput.InitialSelection(key)}; choices.Add(key,box); box.SelectionChanged+=(_,_)=>RefreshDetails(); return Field(label,box,key:key);
    }
    UIElement BuildDetails(UIElement properties)
    {
        AddColumn("PROPRIETÀ MECCANICHE",properties);
        var checks=new WrapPanel();
        foreach(var e in Durability.Exposures)
        {
            var cb=new CheckBox {Content=e.Code,Width=55,Margin=new Thickness(0,4,0,4),ToolTip=e.Description,IsChecked=e.Code=="XC1"};
            exposureChecks.Add(e.Code,cb); checks.Children.Add(cb); cb.Checked+=(_,_)=>RefreshDetails(); cb.Unchecked+=(_,_)=>RefreshDetails();
        }
        foreach(var cb in new[]{highStrength,slab,quality,rough,ntcQuality}) {cb.Content=Text(cb.Content.ToString()!,12);cb.Checked+=(_,_)=>RefreshDetails();cb.Unchecked+=(_,_)=>RefreshDetails();}
        var exposure=BuildExposureSelector(checks);
        ntcOptions=Stack(
            Select("ntcElement","Tipo di elemento"),
            Field("Classe minima del calcestruzzo",minimumConcreteClass),
            minimumConcreteNote,
            ntcQuality);
        ec2Options=new Expander{Header="Riduzioni della classe strutturale EC2",Margin=new Thickness(0,6,0,6),Content=Stack(highStrength,slab,quality,Text("Base S4 per 50 anni; +2 per 100 anni. Riduzioni di una classe, con minimo S1.",12))};
        var cover=Paper(Stack(Text("Dati per il copriferro",15,true),Text("Armatura ordinaria a barre isolate · calcestruzzo normale",12),
            Select("coverMethod","Criterio copriferro"),
            ntcOptions,
            Select("life","Vita utile di progetto"),
            Numeric("diameter","Diametro della barra da verificare · φ","mm"),
            Numeric("aggregate","Dimensione massima aggregato · Dmax","mm"),
            Select("deviationControl","Controllo di esecuzione"),
            Select("deviationValue","Tolleranza di posa · Δcdev"),
            deviationNote,
            Select("ground","Superficie di getto"),
            Select("abrasion","Strato per abrasione"),
            rough,ec2Options));
        var coverDiagram=new Image
        {
            Source=new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Materiali;component/Assets/copriferro.png")),
            Stretch=Stretch.Uniform, HorizontalAlignment=HorizontalAlignment.Stretch,
            Margin=new Thickness(0,12,0,4),
            ToolTip="Schema del copriferro nominale e della posizione delle armature"
        };
        System.Windows.Automation.AutomationProperties.SetName(coverDiagram,"Schema del copriferro: cnom, diametro delle staffe, semidiametro delle barre longitudinali e distanza d′");
        var result=Paper(Stack(Text("Copriferro nominale",15,true),coverHeadline,coverSteps,new Expander{Header="Riferimenti e applicabilità",Content=coverNotes},coverDiagram));
        var exposureBody=Stack(exposure,cover,result); exposureBody.Margin=new Thickness(0);
        AddColumn("ESPOSIZIONE E COPRIFERRO",exposureBody);
        var compositionBody=BuildComposition();
        AddColumn("COMPOSIZIONE",compositionBody);
        detailsReady=true; return sections;
    }
    void AddColumn(string title,UIElement content)
    {
        int index=sections.ColumnDefinitions.Count;
        sections.ColumnDefinitions.Add(new ColumnDefinition());
        var panel=new DockPanel { Margin=new Thickness(5,0,5,0) };
        var heading=Text(title,15,true); heading.HorizontalAlignment=HorizontalAlignment.Center;
        heading.Margin=new Thickness(0,4,0,10); DockPanel.SetDock(heading,Dock.Top); panel.Children.Add(heading);
        var scroll=Scroller(content); scroll.Padding=new Thickness(6); columnScrolls.Add(scroll);
        panel.Children.Add(new Border { Child=scroll, Background=Brushes.White, BorderBrush=Brush("#D8E0EB"), BorderThickness=new Thickness(1) });
        Grid.SetColumn(panel,index); sections.Children.Add(panel);
    }
    string Selected(string key)=>choices[key].SelectedItem?.ToString()??"";
    Exposure[] Active()=>Durability.Exposures.Where(e=>exposureChecks[e.Code].IsChecked==true).ToArray();
    void RefreshDetails()
    {
        if(!detailsReady || choice.SelectedIndex<0 || updatingDeviation || updatingExposure) return;
        UpdateDeviation();
        var active=Active(); RefreshExposureSelector(active);
        var value=presentation=MaterialSheetPresentation.Evaluate(CaptureState(),active.Select(e=>e.Code));
        strength.Text=value.Strength;tension.Text=value.Tension;modulus.Text=value.Modulus;status.Text=value.Status;table.ItemsSource=value.Properties;
        bondValue.Text=value.BondValue;bondDetails.Text=value.BondDetails;
        minimumConcreteClass.Text=value.MinimumConcreteClass;minimumConcreteNote.Text=value.MinimumConcreteNote;
        coverHeadline.Text=value.CoverHeadline;coverSteps.Text=value.CoverSteps;coverNotes.Text=value.CoverNotes;
        coverHeadline.Foreground=Themed("foreground",value.CoverError?"#9A4D0A":"#0B2A4A");
        bool ntc=choices["coverMethod"].SelectedIndex==0;
        ntcOptions!.Visibility=ntc?Visibility.Visible:Visibility.Collapsed;ec2Options!.Visibility=ntc?Visibility.Collapsed:Visibility.Visible;
        foreach(var (key,text) in value.Composition)compositionValues[key].Text=text;
        compositionSource.Text=value.CompositionSource;compositionAirNote.Text=value.CompositionAirNote;
        compositionRequirements.Text=value.CompositionRequirements;consistencyInfo.Text=value.ConsistencyInfo;
    }
    void UpdateDeviation()
    {
        int mode=choices["deviationControl"].SelectedIndex;
        if(mode!=lastDeviationControl)
        {
            updatingDeviation=true;
            try
            {
                var values=MaterialSheetInput.DeviationOptions(mode);
                choices["deviationValue"].ItemsSource=values;
                choices["deviationValue"].SelectedIndex=0;
                choices["deviationValue"].IsEnabled=mode!=0;
                lastDeviationControl=mode;
            }
            finally {updatingDeviation=false;}
        }
        deviationNote.Text=mode switch {
            0=>"Posa ordinaria: Δcdev = 10 mm (Circolare 2019 §C4.1.6.1.3).",
            1=>"Δcdev da 5 a 10 mm: sistema di controllo qualità con misure del copriferro (EC2 §4.4.1.3). Selezionare il valore previsto dal controllo adottato.",
            _=>"Δcdev da 0 a 10 mm: misure molto accurate e scarto degli elementi non conformi, ad esempio prefabbricati (EC2 §4.4.1.3). Selezionare il valore previsto dal controllo adottato."
        };
        deviationNote.ToolTip="La tolleranza di posa è distinta dalla riduzione del copriferro tabellare per controllo qualità.";
    }
}
