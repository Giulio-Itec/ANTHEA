using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GPC.Model.Materials;

namespace Materiali;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        var window = new MaterialWindow();
        MainWindow = window;
        if (e.Args.Contains("--check"))
        {
            window.Loaded += (_, _) =>
            {
                try
                {
                    window.Check();
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "verifica.txt"), "OK: proprietà, esposizioni, copriferro, acqua efficace, validazione input e tre schede a 1100/760 px.");
                    Shutdown(0);
                }
                catch (Exception ex)
                {
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "verifica.txt"), ex.ToString());
                    Shutdown(1);
                }
            };
        }
        window.Show();
    }
}

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
    readonly ComboBox choice = new() { MinWidth=200, Margin=new Thickness(0,8,0,16) };
    readonly TextBlock strength = Text("",32,true), modulus = Text("",32,true), tension = Text("",32,true);
    readonly TextBlock status = Text("",12);
    readonly DataGrid table = new() { IsReadOnly=true, CanUserSortColumns=false, MinHeight=430, HorizontalScrollBarVisibility=ScrollBarVisibility.Auto };
    readonly Grid cards = new();
    static Brush Brush(string value) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
    static TextBlock Text(string text, double size=13, bool bold=false) => new() {
        Text=text,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,
        Foreground=Navy,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,3)
    };
    static Border Paper(UIElement child) => new() {
        Child=child,Background=Brushes.White,BorderBrush=Brush("#D8E0EB"),BorderThickness=new Thickness(1),
        Padding=new Thickness(20),Margin=new Thickness(0,0,0,14)
    };
    static StackPanel Stack(params UIElement[] items) { var panel=new StackPanel(); foreach(var item in items) panel.Children.Add(item); return panel; }

    public MaterialWindow()
    {
        Title="ANTHEA · Materiali · Calcestruzzo"; Width=1100; Height=850; MinWidth=620; MinHeight=500;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        MaxHeight=SystemParameters.WorkArea.Height;
        var root=new DockPanel { Background=Brush("#F3F5F8") };
        var logo=new Image { Source=new BitmapImage(new Uri("pack://application:,,,/Materiali;component/Assets/logo.png")),Width=52,Height=52,Margin=new Thickness(0,0,16,0) };
        var title=Text("ANTHEA",24,true); title.Foreground=Brushes.White;
        var sub=Text("MATERIALI  /  CALCESTRUZZO",12); sub.Foreground=Brush("#C4D8EE");
        var heading=new StackPanel { Orientation=Orientation.Horizontal }; heading.Children.Add(logo); heading.Children.Add(Stack(title,sub));
        var head=new Border { Background=Navy,Padding=new Thickness(24,16,24,16),Child=heading };
        DockPanel.SetDock(head,Dock.Top); root.Children.Add(head);
        var footer=new Border { Padding=new Thickness(24,8,24,8),Background=Brushes.White,Child=status };
        DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
        choice.ItemsSource=Classes.Select(x=>x.Name).ToArray();
        choice.SelectionChanged+=(_,_)=>Refresh();
        var input=Stack(Text("Classe del calcestruzzo",17,true),Text("Seleziona la classe per aggiornare automaticamente le proprietà."),choice);
        choice.HorizontalAlignment=HorizontalAlignment.Left;
        var intro=Stack(Text("Calcestruzzo",28,true),Text("Proprietà meccaniche del materiale",14));
        intro.Margin=new Thickness(0,0,0,18);
        cards.ColumnDefinitions.Add(new ColumnDefinition()); cards.ColumnDefinitions.Add(new ColumnDefinition()); cards.ColumnDefinitions.Add(new ColumnDefinition());
        AddCard(0,"Resistenza caratteristica",strength,"fck · MPa");
        AddCard(1,"Resistenza media a trazione",tension,"fctm · MPa");
        AddCard(2,"Modulo elastico secante",modulus,"Ecm · GPa");
        foreach(var (header,path,width) in new[]{("Proprietà","Nome",2.6),("Simbolo","Simbolo",1.0),("Valore","Valore",1.1),("Unità","Unita",.7)})
            table.Columns.Add(new DataGridTextColumn { Header=header,Binding=new Binding(path),Width=new DataGridLength(width,DataGridLengthUnitType.Star),MinWidth=70 });
        var note=Text("Proprietà derivate dal materiale EN1992 della libreria Checker già utilizzata da ANTHEA. Deformazioni riferite al diagramma parabola-rettangolo. Resistenze e deformazioni di compressione mostrate in valore assoluto.",12);
        note.Foreground=Muted;
        var properties=Stack(cards,Paper(Stack(Text("Proprietà derivate",17,true),table)),note);
        properties.Margin=new Thickness(0,16,0,0);
        var body=new DockPanel { Margin=new Thickness(24,16,24,12) };
        var shared=Paper(input); DockPanel.SetDock(shared,Dock.Top); body.Children.Add(shared);
        body.Children.Add(BuildDetails(properties));
        root.Children.Add(body);
        cards.SizeChanged += (_, _) => FitTable();
        Content=root; choice.SelectedItem="C30/37";
    }
    void FitTable()
    {
        table.Width = Math.Max(400, cards.ActualWidth - 42);
        var available = table.Width - 4;
        double[] fractions = [.48, .18, .20, .14];
        for (int i=0; i<4; i++) table.Columns[i].Width = new DataGridLength(available * fractions[i]);
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
        exposureChecks["XC1"].IsChecked=false; exposureChecks["XS3"].IsChecked=true; exposureChecks["XF4"].IsChecked=true;
        if(!coverHeadline.Text.Contains("55")) throw new Exception("Combinazione esposizioni non aggiornata.");
        numbers["cementMass"].Text="360"; numbers["totalWater"].Text="170"; numbers["absorbedWater"].Text="10";
        if(!mixResults.Text.Contains("0,444") || !mixResults.Text.Contains("entro il limite")) throw new Exception("Rapporto acqua/cemento errato.");
        numbers["totalWater"].Text="250";
        if(!mixResults.Text.Contains("supera il limite")) throw new Exception("Superamento a/c non rilevato.");
        numbers["totalWater"].Text="5";
        if(!mixResults.Text.Contains("correggere") || mixResults.Text.Contains("entro il limite")) throw new Exception("Risultato obsoleto con acqua non valida.");
        numbers["totalWater"].Text="170";
        numbers["diameter"].Text="NaN";
        if(coverHeadline.Text!="Da completare") throw new Exception("Copriferro obsoleto con input non valido.");
        numbers["diameter"].Text="16";
        numbers["air"].Text="4"; choices["consistency"].SelectedIndex=4; choices["chloride"].SelectedIndex=2;
        foreach(var width in new[]{1100,760})
        {
            Width=width;
            for(int tab=0;tab<3;tab++)
            {
                sections.SelectedIndex=tab;
                var scroll=(ScrollViewer)((TabItem)sections.Items[tab]).Content;
                scroll.ScrollToTop();
                Capture(width,tab,"alto");
                scroll.ScrollToBottom();
                Capture(width,tab,"basso");
            }
        }
    }
    void Capture(int width,int tab,string position)
    {
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
