using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.HorizontalPile;
namespace X.Desktop;
internal sealed record SoilRowContext(JsonObject Layer,Action Changed);
internal sealed class HorizontalSoilEditor : ContentControl
{
    SoilRowContext? context;JsonRow? row;string type="";bool building;
    public HorizontalSoilEditor(){DataContextChanged+=(_,_)=>Bind();Unloaded+=(_,_)=>{if(row!=null)row.PropertyChanged-=RowChanged;};Loaded+=(_,_)=>Bind();}
    internal HorizontalSoilEditor(JsonObject layer,Action changed){context=new(layer,changed);Build();}
    internal void Commit() { }
    void Bind(){if(row!=null)row.PropertyChanged-=RowChanged;row=DataContext as JsonRow;context=row?.Context as SoilRowContext;if(row!=null)row.PropertyChanged+=RowChanged;Build();}
    void RowChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e){if(context!=null&&type!=context.Layer.S("tipologia")){HorizontalSoilPresentation.Select(context.Layer,"tipologia",context.Layer.S("tipologia"));Build();}}
    internal static readonly double[] Widths=[180,240,88,78,78,100,240];
    internal static FrameworkElement Header()
    {var panel=new StackPanel{Orientation=Orientation.Horizontal};foreach(var (s,i) in HorizontalPileFields.SoilHeadings().Select((s,i)=>(s,i)))panel.Children.Add(new TextBlock{Text=s,Width=Widths[i],TextAlignment=TextAlignment.Center,Foreground=Brushes.White});return panel;}
    void Build()
    {
        if(context==null||building)return;building=true;
        try{
        var layer=context.Layer;type=layer.S("tipologia");var state=HorizontalSoilPresentation.Prepare(layer);var soil=state.Soil;
        int mode=state.Mode;bool sand=state.Sand;var panel=new StackPanel{Orientation=Orientation.Horizontal};
        void Changed(string key){HorizontalSoilPresentation.RecordManualChoice(soil,key);context.Changed();}
        ComboBox Combo(string key,IEnumerable<string> values,int col){var c=new ComboBox{ItemsSource=values.ToArray(),SelectedItem=soil.S(key),Width=Widths[col]-6,Margin=new Thickness(3),Height=28,ToolTip=soil.S(key)};c.SelectionChanged+=(_,_)=>{if(!building&&c.SelectedItem is string s){HorizontalSoilPresentation.Select(layer,key,s);Changed(key);Build();}};panel.Children.Add(c);return c;}
        Combo(sand?"densita":"categoria",state.Categories,0);
        var lawBox=new ComboBox{Width=Widths[1]-6,Margin=new Thickness(3),Height=28};
        var choices=state.Choices;
        lawBox.ItemsSource=choices.Select(c=>c.Label).ToArray();lawBox.SelectedIndex=state.SelectedChoice;
        lawBox.SelectionChanged+=(_,_)=>{if(building||lawBox.SelectedIndex<0)return;var c=choices[lawBox.SelectedIndex];HorizontalSoilPresentation.SelectLaw(layer,c);context.Changed();Build();};panel.Children.Add(lawBox);
        void Label(string s,int col)=>panel.Children.Add(new TextBlock{Text=s,Width=Widths[col],VerticalAlignment=VerticalAlignment.Center,TextAlignment=TextAlignment.Center,ToolTip=Description(soil)});
        Label(state.Parameter,2);Label(state.Minimum,3);Label(state.Maximum,4);
        string field=state.ValueField;
        if(mode==3)Label(state.TabulatedValue,5);
        else{var value=new TextBox{Text=soil.S(field),Width=Widths[5]-6,Margin=new Thickness(3),Height=28,VerticalContentAlignment=VerticalAlignment.Center,ToolTip="Media iniziale della singola riga; il valore adottato è modificabile."};value.TextChanged+=(_,_)=>{if(!building){soil[field]=value.Text;Changed(field);}};panel.Children.Add(value);}
        var info=Ui.Button("ⓘ Tabelle",()=>ShowDetails(soil));info.Width=90;info.Margin=new Thickness(3);info.ToolTip=Description(soil);panel.Children.Add(info);
        if(mode>=3){var over=new CheckBox{Content="nh*",IsChecked=soil.B("override"),Width=45,VerticalAlignment=VerticalAlignment.Center,ToolTip="Override nh [kN/m³], con motivazione in Info. *Tabella sabbie: non immerso / immerso."};over.Click+=(_,_)=>{soil["override"]=over.IsChecked==true;context.Changed();Build();};panel.Children.Add(over);if(soil.B("override")){var value=new TextBox{Text=soil.S("nh_override"),Width=105,Height=28,ToolTip="nh override [kN/m³]"};value.TextChanged+=(_,_)=>{soil["nh_override"]=value.Text;context.Changed();};panel.Children.Add(value);}}
        Content=panel;ToolTip=Description(soil);
        }finally{building=false;}
    }
    void ShowDetails(JsonObject soil)
    {
        var text=Ui.Text(Description(soil),13);var box=new TextBox{Text=soil.S("fonte"),MinWidth=500,TextWrapping=TextWrapping.Wrap,Height=75};box.TextChanged+=(_,_)=>{soil["fonte"]=box.Text;context!.Changed();};
        var content=Ui.Stack(text,Tables(),Ui.Text("Fonte / condizioni / motivazione della scelta",12),box);
        if(soil.B("override")){var reason=new TextBox{Text=soil.S("motivo_override"),Height=50,TextWrapping=TextWrapping.Wrap};reason.TextChanged+=(_,_)=>{soil["motivo_override"]=reason.Text;context!.Changed();};content.Children.Add(Ui.Text("Motivazione dell'override nh",12));content.Children.Add(reason);}
        if(HorizontalSoilPresentation.CanAdoptMean(soil))content.Children.Add(Ui.Bar(Ui.Button("Adotta nuova media",()=>{try{HorizontalSoilPresentation.AdoptMean(soil);context!.Changed();Build();text.Text=Description(soil);}catch(ArgumentException ex){text.Text=ex.Message;}}),Ui.Button("Mantieni valore con nuova fonte",()=>{HorizontalSoilPresentation.KeepValue(soil);context!.Changed();Build();text.Text=Description(soil);})));
        Ui.Dialog(this,"Parametro dello strato · origine e applicabilità",new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},1080,760).ShowDialog();
    }
    internal static FrameworkElement Tables()
    {
        DataGrid Grid(object rows)=>new(){ItemsSource=(System.Collections.IEnumerable)rows,IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,CanUserDeleteRows=false,Margin=new Thickness(0,5,0,12),MaxHeight=260};
        return Ui.Stack(Ui.Text("Tabella 14.5 · terreni incoerenti",15,true),
            Grid(HorizontalSoilPresentation.SandTable()),
            Ui.Text("A è adimensionale; nh=A·γ/1,35 (γ′ sotto falda). Le due ultime colonne sono la modalità distinta nh tabellato direttamente. La media software non è il valore consigliato bibliografico.",12),
            Ui.Text("Tabella 14.6 · terreni coesivi",15,true),
            Grid(HorizontalSoilPresentation.CohesiveTable()),
            Ui.Text("1 N/cm³ = 1000 kN/m³. Legge lineare: kh=nh·z/D. Argille sovraconsolidate: kh costante assegnato, fuori dalla tabella 14.6. Fonte: Viggiani, PDF 244, pp. stampate 478–479; le categorie e i valori visualizzati provengono dal catalogo Checker usato nel calcolo.",12));
    }
    internal static string Description(JsonObject soil) => HorizontalSoilPresentation.Description(soil);
}
