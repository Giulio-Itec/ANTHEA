using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Presentation.Modules.Rheology;
using Anthea.Application.Actions;
using Anthea.Presentation.Modules.Climate;
using ANTHEA.ModelViewer.Wpf;
using X.Core;
using X.Desktop.Services;

namespace X.Desktop;

/// <summary>New documentation illustrations, never regression baselines. No native solver, screen or user settings.</summary>
internal static class Rev72GuideFigures
{
    internal static async Task Run(string root, string output)
    {
        Directory.CreateDirectory(output);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentCulture;
        var provenance = new List<object>();
        var factory = ModuleHostBench.SheetEditors();
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/geo_fondazioni_superficiali--hansen-non-drenato-caratteristico.json")))!["data"]!.DeepClone().AsObject();
        var input = fixture["input"]!.AsObject();
        foreach (var (key, value) in new[] { ("Width",2d),("Length",3d),("Depth",1d),("Normal",600d),("HorizontalX",60d),("HorizontalY",0d),("MomentX",0d),("MomentY",0d) }) input[key] = value;
        fixture["stratigrafie"] = new JsonArray(new JsonArray(new JsonObject { ["name"]="Terreno omogeneo",["spessore"]=12,["tipologia"]="Coesivo",["peso_specifico"]=18,["peso_specifico_saturo"]=20,["angolo_attrito"]=30,["coesione_efficace"]=0,["coesione_non_drenata"]=75 }));
        fixture["nome_combinazione"] = "Esempio guida · HB = 60 kN, HL = 0";
        File.WriteAllText(Path.Combine(output,"hansen-input.json"),fixture.ToJsonString(J.Options));
        using (var editor = factory.Open("geo_fondazioni_superficiali",fixture))
        {
            await editor.CalculateAsync();
            if (!editor.HasResults) throw new InvalidOperationException("Hansen example has no result.");
            File.WriteAllText(Path.Combine(output,"hansen-result.json"),editor.Result!.ToJsonString(J.Options));
            await Capture(editor.View,"fondazioni-hansen-hl0.png",1400,1050, () =>
            {
                foreach (var expander in Walk(editor.View).OfType<Expander>()) expander.IsExpanded = true;
                editor.View.UpdateLayout();
                var details = Walk(editor.View).OfType<Expander>().Single(e => e.Name == "Details");
                Walk(details).OfType<ScrollViewer>().First().ScrollToBottom();
            });
            provenance.Add(new { file="fondazioni-hansen-hl0.png",module="geo_fondazioni_superficiali",input="hansen-input.json",result="hansen-result.json",note="Esempio documentale; fixture originale letta e clonata, mai riscritta." });
        }
        using (var editor = factory.Open(ClimateDocument.Snow,Anthea.Application.Modules.ModuleDirectory.CreateData(ClimateDocument.Snow)))
        {
            await editor.CalculateAsync();
            var snow=(ClimateSheetViewModel)((MvvmSheetEditor)editor).Sheet;
            var altitude=snow.Forms.SelectMany(f=>f.Form.Fields).First(f=>f.Key=="AltitudeM");
            altitude.IsFocused=true;altitude.EditText="abc";altitude.IsFocused=false;
            await snow.PendingCalculation;
            if(snow.HasResults || altitude.Error is null)throw new InvalidOperationException("Snow invalid input was not diagnosed.");
            await Capture(editor.View,"azioni-neve-finestra-bassa.png",1400,720);
            provenance.Add(new { file="azioni-neve-finestra-bassa.png",module=ClimateDocument.Snow,note="Dati iniziali con quota abc, errore reale del campo; vista compatta, nessuna dimostrazione della persistenza fra fogli." });
        }
        var rheology = new ConcreteTimeEffectsViewModel(25) { Humidity="50",NotionalSize="200",LoadingAge="7",QueryAge="28" };
        rheology.CalculateCommand.Execute(null);
        if (!rheology.HasResults) throw new InvalidOperationException("Rheology example has no result.");
        await Capture(new ConcreteTimeEffectsView(rheology),"reologia-finestra-bassa.png",1320,720);
        provenance.Add(new { file="reologia-finestra-bassa.png",module="Reologia",note="C25, RH=50%, h0=200 mm, t0=7 giorni; cultura it-IT, colonna destra scorrevole." });
        // Render the actual Checks tab alone: its controls are the production XAML. Do not load the 3D viewport or emulate an imported model.
        using (var model = new ModelViewerControl(null,[],false,_=>{},(_,_,_,_)=>{}))
        {
            var tab=Logical(model).OfType<TabItem>().Single(t=>t.Header?.ToString()=="Verifiche");
            var panel=(FrameworkElement)tab.Content;
            tab.Content=null;panel.DataContext=model.DataContext;panel.Resources=model.Resources;
            // Preserve the appearance inherited from the production parent when isolating its tab.
            if (panel is Control control)
            {
                control.Background=model.Background;control.Foreground=model.Foreground;
                control.FontFamily=model.FontFamily;control.FontSize=model.FontSize;
            }
            await Capture(panel,"modelli-verifiche-perimetro.png",700,650);
        }
        provenance.Add(new { file="modelli-verifiche-perimetro.png",module="Modelli",note="Riquadro Verifiche originale isolato, senza modello caricato. Non rappresenta una verifica eseguita." });
        File.WriteAllText(Path.Combine(output,"manifest.json"),JsonSerializer.Serialize(provenance,new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(output,"esito.txt"),"PASS 4 nuove illustrazioni documentali offscreen; nessun atteso riscritto.\n");

        async Task Capture(FrameworkElement view,string name,int width,int height,Action? prepare=null)
        {
            var window=new Window {Content=view,Width=width,Height=height,Left=-32000,Top=-32000,WindowStartupLocation=WindowStartupLocation.Manual,
                ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize};
            Appearance.Watch(window);Appearance.SetDark(window,false);
            try
            {
                window.Show();window.UpdateLayout();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                prepare?.Invoke();window.UpdateLayout();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                var bitmap=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth),(int)Math.Ceiling(view.ActualHeight),96,96,PixelFormats.Pbgra32);
                bitmap.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream=File.Create(Path.Combine(output,name));encoder.Save(stream);
            }
            finally {window.Content=null;window.Close();}
        }
    }
    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        yield return root;
        if(root is Visual)for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            foreach(var child in Walk(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    private static IEnumerable<DependencyObject> Logical(DependencyObject root)
    {
        yield return root;
        foreach(var item in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach(var child in Logical(item))yield return child;
    }
}
