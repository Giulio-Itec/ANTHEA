using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Anthea.Calculations;
using Anthea.FoundationWorkspace;

internal static class Program
{
    static int passed;
    static void Check(bool ok,string name) { if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name); }
    static void Reject(Action action,string name) {try {action();}catch(ArgumentException){Check(true,name);return;}throw new Exception("Accepted "+name);}
    [STAThread] static int Main(string[] args)
    {
        try
        {
            string output=Path.GetFullPath(args.Length>0?args[0]:"supporto/artefatti/fondazioni-superficiali/workspace");Directory.CreateDirectory(output);
            var data=ModuleCatalog.CreateData(FoundationDocument.Module);FoundationDocument.ValidateShape(data);
            Check(ModuleCatalog.Get(FoundationDocument.Module).Area=="Geotecnica","catalogo geotecnica");
            Reject(()=>FoundationDocument.LibraryInput(data),"conferma A1 obbligatoria");data["azioni_confermate"]=true;
            var input=FoundationDocument.LibraryInput(data);
            Check(input["Width"]!.GetValue<double>()==2000 && input["Normal"]!.GetValue<double>()==600000,"m e kN convertiti");
            Check(Math.Abs(input["FrictionAngle"]!.GetValue<double>()-Math.PI/6)<1e-14,"gradi convertiti");
            data["input"]!["MomentX"]="12,5";Check(FoundationDocument.LibraryInput(data)["MomentX"]!.GetValue<double>()==12.5e6,"kNm e virgola italiana");
            data["input"]!["MomentX"]=0;
            var roundtrip=JsonNode.Parse(data.ToJsonString())!.AsObject();Check(roundtrip.ToJsonString()==data.ToJsonString(),"riapertura documento senza perdita");
            var archive=X.Core.Archivio.Documento(FoundationDocument.Module);archive["dati"]=data.DeepClone();
            string file=Path.Combine(output,"fondazione.x");X.Core.Archivio.Scrivi(file,archive);
            Check(JsonNode.DeepEquals(data,X.Core.Archivio.Leggi(file)["dati"]),"salvataggio e lettura archivio ANTHEA");
            roundtrip["input"]!["Width"]="abc";FoundationDocument.ValidateShape(roundtrip);Reject(()=>FoundationDocument.LibraryInput(roundtrip),"testo incompleto conservato e rifiutato dal calcolo");
            roundtrip["input"]!["Width"]="NaN";Reject(()=>FoundationDocument.LibraryInput(roundtrip),"NaN");
            roundtrip["input"]!["Method"]=99;Reject(()=>FoundationDocument.ValidateShape(roundtrip),"metodo sconosciuto in archivio");
            var old=new FoundationLibrary();
            if(!old.Available) {bool blocked=false;try{old.Calculate(data);}catch(NotSupportedException){blocked=true;}Check(blocked,"DLL precedente: nessun risultato simulato");}
            if(args.Contains("--ui"))Ui(output,data,old);
            int candidate=Array.IndexOf(args,"--candidate");
            if(candidate>=0)
            {
                string path=Path.GetFullPath(args[candidate+1]);var context=new CandidateContext(path);
                var library=new FoundationLibrary(context.LoadFromAssemblyPath(path));Check(library.Available,"contratto candidato disponibile");
                string before=data.ToJsonString();var result=library.Calculate(data);Check(before==data.ToJsonString(),"nessuna mutazione del documento");
                Check(Math.Abs(result["Resistance"]!.GetValue<double>()-1906871.3740371945)<.001,"adattatore confrontato con atteso indipendente");
                File.WriteAllText(Path.Combine(output,"risultato.json"),result.ToJsonString(new(){WriteIndented=true}));
                File.WriteAllBytes(Path.Combine(output,"esempio.docx"),FoundationReport.Word("Fondazione rettangolare",result));
                File.WriteAllBytes(Path.Combine(output,"esempio.pdf"),FoundationReport.Pdf("Fondazione rettangolare",result));
                Check(FoundationReport.Pdf("Seconda esportazione",result).Length>5000,"esportazione PDF ripetibile nella sessione");
                Check(File.ReadAllBytes(Path.Combine(output,"esempio.pdf")).Length>5000,"edizioni Word e PDF generate");
                if(args.Contains("--ui"))Ui(output,data,library);
            }
            File.WriteAllText(Path.Combine(output,"esito.txt"),$"PASS {passed} controlli");Console.WriteLine($"PASS {passed} controlli");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void Ui(string output,JsonObject data,FoundationLibrary library)
    {
        var assembly=typeof(X.Desktop.App).Assembly;
        if(Application.Current==null){var app=new X.Desktop.App();app.InitializeComponent();}
        assembly.GetType("X.Desktop.Appearance")!.GetMethod("Initialize",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[false]);
        var t=assembly.GetType("X.Desktop.FoundationWorkspace",true)!;
        var control=(FrameworkElement)Activator.CreateInstance(t,BindingFlags.Instance|BindingFlags.NonPublic,null,[data,library],null)!;
        t.GetMethod("TryCalculate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(control,null);
        var root=new Border { Background=Brushes.White,Child=control,Width=1250,Height=850 };
        root.Measure(new Size(1250,850));root.Arrange(new Rect(0,0,1250,850));root.UpdateLayout();
        var bitmap=new RenderTargetBitmap(1250,850,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(output,library.Available?"foglio-risultati.png":"foglio.png")))encoder.Save(f);
        Check((t.GetProperty("Result",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(control)!=null)==library.Available,"UI: risultato presente solo con API disponibile");
        var width=Descendants(control).OfType<TextBox>().First(x=>x.Tag?.ToString()=="Width");width.Text="2,5";
        Check(t.GetProperty("Result",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(control)==null,"modifica invalida risultato");width.Text="2";
        var wiki=assembly.GetType("X.Desktop.WikiCatalog")!;Check(wiki.GetMethod("ForModule",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[FoundationDocument.Module])!=null,"aiuto contestuale Wiki");
        var found=(Array)wiki.GetMethod("Search",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,["fondazioni superficiali"] )!;
        Check(found.Length>=2,"ricerca Wiki teoria e pratica");
        Check(root.DesiredSize.Width==1250,"layout WPF");
        if(library.Available)
        {
            t.GetMethod("TryCalculate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(control,null);
            var appearance=assembly.GetType("X.Desktop.Appearance")!;var modes=assembly.GetType("X.Desktop.AppAppearance")!;
            foreach(string mode in new[]{"Dark","VeryDark"})
            {
                appearance.GetMethod("Set",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[Enum.Parse(modes,mode),false]);
                appearance.GetMethod("SetDark",BindingFlags.Static|BindingFlags.Public)!.Invoke(null,[root,true]);
                appearance.GetMethod("ApplyTree",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[root]);root.UpdateLayout();
                var frame=new RenderTargetBitmap(1250,850,96,96,PixelFormats.Pbgra32);frame.Render(root);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(frame));using(var f=File.Create(Path.Combine(output,"foglio-"+mode+".png")))png.Save(f);
            }
            appearance.GetMethod("Set",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[Enum.Parse(modes,"Light"),false]);
        }
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}}
    sealed class CandidateContext(string main) : AssemblyLoadContext("foundation-source-tests",true)
    {
        readonly AssemblyDependencyResolver resolver=new(main);
        protected override Assembly? Load(AssemblyName name)
        { var path=resolver.ResolveAssemblyToPath(name);return path is null?null:LoadFromAssemblyPath(path); }
    }
}
