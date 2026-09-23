using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Materiali;

public sealed partial class MaterialWindow
{
    readonly Grid sections=new() { MinWidth=1140, Margin=new Thickness(10) };
    readonly List<ScrollViewer> columnScrolls=new();
    readonly Dictionary<string,CheckBox> exposureChecks=new();
    readonly Dictionary<string,TextBox> numbers=new();
    readonly Dictionary<string,ComboBox> choices=new();
    readonly TextBlock coverHeadline=Text("",23,true),coverSteps=Text(""),exposureDetails=Text(""),mixResults=Text(""),mixLimits=Text(""),mixNotes=Text("",12),coverNotes=Text("",12),consistencyInfo=Text("",12);
    readonly CheckBox highStrength=new(){Content="Riduzione per classe di resistenza (tab. 4.3N)",Margin=new Thickness(0,3,0,3)},
        slab=new(){Content="Geometria a piastra; armature non influenzate dal getto",Margin=new Thickness(0,3,0,3)},
        quality=new(){Content="Controllo speciale della produzione del calcestruzzo",Margin=new Thickness(0,3,0,3)},
        rough=new(){Content="Superficie irregolare / aggregato a vista (+5 mm)",Margin=new Thickness(0,3,0,3)};
    bool detailsReady;
    static ScrollViewer Scroller(UIElement element)=>new(){Content=element,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    UIElement Field(string label,FrameworkElement control,string unit="")
    {
        var g=new Grid { Margin=new Thickness(0,2,0,2) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(136) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(44) });
        var t=Text(label,12); t.Margin=new Thickness(0,0,12,0); g.Children.Add(t);
        control.Margin=new Thickness(0,0,8,0); control.MinHeight=25; Grid.SetColumn(control,1); g.Children.Add(control);
        var u=Text(unit,12); Grid.SetColumn(u,2); g.Children.Add(u); return g;
    }
    UIElement Numeric(string key,string label,string value,string unit)
    {
        var box=new TextBox { Text=value }; numbers.Add(key,box); box.TextChanged+=(_,_)=>RefreshDetails(); return Field(label,box,unit);
    }
    UIElement Select(string key,string label,string[] values,int selected=0)
    {
        var box=new ComboBox {ItemsSource=values,SelectedIndex=selected}; choices.Add(key,box); box.SelectionChanged+=(_,_)=>RefreshDetails(); return Field(label,box);
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
        foreach(var cb in new[]{highStrength,slab,quality,rough}) {cb.Content=Text(cb.Content.ToString()!,12);cb.Checked+=(_,_)=>RefreshDetails();cb.Unchecked+=(_,_)=>RefreshDetails();}
        var exposure=Paper(Stack(Text("Classi di esposizione",15,true),Text("Esposizioni contemporanee sulla stessa superficie.",12),checks,exposureDetails));
        var cover=Paper(Stack(Text("Dati per il copriferro",15,true),Text("Armatura ordinaria a barre isolate · calcestruzzo normale",12),
            Select("life","Vita utile di progetto",["50 anni","100 anni"]),
            Numeric("diameter","Diametro della barra da verificare · φ","16","mm"),
            Numeric("aggregate","Dimensione massima aggregato · Dmax","20","mm"),
            Numeric("deviation","Tolleranza di esecuzione · Δcdev","10","mm"),
            Numeric("deviationReason","Motivo della riduzione di Δcdev","",""),
            Select("ground","Superficie di getto",["Casseratura","Terreno preparato","Direttamente su terra"]),
            Select("abrasion","Strato per abrasione",["Nessuno","XM1 · +5 mm","XM2 · +10 mm","XM3 · +15 mm"]),
            rough,new Expander{Header="Riduzioni della classe strutturale (facoltative)",Margin=new Thickness(0,10,0,10),Content=Stack(highStrength,slab,quality,Text("Base S4 per 50 anni; +2 classi per 100 anni. Ogni riduzione ammessa vale −1, con minimo S1. La riduzione per resistenza è valutata separatamente per ciascuna esposizione. Non è applicata la deroga per aria >4% della nota 2.",12))}));
        var result=Paper(Stack(Text("Copriferro nominale",15,true),coverHeadline,coverSteps,new Expander{Header="Riferimenti e applicabilità",Content=coverNotes}));
        var exposureBody=Stack(exposure,cover,result); exposureBody.Margin=new Thickness(0);
        AddColumn("ESPOSIZIONE E COPRIFERRO",exposureBody);
        var mix=Paper(Stack(Text("Composizione per 1 m³ di calcestruzzo",15,true),
            Text("Inserisci i dosaggi della miscela. La classe di resistenza e la consistenza non determinano da sole il contenuto d'acqua.",12),
            Select("cement","Famiglia del cemento",["CEM I","CEM II","CEM III","CEM IV","CEM V","Altro / da specificare"]),
            Numeric("cementName","Designazione completa / produttore","",""),
            Select("cementClass","Classe del cemento",["32,5","42,5","52,5"]),
            Select("cementEarly","Resistenza iniziale del cemento",["N","R"]),
            Numeric("cementMass","Dosaggio cemento · c","","kg/m³"),
            Numeric("totalWater","Acqua totale · atot","","kg/m³"),
            Numeric("absorbedWater","Acqua assorbita dagli aggregati · aass","0","kg/m³"),
            Numeric("aggregateMass","Dosaggio aggregati (stato SSD)","","kg/m³"),
            Numeric("additionName","Aggiunta · tipo / denominazione","",""),
            Numeric("additionMass","Dosaggio aggiunte","0","kg/m³"),
            Numeric("admixtureName","Additivo · tipo / denominazione","",""),
            Numeric("admixtureMass","Dosaggio additivi","0","kg/m³"),
            Numeric("air","Contenuto d'aria","","%"),
            Select("sulfate","Resistenza ai solfati del cemento",["Da dichiarare","Non dichiarata","Moderata","Alta"]),
            Text("Acqua totale: comprende anche acqua negli aggregati, additivi e sospensioni. Acqua efficace = totale − assorbita. Rapporto a/c riferito al solo cemento; contributo k delle aggiunte non applicato (§5.2.5).",12)));
        var fresh=Paper(Stack(Text("Calcestruzzo fresco e specificazione",15,true),
            Select("consistency","Consistenza · abbassamento al cono",["Da definire","S1","S2","S3","S4","S5"]),consistencyInfo,
            Numeric("slump","Abbassamento di riferimento","","mm"),
            Select("chloride","Classe di contenuto in cloruri",["Da definire","Cl 0,20","Cl 0,40"]),
            Text("Cl 0,20 / 0,40: percentuale massima di ioni Cl⁻ rispetto alla massa di cemento, per armatura ordinaria; scelta secondo le disposizioni del luogo d'impiego (prospetto 10). La selezione non verifica il contenuto reale.",12),
            Text("Dmax è condiviso con la colonna copriferro. Le classi di consistenza sono definite dal prospetto 3; per S1 e S5 considerare i limiti di sensibilità della prova (§5.4.1).",12)));
        var compositionBody=Stack(mix,fresh,Paper(Stack(Text("Valori limite raccomandati · F.1",15,true),mixLimits,mixResults,mixNotes))); compositionBody.Margin=new Thickness(0);
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
    double Read(string key,string label,double min=0,double max=double.MaxValue)
    {
        var raw=numbers[key].Text.Trim().Replace(',','.');
        if(!double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out double value)||!double.IsFinite(value)||value<min||value>max)
            throw new ArgumentException($"{label}: inserire un numero tra {min:g} e {(max==double.MaxValue?"un valore finito":max.ToString("g"))}.");
        return value;
    }
    double? Optional(string key,string label,double min=0,double max=double.MaxValue)=>string.IsNullOrWhiteSpace(numbers[key].Text)?null:Read(key,label,min,max);
    Exposure[] Active()=>Durability.Exposures.Where(e=>exposureChecks[e.Code].IsChecked==true).ToArray();
    void RefreshDetails()
    {
        if(!detailsReady || choice.SelectedIndex<0) return;
        var active=Active(); var fck=Classes[choice.SelectedIndex].Fck;
        exposureDetails.Text=string.Join("\n",active.Select(e=>$"{e.Code} · {e.Description}"));
        coverHeadline.Text="Da completare"; coverSteps.Text=""; coverHeadline.Foreground=Navy;
        coverNotes.Text="EN 1992-1-1:2004, §4.4.1 e tabelle 4.2–4.4N: valori raccomandati, senza annesso nazionale. Δcdur,γ = Δcdur,st = Δcdur,add = 0. Copriferro misurato fino all'armatura più vicina alla superficie, staffe comprese. Verificare ciascuna barra e superficie; requisiti d'incendio secondo EN 1992-1-2 da valutare separatamente.";
        try
        {
            double delta=Read("deviation","Δcdev",0,1000);
            if(delta<10 && string.IsNullOrWhiteSpace(numbers["deviationReason"].Text)) throw new ArgumentException("Per Δcdev < 10 mm specificare il controllo di esecuzione che giustifica la riduzione (§4.4.1.3). Tra 0 e 5 mm occorrono misure molto accurate e scarto degli elementi non conformi.");
            var p=new CoverInput(choices["life"].SelectedIndex==0?50:100,highStrength.IsChecked==true,slab.IsChecked==true,quality.IsChecked==true,
                Read("diameter","Diametro",.1,1000),Read("aggregate","Dmax",.1,1000),delta,rough.IsChecked==true,choices["abrasion"].SelectedIndex*5,
                choices["ground"].SelectedIndex switch{1=>40,2=>75,_=>0});
            var c=Durability.Cover(active,fck,p);
            coverHeadline.Text=$"cnom = {c.Nominal:0.##} mm";
            coverSteps.Text=string.Join("\n",c.Lines.Select(l=>$"{l.Exposure}: classe strutturale S{l.StructuralClass} → cmin,dur = {l.Durability:0.##} mm"))+
                $"\n\ncmin,b = φ{(p.Aggregate>32?" + 5 mm (Dmax > 32 mm)":"")} = {c.Bond:0.##} mm"+
                $"\ncmin = max(cmin,b; cmin,dur; 10 mm) + superficie + abrasione = {c.Minimum:0.##} mm"+
                $"\ncnom = cmin + Δcdev = {c.Minimum+delta:0.##} mm"+
                (p.Ground>0?$"\nLimite per il getto: {p.Ground} mm → cnom adottato {c.Nominal:0.##} mm":"");
            if(active.Any(e=>e.CoverColumn<0)) coverSteps.Text+="\nXF/XA: requisiti aggiuntivi della miscela nella colonna Composizione.";
        }
        catch(ArgumentException ex) {coverSteps.Text=ex.Message;coverHeadline.Foreground=Brush("#9A4D0A");}
        RefreshMix(active,fck);
    }
    void RefreshMix(Exposure[] active,double fck)
    {
        mixLimits.Text=""; mixResults.Text="";
        mixNotes.Text="UNI EN 206-1:2006, appendice F informativa: riferimenti per 50 anni, CEM I e Dmax 20–32 mm. La relazione con la classe di resistenza è riferita a cemento 32,5. Confronti indicativi, non verifica completa di conformità o sostituzione delle disposizioni nazionali.";
        string[] slumpRanges=["Seleziona la classe di consistenza.","S1 · da 10 a 40 mm","S2 · da 50 a 90 mm","S3 · da 100 a 150 mm","S4 · da 160 a 210 mm","S5 · almeno 220 mm"];
        consistencyInfo.Text=slumpRanges[choices["consistency"].SelectedIndex];
        try
        {
            Durability.ValidateExposure(active);
            double? maxRatio=active.Where(e=>e.MaxRatio.HasValue).Select(e=>e.MaxRatio).Min();
            int minCement=active.Max(e=>e.MinCement),minStrength=active.Max(e=>e.MinStrength); double minAir=active.Max(e=>e.MinAir);
            mixLimits.Text=$"Esposizioni: {string.Join(" + ",active.Select(e=>e.Code))}\nClasse minima indicativa: {Durability.Strength(minStrength)}\n"+
                (maxRatio.HasValue?$"Rapporto a/c massimo: {maxRatio:0.00}\n":"Rapporto a/c: nessun limite F.1 per X0\n")+
                (minCement>0?$"Cemento minimo: {minCement} kg/m³\n":"Cemento: nessun minimo F.1 per X0\n")+
                (minAir>0?$"Aria minima raccomandata: {minAir:0.0}% (vedere nota a) F.1)":"Aria: nessun minimo F.1 per le esposizioni selezionate");
            var messages=new List<string>();
            double dmax=Read("aggregate","Dmax",.1,1000);
            if(Selected("cement")!="CEM I"||dmax<20||dmax>32||choices["life"].SelectedIndex!=0)
                messages.Add("FUORI DALLE IPOTESI DI F.1: limiti mostrati solo come riferimento; definire una specifica applicabile a cemento, aggregati e vita utile scelti.");
            if(Selected("cementClass")!="32,5") messages.Add("Cemento diverso da 32,5: la classe minima riportata è solo il riferimento tabellare.");
            messages.Add($"Classe scelta {Classes[choice.SelectedIndex].Name}: "+(fck>=minStrength?"raggiunge il riferimento F.1.":"inferiore al riferimento F.1."));
            double? cement=Optional("cementMass","Cemento",.001),total=Optional("totalWater","Acqua totale"),absorbed=Optional("absorbedWater","Acqua assorbita"),air=Optional("air","Aria",0,100);
            _=Optional("aggregateMass","Dosaggio aggregati",.001); _=Optional("additionMass","Aggiunte"); _=Optional("admixtureMass","Additivi");
            if(cement is double c) messages.Add($"Cemento {c:0.##} kg/m³: "+(minCement==0?"nessun limite F.1.":c>=minCement?"minimo F.1 raggiunto.":"inferiore al minimo F.1."));
            else messages.Add("Dosaggio cemento: da compilare.");
            if(total is double t && absorbed is double a)
            {
                double effective=Durability.EffectiveWater(t,a); messages.Add($"Acqua efficace = {t:0.##} − {a:0.##} = {effective:0.##} kg/m³.");
                if(cement is double cm) {double ratio=effective/cm;messages.Add($"Rapporto a/c = {ratio:0.000}: "+(!maxRatio.HasValue?"nessun limite F.1.":ratio<=maxRatio.Value+1e-10?"entro il limite F.1.":"supera il limite F.1."));}
            }
            else messages.Add("Acqua totale e assorbita: completare i dati per calcolare acqua efficace e a/c.");
            if(cement is double ce && maxRatio is double r) messages.Add($"Acqua efficace massima al dosaggio inserito: {ce*r:0.##} kg/m³ (c × a/c massimo).");
            if(minAir>0) messages.Add(air is null?"Aria: da dichiarare.":air>=minAir?"Aria: minimo tabellare raggiunto.":"Aria inferiore al riferimento: per l'alternativa senza aria aggiunta occorre una prova prestazionale appropriata (nota a, F.1).");
            if(active.Any(e=>e.Code.StartsWith("XF"))) messages.Add("Gelo/disgelo: verificare resistenza degli aggregati secondo EN 12620; il solo contenuto d'aria non esaurisce i requisiti.");
            if(active.Any(e=>e.Code is "XA2" or "XA3")) messages.Add("Se l'attacco XA2/XA3 è dovuto a solfati, occorre cemento resistente ai solfati (nota b F.1); per XA3, alta resistenza. Dichiarazione inserita: "+Selected("sulfate")+".");
            if(active.Any(e=>e.Code.StartsWith("XA"))) messages.Add("La classe XA deve derivare dall'analisi chimica del terreno/acqua secondo il prospetto 2; non è dedotta automaticamente.");
            double? slump=Optional("slump","Abbassamento",0,1000); int index=choices["consistency"].SelectedIndex;
            if(slump is double s && index>0)
            {
                int[] lower=[0,10,50,100,160,220],upper=[0,40,90,150,210,int.MaxValue];
                messages.Add(s>=lower[index] && s<=upper[index]?"Abbassamento di riferimento nella classe selezionata.":"Abbassamento di riferimento fuori dalla classe selezionata (non è una valutazione delle tolleranze di prova).");
            }
            if(index==0) messages.Add("Classe di consistenza: da definire.");
            if(choices["chloride"].SelectedIndex==0) messages.Add("Classe di cloruri: da definire.");
            mixResults.Text=string.Join("\n\n",messages);
        }
        catch(ArgumentException ex) {mixResults.Text="Dati da correggere: "+ex.Message;}
    }
}
