using Anthea.Application.Modules;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.FoundationWorkspace;

namespace X.Desktop;

internal sealed partial class FoundationWorkspace : UserControl
{
    readonly JsonObject data,input;
    readonly FoundationLibrary library;
    readonly IModuleService calculationService;
    readonly Dictionary<string,FrameworkElement> fields=[];
    readonly StackPanel results=new() { Margin=new Thickness(8) };
    readonly TextBlock state=Ui.Text("Dati da verificare · premere Calcola",12,color:Ui.Muted);
    readonly FoundationSoilView diagram=new();
    readonly FoundationInputPlan plan=new();
    readonly TabControl graphicsTabs=new();
    readonly List<ContentControl> graphicsHosts=[];
    readonly ContentControl soilHost=new();
    readonly ComboBox survey=new() { MinWidth=145,Tag="sondaggio_fondazione" };
    JsonGrid? soilGrid;
    bool building=true;
    internal JsonObject? Result { get; private set; }
    internal event Action? Modified;
    internal event Action? ReportRequested;
    internal event Action? SoilTransferRequested;
    internal FoundationWorkspace(JsonObject data) : this(data,new FoundationLibrary()) { }
    internal FoundationWorkspace(JsonObject data,FoundationLibrary library)
    {
        this.library=library;calculationService=ModuleServices.ForFoundation(library);this.data=data;FoundationDocument.ValidateShape(data);FoundationMaterials.Complete(data);input=data["input"]!.AsObject();
        var form=new StackPanel { Margin=new Thickness(8) };
        var method=new StackPanel();Choice(method,"Method","Metodo",FoundationDocument.Methods);
        Choice(method,"Shape","Geometria",FoundationDocument.Shapes);
        Choice(method,"Condition","Drenaggio",FoundationDocument.Conditions);
        Choice(method,"Profile","Verifica",FoundationDocument.Profiles);
        form.Children.Add(InputGroup("Metodo e verifica",method,"metodo"));
        foreach(string group in new[]{"Geometria","Azioni"})
        {
            var panel=new StackPanel();
            foreach(var f in FoundationDocument.Fields.Where(f=>f.Group==group))Number(panel,f.Key,f.Label);
            form.Children.Add(InputGroup(group,panel,group));
        }
        Number(form,"Surcharge","Sovraccarico esterno q [kPa]");
        var general=data["generali"]!.AsObject();
        form.Children.Add(new InputForm(general,[new("presenza_falda","Presenza falda",Bool:true),new("profondita_falda","Profondità falda zf","m")],_=>Changed()));
        var name=new TextBox { Text=data["nome_combinazione"]?.ToString() };
        name.TextChanged+=(_,_)=>{data["nome_combinazione"]=name.Text;Changed();};form.Children.Add(Row("Combinazione",name));
        var confirmed=new CheckBox { Content=new TextBlock { Text="Azioni A1 già combinate, inclusi pesi e azioni idrauliche",TextWrapping=TextWrapping.Wrap },IsChecked=data["azioni_confermate"]!.GetValue<bool>(),Margin=new Thickness(0,4,0,4) };
        confirmed.Click+=(_,_)=>{data["azioni_confermate"]=confirmed.IsChecked==true;Changed();};form.Children.Add(confirmed);fields["confirmation"]=confirmed;
        var advanced=new StackPanel();
        Choice(advanced,"Compression","Compressibilità",FoundationDocument.Compressions);Number(advanced,"RelativeDensity","Densità relativa Dr [0–1]");
        Option(advanced,"IncludeDepthFactors","Includi fattori di profondità");Option(advanced,"RoughBase","Base ruvida · EC7: δ ≥ φ′/2");Number(advanced,"WaterUnitWeight","Peso specifico acqua [kN/m³]");
        form.Children.Add(new Expander { Header="Opzioni del metodo",Content=advanced });
        form.Children.Add(new Expander { Header="Calcestruzzo · dati comuni del progetto",Content=ConcreteEditor() });
        form.Children.Add(Ui.Text("Azioni al piano di posa. Drenato: N efficace; non drenato: N totale. Nessun peso aggiunto automaticamente. Striscia: azioni per metro.",11,color:Ui.Muted));
        var inputViews=new Grid();inputViews.ColumnDefinitions.Add(new());inputViews.ColumnDefinitions.Add(new());
        diagram.Height=double.NaN;diagram.MinWidth=220;plan.Height=double.NaN;plan.MinWidth=220;
        inputViews.Children.Add(diagram);Grid.SetColumn(plan,1);inputViews.Children.Add(plan);
        Ui.Tab(graphicsTabs,"Geometria",inputViews);
        foreach(string label in new[]{"Stratigrafia","Area efficace","Tensioni","Andamenti","Rottura"})
        { var host=new ContentControl { HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch };graphicsHosts.Add(host);Ui.Tab(graphicsTabs,label,host); }
        var body=new Grid { Margin=new Thickness(8) };
        body.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(.8,GridUnitType.Star),MinWidth=370 });body.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1.2,GridUnitType.Star),MinWidth=440 });
        body.RowDefinitions.Add(new RowDefinition());body.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });body.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        var formScroll=new ScrollViewer { Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
        body.Children.Add(formScroll);Grid.SetColumn(graphicsTabs,1);body.Children.Add(graphicsTabs);
        var soils=InputGroup("Stratigrafia",Ui.Dock(soilHost,Ui.Bar(survey,Ui.Button("+ Sondaggio",AddSurvey),Ui.Button("− Sondaggio",()=>DeleteSurvey()),Ui.Button("Invia / carica terreno…",()=>SoilTransferRequested?.Invoke()))),"stratigrafia");
        soils.Margin=new Thickness(0,6,0,0);soils.MaxHeight=270;Grid.SetRow(soils,1);Grid.SetColumnSpan(soils,2);body.Children.Add(soils);
        Grid.SetRow(results,2);Grid.SetColumnSpan(results,2);body.Children.Add(results);
        survey.SelectionChanged+=(_,_)=>{if(building||survey.SelectedIndex<0)return;Commit();general["sondaggio_fondazione"]=survey.SelectedIndex;BuildSoils();Changed();};
        RefreshSurveyChoices();BuildSoils();
        bool compact=ViewFlag("input_raccolti",false);Button? collect=null;
        void LayoutInputs()
        {
            formScroll.Visibility=compact?Visibility.Collapsed:Visibility.Visible;soils.Visibility=compact?Visibility.Collapsed:Visibility.Visible;
            body.ColumnDefinitions[0].MinWidth=compact?0:370;body.ColumnDefinitions[0].Width=compact?new GridLength(0):new GridLength(.8,GridUnitType.Star);
            collect!.Content=compact?"Mostra input":"Raccogli input";
        }
        collect=Ui.Button("Raccogli input",()=>{Commit();compact=!compact;SaveView("input_raccolti",compact);LayoutInputs();},inspection:true);collect.Tag="raccogli-input";LayoutInputs();
        var actions=Ui.Bar(Ui.Text("Fondazioni superficiali",19,true),Ui.Button("Calcola",TryCalculate,true,inspection:true),collect,Ui.Button("Report Word e PDF",()=>ReportRequested?.Invoke(),inspection:true),Ui.Button("Guida",()=>{if(WikiCatalog.ForModule(FoundationDocument.Module) is {} guide)WikiContextHelp.Open(this,guide.Id);},inspection:true));
        Content=Ui.Dock(body,actions,bottom:state);state.Margin=new Thickness(16,3,16,7);
        building=false;RefreshVisibility();ClearResults();
    }
    bool ViewFlag(string key,bool fallback)=>data["vista_fondazioni"]?[key] is JsonValue v&&v.TryGetValue<bool>(out bool b)?b:fallback;
    void SaveView(string key,bool value)
    {
        data["vista_fondazioni"]??=new JsonObject();data["vista_fondazioni"]![key]=value;if(!building)Modified?.Invoke();
    }
    Expander InputGroup(string title,UIElement content,string key)
    {
        var group=new Expander { Header=title,Content=content,IsExpanded=ViewFlag(key,true),Margin=new Thickness(0,2,0,2) };
        group.Expanded+=(_,e)=>{if(e.Source==group)SaveView(key,true);};group.Collapsed+=(_,e)=>{if(e.Source==group)SaveView(key,false);};return group;
    }
    static FrameworkElement Row(string label,FrameworkElement editor)
    {
        var row=new Grid { Margin=new Thickness(0,2,0,2) };row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(215) });row.ColumnDefinitions.Add(new ColumnDefinition());
        var text=Ui.Text(label,12);text.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(text);Grid.SetColumn(editor,1);row.Children.Add(editor);return row;
    }
    void Number(Panel parent,string key,string label)
    {
        var text=new TextBox { Text=input[key]?.ToString()??"",Tag=key,MinWidth=70,Padding=new Thickness(4,2,4,2) };
        var row=Row(label,text);fields[key]=row;parent.Children.Add(row);text.TextChanged+=(_,_)=>{input[key]=text.Text;Changed();};
    }
    void Choice(Panel parent,string key,string label,string[] options)
    {
        var choice=new ComboBox { ItemsSource=options,SelectedIndex=input[key]!.GetValue<int>(),Tag=key };
        var row=Row(label,choice);fields[key]=row;parent.Children.Add(row);choice.SelectionChanged+=(_,_)=>{input[key]=choice.SelectedIndex;Changed();};
    }
    void Option(Panel parent,string key,string label)
    {
        var check=new CheckBox { Content=label,IsChecked=input[key]!.GetValue<bool>(),Margin=new Thickness(0,4,0,5) };
        fields[key]=check;parent.Children.Add(check);check.Click+=(_,_)=>{input[key]=check.IsChecked==true;Changed();};
    }
    void RefreshVisibility()
    {
        var visibility=input.DeepClone().AsObject();visibility["WaterEnabled"]=data["generali"]!["presenza_falda"]!.DeepClone();
        foreach(var pair in fields)
        {
            bool visible=pair.Key=="confirmation"?input["Profile"]!.GetValue<int>()==0:FoundationPresentation.Visible(pair.Key,visibility);
            if(pair.Key is "MomentX" or "MomentY" or "HorizontalY" && input[pair.Key]?.ToString() is string s && s!="0")visible=true;
            if(pair.Key=="Compression" && input[pair.Key]!.GetValue<int>()!=0)visible=true;
            pair.Value.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
        }
        diagram.Data=data;diagram.InvalidateVisual();plan.Data=data;plan.InvalidateVisual();
        graphicsHosts[0].Content=new FoundationSoilView { Data=data,Height=double.NaN };
    }
    void Changed()
    {
        if(building)return;Result=null;RefreshVisibility();ClearResults();state.Text="Dati modificati · premere Calcola";Modified?.Invoke();
    }
    void ClearResults()
    {
        results.Children.Clear();results.Children.Add(Ui.Text(library.Available?"Verifica da calcolare · selezionare il sondaggio e confermare le azioni.":"Calcolo in attesa del rilascio approvato GPC · puoi preparare e salvare il foglio.",13,true));
        for(int i=1;i<graphicsHosts.Count;i++)graphicsHosts[i].Content=Ui.Text("Calcolare per aggiornare questa vista.",14,color:Ui.Muted);
    }
    internal void Commit()=>soilGrid?.Commit();
    internal void TryCalculate()
    {
        Commit();Result=null;ClearResults();
        try
        {
            Result=calculationService.Calculate(data).Values;results.Children.Clear();
            results.Children.Add(Ui.Text(Result["Status"]+"     N = "+FoundationPresentation.Value(input["Normal"])+" kN     R = "+FoundationPresentation.Value(Result["Resistance"],.001)+" kN     N/R = "+FoundationPresentation.Value(Result["Utilization"],1,"0.000")+"     A′ = "+FoundationPresentation.Value(Result["EffectiveArea"],1e-6)+" m²",16,true));
            var details=new StackPanel();
            foreach(var row in FoundationPresentation.Summary(Result))details.Children.Add(Ui.Text(row.Label+"   "+row.Value,12));
            details.Children.Add(Ui.Table(["Contributo","N","s","d","i","kPa"],FoundationPresentation.CoefficientRows(Result)));
            var additive=FoundationPresentation.AdditiveRows(Result).ToArray();
            if(additive.Length>0)
            {
                details.Children.Add(Ui.Text("Hansen non drenato · correzioni additive",12,true));
                details.Children.Add(Ui.Table(["Contributo","s′ (+)","d′ (+)","i′ (−)","1+s′+d′−i′"],additive));
            }
            details.Children.Add(Ui.Text("Verifica statica di portanza; scorrimento, cedimenti, stabilità globale, sisma e struttura esclusi.",12,color:Ui.Muted));
            details.Children.Add(Ui.Text(Result["Reference"]!.ToString(),12,color:Ui.Muted));
            foreach(var warning in Result["Warnings"]!.AsArray())details.Children.Add(Ui.Text(warning!.ToString(),12,color:Ui.Muted));
            results.Children.Add(new Expander { Header="Dettagli della verifica, coefficienti e limiti",Content=new ScrollViewer { Content=details,MaxHeight=200,VerticalScrollBarVisibility=ScrollBarVisibility.Auto } });
            if(Result["Graphics"] is JsonObject graphics)AddGraphics(graphics);
            else for(int i=1;i<graphicsHosts.Count;i++)graphicsHosts[i].Content=Ui.Text("Vista non disponibile per questo stato di carico. Consultare gli avvisi della verifica.",14,color:Ui.Muted);
            state.Text="Calcolato · sondaggio "+(survey.SelectedIndex+1)+" · unica combinazione: "+data["nome_combinazione"];
        }
        catch(Exception e){Result=null;state.Text=e.Message;}
    }
    void AddGraphics(JsonObject graphics)
    {
        graphicsHosts[1].Content=new FoundationPlanView(graphics,false){Height=double.NaN};
        graphicsHosts[2].Content=Ui.Dock(new FoundationPlanView(graphics,true){Height=double.NaN},bottom:Ui.Text((input["Condition"]!.GetValue<int>()==0?"Efficaci · ":"Totali · ")+"pmin = "+FoundationPresentation.Value(graphics["MinimumPressure"],1000)+" kPa · pmax = "+FoundationPresentation.Value(graphics["MaximumPressure"],1000)+" kPa · area compressa = "+FoundationPresentation.Value(graphics["CompressedArea"],1e-6)+" m²",12,true));
        var cuts=new Grid();cuts.RowDefinitions.Add(new());cuts.RowDefinitions.Add(new());
        cuts.Children.Add(new FoundationPressureSection(graphics,true){Height=double.NaN});var y=new FoundationPressureSection(graphics,false){Height=double.NaN};Grid.SetRow(y,1);cuts.Children.Add(y);
        graphicsHosts[3].Content=Ui.Dock(cuts,bottom:Ui.Text(graphics["ContactDescription"]!.ToString(),11,color:Ui.Muted));
        var axis=Ui.Choice(new[]{"Sezione X · larghezza B","Sezione Y · lunghezza L"},"Sezione X · larghezza B");var host=new ContentControl { HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch };
        void ShowMechanism()=>host.Content=new FoundationMechanismView(graphics[axis.SelectedIndex==1?"MechanismY":"MechanismX"]!.AsObject()){Height=double.NaN};
        axis.SelectionChanged+=(_,_)=>ShowMechanism();ShowMechanism();graphicsHosts[4].Content=Ui.Dock(host,axis,bottom:Ui.Text(graphics["MechanismDescription"]!.ToString(),11,color:Ui.Muted));
    }
}
