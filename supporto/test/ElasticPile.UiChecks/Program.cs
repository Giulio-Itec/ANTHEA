using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Calculations;
using X.Core;
using GPC.Checkers.Geotechnics.Piles;
static partial class Program
{
    const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static bool capture;
    static void Check(bool b,string text){if(!b)throw new Exception(text);Console.WriteLine("PASS "+text);checks++;}
    static void Bad(Action a,string text){try{a();throw new Exception("Accepted "+text);}catch(ArgumentException){Check(true,text);}}
    static void Wait(Task task){var frame=new DispatcherFrame();task.ContinueWith(_=>Application.Current.Dispatcher.BeginInvoke(()=>frame.Continue=false));Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();}
    static void Until(Func<bool> condition){var tcs=new TaskCompletionSource();var end=DateTime.UtcNow.AddSeconds(15);var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};timer.Tick+=(_,_)=>{if(condition()){timer.Stop();tcs.SetResult();}else if(DateTime.UtcNow>end){timer.Stop();tcs.SetException(new TimeoutException());}};timer.Start();Wait(tcs.Task);}
    static void Snapshot(FrameworkElement view,string path,int width=1500,int height=1050)
    {
        view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();if(!capture)return;
        var b=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);var background=new DrawingVisual();using(var drawing=background.RenderOpen())drawing.DrawRectangle(Brushes.White,null,new Rect(0,0,width,height));b.Render(background);b.Render(view);
        // Deterministic lossless off-screen artifact, without an external image codec.
        var pixels=new byte[width*height*4];b.CopyPixels(pixels,width*4,0);using var f=new BinaryWriter(File.Create(Path.ChangeExtension(path,".bmp")));
        f.Write((ushort)0x4d42);f.Write(54+pixels.Length);f.Write(0);f.Write(54);f.Write(40);f.Write(width);f.Write(-height);f.Write((ushort)1);f.Write((ushort)32);f.Write(0);f.Write(pixels.Length);f.Write(3780);f.Write(3780);f.Write(0);f.Write(0);f.Write(pixels);
    }
    static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T value)yield return value;foreach(var d in Descendants<T>(child))yield return d;}}
    static JsonObject Input()
    {
        var root=PaloOrizzontale.Defaults();var a=PaloOrizzontale.Layer();a["nome"]="Sabbia media";a["spessore"]=4;var b=PaloOrizzontale.Layer();b["nome"]="Argilla NC";b["tipologia"]="Coesivo";b["spessore"]=6;
        root["stratigrafie"]=new JsonArray(new JsonArray(a,b));root["generali"]!["tratto_libero"]=2;root["generali"]!["eccentricita"]=3;root["generali"]!["presenza_falda"]=true;root["generali"]!["profondita_falda"]=2;ElasticHorizontalPile.PrepareShared(root);
        var sa=ElasticHorizontalPile.EnsureSoil(a);sa["legge"]=ElasticHorizontalPile.Laws[5];sa["densita"]="Medio";ElasticHorizontalPile.InitializeMean(sa);
        var sb=ElasticHorizontalPile.EnsureSoil(b);sb["legge"]=ElasticHorizontalPile.Laws[4];sb["riga_146"]="Argilla n.c. o lievemente o.c. — Reese, Matlock, 1956";ElasticHorizontalPile.InitializeMean(sb);
        root["elastico"]!["passo"]=.5;root["vista_orizzontale"]="elastico";return root;
    }
    [STAThread] static int Main(string[] args)
    {
        string dir=Path.GetFullPath(args.Length>0?args[0]:"supporto/artefatti/palo-condiviso");Directory.CreateDirectory(dir);
        capture=args.Contains("--images");
        RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
        AppDomain.CurrentDomain.UnhandledException+=(_,e)=>File.WriteAllText(Path.Combine(dir,"unhandled.txt"),e.ExceptionObject.ToString());
        AppDomain.CurrentDomain.ProcessExit+=(_,_)=>File.WriteAllText(Path.Combine(dir,"process-exit.txt"),$"checks={checks}; exit={Environment.ExitCode}");
        try
        {
            if(args.Contains("--inner-ring-only"))return InnerRingChecks(dir,args.Contains("--probe"));
            if(args.Contains("--drawing-only"))return DrawingChecks(dir);
            if(args.Contains("--clarity-only"))return ClarityChecks(dir);
            if(args.Contains("--seismic-only"))return SeismicUiChecks(dir);
            if(args.Contains("--cuts-example-only"))return PileExample(dir,true);
            if(args.Contains("--example-only"))return PileExample(dir);
            if(args.Contains("--visual-only"))
            {
                capture=true;var visualRoot=JsonNode.Parse(File.ReadAllText(Path.Combine(dir,"input.json")))!.AsObject();visualRoot["elastico"]!["dettagli"]!["azioni_progetto"]=true;visualRoot["elastico"]!["dettagli"]!["taglio_confermato"]=true;visualRoot["sezione"]!["transverse_spacing_mm"]=150;
                visualRoot["elastico"]!["tratti"]=new JsonArray(ElasticHorizontalPile.NewSegment("T1",6),ElasticHorizontalPile.NewSegment("T2",null));visualRoot["elastico"]!["tratti"]![1]!["collegato"]=false;visualRoot["elastico"]!["tratti"]![1]!["longitudinal_bar_count"]=12;
                var visualResult=ElasticHorizontalPile.CalculateShared(visualRoot);var visualApp=new TestApp();visualApp.LoadStyles();visualApp.ShutdownMode=ShutdownMode.OnExplicitShutdown;
                var visualEditorType=typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.PileReinforcementEditor")!;var visualEditor=(FrameworkElement)Activator.CreateInstance(visualEditorType,flags,null,[visualRoot,(Action)(()=>{}),(Func<JsonObject?>)(()=>visualResult),null],null)!;
                visualEditor.Measure(new Size(1500,900));visualEditor.Arrange(new Rect(0,0,1500,900));visualEditor.UpdateLayout();
                var inheritedFields=Descendants<TextBox>(visualEditor).Where(b=>b.ToolTip?.ToString()?.Contains("campo non assegnato")==true).ToArray();if(inheritedFields.Length!=3||inheritedFields.Any(b=>string.IsNullOrWhiteSpace(b.Text)))throw new Exception("Partial archive adopted values are not visible");Console.WriteLine("PASS partial archive shows all three adopted values");
                Snapshot(visualEditor,Path.Combine(dir,"tratti-riepilogo.png"),1500,900);var modes=Descendants<ComboBox>(visualEditor).Single(b=>b.Items.Contains("Interferro"));modes.SelectedItem="Interferro";Snapshot(visualEditor,Path.Combine(dir,"tratti-interferro.png"),1500,900);
                foreach(var expand in Descendants<Expander>(visualEditor).Where(e=>e.Header?.ToString()?.StartsWith("Distinta ferri")==true).ToArray())expand.IsExpanded=true;Snapshot(visualEditor,Path.Combine(dir,"tratti-distinta.png"),1500,1100);
                File.WriteAllText(Path.Combine(dir,"visual-input.json"),visualRoot.ToJsonString(J.Options));Console.WriteLine("VISUAL COMPLETE");return 0;
            }
            var fresh=PaloOrizzontale.Defaults();ElasticHorizontalPile.PrepareShared(fresh);Check(fresh["elastico"].D("passo")==.5,"new shared pile defaults to 0.50 m FEM step");
            var freshSteel=fresh["sezione"]!.AsObject();RebarMaterial.CompleteLegacyInput(freshSteel);
            Check(RebarMaterial.Match(freshSteel)?.Name=="B450C","new pile starts with complete B450C catalogue properties after opening");
            Check(RebarMaterial.Keys.All(k=>J.Equivalent(freshSteel[k],SezioneCA.DefaultData()["input"]![k])),"new pile and concrete section share material initialization");
            var oldSteel=SezioneCA.DefaultInput();oldSteel["fyk_mpa"]=500;RebarMaterial.CompleteLegacyInput(oldSteel);
            Check(oldSteel.S("classe_acciaio")=="Personalizzato"&&oldSteel.D("fyk_mpa")==500&&oldSteel.D("steel_fu_mpa")==500,"legacy custom steel preserves its saved strength and constitutive law");
            fresh["elastico"]!["passo"]=.2;ElasticHorizontalPile.PrepareShared(fresh);Check(fresh["elastico"].D("passo")==.2,"saved explicit mesh choice survives reopening");
            Check(ElasticHorizontalPile.Defaults(fresh).D("passo")==.5,"legacy model factory also defaults to 0.50 m");
            var root=Input();var a=root["stratigrafie"]![0]![0]!["reazione_orizzontale"]!.AsObject();Check(a.D("A")==650,"mean A stored once in initial soil inputs");
            var before=root.ToJsonString();var r=ElasticHorizontalPile.CalculateShared(root);Check(root.ToJsonString()==before,"calculation does not mutate archive");Check(r["input"].D("lunghezza")==12&&r["input"].D("libero")==2&&r["input"].D("C")==-100,"shared geometry and ground eccentricity");
            a["A"]="abc";Bad(()=>ElasticHorizontalPile.CalculateShared(root),"invalid adopted value not silently replaced with mean");a["A"]=650;
            var reopened=JsonNode.Parse(root.ToJsonString())!.AsObject();Check(ElasticHorizontalPile.CalculateShared(reopened)["risposta"].D("HeadDisplacement")==r["risposta"].D("HeadDisplacement"),"archive JSON round trip preserves shared response");
            Check(root["elastico"]!["diametro"]==null&&root["elastico"]!["EI"]==null&&root["elastico"]!["strati"]==null,"archive has no duplicate engineering inputs");
            Check(r.Array("parametri_terreno").Count==3&&r.Array("parametri_terreno")[0].D("InitialMean")==650,"water split and mean provenance");
            Check(r["risposta"]!["Section"]!=null&&r["risposta"]!.Array("SectionDemands").Count>0,"EJ and section demand metadata");
            a["A"]=700;ElasticHorizontalPile.InitializeMean(a);Check(a.D("A")==700,"manual value not overwritten");
            a["densita"]="Denso";Bad(()=>ElasticHorizontalPile.CalculateShared(root),"changed category requires explicit resolution");ElasticHorizontalPile.SetMean(a);Check(a.D("A")==2000,"new category mean");a["densita"]="Medio";ElasticHorizontalPile.SetMean(a);
            var multiple=PaloOrizzontale.Defaults();multiple.Array("stratigrafie").Add(new JsonArray(PaloOrizzontale.Layer()));ElasticHorizontalPile.PrepareShared(multiple);Bad(()=>ElasticHorizontalPile.CalculateShared(multiple),"multiple surveys no silent first choice");
            var oldRoot=Input();var legacy=ElasticHorizontalPile.Defaults(oldRoot);legacy["EI"]=50000;legacy["fonte_EI"]="legacy test";legacy["lunghezza"]=12;legacy["libero"]=2;legacy["C"]=25;legacy["H"]=100;legacy["e"]=0;legacy["strati"]=new JsonArray(J.Obj(("nome","Legacy"),("spessore",10),("legge",ElasticHorizontalPile.Laws[0]),("valore",10000),("fonte","Benchmark")));oldRoot["elastico"]=legacy;string old=legacy.ToJsonString();ElasticHorizontalPile.PrepareShared(oldRoot);Bad(()=>ElasticHorizontalPile.CalculateShared(oldRoot),"legacy conflict blocks calculation");
            var main=(JsonObject)oldRoot.DeepClone();ElasticHorizontalPile.ResolveMigration(main,false);Check(main["elastico_legacy"]!.ToJsonString()==old&&main["generali"].D("eccentricita")==3,"choose common keeps legacy and main values");
            ElasticHorizontalPile.ResolveMigration(oldRoot,true);Check(oldRoot.Array("stratigrafie").Count==2&&oldRoot["elastico"].D("stratigrafia")==1&&oldRoot["generali"].D("eccentricita")==1.75,"legacy import appends survey and translates eccentricity");
            var imported=ElasticHorizontalPile.CalculateShared(oldRoot);Check(imported["input"].D("C")==25&&imported["input"].D("EI")==50000,"legacy force couple and EI preserved");
            var importedReference=ElasticHorizontalPile.Calculate(legacy);Check(Math.Abs(imported["risposta"].D("HeadDisplacement")-importedReference["risposta"].D("HeadDisplacement"))<1e-10,"migrated response matches legacy solver");
            var app=new TestApp();app.LoadStyles();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var soilType=typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.HorizontalSoilEditor")!;
            var testLayer=PaloOrizzontale.Layer();testLayer["tipologia"]="Coesivo";var testSoil=ElasticHorizontalPile.EnsureSoil(testLayer);
            testSoil["categoria"]="Argilla n.c. / debolmente o.c.";ElasticHorizontalPile.EnsureSoil(testLayer);Check(testSoil.S("categoria")==ViggianiHorizontalSoil.CohesiveTable[0].Category,"legacy clay category normalized to catalog");
            var soilEditor=(FrameworkElement)Activator.CreateInstance(soilType,flags,null,[testLayer,(Action)(()=>{})],null)!;Snapshot(soilEditor,Path.Combine(dir,"riga.png"),1100,100);
            var categoryBox=Descendants<ComboBox>(soilEditor).Single(b=>b.Items.Contains("Torba"));Check(categoryBox.Items.Cast<string>().Take(4).SequenceEqual(ViggianiHorizontalSoil.CohesiveTable.Select(c=>c.Category).Distinct()),"category choices come directly from Checker");
            categoryBox.SelectedItem="Argilla organica n.c.";Snapshot(soilEditor,Path.Combine(dir,"riga-organica.png"),1100,100);
            var authorBox=Descendants<ComboBox>(soilEditor).Single(b=>b.Items.Cast<string>().Any(v=>v.StartsWith("14.6")));Check(authorBox.Items.Cast<string>().Count(v=>v.StartsWith("14.6"))==2&&authorBox.SelectedIndex>=0&&testSoil.S("riga_146")=="organic-peck-1970","organic category selects only its two authors and updates row");
            Check(Math.Abs(testSoil.D("nh_tabella")-.55)<1e-12,"new organic category initializes correct mean");
            testSoil["nh_tabella"]=.4;testSoil["origine_scelta"]="Valore modificato dall'utente";ElasticHorizontalPile.SelectSoil(testLayer,"riga_146","organic-davisson-1970");Check(testSoil.D("nh_tabella")==.4&&testSoil.S("scelta_per")!=ElasticHorizontalPile.SelectionKey(testSoil),"manual nh retained awaiting explicit confirmation after source change");
            ElasticHorizontalPile.SelectSoil(testLayer,"tipologia","Granulare");Check(testSoil.S("legge")==ElasticHorizontalPile.Laws[5]&&testSoil.D("A")==200&&testSoil.Array("storico_scelte").Count>0,"material change adopts compatible A mode with previous choices archived");
            var tables=(FrameworkElement)soilType.GetMethod("Tables",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;Snapshot(tables,Path.Combine(dir,"tabelle.png"),1000,650);Check(Descendants<DataGrid>(tables).Select(t=>t.Items.Count).SequenceEqual(new[]{3,7}),"Info shows all rows of both actual Checker tables");
            var hd=(FrameworkElement)soilType.GetMethod("Header",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;Snapshot(hd,Path.Combine(dir,"intestazioni.png"),1050,50);var labels=Descendants<TextBlock>(hd).Select(t=>t.Text).ToArray();Check(labels.Contains("Addensamento")&&labels.Contains("Minimo")&&labels.Contains("Massimo")&&!labels.Any(t=>t.Contains("categoria")),"requested concise column labels");
            var hostType=typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.HorizontalWorkspace")!;var host=(FrameworkElement)Activator.CreateInstance(hostType,flags,null,[root],null)!;var ui=(FrameworkElement)hostType.GetField("elastic",flags)!.GetValue(host)!;var type=ui.GetType();
            JsonObject? Result()=>type.GetProperty("Result",flags)!.GetValue(ui) as JsonObject;
            JsonObject? Response()=>type.GetProperty("Response",flags)!.GetValue(ui) as JsonObject;
            int ResponseCalculations()=>(int)type.GetProperty("ResponseCalculations",flags)!.GetValue(ui)!;
            var firstCalculation=(Task)type.GetMethod("CalculateAsync",flags)!.Invoke(ui,null)!;Until(()=>Response()!=null);Check(Result()==null,"FEM becomes available before section verifications complete");Wait(firstCalculation);Check(Result()!=null,"shared workspace response: "+((TextBlock)type.GetField("status",flags)!.GetValue(ui)!).Text);Snapshot(ui,Path.Combine(dir,"ui.png"));
            var editors=(System.Collections.IDictionary)type.GetField("options",flags)!.GetValue(ui)!.GetType().GetField("Editors",flags)!.GetValue(type.GetField("options",flags)!.GetValue(ui))!;Check(editors.Count==2&&editors.Contains("passo")&&editors.Contains("punta"),"elastic tab only analysis options no repeated inputs");
            var drawing=(FrameworkElement)type.GetField("drawing",flags)!.GetValue(ui)!;Snapshot(drawing,Path.Combine(dir,"diagrammi.png"),1900,650);
            foreach(var key in new[]{"strati","falda","kh","mesh","molle","carichi","k","y","theta","q","N","V","M","tratti"}){var box=Descendants<CheckBox>(ui).Single(b=>b.Tag?.ToString()==key);bool state=box.IsChecked==true;box.IsChecked=!state;box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Check(root["elastico"]!["visualizzazione"].B(key)==!state,"visibility "+key);box.IsChecked=state;box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));}
            drawing.GetType().GetMethod("SelectDepth",flags)!.Invoke(drawing,[6d]);var selection=(TextBlock)type.GetField("selection",flags)!.GetValue(ui)!;string side=selection.Text;drawing.GetType().GetMethod("SelectDepth",flags)!.Invoke(drawing,[6d]);Check(selection.Text!=side&&selection.Text.Contains("kh="),"depth selection preserves both interface sides");
            var sectionFields=hostType.GetField("sectionFields",flags)!.GetValue(host)!;var originalResponse=Response();string forces=Response()!["risposta"]!.ToJsonString();int solved=ResponseCalculations();double originalMrd=Result()!["armature"]!.Array("tratti")[0]!["critica"].D("MRdPositive");
            sectionFields.GetType().GetMethod("Set",flags)!.Invoke(sectionFields,["longitudinal_bar_count","20",false]);
            Check(((ProgressBar)type.GetField("calculationProgress",flags)!.GetValue(ui)!).Visibility==Visibility.Visible,"progress shown immediately on invalidation");
            Check(ReferenceEquals(Response(),originalResponse)&&Result()==null,"reinforcement edit retains current FEM and invalidates only verification");
            Check(!((Button)type.GetField("csv",flags)!.GetValue(ui)!).IsEnabled,"obsolete verification export blocked while FEM is visible");
            Until(()=>Result()!=null);Check(ResponseCalculations()==solved&&Result()!["risposta"]!.ToJsonString()==forces,"bar count changes no N V M y or mesh result");
            Check(Math.Abs(Result()!["armature"]!.Array("tratti")[0]!["critica"].D("MRdPositive")-originalMrd)>1,"new reinforcement updates MRd");
            var atDepth=(TextBlock)type.GetField("sectionDetail",flags)!.GetValue(ui)!;Check(atDepth.Text.Contains("x=6 m DALLA TESTA")&&atDepth.Text.Contains("PIANO CAMPAGNA"),"selected section depth survives reinforcement recalculation and states origins");
            var preview=type.GetField("sectionPreview",flags)!.GetValue(ui)!;Check((double)preview.GetType().GetProperty("CircularLinkRadius",flags)!.GetValue(preview)!>0,"selected section draws stirrup from Checker centreline");
            var progressBar=(ProgressBar)type.GetField("calculationProgress",flags)!.GetValue(ui)!;Check(progressBar.Visibility==Visibility.Collapsed,"progress finishes after current results publish");
            sectionFields.GetType().GetMethod("Set",flags)!.Invoke(sectionFields,["longitudinal_bar_count","22",false]);var superseded=(Task)type.GetMethod("CalculateAsync",flags)!.Invoke(ui,null)!;
            sectionFields.GetType().GetMethod("Set",flags)!.Invoke(sectionFields,["longitudinal_bar_count","24",false]);Wait(superseded);Until(()=>Result()!=null);
            Check(Result()!["armature"]!.Array("tratti")[0]!["sezione"].D("longitudinal_bar_count")==24&&ResponseCalculations()==solved,"superseded parallel calculation cannot publish stale reinforcement");
            sectionFields.GetType().GetMethod("Set",flags)!.Invoke(sectionFields,["transverse_spacing_mm","250",false]);Until(()=>Result()!=null);Check(ResponseCalculations()==solved&&Response()!["risposta"]!.ToJsonString()==forces,"stirrups change preserves the FEM diagrams");
            sectionFields.GetType().GetMethod("Set",flags)!.Invoke(sectionFields,["longitudinal_bar_count","16",false]);sectionFields.GetType().GetMethod("Set",flags)!.Invoke(sectionFields,["transverse_spacing_mm","200",false]);Until(()=>Result()!=null);
            var general=hostType.GetField("general",flags)!.GetValue(host)!;double y=Result()!["risposta"].D("HeadDisplacement");general.GetType().GetMethod("Set",flags)!.Invoke(general,["azione_orizzontale","200",false]);Check(Result()==null&&Response()==null,"shared load immediately invalidates both FEM and verification");Until(()=>Result()!=null);Check(Math.Abs(Result()!["risposta"].D("HeadDisplacement")/y-2)<1e-8,"shared force automatically recalculates");
            general.GetType().GetMethod("Set",flags)!.Invoke(general,["diametro","",false]);Until(()=>((TextBlock)type.GetField("status",flags)!.GetValue(ui)!).Text.Contains("non disponibile"));Check(Result()==null,"invalid shared geometry blocks exports");
            general.GetType().GetMethod("Set",flags)!.Invoke(general,["diametro","1",false]);Until(()=>Result()!=null);
            // Inspect a detached archive snapshot: do not resize an inactive tab inside the live calculation host.
            var strataHost=(FrameworkElement)Activator.CreateInstance(hostType,flags,null,[(JsonObject)root.DeepClone()],null)!;((IDisposable)strataHost).Dispose();
            var surveys=(TabControl)hostType.GetField("surveys",flags)!.GetValue(strataHost)!;var tab=(TabItem)surveys.Items[0];var strataView=(FrameworkElement)tab.Content;strataView.Measure(new Size(1400,650));strataView.Arrange(new Rect(0,0,1400,650));strataView.UpdateLayout();Check(Descendants<ComboBox>(strataView).Any(b=>b.Items.Contains("Medio")),"density editor in initial stratigraphy");

            var initialResult=Result()!;Check(initialResult["risposta"]!.Array("Points").Last().D("AxialForce")>0,"axial diagram includes self weight");
            Check(initialResult["armature"]!.Array("tratti")[0]!["critica"]!["MRdPositive"]!=null,"native section resistance integrated");
            Check(initialResult["armature"]!.Array("tratti")[0]!.Array("verifiche").First()!["UsableMRdPositive"]==null,"missing head development not credited in diagram resistance");
            var trial=(JsonObject)root.DeepClone();trial["sezione"]!["esposizione"]="XC2";trial["elastico"]!["dettagli"]!["azioni_progetto"]=true;trial["elastico"]!["dettagli"]!["taglio_confermato"]=true;
            var two=new JsonArray(ElasticHorizontalPile.NewSegment("T1",5),ElasticHorizontalPile.NewSegment("T2",null));trial["elastico"]!["tratti"]=two;two[1]!["collegato"]=false;two[1]!["longitudinal_bar_count"]=8;
            var checkedTwo=ElasticHorizontalPile.CalculateShared(trial);Check(checkedTwo["armature"]!.Array("tratti").Count==2,"two verification segments calculated");
            Check(checkedTwo["armature"]!.Array("tratti")[1]!["sezione"].D("longitudinal_bar_count")==8,"custom reinforcement preserved");
            trial["sezione"]!["longitudinal_bar_count"]=20;var inherited=ElasticHorizontalPile.CalculateShared(trial);Check(inherited["armature"]!.Array("tratti")[0]!["sezione"].D("longitudinal_bar_count")==20&&inherited["armature"]!.Array("tratti")[1]!["sezione"].D("longitudinal_bar_count")==8,"main section change propagates only to linked segments");
            var reopenedSegments=JsonNode.Parse(trial.ToJsonString())!.AsObject();Check(ElasticHorizontalPile.ReadSegments(reopenedSegments).Length==2,"segment archive round trip");
            var nativeInput=ElasticHorizontalPile.SegmentSection(trial,two[0]!.AsObject());var archivedFactors=nativeInput.ToJsonString();nativeInput["coefficienti_unitari"]=true;
            var customStandard=ConcreteStandards.Effective(nativeInput,ConcreteStandards.PileWorkspace(nativeInput));
            Check(typeof(GPC.Model.Standards.StandardModelCode2010).GetProperties().Where(p=>p.CanWrite&&p.PropertyType==typeof(double)&&(p.Name.StartsWith("Gamma")||p.Name=="AlphaCC")).All(p=>(double)p.GetValue(customStandard)! == 1d),"unitary custom standard sets every native gamma and alpha_cc to one");
            var designStrength=ConcreteMaterials.DesignValues(nativeInput,ConcreteStandards.PileWorkspace(nativeInput));Check(designStrength.Fcd==nativeInput.D("fck_mpa")&&designStrength.Fyd==nativeInput.D("fyk_mpa"),"unitary factor material strengths");nativeInput["coefficienti_unitari"]=false;
            Check(ConcreteStandards.Effective(nativeInput,ConcreteStandards.PileWorkspace(nativeInput)).GammaC==nativeInput.D("gamma_c"),"unitary toggle off restores archived factors");
            var cachedRoot=(JsonObject)trial.DeepClone();var femResponse=ElasticHorizontalPile.CalculateResponse(cachedRoot);var resistanceCache=new ElasticPileVerificationCache();ElasticHorizontalPile.CompleteReinforcement(cachedRoot,femResponse,resistanceCache);cachedRoot["sezione"]!["transverse_spacing_mm"]=125;
            var changedPitch=ElasticHorizontalPile.CompleteReinforcement(cachedRoot,femResponse,resistanceCache);Check(changedPitch["armature"]!.Array("tratti").Sum(t=>t.D("nuovi_valori_N"))==0,"link pitch change reuses all MRd and updates shear separately");
            var designed=ElasticHorizontalPile.DesignSegment(trial,inherited,1,[8,16],[100,150]);Check(designed.D("longitudinal_bar_count")>=8&&designed.D("transverse_spacing_mm")<=150,"adapter design calls native Checker with explicit candidate catalogue");
            var varied=ElasticHorizontalPile.DesignSegment(trial,inherited,1,[8,16],[16,24],[8,10],[100,150]);Check(new[]{16d,24}.Contains(varied.D("longitudinal_bar_diameter_mm"))&&new[]{8d,10}.Contains(varied.D("transverse_bar_diameter_mm")),"design includes longitudinal and transverse diameters in candidate catalogue");
            var proposal=ElasticHorizontalPile.ProposeSegments(trial,inherited);Check(two[1]!.D("longitudinal_bar_count")==8&&proposal.Count>=1,"proposal does not overwrite custom sections");
            Check(inherited["armature"]!.Array("tratti")[0]!.Array("da_completare").Any(v=>v!.ToString().Contains("SLE")),"missing and excluded checks exported explicitly");
            var durabilityInput=ElasticHorizontalPile.SegmentSection(trial,two[0]!.AsObject());durabilityInput["esposizione"]="XC2";durabilityInput["fck_mpa"]=30;durabilityInput["vita_durabilita"]=50;
            var dur=ElasticHorizontalPile.PileDurability(durabilityInput,trial["elastico"]!["dettagli"]!);Check(dur.D("cmin_dur")==25&&!dur.B("override"),"durability uses shared exposure through native Checker reference XC2 C30");
            durabilityInput["vita_durabilita"]=100;Check(ElasticHorizontalPile.PileDurability(durabilityInput,trial["elastico"]!["dettagli"]!).D("cmin_dur")==35,"shared service life changes cover without geometry duplicate");
            Check(inherited["armature"]!.Array("tratti")[0]!.Array("controlli_costruttivi").Any(c=>c.S("Key")=="MinimumLongitudinal"),"default column detailing integrated in segment result");
            var detailed=inherited["armature"]!.Array("tratti")[0]!;Check(detailed.Array("controlli_costruttivi").Any(c=>c.S("Key")=="ClearSpacing"),"native spacing and minimum checks returned per segment");
            Check(inherited["armature"]!.Array("distinta").All(b=>b.D("Lap")>=b.D("InitialLap")&&b.D("Lap")>=b.D("RequiredLap")),"lap initial required adopted exported separately");
            var sheet=ElasticHorizontalPile.CreateConcreteSheet(trial,inherited,1);Check(sheet["combinazioni"]!["SLU"]![0]!["azioni"]![0]!.GetValue<double>()==-inherited["armature"]!.Array("tratti")[1]!["critica"]!["Action"].D("N"),"concrete sheet converts compression sign once");
            Check(sheet["workspace_ca"]!["sle_comuni"].S("esposizione")==trial["sezione"].S("esposizione")&&sheet["workspace_ca"]!["dettagli_costruttivi"].S("elemento")=="Pilastro","concrete verifier receives shared exposure and column behavior");
            sheet["combinazioni"]!["SLU"]![0]!["azioni"]![0]=-321;two[1]!["foglio_cls"]=sheet.DeepClone();var again=ElasticHorizontalPile.CreateConcreteSheet(trial,inherited,1);Check(J.Number(again["combinazioni"]!["SLU"]![0]!["azioni"]![0])==-321,"user forces survive concrete sheet reopening");
            string beforeApply=ElasticHorizontalPile.ResponseKey(trial);sheet["input"]!["longitudinal_bar_count"]=12;ElasticHorizontalPile.ApplyConcreteSheetReinforcement(trial,sheet,1);Check(ElasticHorizontalPile.ResponseKey(trial)==beforeApply&&two[1].D("longitudinal_bar_count")==12&&!two[1].B("collegato"),"apply concrete reinforcement preserves FEM and records custom segment");
            var caType=typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.ConcreteWorkspace")!;var ca=(FrameworkElement)Activator.CreateInstance(caType,flags,null,[again],null)!;Snapshot(ca,Path.Combine(dir,"verificatore-cls.png"),1400,950);((IDisposable)ca).Dispose();Check(true,"native concrete workspace opens for segment with editable loads");
            JsonObject? shown=inherited;var editorType=typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.PileReinforcementEditor")!;var editor=(FrameworkElement)Activator.CreateInstance(editorType,flags,null,[trial,(Action)(()=>{}),(Func<JsonObject?>)(()=>shown),null],null)!;Snapshot(editor,Path.Combine(dir,"tratti-riepilogo.png"),1500,1050);
            Check(Descendants<Button>(editor).Count(b=>b.Content?.ToString()=="Apri verificatore c.a.")==3&&Descendants<ComboBox>(editor).Any(b=>b.Items.Contains("Interferro")),"segment cards offer native verifier and selectable compact check summary");
            var choice=Descendants<ComboBox>(editor).Single(b=>b.Items.Contains("Interferro"));choice.SelectedItem="Interferro";Snapshot(editor,Path.Combine(dir,"tratti-interferro.png"),1500,1050);
            var billPanels=Descendants<Expander>(editor).Where(e=>e.Header?.ToString()?.StartsWith("Distinta ferri")==true).ToArray();
            Check(billPanels.Length==2&&billPanels.All(e=>e.IsExpanded),"bill of bars and links immediately visible for every segment");
            var billText=(TextBlock)billPanels[0].Content;editorType.GetMethod("InvalidateResults",flags)!.Invoke(editor,null);
            Check(billText.Text.Contains("aggiornamento"),"old bill cleared immediately during recalculation");
            editorType.GetMethod("UpdateResults",flags)!.Invoke(editor,null);
            Check(ReferenceEquals(billText,billPanels[0].Content)&&billText.Text.Contains("φ")&&billText.Text.Contains("Staffe singole:"),"new bill refreshes existing labels without rebuilding focused inputs");
            billPanels[0].IsExpanded=false;editorType.GetMethod("Refresh",flags)!.Invoke(editor,null);Snapshot(editor,Path.Combine(dir,"distinta-refresh.png"),1500,1500);
            Check(!Descendants<Expander>(editor).First(e=>e.Header?.ToString()?.StartsWith("Distinta ferri")==true).IsExpanded,"bill expansion choice survives refresh");
            Descendants<Button>(editor).First(b=>b.Content?.ToString()=="Distinta ferri").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Snapshot(editor,Path.Combine(dir,"distinta-aperta.png"),1500,1500);
            Check(Descendants<Expander>(editor).First(e=>e.Header?.ToString()?.StartsWith("Distinta ferri")==true).IsExpanded&&Descendants<ComboBox>(editor).Any(b=>b.SelectedItem?.ToString()=="Distinta ferri"),"explicit bill button opens both segment and summary bill");
            shown=(JsonObject)inherited.DeepClone();shown["armature"]!["distinta"]=new JsonArray();shown["armature"]!["tratti"]![0]!["errore"]="Lunghezza commerciale insufficiente rispetto alla sovrapposizione.";
            editorType.GetMethod("UpdateResults",flags)!.Invoke(editor,null);
            Check(((TextBlock)Descendants<Expander>(editor).First(e=>e.Header?.ToString()?.StartsWith("Distinta ferri")==true).Content).Text.Contains("Lunghezza commerciale insufficiente"),"missing bill explains the actual calculation error instead of remaining pending");
            var billDrawingType=typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.PileReinforcementDrawing")!;var billDrawing=(FrameworkElement)Activator.CreateInstance(billDrawingType,true)!;
            billDrawingType.GetMethod("Set",flags)!.Invoke(billDrawing,[shown,null]);
            Check(((string)billDrawingType.GetProperty("EmptyMessage",flags)!.GetValue(billDrawing)!).Contains("Lunghezza commerciale insufficiente"),"empty reinforcement view reports segment failure");
            // Raster artefacts are optional (--images); the drawing and clarity suites render them separately.
            bool previousCapture=capture;Snapshot(billDrawing,Path.Combine(dir,"distinta-errore.png"),1100,250);
            billDrawingType.GetMethod("Set",flags)!.Invoke(billDrawing,[null,"Distinta non disponibile: diametro non valido."]);
            Check(((string)billDrawingType.GetProperty("EmptyMessage",flags)!.GetValue(billDrawing)!).Contains("diametro non valido"),"empty reinforcement view reports global calculation failure");
            shown=inherited;editorType.GetMethod("UpdateResults",flags)!.Invoke(editor,null);Snapshot(editor,Path.Combine(dir,"distinta-visibile.png"),1500,1500);
            billDrawingType.GetMethod("Set",flags)!.Invoke(billDrawing,[shown,null]);var sheetSize=(Size)billDrawingType.GetProperty("SheetSize",flags)!.GetValue(billDrawing)!;Snapshot(billDrawing,Path.Combine(dir,"distinta-disegno.png"),(int)sheetSize.Width,(int)sheetSize.Height);capture=previousCapture;
            var reinf=(FrameworkElement)type.GetField("reinforcementDrawing",flags)!.GetValue(ui)!;Snapshot(reinf,Path.Combine(dir,"armature.png"),1400,650);
            File.WriteAllText(Path.Combine(dir,"due-tratti.json"),checkedTwo.ToJsonString(J.Options));
            var final=Result()!;string csv=ElasticHorizontalPile.Csv(final);Check(csv.Contains("DATI_CONDIVISI")&&csv.Contains("SEZIONE_EJ")&&csv.Contains("InitialMean")&&csv.Contains("NODO_EQUIVALENTE"),"CSV shared references spring units parameter provenance");var report=ReportElasticPile.Create("Palo con dati condivisi",final);
            using(var zip=new System.IO.Compression.ZipArchive(new MemoryStream(report))){using var reader=new StreamReader(zip.GetEntry("word/document.xml")!.Open());var xml=reader.ReadToEnd();Check(xml.Contains("Media iniziale del software")&&xml.Contains("EJ da sezione")&&xml.Contains("K* [kN/m]"),"report parameters EJ and mesh");}
            Check(hostType.GetProperty("ActiveResult",flags)!.GetValue(host)==final,"active result routing");
            ((IDisposable)host).Dispose();File.WriteAllText(Path.Combine(dir,"ui-checks.txt"),$"PASS {checks} checks");Console.WriteLine($"TOTAL {checks}");
            File.WriteAllText(Path.Combine(dir,"input.json"),root.ToJsonString(J.Options));File.WriteAllText(Path.Combine(dir,"result.json"),final.ToJsonString(J.Options));File.WriteAllText(Path.Combine(dir,"risultati.csv"),csv);File.WriteAllBytes(Path.Combine(dir,"esempio.docx"),report);Console.WriteLine("Artifacts written");return 0;
        }catch(Exception ex){File.WriteAllText(Path.Combine(dir,"ui-failure.txt"),ex.ToString());Console.Error.WriteLine(ex);return 1;}
    }
}
internal sealed class TestApp : Application { internal void LoadStyles() { var document=System.Xml.Linq.XDocument.Load("X.Desktop/App.xaml"); var dictionary=document.Root!.Elements().Single().Elements().Single(); dictionary.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns+"x","http://schemas.microsoft.com/winfx/2006/xaml"); dictionary.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns+"local","clr-namespace:X.Desktop;assembly=ANTHEA"); foreach(var source in dictionary.Descendants().SelectMany(e=>e.Attributes("Source"))) source.Value="/ANTHEA;component/"+source.Value; Resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Parse(dictionary.ToString()); } }
