using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Anthea.Muro;

public sealed class MainWindow : Window
{
    static readonly Brush Navy=new SolidColorBrush(Color.FromRgb(11,42,74)), Muted=new SolidColorBrush(Color.FromRgb(100,116,139));
    readonly Dictionary<string,TextBox> boxes=[];
    readonly TextBox titleBox=new(){MinWidth=200,Text="Nuovo muro"};
    readonly TextBlock status=Text("Compilare i dati oppure caricare l’esempio.");
    readonly StackPanel results=new(), detail=new();
    readonly WallDrawing drawing=new();
    readonly WrapPanel inputs=new();
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(350)};
    readonly List<FrameworkElement> inputCards=[];
    readonly Grid outputGrid=new();
    readonly Border drawingCard, resultCard;
    readonly TabControl tabs=new();
    Result? result;
    string? filename;
    bool filling,dirty;
    internal bool Testing {get;set;}
    public MainWindow()
    {
        Title="ANTHEA · Muro di sostegno";Width=1480;Height=960;MinWidth=780;MinHeight=560;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=new SolidColorBrush(Color.FromRgb(243,245,248));Foreground=Navy;FontFamily=new("Segoe UI");FontSize=13;UseLayoutRounding=true;
        Resources.Add(typeof(TextBox),new Style(typeof(TextBox)){Setters={new Setter(Control.PaddingProperty,new Thickness(6,4,6,4)),new Setter(Control.BorderBrushProperty,new SolidColorBrush(Color.FromRgb(215,224,234)))}});
        Resources.Add(typeof(TabItem),new Style(typeof(TabItem)){Setters={new Setter(Control.PaddingProperty,new Thickness(14,8,14,8)),new Setter(Control.FontSizeProperty,13d)}});
        var header=new DockPanel{Background=Navy,Margin=new Thickness(0),LastChildFill=true};
        var brand=Text("ANTHEA",23,true);brand.Foreground=Brushes.White;brand.Margin=new(24,18,26,18);header.Children.Add(brand);
        var heading=Text("Muro di sostegno",23,true);heading.Foreground=Brushes.White;header.Children.Add(heading);
        var bar=new WrapPanel{Margin=new Thickness(16,8,16,8)};
        foreach(var (name,action) in new (string,Action)[]{("Nuovo",()=>New(false)),("Apri…",Open),("Salva",Save),("Salva con nome…",()=>SaveAs()),("Carica esempio",()=>New(true)),("Relazione HTML…",ExportHtml),("Risultati JSON…",ExportJson)})bar.Children.Add(Button(name,()=>Safe(action)));
        var titleRow=new DockPanel{Margin=new Thickness(22,0,22,12)};var label=Text("Opera / riferimento",13,true);label.Width=150;titleRow.Children.Add(label);titleRow.Children.Add(titleBox);
        var banner=Text("STATICA · A1 + M1 + R3 · Terreno asciutto e orizzontale · Calcoli riferiti a 1 m di muro",13,true);banner.Margin=new(20,12,20,8);
        var work=new StackPanel();work.Children.Add(banner);work.Children.Add(inputs);
        foreach(var group in Input.Fields.GroupBy(f=>f.Group))
        {
            var panel=new StackPanel();panel.Children.Add(Text(group.Key,17,true));
            foreach(var f in group)
            {
                var row=new Grid{Margin=new Thickness(0,7,0,0)};
                foreach(double width in new[]{1d,35,75,50})row.ColumnDefinitions.Add(new(){Width=width==1?new GridLength(1,GridUnitType.Star):new GridLength(width)});
                var name=Text(f.Label,12);name.Margin=new(0,0,4,0);row.Children.Add(name);
                var symbol=Text(f.Symbol,12);Grid.SetColumn(symbol,1);row.Children.Add(symbol);
                var box=new TextBox{Text="",TextAlignment=TextAlignment.Right,ToolTip=$"{f.Label} [{f.Unit}]"};
                System.Windows.Automation.AutomationProperties.SetName(box,f.Label);Grid.SetColumn(box,2);row.Children.Add(box);boxes[f.Key]=box;box.TextChanged+=(_,_)=>Changed();
                var unit=Text(f.Unit,11);unit.Margin=new(6,0,0,0);Grid.SetColumn(unit,3);row.Children.Add(unit);panel.Children.Add(row);
            }
            var card=Card(panel);card.Width=440;card.Margin=new(8);inputCards.Add(card);inputs.Children.Add(card);
        }
        inputs.Margin=new(12,0,12,0);
        drawingCard=Card(drawing);drawingCard.Margin=new(20,10,8,10);drawing.MinHeight=390;
        resultCard=Card(results);resultCard.Margin=new(8,10,20,10);
        outputGrid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});outputGrid.ColumnDefinitions.Add(new(){Width=new GridLength(1.2,GridUnitType.Star)});
        outputGrid.RowDefinitions.Add(new());outputGrid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        outputGrid.Children.Add(drawingCard);Grid.SetColumn(resultCard,1);outputGrid.Children.Add(resultCard);work.Children.Add(outputGrid);
        var limits=Text(Engine.Exclusions,12);limits.Foreground=new SolidColorBrush(Color.FromRgb(137,80,10));limits.Margin=new(22,0,22,18);work.Children.Add(limits);
        tabs.Items.Add(new TabItem{Header="Dati e verifiche",Content=new ScrollViewer{Content=work,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});
        tabs.Items.Add(new TabItem{Header="Combinazioni e sollecitazioni",Content=new ScrollViewer{Content=detail,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto}});
        var method=new StackPanel{Margin=new Thickness(24)};
        foreach(var (head,body) in Report.MethodSections){method.Children.Add(Text(head,18,true));var p=Text(body);p.Margin=new(0,8,0,20);method.Children.Add(p);}
        tabs.Items.Add(new TabItem{Header="Metodo e limiti",Content=new ScrollViewer{Content=method,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}});
        var top=new StackPanel();top.Children.Add(header);top.Children.Add(bar);top.Children.Add(titleRow);
        var root=new DockPanel{Background=Background};root.SetValue(Control.ForegroundProperty,Navy);DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        status.Margin=new(22,10,22,10);DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);root.Children.Add(tabs);Content=root;
        timer.Tick+=(_,_)=>{timer.Stop();Recalculate();};titleBox.TextChanged+=(_,_)=>Changed();
        SizeChanged+=(_,_)=>Adapt();inputs.SizeChanged+=(_,_)=>Adapt();
        Loaded+=(_,_)=>{MaxHeight=SystemParameters.WorkArea.Height;Height=Math.Min(Height,MaxHeight);Width=Math.Min(Width,SystemParameters.WorkArea.Width);Adapt();Recalculate();};
        Closing+=(_,e)=>{if(!ConfirmDiscard())e.Cancel=true;};Closed+=(_,_)=>timer.Stop();
    }
    void Adapt()
    {
        double w=Math.Max(700,inputs.ActualWidth>100?inputs.ActualWidth-8:ActualWidth-110);int cols=w>=1200?3:w>=850?2:1;
        foreach(var c in inputCards)c.Width=Math.Floor((w-16*cols)/cols)-2;
        bool narrow=w<1050;outputGrid.ColumnDefinitions[1].Width=narrow?new GridLength(0):new GridLength(1.2,GridUnitType.Star);
        Grid.SetColumn(resultCard,narrow?0:1);Grid.SetRow(resultCard,narrow?1:0);
    }
    internal Input ReadInput()=>new(){Title=titleBox.Text,Values=boxes.ToDictionary(x=>x.Key,x=>x.Value.Text)};
    internal void LoadInput(Input i)
    {
        filling=true;try{titleBox.Text=i.Title;foreach(var (key,box) in boxes)box.Text=i.Values.GetValueOrDefault(key,"");}finally{filling=false;}
        timer.Stop();Recalculate();
    }
    void Changed(){if(filling)return;dirty=true;result=null;drawing.Result=null;drawing.InvalidateVisual();results.Children.Clear();detail.Children.Clear();status.Text="Dati modificati · aggiornamento…";timer.Stop();timer.Start();}
    internal void Recalculate()
    {
        result=null;results.Children.Clear();detail.Children.Clear();
        try
        {
            result=Engine.Calculate(ReadInput());status.Text="Calcolo aggiornato · verifiche locali disponibili; progetto completo da integrare.";
            results.Children.Add(Text("Verifiche geotecniche locali",19,true));
            results.Children.Add(Text($"B = {F(result.Width)} m     Ka = {F(result.Ka,3)}     Inviluppo di 8 combinazioni",12));
            foreach(var c in result.Checks)
            {
                var p=new StackPanel{Margin=new(0,13,0,0)};
                var t=Text($"{c.Name} · {c.Status}",15,true);t.Foreground=c.Status=="Soddisfatta"?Brushes.DarkGreen:Brushes.Firebrick;p.Children.Add(t);
                p.Children.Add(Text($"{c.Case}: Ed = {F(c.Demand)} / Rd = {F(c.Resistance)} {c.Unit}    η = {(c.Ratio is double r?F(r,3):"non definito")}",12));results.Children.Add(p);
            }
            var k=result.Characteristic;
            results.Children.Add(Text($"\nCaratteristica (coefficienti unitari)\nSpinta Hk = {F(k.Horizontal)} kN/m · Peso Nk = {F(k.Vertical)} kN/m\nEccentricità e = {F(k.Eccentricity,3)} m · pmax = {F(k.MaxPressure)} kPa",12));
            foreach(var note in result.Notes){var t=Text(note,12);t.Margin=new(0,10,0,0);results.Children.Add(t);}
            BuildDetails(result);
        }
        catch(ArgumentException ex){status.Text=ex.Message;results.Children.Add(Text("Dati da completare",18,true));results.Children.Add(Text(ex.Message));}
        drawing.Result=result;drawing.InvalidateVisual();
    }
    void BuildDetails(Result r)
    {
        detail.Margin=new(20);detail.Children.Add(Text("Coefficienti, equilibrio e portanza",20,true));detail.Children.Add(Text(Engine.Factors));
        detail.Children.Add(Table(["Caso","γG,c","γG,t","γQ","H [kN/m]","N [kN/m]","e [m]","B′ [m]","pmax [kPa]","Rd port. [kN/m]"],
            r.Cases.Select(c=>new[]{c.Name,F(c.ConcreteFactor,2),F(c.SoilFactor,2),F(c.LiveFactor,2),F(c.Horizontal),F(c.Vertical),F(c.Eccentricity,3),F(c.EffectiveWidth,3),c.ContactWidth>0?F(c.MaxPressure):"Fuori base",F(c.BearingResistance)})));
        detail.Children.Add(Text("Sollecitazioni agli incastri per il dimensionamento del c.a.",19,true));
        detail.Children.Add(Text("Fusto: momento positivo tende il lato monte. Mensole: M positivo tende il lato inferiore, negativo il superiore. V è al filo del fusto; nessuna riduzione del taglio a distanza d. I valori non sono verifiche di resistenza."));
        detail.Children.Add(Table(["Caso","M fusto","V fusto","M valle","V valle","M monte","V monte"],r.Cases.Select(c=>new[]{c.Name,F(c.StemMoment),F(c.StemShear),c.ContactWidth>0?F(c.ToeMoment):"n.d.",c.ContactWidth>0?F(c.ToeShear):"n.d.",c.ContactWidth>0?F(c.HeelMoment):"n.d.",c.ContactWidth>0?F(c.HeelShear):"n.d."})));
        detail.Children.Add(Text("Momenti in kNm/m; tagli in kN/m. Il calcolo del c.a., delle armature, degli ancoraggi e della fessurazione deve essere completato separatamente.",12));
    }
    static DataGrid Table(string[] headers,IEnumerable<string[]> rows)
    {
        var grid=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,HeadersVisibility=DataGridHeadersVisibility.Column,RowHeight=32,Margin=new(0,12,0,22),MinHeight=300,Background=Brushes.White};
        for(int i=0;i<headers.Length;i++)grid.Columns.Add(new DataGridTextColumn{Header=headers[i],Binding=new Binding($"[{i}]"),MinWidth=80,Width=DataGridLength.SizeToCells});
        grid.ItemsSource=rows.ToList();return grid;
    }
    internal static string F(double n,int decimals=1)=>n.ToString("F"+decimals,CultureInfo.GetCultureInfo("it-IT"));
    static TextBlock Text(string s,double size=13,bool bold=false)=>new(){Text=s,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
    static Border Card(UIElement c)=>new(){Child=c,Background=Brushes.White,Padding=new Thickness(18),BorderBrush=new SolidColorBrush(Color.FromRgb(219,227,235)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4)};
    static Button Button(string label,Action action){var b=new Button{Content=label,Padding=new Thickness(13,7,13,7),Margin=new Thickness(4),Background=Brushes.White,Foreground=Navy,BorderBrush=new SolidColorBrush(Color.FromRgb(205,217,229))};b.Click+=(_,_)=>action();return b;}
    void Safe(Action a){try{a();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Operazione non completata",MessageBoxButton.OK,MessageBoxImage.Error);}}
    bool ConfirmDiscard()=>Testing||!dirty||MessageBox.Show(this,"Abbandonare le modifiche non salvate?","Muro di sostegno",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
    void New(bool example){if(!ConfirmDiscard())return;LoadInput(example?Input.Example():new());filename=null;dirty=example;}
    void Open(){if(!ConfirmDiscard())return;var d=new OpenFileDialog{Filter="Calcolo muro ANTHEA|*.muro.json|JSON|*.json"};if(d.ShowDialog(this)!=true)return;var i=Engine.Open(d.FileName);LoadInput(i);filename=d.FileName;dirty=false;}
    void Save(){if(filename is null){SaveAs();return;}Engine.Save(filename,ReadInput());dirty=false;status.Text="Calcolo salvato: "+filename;}
    void SaveAs(){var d=new SaveFileDialog{Filter="Calcolo muro ANTHEA|*.muro.json",FileName=filename is null?"Muro.muro.json":Path.GetFileName(filename)};if(d.ShowDialog(this)!=true)return;Engine.Save(d.FileName,ReadInput());filename=d.FileName;dirty=false;status.Text="Calcolo salvato: "+filename;}
    Result Current(){timer.Stop();Recalculate();return result??throw new InvalidOperationException("Completare i dati prima di esportare i risultati.");}
    void ExportHtml(){var r=Current();var d=new SaveFileDialog{Filter="Relazione HTML|*.html",FileName="Relazione muro.html"};if(d.ShowDialog(this)==true){File.WriteAllText(d.FileName,Report.Html(r));status.Text="Relazione salvata. Aprirla nel browser per stamparla o salvarla in PDF.";}}
    void ExportJson(){var r=Current();var d=new SaveFileDialog{Filter="Risultati JSON|*.json",FileName="Risultati muro.json"};if(d.ShowDialog(this)==true)File.WriteAllText(d.FileName,JsonSerializer.Serialize(r,Engine.JsonOptions));}
    internal void Snapshot(string file)
    {
        UpdateLayout();var content=(FrameworkElement)Content;var b=new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,PixelFormats.Pbgra32);b.Render(content);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(file);encoder.Save(f);
    }
    internal bool HasResult=>result is not null;
    internal void SetInput(string key,string text)=>boxes[key].Text=text;
    internal void SelectDetails()=>tabs.SelectedIndex=1;
    internal void ScrollCalculationToBottom(){tabs.SelectedIndex=0;((ScrollViewer)((TabItem)tabs.Items[0]).Content).ScrollToBottom();}
    internal void SelectMethod()=>tabs.SelectedIndex=2;
}

public sealed class WallDrawing : FrameworkElement
{
    public Result? Result {get;set;}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);double w=ActualWidth,h=ActualHeight; if(w<50||h<50)return;
        void Label(string text,double x,double y,double size=12)=>dc.DrawText(new FormattedText(text,CultureInfo.GetCultureInfo("it-IT"),FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,Brushes.DarkSlateGray,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(x,y));
        Label("Sezione e pressioni caratteristiche",12,6,16);
        if(Result is not {} r){Label("Il disegno appare con i dati completi.",12,70);return;}
        var v=r.Input.Parse();double total=v["H"]+v["D"],s=Math.Min((w-110)/(r.Width+1),(h-135)/total),ox=38,by=h-90;
        Point P(double x,double y)=>new(ox+x*s,by-y*s);
        var outline=new Pen(Brushes.SlateGray,1.6);var earth=new SolidColorBrush(Color.FromRgb(231,224,206));
        dc.DrawRectangle(earth,null,new Rect(P(v["Toe"]+v["T"],total),P(r.Width+.7,v["D"])));
        dc.DrawRectangle(Brushes.LightSlateGray,outline,new Rect(P(0,v["D"]),P(r.Width,0)));
        dc.DrawRectangle(Brushes.LightSlateGray,outline,new Rect(P(v["Toe"],total),P(v["Toe"]+v["T"],v["D"])));
        dc.DrawLine(outline,new Point(ox-20,by),new Point(Math.Min(w-10,ox+(r.Width+.7)*s),by));
        Label("VALLE",ox,42);Label("MONTE",ox+(v["Toe"]+v["T"])*s+12,42);
        Label($"H = {MainWindow.F(v["H"])} m",ox+4,by-total*s/2);
        Label($"B = {MainWindow.F(r.Width)} m",ox+r.Width*s/2-28,by+64);
        var k=r.Characteristic;
        if(k.ContactWidth>0)
        {
            var geometry=new StreamGeometry();using(var c=geometry.Open())
            {
                c.BeginFigure(new(ox,by+8),true,true);
                for(int i=0;i<=100;i++){double x=r.Width*i/100;double p=Engine.Pressure(r.Width,k.Vertical,k.X,x);c.LineTo(new(ox+x*s,by+8+42*p/Math.Max(k.MaxPressure,1)),true,false);}
                c.LineTo(new(ox+r.Width*s,by+8),true,false);
            }
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(100,55,136,185)),new Pen(Brushes.SteelBlue,1),geometry);
            Label($"pmax = {MainWindow.F(k.MaxPressure)} kPa",ox+8,by+49);
        }
        else Label("Risultante esterna alla base",ox,by+20);
        double arrowY=by-k.Overturning/k.Horizontal*s;
        dc.DrawLine(new Pen(Brushes.IndianRed,2),new Point(ox+r.Width*s+25,arrowY),new Point(ox+r.Width*s-20,arrowY));
        dc.DrawLine(new Pen(Brushes.IndianRed,2),new Point(ox+r.Width*s-20,arrowY),new Point(ox+r.Width*s-12,arrowY-6));
        dc.DrawLine(new Pen(Brushes.IndianRed,2),new Point(ox+r.Width*s-20,arrowY),new Point(ox+r.Width*s-12,arrowY+6));
        Label($"Hk {MainWindow.F(k.Horizontal)}",Math.Min(w-95,ox+r.Width*s-30),arrowY-26,11);
    }
}
