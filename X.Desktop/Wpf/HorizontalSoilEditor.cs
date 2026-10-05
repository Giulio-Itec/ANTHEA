using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;
using GPC.Checkers.Geotechnics.Piles;
namespace X.Desktop;
internal sealed record SoilRowContext(JsonObject Layer,Action Changed);
internal sealed class HorizontalSoilEditor : ContentControl
{
    SoilRowContext? context;JsonRow? row;string type="";bool building;
    public HorizontalSoilEditor(){DataContextChanged+=(_,_)=>Bind();Unloaded+=(_,_)=>{if(row!=null)row.PropertyChanged-=RowChanged;};Loaded+=(_,_)=>Bind();}
    internal HorizontalSoilEditor(JsonObject layer,Action changed){context=new(layer,changed);Build();}
    internal void Commit() { }
    void Bind(){if(row!=null)row.PropertyChanged-=RowChanged;row=DataContext as JsonRow;context=row?.Context as SoilRowContext;if(row!=null)row.PropertyChanged+=RowChanged;Build();}
    void RowChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e){if(context!=null&&type!=context.Layer.S("tipologia")){ElasticHorizontalPile.SelectSoil(context.Layer,"tipologia",context.Layer.S("tipologia"));Build();}}
    internal static readonly double[] Widths=[180,240,88,78,78,100,240];
    internal static FrameworkElement Header()
    {var panel=new StackPanel{Orientation=Orientation.Horizontal};foreach(var (s,i) in new[]{"Addensamento","Determinazione · autore / teoria","Parametro\nunità","Minimo","Massimo","Adottato\nmodificabile","Origine / Info"}.Select((s,i)=>(s,i)))panel.Children.Add(new TextBlock{Text=s,Width=Widths[i],TextAlignment=TextAlignment.Center,Foreground=Brushes.White});return panel;}
    void Build()
    {
        if(context==null||building)return;building=true;
        try{
        var layer=context.Layer;type=layer.S("tipologia");var soil=ElasticHorizontalPile.EnsureSoil(layer);ElasticHorizontalPile.InitializeMean(soil);
        int mode=Array.IndexOf(ElasticHorizontalPile.Laws,soil.S("legge"));bool sand=type=="Granulare";var panel=new StackPanel{Orientation=Orientation.Horizontal};
        void Changed(string key){if(key is "A" or "nh_tabella" or "valore"){soil["origine_scelta"]="Valore modificato dall'utente";soil["scelta_per"]=ElasticHorizontalPile.SelectionKey(soil);}context.Changed();}
        ComboBox Combo(string key,IEnumerable<string> values,int col){var c=new ComboBox{ItemsSource=values.ToArray(),SelectedItem=soil.S(key),Width=Widths[col]-6,Margin=new Thickness(3),Height=28,ToolTip=soil.S(key)};c.SelectionChanged+=(_,_)=>{if(!building&&c.SelectedItem is string s){ElasticHorizontalPile.SelectSoil(layer,key,s);Changed(key);Build();}};panel.Children.Add(c);return c;}
        var clay=ViggianiHorizontalSoil.CohesiveTable.FirstOrDefault(r=>r.Id==soil.S("riga_146")||r.Label==soil.S("riga_146"));
        string Category(string id)=>ViggianiHorizontalSoil.CohesiveTable.Single(r=>r.Id==id).Category;
        if(!sand&&soil["categoria"]==null)soil["categoria"]=clay==null?"Argilla n.c. / debolmente o.c.":Category(clay.Id);
        if(sand)Combo("densita",ViggianiHorizontalSoil.SandTable.Select(s=>s.Density),0);
        else Combo("categoria",ElasticHorizontalPile.SoilCategories,0);
        var lawChoices=sand?new[]{5,3,0,1,2}:soil.S("categoria")=="Argilla sovraconsolidata"?new[]{0,2}:new[]{4,0,1,2};
        var lawBox=new ComboBox{Width=Widths[1]-6,Margin=new Thickness(3),Height=28};
        var choices=lawChoices.SelectMany(m=>m==4?ViggianiHorizontalSoil.CohesiveTable.Where(r=>Category(r.Id)==soil.S("categoria")).Select(r=>(Label:"14.6 · "+r.Author,Mode:m,Row:r.Id)):new[]{(Label:ElasticHorizontalPile.Laws[m],Mode:m,Row:"")}).ToArray();
        lawBox.ItemsSource=choices.Select(c=>c.Label).ToArray();lawBox.SelectedIndex=Array.FindIndex(choices,c=>c.Mode==mode&&(mode!=4||c.Row==clay?.Id));
        lawBox.SelectionChanged+=(_,_)=>{if(building||lawBox.SelectedIndex<0)return;var c=choices[lawBox.SelectedIndex];soil["legge"]=ElasticHorizontalPile.Laws[c.Mode];if(c.Mode==4)ElasticHorizontalPile.SelectSoil(layer,"riga_146",c.Row);else ElasticHorizontalPile.InitializeMean(soil);context.Changed();Build();};panel.Children.Add(lawBox);
        var srow=ViggianiHorizontalSoil.SandTable.FirstOrDefault(r=>r.Density==soil.S("densita"));
        double? min=mode==5?srow?.MinimumA:mode==4?clay?.MinimumNPerCm3:null,max=mode==5?srow?.MaximumA:mode==4?clay?.MaximumNPerCm3:null;
        if(lawBox.SelectedIndex<0)min=max=null;
        void Label(string s,int col)=>panel.Children.Add(new TextBlock{Text=s,Width=Widths[col],VerticalAlignment=VerticalAlignment.Center,TextAlignment=TextAlignment.Center,ToolTip=Description(soil)});
        Label(mode==5?"A [−]":mode is 3 or 4?"nh [N/cm³]":mode==2?"k [kN/m²]":mode==0?"kh [kN/m³]":"nh [kN/m³]",2);
        Label(min?.ToString("G5")??"—",3);Label(max?.ToString("G5")??"—",4);
        string field=mode==5?"A":mode==4?"nh_tabella":"valore";
        if(mode==3)Label(srow==null?"—":$"{srow.DryNhNPerCm3} / {srow.SubmergedNhNPerCm3}*",5);
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
        if(soil.S("legge")==ElasticHorizontalPile.Laws[4]||soil.S("legge")==ElasticHorizontalPile.Laws[5])content.Children.Add(Ui.Bar(Ui.Button("Adotta nuova media",()=>{try{ElasticHorizontalPile.SetMean(soil);context!.Changed();Build();text.Text=Description(soil);}catch(ArgumentException ex){text.Text=ex.Message;}}),Ui.Button("Mantieni valore con nuova fonte",()=>{soil["scelta_per"]=ElasticHorizontalPile.SelectionKey(soil);soil["origine_scelta"]="Valore mantenuto esplicitamente dopo cambio fonte";context!.Changed();Build();text.Text=Description(soil);}))); 
        Ui.Dialog(this,"Parametro dello strato · origine e applicabilità",new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},1080,760).ShowDialog();
    }
    internal static FrameworkElement Tables()
    {
        DataGrid Grid(object rows)=>new(){ItemsSource=(System.Collections.IEnumerable)rows,IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,CanUserDeleteRows=false,Margin=new Thickness(0,5,0,12),MaxHeight=260};
        return Ui.Stack(Ui.Text("Tabella 14.5 · terreni incoerenti",15,true),
            Grid(ViggianiHorizontalSoil.SandTable.Select(r=>new{Addensamento=r.Density,A_min=r.MinimumA,A_max=r.MaximumA,Media_software=ViggianiHorizontalSoil.MeanA(r.Density),A_consigliato_libro=r.RecommendedA,Nh_non_immerso_N_cm3=r.DryNhNPerCm3,Nh_immerso_N_cm3=r.SubmergedNhNPerCm3}).ToArray()),
            Ui.Text("A è adimensionale; nh=A·γ/1,35 (γ′ sotto falda). Le due ultime colonne sono la modalità distinta nh tabellato direttamente. La media software non è il valore consigliato bibliografico.",12),
            Ui.Text("Tabella 14.6 · terreni coesivi",15,true),
            Grid(ViggianiHorizontalSoil.CohesiveTable.Select(r=>new{Terreno=r.Category,Autori=r.Author,Minimo_N_cm3=r.MinimumNPerCm3,Massimo_N_cm3=r.MaximumNPerCm3,Media_software_N_cm3=ViggianiHorizontalSoil.MeanNh(r.Id)}).ToArray()),
            Ui.Text("1 N/cm³ = 1000 kN/m³. Legge lineare: kh=nh·z/D. Argille sovraconsolidate: kh costante assegnato, fuori dalla tabella 14.6. Fonte: Viggiani, PDF 244, pp. stampate 478–479; le categorie e i valori visualizzati provengono dal catalogo Checker usato nel calcolo.",12));
    }
    internal static string Description(JsonObject soil)
    {
        var sand=ViggianiHorizontalSoil.SandTable.FirstOrDefault(r=>r.Density==soil.S("densita"));var clay=ViggianiHorizontalSoil.CohesiveTable.FirstOrDefault(r=>r.Id==soil.S("riga_146")||r.Label==soil.S("riga_146"));string law=soil.S("legge"),text=law;
        if(law==ElasticHorizontalPile.Laws[5]&&sand!=null)text+=$"\nA {sand.MinimumA}–{sand.MaximumA}; media iniziale {ViggianiHorizontalSoil.MeanA(sand.Density)}; consigliato dal libro {sand.RecommendedA}. nh=A γ/1,35; sotto falda γ′=γsat−γw.";
        if(law==ElasticHorizontalPile.Laws[3]&&sand!=null)text+=$"\nnh non immerso {sand.DryNhNPerCm3}; immerso {sand.SubmergedNhNPerCm3} N/cm³. Selezione dalla falda condivisa; dettagli per quota nei risultati.";
        if(law==ElasticHorizontalPile.Laws[4]&&clay!=null)text+=$"\n{clay.Author}: {clay.MinimumNPerCm3}–{clay.MaximumNPerCm3} N/cm³; media software {ViggianiHorizontalSoil.MeanNh(clay.Id)}. Singola fonte, nessuna media tra autori.";
        if(law==ElasticHorizontalPile.Laws[0])text+="\nkh costante: argille sovraconsolidate; assegnazione esplicita.";
        text+="\n"+soil.S("origine_scelta")+"\n"+ViggianiHorizontalSoil.Source;
        if((law==ElasticHorizontalPile.Laws[4]||law==ElasticHorizontalPile.Laws[5])&&soil.S("scelta_per")!=ElasticHorizontalPile.SelectionKey(soil))text+="\nFonte cambiata: adottare la nuova media o confermare il mantenimento del valore.";return text;
    }
}
