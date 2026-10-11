using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.FoundationWorkspace;

namespace X.Desktop;

internal sealed class FoundationWorkspace : UserControl
{
    readonly JsonObject data,input;
    readonly FoundationLibrary library;
    readonly Dictionary<string,FrameworkElement> fields=[];
    readonly StackPanel results=new() { Margin=new Thickness(18) };
    readonly TextBlock state=Ui.Text("Dati da verificare · premere Calcola",12,color:Ui.Muted);
    readonly FoundationDiagram diagram=new();
    bool building=true;
    internal JsonObject? Result { get; private set; }
    internal event Action? Modified;
    internal event Action? ReportRequested;
    internal FoundationWorkspace(JsonObject data) : this(data,new FoundationLibrary()) { }
    internal FoundationWorkspace(JsonObject data,FoundationLibrary library)
    {
        this.library=library;
        this.data=data; FoundationDocument.ValidateShape(data); input=data["input"]!.AsObject();
        var form=new StackPanel { Margin=new Thickness(16) };
        form.Children.Add(Ui.Text("Fondazioni superficiali",22,true));
        form.Children.Add(Ui.Text("Capacità portante statica · terreno omogeneo · base e terreno orizzontali",12,color:Ui.Muted));
        Choice(form,"Method","Metodo",FoundationDocument.Methods);
        Choice(form,"Shape","Geometria",FoundationDocument.Shapes);
        Choice(form,"Condition","Condizioni di drenaggio",FoundationDocument.Conditions);
        Choice(form,"Profile","Profilo di verifica",FoundationDocument.Profiles);
        Choice(form,"Compression","Meccanismo di rottura",FoundationDocument.Compressions);
        Option(form,"IncludeDepthFactors","Includi fattori di profondità");
        Option(form,"RoughBase","Base ruvida · EC7: δ ≥ φ′/2");
        Option(form,"WaterEnabled","Falda idrostatica");
        foreach(var group in FoundationDocument.Fields.GroupBy(f=>f.Group))
        {
            var panel=new StackPanel();
            foreach(var f in group)
            {
                var text=new TextBox { Text=input[f.Key]?.ToString() ?? "",Margin=new Thickness(0,2,0,6),MinWidth=90,Tag=f.Key };
                var row=Ui.Stack(Ui.Text(f.Label,12),text);fields[f.Key]=row;panel.Children.Add(row);
                text.TextChanged+=(_,_)=> { input[f.Key]=text.Text;Changed(); };
            }
            form.Children.Add(new Expander { Header=group.Key,Content=panel,IsExpanded=true,Margin=new Thickness(0,10,0,0) });
        }
        form.Children.Add(Ui.Text("Azioni al piano di posa già combinate, inclusi tutti i pesi. Drenato: N efficace al netto della sottospinta. Non drenato: N totale, senza sottrarre la pressione interstiziale alla base. Striscia: azioni per 1 m. Confronto caratteristico: azioni caratteristiche.",12,color:Ui.Muted));
        var name=new TextBox { Text=data["nome_combinazione"]?.ToString(),Margin=new Thickness(0,5,0,5) };
        name.TextChanged+=(_,_)=>{data["nome_combinazione"]=name.Text;Changed();};
        form.Children.Add(Ui.Text("Nome della combinazione",12));form.Children.Add(name);
        var confirmed=new CheckBox { Content=new TextBlock { Text="Confermo azioni A1 già combinate e convenzione N coerente con il drenaggio",TextWrapping=TextWrapping.Wrap },IsChecked=data["azioni_confermate"]?.GetValue<bool>()==true,Margin=new Thickness(0,5,0,10) };
        confirmed.Click+=(_,_)=>{data["azioni_confermate"]=confirmed.IsChecked==true;Changed();};form.Children.Add(confirmed);
        fields["confirmation"]=confirmed;
        var left=new ScrollViewer { Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
        var right=new ScrollViewer { Content=Ui.Stack(diagram,results),VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(380),MinWidth=290 });grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(left);Grid.SetColumn(right,1);grid.Children.Add(right);
        var actions=Ui.Bar(Ui.Button("Calcola",TryCalculate,true,inspection:true),Ui.Button("Report Word e PDF",()=>ReportRequested?.Invoke(),inspection:true),Ui.Button("Guida",()=>{if(WikiCatalog.ForModule(FoundationDocument.Module) is {} guide)WikiContextHelp.Open(this,guide.Id);},inspection:true));
        Content=Ui.Dock(grid,actions,bottom:state); state.Margin=new Thickness(16,6,16,8);
        building=false;RefreshVisibility();ClearResults();
    }
    void Choice(Panel parent,string key,string label,string[] options)
    {
        var choice=new ComboBox { ItemsSource=options,SelectedIndex=input[key]!.GetValue<int>(),Margin=new Thickness(0,3,0,8),Tag=key };
        var row=Ui.Stack(Ui.Text(label,12),choice);fields[key]=row;parent.Children.Add(row);
        choice.SelectionChanged+=(_,_)=>{input[key]=choice.SelectedIndex;Changed();};
    }
    void Option(Panel parent,string key,string label)
    {
        var check=new CheckBox { Content=label,IsChecked=input[key]!.GetValue<bool>(),Margin=new Thickness(0,4,0,5) };
        fields[key]=check;parent.Children.Add(check);check.Click+=(_,_)=>{input[key]=check.IsChecked==true;Changed();};
    }
    void RefreshVisibility()
    {
        foreach(var pair in fields)
        {
            bool visible=pair.Key=="confirmation" ? input["Profile"]!.GetValue<int>()==0 : FoundationPresentation.Visible(pair.Key,input);
            // Keep unsupported nonzero actions visible so the user can correct them, never discard them.
            if(pair.Key is "MomentX" or "MomentY" or "HorizontalY" && input[pair.Key]?.ToString() is string s && s!="0") visible=true;
            if(pair.Key=="Compression" && input[pair.Key]!.GetValue<int>()!=0)visible=true;
            pair.Value.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
        }
        diagram.Input=input;diagram.InvalidateVisual();
    }
    void Changed()
    {
        if(building)return;Result=null;RefreshVisibility();ClearResults();state.Text="Dati modificati · premere Calcola";Modified?.Invoke();
    }
    void ClearResults()
    {
        results.Children.Clear();results.Children.Add(Ui.Text(library.Available ? "Compila i dati e calcola la capacità portante." : "Calcolo in attesa del rilascio approvato della libreria GPC. Puoi preparare e salvare i dati del foglio.",15,true));
        results.Children.Add(Ui.Text("La verifica riguarda una sola combinazione alla volta. Non include scorrimento, cedimenti, stabilità globale, sisma o resistenza strutturale.",12,color:Ui.Muted));
    }
    internal void Commit() { /* Every edit is stored immediately, including incomplete numeric text. */ }
    internal void TryCalculate()
    {
        Result=null;ClearResults();
        try
        {
            Result=library.Calculate(data);results.Children.Clear();
            results.Children.Add(Ui.Text(Result["Status"]!.ToString(),23,true));
            results.Children.Add(Ui.Text(Result["Profile"]!.ToString(),12,color:Ui.Muted));
            foreach(var row in FoundationPresentation.Summary(Result))results.Children.Add(Ui.Text(row.Label+"   "+row.Value,13));
            var coefficients=Ui.Table(["Contributo","N","s","d","i","kPa"],new[]{"Cohesion","Surcharge","Weight"}.Select(k=>new[]{k=="Cohesion"?"Coesione":k=="Surcharge"?"Sovraccarico":"Peso terreno",FoundationPresentation.Value(Result[k]!["N"]),FoundationPresentation.Value(Result[k]!["Shape"]),FoundationPresentation.Value(Result[k]!["Depth"]),FoundationPresentation.Value(Result[k]!["Inclination"]),FoundationPresentation.Value(Result[k]!["Pressure"],1000)}));
            coefficients.Columns[0].Width=150;
            coefficients.Margin=new Thickness(0,14,0,14);results.Children.Add(coefficients);
            results.Children.Add(Ui.Text("Hansen non drenato: coefficienti s, d, i additivi. La pressione resistente è lorda; il valore netto è solo un dettaglio.",12,color:Ui.Muted));
            results.Children.Add(Ui.Text(Result["Reference"]!.ToString(),12,color:Ui.Muted));
            foreach(var warning in Result["Warnings"]!.AsArray())results.Children.Add(Ui.Text(warning!.ToString(),12,color:Ui.Muted));
            state.Text="Calcolo completato · unica combinazione esaminata: "+data["nome_combinazione"];
        }
        catch(Exception e) { Result=null;state.Text=e.Message; }
    }
}

/// <summary>Schematic diagram, deliberately not an engineering geometry calculation.</summary>
internal sealed class FoundationDiagram : FrameworkElement
{
    internal JsonObject? Input {get;set;}
    internal FoundationDiagram() { Height=220;MinWidth=280;Margin=new Thickness(16,12,16,0); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);double w=ActualWidth,c=w/2;
        var muted=Appearance.Foreground(Ui.Muted);
        var pen=new Pen(muted,1.4);var accent=new Pen(Appearance.Accent,2);
        dc.DrawLine(pen,new Point(15,90),new Point(w-15,90));
        dc.DrawRectangle(null,pen,new Rect(c-85,100,170,42));dc.DrawRectangle(null,pen,new Rect(c-20,35,40,65));
        void Label(string text,double x,double y)=>dc.DrawText(new FormattedText(text,CultureInfo.GetCultureInfo("it-IT"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,muted,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));
        void Arrow(double x,double y,double dx,double dy)
        {dc.DrawLine(accent,new Point(x,y),new Point(x+dx,y+dy));dc.DrawLine(accent,new Point(x+dx,y+dy),new Point(x+dx-dx*.18-dy*.13,y+dy-dy*.18+dx*.13));dc.DrawLine(accent,new Point(x+dx,y+dy),new Point(x+dx-dx*.18+dy*.13,y+dy-dy*.18-dx*.13));}
        Arrow(c,8,0,58);Arrow(c+25,63,65,0);Label("N (+ compressione)",c+9,7);Label("Hx (+X)",c+40,42);
        dc.DrawLine(pen,new Point(c-85,162),new Point(c+85,162));Label("B · direzione X",c-45,165);
        Label("D",c-110,106);Label("Sezione schematica · piano di posa orizzontale",15,195);
        if(Input?["WaterEnabled"]?.GetValue<bool>()==true){var water=new Pen(Brushes.SteelBlue,1){DashStyle=DashStyles.Dash};dc.DrawLine(water,new Point(15,151),new Point(w-15,151));Label("Falda (quota simbolica)",15,72);}
    }
}
