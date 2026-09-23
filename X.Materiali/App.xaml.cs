using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GPC.Model.Materials;

namespace Materiali;

public record PropertyRow(string Nome, string Simbolo, string Valore, string Unita);
public sealed partial class MaterialWindow : Window
{
    static readonly Brush Navy = Brush("#0B2A4A"), Blue = Brush("#0B5CAD"), Muted = Brush("#64748B");
    static readonly (string Name, double Fck)[] Classes =
    [
        ("C12/15",12), ("C16/20",16), ("C20/25",20), ("C25/30",25), ("C28/35",28),
        ("C30/37",30), ("C32/40",32), ("C35/45",35), ("C40/50",40), ("C45/55",45),
        ("C50/60",50), ("C55/67",55), ("C60/75",60), ("C70/85",70), ("C80/95",80), ("C90/105",90)
    ];
    readonly ComboBox choice = new() { MinWidth=130, Margin=new Thickness(0,4,0,6) };
    readonly TextBlock strength = Text("",32,true), modulus = Text("",32,true), tension = Text("",32,true);
    readonly TextBlock status = Text("",12);
    readonly DataGrid table = new() { IsReadOnly=true, CanUserSortColumns=false, RowHeight=double.NaN, MinRowHeight=34, HorizontalScrollBarVisibility=ScrollBarVisibility.Auto };
    readonly Grid cards = new();
    static Brush Brush(string value) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
    static TextBlock Text(string text, double size=13, bool bold=false) => new() {
        Text=text,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,
        Foreground=Navy,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,3)
    };
    static Border Paper(UIElement child) => new() {
        Child=child,Background=Brushes.White,BorderBrush=Brush("#D8E0EB"),BorderThickness=new Thickness(1),
        Padding=new Thickness(10),Margin=new Thickness(0,0,0,8)
    };
    static StackPanel Stack(params UIElement[] items) { var panel=new StackPanel(); foreach(var item in items) panel.Children.Add(item); return panel; }

    public MaterialWindow()
    {
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Materiali;component/Styles.xaml") });
        Title="ANTHEA · Materiali · Calcestruzzo"; Width=1500; Height=850; MinWidth=740; MinHeight=500;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        WindowState=WindowState.Maximized;
        DisplayAdaptation.Attach(this);
        var root=new DockPanel { Background=Brush("#F3F5F8") };
        var logo=new Image { Source=new BitmapImage(new Uri("pack://application:,,,/Materiali;component/Assets/logo.png")),Width=34,Height=34,Margin=new Thickness(0,0,16,0) };
        var title=Text("ANTHEA",20,true); title.Foreground=Brushes.White;
        var sub=Text("MATERIALI  /  CALCESTRUZZO",12); sub.Foreground=Brush("#C4D8EE");
        var heading=new StackPanel { Orientation=Orientation.Horizontal }; heading.Children.Add(logo); heading.Children.Add(Stack(title,sub));
        var head=new Border { Background=Navy,Padding=new Thickness(16,8,16,8),Child=heading };
        DockPanel.SetDock(head,Dock.Top); root.Children.Add(head);
        var footer=new Border { Padding=new Thickness(24,8,24,8),Background=Brushes.White,Child=status };
        DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
        choice.ItemsSource=Classes.Select(x=>x.Name).ToArray();
        choice.SelectionChanged+=(_,_)=>Refresh();
        var input=Field("Classe del calcestruzzo",choice);
        choice.HorizontalAlignment=HorizontalAlignment.Stretch;
        var intro=Stack(Text("Calcestruzzo",28,true),Text("Proprietà meccaniche del materiale",14));
        intro.Margin=new Thickness(0,0,0,18);
        foreach(var (header,path,width) in new[]{("Proprietà","Nome",2.6),("Simbolo","Simbolo",1.0),("Valore","Valore",1.1),("Unità","Unita",.7)})
            table.Columns.Add(new DataGridTextColumn { Header=header,Binding=new Binding(path),Width=new DataGridLength(width,DataGridLengthUnitType.Star),MinWidth=38 });
        var note=Text("Proprietà derivate dal materiale EN1992 della libreria Checker già utilizzata da ANTHEA. Deformazioni riferite al diagramma parabola-rettangolo. Resistenze e deformazioni di compressione mostrate in valore assoluto.",12);
        note.Foreground=Muted;
        var cellStyle=new Style(typeof(TextBlock));
        cellStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap));
        cellStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty,TextAlignment.Center));
        cellStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center));
        var headers=new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader), (Style)FindResource(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)));
        headers.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Center));
        table.ColumnHeaderStyle=headers;
        cellStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(3,4,3,4)));
        foreach(DataGridTextColumn column in table.Columns) column.ElementStyle=cellStyle;
        var properties=Stack(Paper(input),Paper(table),new Expander { Header="Riferimenti delle proprietà", Content=note });
        var body=BuildDetails(properties);
        properties.Children.Insert(2,BuildBond());
        var outer=new ScrollViewer { Content=body, HorizontalScrollBarVisibility=ScrollBarVisibility.Auto, VerticalScrollBarVisibility=ScrollBarVisibility.Disabled };
        outer.SizeChanged+=(_,_)=>ResizeColumns(outer.ActualWidth-20);
        ResizeColumns(Width-36);
        root.Children.Add(outer);
        
        Content=root; choice.SelectedItem="C30/37";
    }
    void ResizeColumns(double available)
    {
        sections.Width=Math.Max(1140,available);
        foreach(var col in sections.ColumnDefinitions) col.Width=new GridLength(sections.Width/3);
        table.Width=sections.Width/3-62; FitTable();
    }
    void FitTable()
    {
        var available=Math.Max(290,table.Width-4);
        double[] fractions=[.45,.18,.23,.14];
        for(int i=0;i<4;i++) table.Columns[i].Width=new DataGridLength(available*fractions[i]);
    }
    void AddCard(int column,string label,TextBlock value,string unit)
    {
        var card=Paper(Stack(Text(label,12),value,Text(unit,12)));
        card.Margin=new Thickness(column==0?0:7,0,column==2?0:7,14);
        Grid.SetColumn(card,column); cards.Children.Add(card);
    }
    static ConcreteMaterialEN1992 Material(double fck) => new("Calcestruzzo",fck,ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);
    static string Number(double value) => Math.Abs(value).ToString("N2",CultureInfo.CurrentCulture);
    void Refresh()
    {
        if(choice.SelectedIndex<0) return;
        var selected=Classes[choice.SelectedIndex];
        var m=Material(selected.Fck);
        strength.Text=Number(m.Fck); tension.Text=Number(m.Fctm); modulus.Text=Number(m.Ecm/1000);
        table.ItemsSource=new PropertyRow[] {
            new("Resistenza caratteristica cilindrica","fck",Number(m.Fck),"MPa"),
            new("Resistenza caratteristica cubica (classe)","Rck",selected.Name.Split('/')[1],"MPa"),
            new("Resistenza media a compressione","fcm",Number(m.Fcm),"MPa"),
            new("Resistenza media a trazione","fctm",Number(m.Fctm),"MPa"),
            new("Resistenza caratteristica a trazione · 5%","fctk,0.05",Number(m.Fctk05),"MPa"),
            new("Resistenza caratteristica a trazione · 95%","fctk,0.95",Number(m.Fctk95),"MPa"),
            new("Modulo elastico secante","Ecm",Number(m.Ecm),"MPa"),
            new("Deformazione al picco · parabola-rettangolo","εc2",Number(m.StrainYCompression*1000),"‰"),
            new("Deformazione ultima · parabola-rettangolo","εcu2",Number(m.StrainUCompression*1000),"‰"),
            new("Coefficiente di Poisson","ν",Number(m.Ni),"—")
        };
        status.Text=selected.Name+"  ·  EN 1992-1-1:2004 / UNI EN 206-1:2006";
        RefreshDetails();
    }
    public void Check()
    {
        WindowState=WindowState.Normal;
        foreach(var item in Classes)
        {
            var m=Material(item.Fck);
            if(!double.IsFinite(m.Ecm) || m.Ecm<=0 || Math.Abs(Math.Abs(m.Fck)-item.Fck)>1e-9)
                throw new Exception("Proprietà non valide: "+item.Name);
        }
        var baseline=Material(30);
        if(Math.Abs(Math.Abs(baseline.Fcm)-38)>1e-6 || Math.Abs(baseline.Ecm-32836.568)>1 ||
           Math.Abs(Math.Abs(baseline.Fctm)-2.896468)>1e-5 ||
           Math.Abs(Math.Abs(baseline.StrainUCompression)-.0035)>1e-8)
            throw new Exception("Valori C30/37 inattesi.");
        choice.SelectedItem="C50/60";
        if(strength.Text!=Number(50)) throw new Exception("Aggiornamento classe non riuscito.");
        choice.SelectedItem="C30/37";
        Durability.Check();
        CheckBond();
        NtcCover.Check();
        CheckAutomaticMix();
        CheckExposureSelector();
        exposureChecks["XC1"].IsChecked=false; exposureChecks["XF2"].IsChecked=true;
        if(!coverHeadline.Text.Contains("45")) throw new Exception("XF2 NTC trave errato.");
        choices["deviationControl"].SelectedIndex=1;
        choices["deviationValue"].SelectedItem="5 mm";
        if(!coverHeadline.Text.Contains("40")) throw new Exception("Tolleranza ridotta errata.");
        choices["deviationControl"].SelectedIndex=2;
        choices["deviationValue"].SelectedItem="0 mm";
        if(!coverHeadline.Text.Contains("35")) throw new Exception("Tolleranza zero errata.");
        choices["deviationControl"].SelectedIndex=0;
        if(Selected("deviationValue")!="10 mm" || choices["deviationValue"].Items.Count!=1 || !coverHeadline.Text.Contains("45"))
            throw new Exception("Ripristino tolleranza ordinaria errato.");
        choices["ntcElement"].SelectedIndex=1;
        if(!coverHeadline.Text.Contains("40")) throw new Exception("XF2 NTC piastra errato.");
        choices["coverMethod"].SelectedIndex=1;
        if(coverHeadline.Text!="Da completare") throw new Exception("XF2 EC2 non invalidato.");
        choices["coverMethod"].SelectedIndex=0; choices["ntcElement"].SelectedIndex=0;
        exposureChecks["XF2"].IsChecked=false;

        exposureChecks["XC1"].IsChecked=false; exposureChecks["XS3"].IsChecked=true; exposureChecks["XF4"].IsChecked=true;
        if(!coverHeadline.Text.Contains("60")) throw new Exception("Combinazione esposizioni non aggiornata.");
        numbers["diameter"].Text="NaN";
        if(coverHeadline.Text!="Da completare") throw new Exception("Copriferro obsoleto con input non valido.");
        numbers["diameter"].Text="16";
        choices["consistency"].SelectedIndex=4;
        exposureChecks["XS3"].IsChecked=false; exposureChecks["XF4"].IsChecked=false; exposureChecks["XF2"].IsChecked=true;

        foreach(var width in new[]{1500,1200})
        {
            Width=width;
            foreach(var scroll in columnScrolls) scroll.ScrollToTop();
            Capture(width,0,"alto");
            foreach(var scroll in columnScrolls) scroll.ScrollToBottom();
            Capture(width,0,"basso");
        }
    }
    void Capture(int width,int tab,string position)
    {
        ResizeColumns(width-36);
        UpdateLayout();
        var element=(FrameworkElement)Content;
        element.Measure(new Size(width-16,760)); element.Arrange(new Rect(0,0,width-16,760)); element.UpdateLayout();
        if(tab==0) FitTable();
        element.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)element.ActualWidth,(int)element.ActualHeight,96,96,PixelFormats.Pbgra32);
        bitmap.Render(element); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=File.Create(Path.Combine(AppContext.BaseDirectory,$"anteprima-{width}-{tab}-{position}.png")); encoder.Save(stream);
    }
}



