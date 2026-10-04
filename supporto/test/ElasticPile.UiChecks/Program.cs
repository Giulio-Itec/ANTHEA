using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Calculations;
using X.Core;
static class Program
{
    const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool b,string text){if(!b)throw new Exception(text);Console.WriteLine("PASS "+text);checks++;}
    static void Wait(Task task){var frame=new DispatcherFrame();task.ContinueWith(_=>Application.Current.Dispatcher.BeginInvoke(()=>frame.Continue=false));Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();}
    static void WaitFor(Func<bool> condition){var done=new TaskCompletionSource<bool>();var deadline=DateTime.UtcNow.AddSeconds(10);var poll=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(25)};poll.Tick+=(_,_)=>{if(condition()){poll.Stop();done.SetResult(true);}else if(DateTime.UtcNow>deadline){poll.Stop();done.SetException(new TimeoutException("Automatic recalculation did not complete."));}};poll.Start();Wait(done.Task);}
    static void Snapshot(FrameworkElement view,string path,int width=1400,int height=1050){view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();var b=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);b.Render(view);var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(path);e.Save(f);}
    [STAThread] static int Main(string[] args)
    {
        string dir=Path.GetFullPath(args.Length>0?args[0]:"supporto/artefatti/palo-elastico");Directory.CreateDirectory(dir);
        try
        {
            var app=new TestApp();app.LoadStyles();app.ShutdownMode=ShutdownMode.OnExplicitShutdown; SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var parent=PaloOrizzontale.Defaults();var input=ElasticHorizontalPile.Defaults(parent);input["EI"]=50000;input["fonte_EI"]="Benchmark elastico EI assegnato";input["lunghezza"]=12;input["strati"]=new JsonArray(J.Obj(("nome","Strato A"),("spessore",4),("legge",ElasticHorizontalPile.Laws[0]),("valore",10000),("fonte","Esempio numerico assegnato")),J.Obj(("nome","Strato B"),("spessore",8),("legge",ElasticHorizontalPile.Laws[1]),("valore",5000),("fonte","Esempio numerico assegnato")));parent["elastico"]=input;parent["vista_orizzontale"]="elastico";
            string before=parent.ToJsonString();var result=CalculationService.Calculate(PaloOrizzontale.Module,parent);Check(result.S("tipo_risultato")=="palo_elastico","calculation service elastic routing");Check(parent.ToJsonString()==before,"adapter input unchanged");
            input["falda"]=true;input["z_falda"]=2;input["gamma_w"]=9.81;input["strati"]![0]!["legge"]=ElasticHorizontalPile.Laws[3];input["strati"]![0]!["densita"]="Medio";
            input["strati"]![1]!["legge"]=ElasticHorizontalPile.Laws[4];input["strati"]![1]!["riga_146"]="Argilla n.c. o lievemente o.c. — Reese, Matlock, 1956";input["strati"]![1]!["nh_tabella"]=1.2;
            var type=typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.ElasticPileWorkspace")!;var ui=(FrameworkElement)Activator.CreateInstance(type,flags,null,[parent],null)!;
            void Calculate()=>Wait((Task)type.GetMethod("CalculateAsync",flags)!.Invoke(ui,null)!);
            JsonObject? Result()=>type.GetProperty("Result",flags)!.GetValue(ui) as JsonObject;
            Calculate();Check(Result()!=null,"UI elastic response");Check(Result()!.Array("parametri_terreno").Count==3,"UI resolves water and cohesive table");
            var soilForm=type.GetField("soilForm",flags)!.GetValue(ui)!;var editors=(System.Collections.IDictionary)soilForm.GetType().GetField("Editors",flags)!.GetValue(soilForm)!;Check(editors.Contains("densita")&&!editors.Contains("A")&&!editors.Contains("valore"),"only table-specific inputs shown");
            var layerGrid=(DataGrid)type.GetField("layers",flags)!.GetValue(ui)!;var layerRow=layerGrid.Items[0];input["strati"]![0]!["A"]=600;input["strati"]![0]!["gamma"]=18;input["strati"]![0]!["gamma_sat"]=20;layerRow.GetType().GetProperty("Item")!.SetValue(layerRow,ElasticHorizontalPile.Laws[5],["legge"]);Check(Result()==null,"soil mode change invalidates result");Calculate();Check(Math.Abs(Result()!.Array("parametri_terreno")[0].D("BaseValue")-8000)<1e-8,"correlation mode through UI");
            Snapshot(ui,Path.Combine(dir,"vig-current-ui.png"));
            var drawing=(FrameworkElement)type.GetField("drawing",flags)!.GetValue(ui)!;Snapshot(drawing,Path.Combine(dir,"vig-current-diagrams.png"),1400,560);
            double previousY=Result()!["risposta"].D("HeadDisplacement");var form=type.GetField("form",flags)!.GetValue(ui)!;form.GetType().GetMethod("Set",flags)!.Invoke(form,["H","200",false]);Check(Result()==null,"immediate stale invalidation");WaitFor(()=>Result()!=null);Check(Math.Abs(Result()!["risposta"].D("HeadDisplacement")/previousY-2)<1e-8,"automatic recalculation changed load");
            form.GetType().GetMethod("Set",flags)!.Invoke(form,["EI","",false]);Calculate();Check(Result()==null,"invalid inputs block export");form.GetType().GetMethod("Set",flags)!.Invoke(form,["EI","50000",false]);Calculate();
            var finalResult=Result()!;((IDisposable)ui).Dispose();
            var hostType=typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.HorizontalWorkspace")!;var host=(FrameworkElement)Activator.CreateInstance(hostType,flags,null,[parent],null)!;Wait((Task)hostType.GetMethod("CalculateActiveAsync",flags)!.Invoke(host,null)!);Check(hostType.GetProperty("ActiveResult",flags)!.GetValue(host) is JsonObject active && active.S("tipo_risultato")=="palo_elastico","existing horizontal workspace active result");((IDisposable)host).Dispose();
            var csv=ElasticHorizontalPile.Csv(finalResult);var report=ReportElasticPile.Create("Esempio palo su terreno stratificato",finalResult);
            using(var zip=new System.IO.Compression.ZipArchive(new MemoryStream(report))){using var reader=new StreamReader(zip.GetEntry("word/document.xml")!.Open());var xml=reader.ReadToEnd();Check(xml.Contains("4528")&&xml.Contains("Reese, Matlock"),"report contains water correlation and source");Check(!xml.Contains("SandCorrelation")&&xml.Contains("Rigidezze del terreno e reazione sul palo"),"report readable labels and separate numeric tables");}
            Check(csv.Contains("# PARAMETRI")&&csv.Contains("nh [kN/m3]"),"CSV includes parameter provenance and units");
            File.WriteAllText(Path.Combine(dir,"ui-checks.txt"),$"PASS {checks} checks");Console.WriteLine($"TOTAL {checks}");
            File.WriteAllText(Path.Combine(dir,"elastic-input.json"),parent.ToJsonString(J.Options));File.WriteAllText(Path.Combine(dir,"elastic-result.json"),finalResult.ToJsonString(J.Options));File.WriteAllText(Path.Combine(dir,"elastic.csv"),csv);
            File.WriteAllBytes(Path.Combine(dir,"esempio-viggiani-finale.docx"),report);Console.WriteLine("Artifacts written");return 0;
        }catch(Exception ex){File.WriteAllText(Path.Combine(dir,"ui-failure.txt"),ex.ToString());Console.Error.WriteLine(ex);return 1;}
    }
}

internal sealed class TestApp : Application { internal void LoadStyles() { var document=System.Xml.Linq.XDocument.Load("X.Desktop/App.xaml"); var dictionary=document.Root!.Elements().Single().Elements().Single(); dictionary.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns+"x","http://schemas.microsoft.com/winfx/2006/xaml"); foreach(var source in dictionary.Descendants().SelectMany(e=>e.Attributes("Source"))) source.Value="/ANTHEA;component/"+source.Value; Resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Parse(dictionary.ToString()); } }
