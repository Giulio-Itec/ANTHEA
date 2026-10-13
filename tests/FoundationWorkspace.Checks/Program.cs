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
            Check(Math.Abs(FoundationMaterials.Profile(data)!.LayerAt(-1000).Soil.FrictionAngle-Math.PI/6)<1e-14,"classi Model riutilizzate e gradi convertiti");
            var originalProfile = FoundationMaterials.Profile(data)!; var viewProfile = FoundationPresentation.SoilProfile(data)!;
            Check(originalProfile.Base == viewProfile.Base && originalProfile.GroundwaterElevation == viewProfile.GroundwaterElevation && originalProfile.Layers.Count == viewProfile.Layers.Count, "profilo grafico posseduto identico");
            foreach (var pair in originalProfile.Layers.Zip(viewProfile.Layers))
                Check(pair.First.Top == pair.Second.Top && pair.First.Bottom == pair.Second.Bottom && pair.First.Soil.Name == pair.Second.Soil.Name && pair.First.Soil.UnitWeight == pair.Second.Soil.UnitWeight && pair.First.Soil.SaturatedUnitWeight == pair.Second.Soil.SaturatedUnitWeight && pair.First.Soil.FrictionAngle == pair.Second.Soil.FrictionAngle && pair.First.Soil.EffectiveCohesion == pair.Second.Soil.EffectiveCohesion, "valori dello strato grafico invariati");
            MaterialChecks(output,data);
            data["input"]!["MomentX"]="12,5";Check(FoundationDocument.LibraryInput(data)["MomentX"]!.GetValue<double>()==12.5e6,"kNm e virgola italiana");
            data["input"]!["MomentX"]=0;
            var roundtrip=JsonNode.Parse(data.ToJsonString())!.AsObject();Check(roundtrip.ToJsonString()==data.ToJsonString(),"riapertura documento senza perdita");
            var archive=X.Core.Archivio.Documento(FoundationDocument.Module);archive["dati"]=data.DeepClone();
            string file=Path.Combine(output,"fondazione.x");X.Core.Archivio.Scrivi(file,archive);
            Check(JsonNode.DeepEquals(data,X.Core.Archivio.Leggi(file)["dati"]),"salvataggio e lettura archivio ANTHEA");
            roundtrip["input"]!["Width"]="abc";FoundationDocument.ValidateShape(roundtrip);Reject(()=>FoundationDocument.LibraryInput(roundtrip),"testo incompleto conservato e rifiutato dal calcolo");
            roundtrip["input"]!["Width"]="NaN";Reject(()=>FoundationDocument.LibraryInput(roundtrip),"NaN");
            roundtrip["input"]!["Method"]=99;Reject(()=>FoundationDocument.ValidateShape(roundtrip),"metodo sconosciuto in archivio");
            var installed=new FoundationLibrary();
            Check(installed.Available,"contratto del rilascio distribuito disponibile: nessun fallback silenzioso");
            var unavailable=new FoundationLibrary(typeof(Program).Assembly);
            bool blocked=false;try{unavailable.Calculate(data);}catch(NotSupportedException){blocked=true;}
            Check(!unavailable.Available && blocked,"API assente: nessun risultato simulato");
            VerifyCalculation(output,data,installed);
            var routed=CalculationService.Calculate(FoundationDocument.Module,data);
            Check(Math.Abs(routed["Resistance"]!.GetValue<double>()-1906871.3740371945)<.001,"calcolo dalla DLL installata attraverso il servizio ANTHEA");
            ContractChecks(output,data,installed,args.Contains("--ui"));
            if(args.Contains("--ui"))Ui(output,data,installed);
            int candidate=Array.IndexOf(args,"--candidate");
            if(candidate>=0)
            {
                string path=Path.GetFullPath(args[candidate+1]);var context=new CandidateContext(path);
                var library=new FoundationLibrary(context.LoadFromAssemblyPath(path));Check(library.Available,"contratto candidato disponibile");
                VerifyCalculation(output,data,library);
                if(args.Contains("--ui"))
                {
                    Ui(output,data,library);
                    var eccentric=data.DeepClone().AsObject();eccentric["input"]!["MomentX"]=450;eccentric["input"]!["MomentY"]=300;eccentric["generali"]!["presenza_falda"]=true;eccentric["generali"]!["profondita_falda"]=.5;
                    string extra=Path.Combine(output,"eccentrica");Directory.CreateDirectory(extra);Ui(extra,eccentric,library);
                }
            }
            File.WriteAllText(Path.Combine(output,"esito.txt"),$"PASS {passed} controlli");Console.WriteLine($"PASS {passed} controlli");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void MaterialChecks(string output,JsonObject original)
    {
        JsonObject Sheet(string module,JsonObject? data=null)=>new(){["modulo_id"]=module,["nome"]=module,["dati"]=data??ModuleCatalog.CreateData(module)};
        var foundation=Sheet(FoundationDocument.Module,original.DeepClone().AsObject());
        var exported=X.Core.SoilProfileTransfer.Extract(FoundationDocument.Module,foundation["dati"]!.AsObject());
        var pile=Sheet("geo_palo_verticale",X.Core.SoilProfileTransfer.Create("geo_palo_verticale",exported));
        var returned=X.Core.SoilProfileTransfer.Create(FoundationDocument.Module,X.Core.SoilProfileTransfer.Extract("geo_palo_verticale",pile["dati"]!.AsObject()));
        Check(JsonNode.DeepEquals(returned["stratigrafie"],foundation["dati"]!["stratigrafie"]),"trasferimento terreno fondazione-palo-fondazione senza perdita");
        var material=Sheet("mat_calcestruzzo");material["dati"]!["classe"]="C35/45";
        var root=new JsonObject { ["nome"]="Progetto",["fogli"]=new JsonArray(material,pile,foundation),["sezioni"]=new JsonArray() };
        Check(X.Core.ProjectSharedData.SupportsSharedSoils(foundation),"Progetti: fondazione tra i moduli terreno");
        pile["dati"]!["generali"]!["presenza_falda"]=true;
        Check(X.Core.ProjectSharedData.Common(pile,foundation).Count(p=>p.Source.Key.StartsWith("Strato · "))==14,"Progetti: sette proprietà per ogni strato comuni al palo");
        pile["dati"]!["stratigrafie"]![0]![1]!["angolo_attrito"]=33;
        pile["dati"]!["generali"]!["presenza_falda"]=true;pile["dati"]!["generali"]!["profondita_falda"]=.6;
        Check(X.Core.ProjectSharedData.Apply(pile,root,["Terreno"],foundation)==1,"Progetti: applicazione terreno");
        var profile=FoundationMaterials.Profile(foundation["dati"]!.AsObject())!;
        Check(Math.Abs(profile.LayerAt(-1000).Soil.FrictionAngle-33*Math.PI/180)<1e-14 && profile.GroundwaterElevation==-600,"Progetti: terreno e falda arrivano al Model");
        Check(X.Core.ProjectSharedData.Apply(material,root,["Materiali"],foundation)==1,"Progetti: classe CLS comune");
        Check(foundation["dati"]!["sezione"]!["classe_cls"]!.ToString()=="C35/45" && foundation["dati"]!["sezione"]!["fck_mpa"]!.GetValue<double>()==35,"Progetti: nome CLS e fck coerenti");
        var report=new X.Core.ProjectReportPlan(root);Check(report.Common.Any(c=>c.Field.Key=="CLS · fck [MPa]" && c.Sheets.Contains(foundation)),"report progetto: CLS comune");
        Check(!report.LocalInputs(foundation).Any(r=>r.Label.Contains("FrictionAngle")),"report progetto: nessun parametro terreno legacy duplicato");
        File.WriteAllText(Path.Combine(output,"progetto-materiali.json"),root.ToJsonString(new(){WriteIndented=true}));
        var roundtrip=JsonNode.Parse(root.ToJsonString())!.AsObject();Check(new X.Core.ProjectReportPlan(roundtrip).Common.Count==report.Common.Count,"riapertura progetto mantiene dati condivisi");
        var fdata=foundation["dati"]!.AsObject();var layer=FoundationMaterials.Layers(fdata)[1]!;layer["peso_specifico_saturo"]="";
        Check(FoundationMaterials.Profile(fdata)!.LayerAt(-1000).Soil.SaturatedUnitWeight==18e-6,"γsat vuoto segue γ come nel palo verticale");
        layer["peso_specifico"]=19;Check(Math.Abs(FoundationMaterials.Profile(fdata)!.LayerAt(-1000).Soil.SaturatedUnitWeight-19e-6)<1e-15,"γsat automatico segue successive modifiche γ");
        var legacy=original.DeepClone().AsObject();legacy.Remove("stratigrafie");legacy.Remove("generali");legacy.Remove("sezione");
        Reject(()=>FoundationDocument.ValidateShape(legacy),"versione 2 senza profilo non ripiega sui dati legacy");legacy["versione_fondazioni"]=1;
        FoundationDocument.ValidateShape(legacy);
        legacy["input"]!["CoverUnitWeight"]=17;FoundationMaterials.Complete(legacy);Check(Math.Abs(FoundationMaterials.Profile(legacy)!.Layers[0].Soil.UnitWeight-17e-6)<1e-15,"migrazione legacy conserva il ricoprimento distinto");
        Check(legacy["versione_fondazioni"]!.GetValue<int>()==2,"migrazione v1 a v2 esplicita per evitare letture errate dalle vecchie applicazioni");
        foreach(string zero in new[]{"0","0.0","0,00"})
        {
            var surface=original.DeepClone().AsObject();surface.Remove("stratigrafie");surface["input"]!["Depth"]=zero;
            FoundationMaterials.Complete(surface);
            Check(FoundationMaterials.Profile(surface)!.Layers.Count==1,"migrazione fondazione a piano campagna: D="+zero);
        }
    }
    static void VerifyCalculation(string output,JsonObject data,FoundationLibrary library)
    {
        string before=data.ToJsonString();var result=library.Calculate(data);Check(before==data.ToJsonString(),"nessuna mutazione del documento");
        Check(Math.Abs(result["Resistance"]!.GetValue<double>()-1906871.3740371945)<.001,"adattatore confrontato con atteso indipendente");
        File.WriteAllText(Path.Combine(output,"risultato.json"),result.ToJsonString(new(){WriteIndented=true}));
        File.WriteAllBytes(Path.Combine(output,"esempio.docx"),FoundationReport.Word("Fondazione rettangolare",result));
        File.WriteAllBytes(Path.Combine(output,"esempio.pdf"),FoundationReport.Pdf("Fondazione rettangolare",result));
        Check(FoundationReport.Pdf("Seconda esportazione",result).Length>5000,"esportazione PDF ripetibile nella sessione");
        Check(File.ReadAllBytes(Path.Combine(output,"esempio.pdf")).Length>5000,"edizioni Word e PDF generate");
    }
    static void ContractChecks(string output,JsonObject data,FoundationLibrary library,bool ui)
    {
        var outside=data.DeepClone().AsObject();outside["input"]!["MomentY"]=660;
        var result=CalculationService.Calculate(FoundationDocument.Module,outside);
        Check(result["Status"]!.ToString()=="Non verificata" && result["DomainExhausted"]!.GetValue<bool>() && result["Resistance"]!.GetValue<double>()==0,"risultante esterna: esito negativo conservato dal servizio");
        Check(result["DemandPressure"] is null && result["Utilization"] is null && result["EffectiveArea"]!.GetValue<double>()==0,"area nulla: rapporti indefiniti restano nulli");
        Check(FoundationPresentation.Summary(result).Single(x=>x.Label=="Indice N/R").Value=="—","indice indefinito non visualizzato come zero");
        File.WriteAllText(Path.Combine(output,"fuori-dominio.json"),result.ToJsonString(new(){WriteIndented=true}));
        File.WriteAllBytes(Path.Combine(output,"fuori-dominio.pdf"),FoundationReport.Pdf("Risultante esterna",result));
        File.WriteAllBytes(Path.Combine(output,"fuori-dominio.docx"),FoundationReport.Word("Risultante esterna",result));
        Check(FoundationPresentation.ReportLines(result).Contains("Esito: Non verificata"),"report conserva esito negativo");
        var hansen=data.DeepClone().AsObject();hansen["input"]!["Method"]=3;hansen["input"]!["Condition"]=1;
        result=library.Calculate(hansen);var term=result["Cohesion"]!;
        // Independent reference: Checker/tests/ShallowFoundation.Tests/independent.py,
        // B=2 m, L=3 m, D=1 m: s'=0.2*2/3, d'=0.4*0.5, i'=0.
        Check(term["Additive"]!.GetValue<bool>() && Math.Abs(term["AdditiveShape"]!.GetValue<double>()-2d/15)<1e-14 && Math.Abs(term["AdditiveDepth"]!.GetValue<double>()-.2)<1e-14,"Hansen: incrementi additivi distinti dai moltiplicatori");
        Check(FoundationPresentation.AdditiveRows(result).Single().SequenceEqual(new[]{"Coesione","0,133","0,2","0","1,333"}),"Hansen: stessi coefficienti in tabella e relazione");
        Check(FoundationPresentation.CoefficientRows(result).First().Skip(2).Take(3).All(v=>v=="—"),"Hansen: nessun moltiplicatore fittizio in tabella");
        Check(FoundationPresentation.ReportLines(result).Any(v=>v.Contains("s′=0,133, d′=0,2, i′=0")),"Hansen: relazione con incrementi effettivi");
        Check(!FoundationPresentation.AdditiveRows(library.Calculate(data)).Any(),"EC7: nessuna correzione Hansen estranea al metodo");
        File.WriteAllText(Path.Combine(output,"hansen.json"),result.ToJsonString(new(){WriteIndented=true}));
        File.WriteAllBytes(Path.Combine(output,"hansen.pdf"),FoundationReport.Pdf("Hansen non drenato",result));
        File.WriteAllBytes(Path.Combine(output,"hansen.docx"),FoundationReport.Word("Hansen non drenato",result));
        if(ui)foreach(var sample in new[]{("fuori-dominio",outside),("hansen",hansen)})
        {string folder=Path.Combine(output,sample.Item1);Directory.CreateDirectory(folder);Ui(folder,sample.Item2,library);}
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
        Check(Anthea.Wiki.WikiCatalog.ForModule(FoundationDocument.Module)!=null,"aiuto contestuale Wiki");
        var found=Anthea.Wiki.WikiCatalog.Search("fondazioni superficiali");
        Check(found.Length>=2,"ricerca Wiki teoria e pratica");
        Check(root.DesiredSize.Width==1250,"layout WPF");
        if(library.Available)
        {
            t.GetMethod("TryCalculate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(control,null);
            var tabs=Descendants(control).OfType<TabControl>().First();
            Check(tabs.Items.Count==6,"sei schede riservate ai grafici");
            for(int i=0;i<tabs.Items.Count;i++)
            {
                tabs.SelectedIndex=i;root.UpdateLayout();
                Check(Descendants(control).OfType<TextBox>().Any(x=>x.Tag?.ToString()=="Width") && Descendants(control).OfType<DataGrid>().Any(),"dati e strati restano visibili con grafico "+i);
                var frame=new RenderTargetBitmap(1250,850,96,96,PixelFormats.Pbgra32);frame.Render(root);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(frame));using(var f=File.Create(Path.Combine(output,"vista-"+i+".png")))png.Save(f);
            }
            tabs.SelectedIndex=3;root.UpdateLayout();
            var collect=Descendants(control).OfType<Button>().Single(b=>b.Tag?.ToString()=="raccogli-input");
            var previousResult=t.GetProperty("Result",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(control);double beforeWidth=tabs.ActualWidth;
            collect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));root.UpdateLayout();
            Check(tabs.ActualWidth>beforeWidth+200 && ReferenceEquals(previousResult,t.GetProperty("Result",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(control)),"raccogli input amplia grafico e conserva risultato");
            Check(data["vista_fondazioni"]!["input_raccolti"]!.GetValue<bool>(),"stato pannelli salvato nel foglio");
            var compactFrame=new RenderTargetBitmap(1250,850,96,96,PixelFormats.Pbgra32);compactFrame.Render(root);var compactPng=new PngBitmapEncoder();compactPng.Frames.Add(BitmapFrame.Create(compactFrame));using(var f=File.Create(Path.Combine(output,"input-raccolti.png")))compactPng.Save(f);
            collect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));root.UpdateLayout();
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
        { if(name.Name=="GPCModel")return typeof(GPC.Model.Geotechnics.SoilProfile).Assembly;var path=resolver.ResolveAssemblyToPath(name);return path is null?null:LoadFromAssemblyPath(path); }
    }
}
