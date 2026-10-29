using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.FoundationWorkspace;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.ShallowFoundation;
using Anthea.Presentation.Services;

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
            string file=Path.Combine(output,"fondazione.x");Anthea.Application.Documents.DocumentService.Save(file,archive);
            Check(JsonNode.DeepEquals(data,Anthea.Application.Documents.DocumentService.Open(file)["dati"]),"salvataggio e lettura archivio ANTHEA");
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
        // Brinch Hansen with an inclined load (1.16.0, FON-HANSEN-01). Independent references: Checker/tests/ShallowFoundation.Tests/
        // califfo-reference.json, cases 426 (drained) and 459 (undrained), B=2 m, L=3 m, D=1 m, φ′=30°, cu=75 kPa, N=600 kN, Hx=60 kN.
        Check(FoundationDocument.Methods[3]=="Brinch Hansen","Hansen: etichetta del metodo senza «carico normale»");
        var inclined=data.DeepClone().AsObject();inclined["input"]!["Method"]=3;inclined["input"]!["HorizontalX"]=60;
        var drained=library.Calculate(inclined);
        Check(Math.Abs(drained["UltimatePressure"]!.GetValue<double>()/0.522631572822004-1)<1e-9 && !drained["DomainExhausted"]!.GetValue<bool>(),"Hansen drenato con H: atteso indipendente (caso 426)");
        var lines=FoundationPresentation.ReportLines(drained);
        Check(lines.Any(v=>v.Contains("direzioni di B′ e di L′")) && lines.Any(v=>v.Contains("si adotta la minore (B′)")) && lines.Any(v=>v.Contains("Con H = 0")),"Hansen drenato con H: relazione con le due direzioni e il carico normale");
        var inclinedUndrained=inclined.DeepClone().AsObject();inclinedUndrained["input"]!["Condition"]=1;
        var undrained=library.Calculate(inclinedUndrained);
        Check(Math.Abs(undrained["UltimatePressure"]!.GetValue<double>()/0.4550353755551324-1)<1e-9,"Hansen non drenato con H: atteso indipendente (caso 459)");
        Check(FoundationPresentation.AdditiveRows(undrained).Single().SequenceEqual(new[]{"Coesione","0","0,133","0","1,133"}),"Hansen non drenato con H: correzioni della direzione di L′ in tabella");
        Check(FoundationPresentation.ReportLines(undrained).Any(v=>v.Contains("0,2 i′c B′/L′")),"Hansen non drenato con H: discontinuità dichiarata nella relazione");
        foreach(var (name,sample) in new[]{("hansen-inclinato",drained),("hansen-inclinato-non-drenato",undrained)})
        {
            File.WriteAllText(Path.Combine(output,name+".json"),sample.ToJsonString(new(){WriteIndented=true}));
            File.WriteAllBytes(Path.Combine(output,name+".pdf"),FoundationReport.Pdf("Hansen con carico inclinato",sample));
            File.WriteAllBytes(Path.Combine(output,name+".docx"),FoundationReport.Word("Hansen con carico inclinato",sample));
        }
        if(ui)foreach(var sample in new[]{("fuori-dominio",outside),("hansen",hansen),("hansen-inclinato",inclined),("hansen-inclinato-non-drenato",inclinedUndrained)})
        {string folder=Path.Combine(output,sample.Item1);Directory.CreateDirectory(folder);Ui(folder,sample.Item2,library);}
    }
    /// <summary>
    /// The sheet of the shallow foundations as the window opens it since the Release switch of 23/10 (f5-pass-geo): the XAML view
    /// (ShallowFoundationSheetView, through its DataTemplate) hosted by MvvmSheetEditor, outside every window (laid out at 1250×850 and
    /// rendered off screen, as before), with <paramref name="library"/> injected in the calculation service of the context
    /// (ModuleServices.ForFoundation: the installed library, or the candidate of --candidate). The checks of the former historic
    /// FoundationWorkspace, read for reflection, on the same identifiers of the view (the field Width of its FieldForm, the button
    /// «raccogli-input», six graphic tabs, the table of the layers).
    /// </summary>
    static void Ui(string output,JsonObject data,FoundationLibrary library)
    {
        var assembly=typeof(X.Desktop.App).Assembly;
        // F5.13 g3 (G9): an application with the resources of App.xaml and no startup, as ModelViewer.UiChecks. X.Desktop.App posts its startup to the
        // dispatcher when it is created and the pumps of this check ran it: since 23/10 App.OnStartup opens the shell of the Release, on the screen, with the
        // arguments of the check as a document to open («Operazione non completata», check hung). Its window kept the application alive between the two
        // calls of Ui: without it the application must not shut down when the window of the first call closes.
        if(Application.Current==null){var app=new ResourcesOnlyApp{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.LoadStyles();}
        assembly.GetType("X.Desktop.Appearance")!.GetMethod("Initialize",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[false]);
        const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        // The continuations of the calculation of the sheet on this thread, as in the window.
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        // The services of the composition root of the product (internal DesktopComposition), as App.OnStartup creates them.
        var composition=Activator.CreateInstance(assembly.GetType("X.Desktop.Services.DesktopComposition",true)!,Members,null,[(Func<Window?>)(()=>null)],null)!;
        var services=(PresentationServices)composition.GetType().GetProperty("Services",Members)!.GetValue(composition)!;
        var hostType=assembly.GetType("X.Desktop.MvvmSheetEditor",true)!;
        var editor=Activator.CreateInstance(hostType,Members,null,[new ShallowFoundationSheetModule(()=>library.Available),data,services,ModuleServices.ForFoundation(library),new CalculationLimiter(CalculationLimiter.DefaultMaximum)],null)!;
        using var disposable=(IDisposable)editor;
        var sheet=(ISheetViewModel)hostType.GetProperty("Sheet",Members)!.GetValue(editor)!;
        var control=(FrameworkElement)hostType.GetProperty("View",Members)!.GetValue(editor)!;
        Check(sheet is ShallowFoundationSheetViewModel,"UI: la vista XAML delle fondazioni ospitata da MvvmSheetEditor");
        var root=new Border { Background=Brushes.White,Child=control,Width=1250,Height=850 };
        // A window outside the screen, never activated (the view lays out inside a presentation source, as in ANTHEA).
        var window=new Window { Content=root,Width=1250,Height=850,Left=-32000,Top=-32000,WindowStartupLocation=WindowStartupLocation.Manual,
            ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize };
        window.Show();
        // The dispatcher runs in nested frames (no Application.Run here): a short pause for the bindings and the layout, the whole calculation.
        void Pump(int milliseconds){Thread.Sleep(milliseconds);Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background,new Action(()=>{}));}
        void Layout(){window.UpdateLayout();Pump(20);window.UpdateLayout();}
        void Calculate()
        {
            sheet.Commit();var task=sheet.CalculateAsync();
            for(int i=0;i<1500 && !task.IsCompleted;i++)Pump(20);
            Check(task.IsCompleted,"UI: calcolo terminato entro 30 s");task.GetAwaiter().GetResult();Layout();
        }
        void Render(string name){var bitmap=new RenderTargetBitmap(1250,850,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var f=File.Create(Path.Combine(output,name));encoder.Save(f);}
        TextBox WidthBox()=>Descendants(control).OfType<TextBox>().First(x=>x.DataContext is FieldViewModel { Key: "Width" } && x.TemplatedParent is not Control);
        Layout();
        Calculate();
        Render(library.Available?"foglio-risultati.png":"foglio.png");
        Check((sheet.Result!=null)==library.Available,"UI: risultato presente solo con API disponibile");
        var width=WidthBox();width.Text="2,5";Layout();
        Check(sheet.Result==null,"modifica invalida risultato");width.Text="2";Layout();
        Check(Anthea.Wiki.WikiCatalog.ForModule(FoundationDocument.Module)!=null,"aiuto contestuale Wiki");
        var found=Anthea.Wiki.WikiCatalog.Search("fondazioni superficiali");
        Check(found.Length>=2,"ricerca Wiki teoria e pratica");
        // WPF layout rounding maps 1250 DIP to the nearest physical pixel (1249,6 DIP at 144 DPI): at most half a pixel, as ui/check-stratigrafia-view.
        var dpi=VisualTreeHelper.GetDpi(root);
        Check(Math.Abs(root.ActualWidth-1250)<=.5/dpi.DpiScaleX+1e-7 && Math.Abs(root.ActualHeight-850)<=.5/dpi.DpiScaleY+1e-7,"layout WPF");
        if(library.Available)
        {
            Calculate();
            var tabs=Descendants(control).OfType<TabControl>().First();
            Check(tabs.Items.Count==6,"sei schede riservate ai grafici");
            for(int i=0;i<tabs.Items.Count;i++)
            {
                tabs.SelectedIndex=i;Layout();
                Check(Descendants(control).OfType<TextBox>().Any(x=>x.DataContext is FieldViewModel { Key: "Width" }) && Descendants(control).OfType<DataGrid>().Any(),"dati e strati restano visibili con grafico "+i);
                Render("vista-"+i+".png");
            }
            tabs.SelectedIndex=3;Layout();
            var collect=Descendants(control).OfType<Button>().Single(b=>b.Tag?.ToString()=="raccogli-input");
            var previousResult=sheet.Result;double beforeWidth=tabs.ActualWidth;
            void Click(ButtonBase button)=>typeof(ButtonBase).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(button,null);
            Click(collect);Layout();
            Check(tabs.ActualWidth>beforeWidth+200 && ReferenceEquals(previousResult,sheet.Result),"raccogli input amplia grafico e conserva risultato");
            Check(sheet.Data["vista_fondazioni"]!["input_raccolti"]!.GetValue<bool>(),"stato pannelli salvato nel foglio");
            Render("input-raccolti.png");
            Click(collect);Layout();
            var appearance=assembly.GetType("X.Desktop.Appearance")!;var modes=assembly.GetType("X.Desktop.AppAppearance")!;
            foreach(string mode in new[]{"Dark","VeryDark"})
            {
                appearance.GetMethod("Set",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[Enum.Parse(modes,mode),false]);
                appearance.GetMethod("SetDark",BindingFlags.Static|BindingFlags.Public)!.Invoke(null,[root,true]);
                appearance.GetMethod("ApplyTree",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[root]);Layout();
                Render("foglio-"+mode+".png");
            }
            appearance.GetMethod("Set",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[Enum.Parse(modes,"Light"),false]);
        }
        window.Content=null;root.Child=null;window.Close();
    }
    /// <summary>An application with the resources of App.xaml of X.Desktop (embedded, sources rebased on the ANTHEA assembly) and no startup (G9).</summary>
    sealed class ResourcesOnlyApp : Application
    {
        internal void LoadStyles()
        {
            using var source=typeof(ResourcesOnlyApp).Assembly.GetManifestResourceStream("FoundationWorkspace.App.xaml")??throw new InvalidOperationException("Risorse di App.xaml mancanti.");
            var document=System.Xml.Linq.XDocument.Load(source);var dictionary=document.Root!.Elements().Single().Elements().Single();
            dictionary.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns+"x","http://schemas.microsoft.com/winfx/2006/xaml");
            dictionary.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns+"local","clr-namespace:X.Desktop;assembly=ANTHEA");
            foreach(var attribute in dictionary.Descendants().SelectMany(e=>e.Attributes("Source")))attribute.Value="/ANTHEA;component/"+attribute.Value;
            Resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Parse(dictionary.ToString());
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
