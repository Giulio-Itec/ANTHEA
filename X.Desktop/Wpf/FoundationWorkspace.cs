using Anthea.Application.Modules;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.FoundationWorkspace;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.ShallowFoundation;

namespace X.Desktop;

internal sealed partial class FoundationWorkspace : UserControl
{
    readonly JsonObject data,input;
    readonly FoundationLibrary library;
    readonly IModuleService calculationService;
    readonly Dictionary<string,FrameworkElement> fields=[];
    readonly StackPanel results=new() { Margin=new Thickness(8) };
    readonly TextBlock state=Ui.Text(ShallowFoundationPresentation.StateToVerify,12,color:Ui.Muted);
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
        var method=new StackPanel();foreach(var choice in ShallowFoundationFields.MethodChoices)Choice(method,choice);
        form.Children.Add(InputGroup(ShallowFoundationFields.MethodGroup,method));
        foreach(var (group,numbers) in new[]{(ShallowFoundationFields.GeometryGroup,ShallowFoundationFields.Geometry),(ShallowFoundationFields.ActionsGroup,ShallowFoundationFields.Actions)})
        {
            var panel=new StackPanel();
            foreach(var f in numbers)Number(panel,f);
            form.Children.Add(InputGroup(group,panel));
        }
        Number(form,ShallowFoundationFields.Surcharge);
        var general=data["generali"]!.AsObject();
        form.Children.Add(new InputForm(general,ShallowFoundationFields.Water,_=>Changed()));
        var name=new TextBox { Text=data["nome_combinazione"]?.ToString() };
        name.TextChanged+=(_,_)=>{data["nome_combinazione"]=name.Text;Changed();};form.Children.Add(Row(ShallowFoundationFields.CombinationLabel,name));
        var confirmed=new CheckBox { Content=new TextBlock { Text=ShallowFoundationPresentation.ConfirmationLabel,TextWrapping=TextWrapping.Wrap },IsChecked=data["azioni_confermate"]!.GetValue<bool>(),Margin=new Thickness(0,4,0,4) };
        confirmed.Click+=(_,_)=>{data["azioni_confermate"]=confirmed.IsChecked==true;Changed();};form.Children.Add(confirmed);fields["confirmation"]=confirmed;
        var advanced=new StackPanel();
        Choice(advanced,ShallowFoundationFields.Compression);Number(advanced,ShallowFoundationFields.RelativeDensity);
        foreach(var option in ShallowFoundationFields.MethodOptions)Option(advanced,option);Number(advanced,ShallowFoundationFields.WaterUnitWeight);
        form.Children.Add(new Expander { Header=ShallowFoundationFields.MethodOptionsTitle,Content=advanced });
        form.Children.Add(new Expander { Header=ShallowFoundationFields.ConcreteTitle,Content=ConcreteEditor() });
        form.Children.Add(Ui.Text(ShallowFoundationPresentation.ActionsNote,11,color:Ui.Muted));
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
        var soils=InputGroup(ShallowFoundationFields.SoilGroup,Ui.Dock(soilHost,Ui.Bar(survey,Ui.Button("+ Sondaggio",AddSurvey),Ui.Button("− Sondaggio",()=>DeleteSurvey()),Ui.Button("Invia / carica terreno…",()=>SoilTransferRequested?.Invoke()))));
        soils.Margin=new Thickness(0,6,0,0);soils.MaxHeight=270;Grid.SetRow(soils,1);Grid.SetColumnSpan(soils,2);body.Children.Add(soils);
        Grid.SetRow(results,2);Grid.SetColumnSpan(results,2);body.Children.Add(results);
        survey.SelectionChanged+=(_,_)=>{if(building||survey.SelectedIndex<0)return;Commit();general["sondaggio_fondazione"]=survey.SelectedIndex;BuildSoils();Changed();};
        RefreshSurveyChoices();BuildSoils();
        bool compact=ViewFlag("input_raccolti",false);Button? collect=null;
        void LayoutInputs()
        {
            formScroll.Visibility=compact?Visibility.Collapsed:Visibility.Visible;soils.Visibility=compact?Visibility.Collapsed:Visibility.Visible;
            body.ColumnDefinitions[0].MinWidth=compact?0:370;body.ColumnDefinitions[0].Width=compact?new GridLength(0):new GridLength(.8,GridUnitType.Star);
            collect!.Content=compact?ShallowFoundationFields.ShowInputs:ShallowFoundationFields.CollectInputs;
        }
        collect=Ui.Button(ShallowFoundationFields.CollectInputs,()=>{Commit();compact=!compact;SaveView("input_raccolti",compact);LayoutInputs();},inspection:true);collect.Tag="raccogli-input";LayoutInputs();
        var actions=Ui.Bar(Ui.Text(ShallowFoundationFields.Title,19,true),Ui.Button("Calcola",TryCalculate,true,inspection:true),collect,Ui.Button("Report Word e PDF",()=>ReportRequested?.Invoke(),inspection:true),Ui.Button("Guida",()=>{if(WikiCatalog.ForModule(FoundationDocument.Module) is {} guide)WikiContextHelp.Open(this,guide.Id);},inspection:true));
        Content=Ui.Dock(body,actions,bottom:state);state.Margin=new Thickness(16,3,16,7);
        building=false;RefreshVisibility();ClearResults();
    }
    bool ViewFlag(string key,bool fallback)=>ShallowFoundationPresentation.ViewFlag(data,key,fallback);
    void SaveView(string key,bool value)
    {
        ShallowFoundationPresentation.SaveViewFlag(data,key,value);if(!building)Modified?.Invoke();
    }
    Expander InputGroup(ShallowFoundationGroup definition,UIElement content)
    {
        string key=definition.Key;
        var group=new Expander { Header=definition.Title,Content=content,IsExpanded=ViewFlag(key,true),Margin=new Thickness(0,2,0,2) };
        group.Expanded+=(_,e)=>{if(e.Source==group)SaveView(key,true);};group.Collapsed+=(_,e)=>{if(e.Source==group)SaveView(key,false);};return group;
    }
    static FrameworkElement Row(string label,FrameworkElement editor)
    {
        var row=new Grid { Margin=new Thickness(0,2,0,2) };row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(215) });row.ColumnDefinitions.Add(new ColumnDefinition());
        var text=Ui.Text(label,12);text.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(text);Grid.SetColumn(editor,1);row.Children.Add(editor);return row;
    }
    void Number(Panel parent,FieldSpec field)
    {
        string key=field.Key,label=field.Label;
        var text=new TextBox { Text=input[key]?.ToString()??"",Tag=key,MinWidth=70,Padding=new Thickness(4,2,4,2) };
        var row=Row(label,text);fields[key]=row;parent.Children.Add(row);text.TextChanged+=(_,_)=>{input[key]=text.Text;Changed();};
    }
    void Choice(Panel parent,ShallowFoundationChoice definition)
    {
        string key=definition.Key,label=definition.Label;
        var choice=new ComboBox { ItemsSource=definition.Items,SelectedIndex=input[key]!.GetValue<int>(),Tag=key };
        var row=Row(label,choice);fields[key]=row;parent.Children.Add(row);choice.SelectionChanged+=(_,_)=>{input[key]=choice.SelectedIndex;Changed();};
    }
    void Option(Panel parent,FieldSpec field)
    {
        string key=field.Key,label=field.Label;
        var check=new CheckBox { Content=label,IsChecked=input[key]!.GetValue<bool>(),Margin=new Thickness(0,4,0,5) };
        fields[key]=check;parent.Children.Add(check);check.Click+=(_,_)=>{input[key]=check.IsChecked==true;Changed();};
    }
    void RefreshVisibility()
    {
        var visibility=ShallowFoundationPresentation.VisibilityInput(data);
        foreach(var pair in fields)
        {
            bool visible=ShallowFoundationPresentation.FieldVisible(input,visibility,pair.Key=="confirmation"?"azioni_confermate":pair.Key);
            pair.Value.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
        }
        diagram.Data=data;diagram.InvalidateVisual();plan.Data=data;plan.InvalidateVisual();
        graphicsHosts[0].Content=new FoundationSoilView { Data=data,Height=double.NaN };
    }
    void Changed()
    {
        if(building)return;Result=null;RefreshVisibility();ClearResults();state.Text=ShallowFoundationPresentation.StateChanged;Modified?.Invoke();
    }
    void ClearResults()
    {
        results.Children.Clear();results.Children.Add(Ui.Text(ShallowFoundationPresentation.ResultsPlaceholder(library.Available),13,true));
        for(int i=1;i<graphicsHosts.Count;i++)graphicsHosts[i].Content=Ui.Text(ShallowFoundationPresentation.ViewToCalculate,14,color:Ui.Muted);
    }
    internal void Commit()=>soilGrid?.Commit();
    internal void TryCalculate()
    {
        Commit();Result=null;ClearResults();
        try
        {
            Result=calculationService.Calculate(data).Values;results.Children.Clear();
            results.Children.Add(Ui.Text(ShallowFoundationPresentation.Headline(input,Result),16,true));
            var details=new StackPanel();
            foreach(var row in FoundationPresentation.Summary(Result))details.Children.Add(Ui.Text(ShallowFoundationPresentation.SummaryLine(row),12));
            details.Children.Add(Ui.Table([..ShallowFoundationPresentation.CoefficientColumns],FoundationPresentation.CoefficientRows(Result)));
            var additive=FoundationPresentation.AdditiveRows(Result).ToArray();
            if(additive.Length>0)
            {
                details.Children.Add(Ui.Text(ShallowFoundationPresentation.AdditiveTitle,12,true));
                details.Children.Add(Ui.Table([..ShallowFoundationPresentation.AdditiveColumns],additive));
            }
            details.Children.Add(Ui.Text(ShallowFoundationPresentation.Limits,12,color:Ui.Muted));
            details.Children.Add(Ui.Text(Result["Reference"]!.ToString(),12,color:Ui.Muted));
            foreach(var warning in Result["Warnings"]!.AsArray())details.Children.Add(Ui.Text(warning!.ToString(),12,color:Ui.Muted));
            results.Children.Add(new Expander { Header="Dettagli della verifica, coefficienti e limiti",Content=new ScrollViewer { Content=details,MaxHeight=200,VerticalScrollBarVisibility=ScrollBarVisibility.Auto } });
            if(Result["Graphics"] is JsonObject graphics)AddGraphics(graphics);
            else for(int i=1;i<graphicsHosts.Count;i++)graphicsHosts[i].Content=Ui.Text(ShallowFoundationPresentation.ViewNotAvailable,14,color:Ui.Muted);
            state.Text=ShallowFoundationPresentation.CalculatedState(survey.SelectedIndex,data);
        }
        catch(Exception e){Result=null;state.Text=e.Message;}
    }
    void AddGraphics(JsonObject graphics)
    {
        graphicsHosts[1].Content=new FoundationPlanView(graphics,false){Height=double.NaN};
        graphicsHosts[2].Content=Ui.Dock(new FoundationPlanView(graphics,true){Height=double.NaN},bottom:Ui.Text(ShallowFoundationPresentation.PressureSummary(input,graphics),12,true));
        var cuts=new Grid();cuts.RowDefinitions.Add(new());cuts.RowDefinitions.Add(new());
        cuts.Children.Add(new FoundationPressureSection(graphics,true){Height=double.NaN});var y=new FoundationPressureSection(graphics,false){Height=double.NaN};Grid.SetRow(y,1);cuts.Children.Add(y);
        graphicsHosts[3].Content=Ui.Dock(cuts,bottom:Ui.Text(graphics["ContactDescription"]!.ToString(),11,color:Ui.Muted));
        var axis=Ui.Choice(ShallowFoundationPresentation.MechanismAxes,ShallowFoundationPresentation.MechanismAxes[0]);var host=new ContentControl { HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch };
        void ShowMechanism()=>host.Content=new FoundationMechanismView(graphics[ShallowFoundationPresentation.MechanismKey(axis.SelectedIndex)]!.AsObject()){Height=double.NaN};
        axis.SelectionChanged+=(_,_)=>ShowMechanism();ShowMechanism();graphicsHosts[4].Content=Ui.Dock(host,axis,bottom:Ui.Text(graphics["MechanismDescription"]!.ToString(),11,color:Ui.Muted));
    }
}
