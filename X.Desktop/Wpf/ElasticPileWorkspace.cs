using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Threading;
using X.Core;
using GPC.Checkers.Geotechnics.Piles;
namespace X.Desktop;
internal sealed class ElasticPileWorkspace : UserControl,IDisposable
{
    readonly JsonObject parent;readonly Action? editInputs,migrated;
    readonly ContentControl header=new(),migration=new();
    readonly TextBlock status=Ui.Text("",13),summary=Ui.Text("",13),section=Ui.Text("",13,true),selection=Ui.Text("Selezionare una quota sul profilo o su un diagramma.",12);
    readonly ElasticPileDrawing drawing=new(){Height=650,MinWidth=1750};
    readonly PileReinforcementDrawing reinforcementDrawing=new(){HorizontalAlignment=HorizontalAlignment.Left};
    readonly PileReinforcementEditor reinforcementEditor;
    readonly TextBlock reinforcementSummary=Ui.Text("",12);
    readonly SectionDrawing sectionPreview=new(){Width=280,Height=280};
    readonly TextBlock sectionDetail=Ui.Text("Selezionare una quota per la sezione trasversale.",12);
    readonly DataGrid table=new(){IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,Height=230};
    readonly ProgressBar calculationProgress=new(){Height=8,Maximum=100,Visibility=Visibility.Collapsed,Margin=new Thickness(0,5,0,5)};
    JsonNode? selectedPoint;
    void CalculationState(string text,bool running,int done=0,int total=0){status.Text=text;calculationProgress.Visibility=running?Visibility.Visible:Visibility.Collapsed;calculationProgress.IsIndeterminate=running&&total==0;calculationProgress.Value=total>0?100d*done/total:0;reinforcementEditor.SetCalculationState(text,running,done,total);}
    readonly Button csv;readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(300)};
    InputForm? options;bool busy,disposed;int revision;int surveyCount=-1;
    readonly ElasticPileVerificationCache resistanceCache=new();
    CancellationTokenSource? calculationCancellation;
    internal JsonObject? Response{get;private set;}
    internal int ResponseCalculations{get;private set;}
    internal JsonObject? Result{get;private set;}
    internal event Action? Modified;
    internal ElasticPileWorkspace(JsonObject root,Action? editInputs=null,Action? migrated=null)
    {
        parent=root;this.editInputs=editInputs;this.migrated=migrated;Background = Appearance.Surface;ElasticHorizontalPile.PrepareShared(root);SetValue(InputForm.CommitOnFocusLossProperty,false);
        csv=Ui.Button("Esporta CSV",Export,inspection:true);csv.IsEnabled=false;
        reinforcementEditor=new(root,InvalidateShared,()=>Result,()=>migrated?.Invoke());
        drawing.Selected+=p=>{selectedPoint=p;if(p==null){selection.Text="";return;}selection.Text=$"x={p.D("Depth"):G5} m · z={p.D("GroundDepth"):G5} m · strato {p.D("Layer")} · {p.S("Side")} · kh={p.D("Kh"):G6} kN/m³ · k={p.D("DistributedStiffness"):G6} kN/m²\ny={p.D("Displacement"):G6} m · θ={p.D("Rotation"):G6} rad · N={p.D("AxialForce"):G6} kN · V={p.D("Shear"):G6} kN · M={p.D("Moment"):G6} kNm · q={p.D("SoilReaction"):G6} kN/m.";};
        Content=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=Ui.Stack(migration,Ui.Paper(Ui.Stack(header,section,status,calculationProgress),12),Ui.Paper(Ui.Stack(new ScrollViewer{Content=drawing,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled},selection),12),Ui.Paper(reinforcementEditor,12),new Expander{Header="Verifiche per tratto e sezioni critiche",IsExpanded=true,Content=reinforcementSummary},new Expander{Header="Tavola armature e distinta ferri",IsExpanded=true,Content=Ui.Stack(Ui.Bar(Ui.Button("Apri tavola armature…",ShowReinforcement,inspection:true)),new ScrollViewer{Content=reinforcementDrawing,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled})},new Expander{Header="Sezione trasversale alla quota selezionata",IsExpanded=true,Content=Ui.Bar(sectionPreview,sectionDetail)},new Expander{Header="Estremi, equilibrio e convergenza",Content=summary},new Expander{Header="Valori per quota · N–V–M concomitanti",Content=Ui.Stack(csv,table)})};
        timer.Tick+=Tick;BuildHeader();InvalidateShared();
        drawing.Selected+=ShowSection;
    }
    void BuildHeader()
    {
        surveyCount=parent.Array("stratigrafie").Count;header.Content=null;migration.Content=null;
        if(ElasticHorizontalPile.NeedsMigration(parent))
        {
            var old=parent["elastico_legacy"]!;var g=parent["generali"]!;
            var text=Ui.Text($"Questo archivio contiene input elastici separati. Confrontare prima di proseguire.\nDati comuni: D={g.S("diametro")} m; L infissa={g.S("lunghezza")} m; H={g.S("azione_orizzontale")} kN; e da p.c.={g.S("eccentricita")} m.\nLegacy: D={old.S("diametro")} m; L totale={old.S("lunghezza")} m; libero={old.S("libero")} m; H={old.S("H")} kN; C={old.S("C")} kNm; e dalla testa={old.S("e")} m; EJ={old.S("EI")} kNm².\nGli originali vengono conservati. Importare aggiunge una stratigrafia e imposta EJ come override: completare poi la classificazione per Broms.",13);
            void Resolve(bool legacy){try{ElasticHorizontalPile.ResolveMigration(parent,legacy);BuildHeader();migrated?.Invoke();InvalidateShared();}catch(ArgumentException ex){status.Text=ex.Message;}}
            migration.Content=Ui.Paper(Ui.Stack(Ui.Text("Confronto archivio precedente",17,true),text,Ui.Bar(Ui.Button("Usa dati comuni e conserva legacy",()=>Resolve(false)),Ui.Button("Importa legacy nei dati comuni",()=>Resolve(true))),Ui.Button("Mostra confronto completo",()=>Info("Confronto completo",parent.ToJsonString(J.Options)))),14);return;
        }
        var e=parent["elastico"]!.AsObject();if(e["visualizzazione"] is not JsonObject)e["visualizzazione"]=new JsonObject();var view=e["visualizzazione"]!.AsObject();drawing.Options=view;
        var choice=new ComboBox{ItemsSource=Enumerable.Range(0,surveyCount).Select(i=>$"Stratigrafia {i+1}").ToArray(),SelectedIndex=(int)e.D("stratigrafia",-1),MinWidth=180,Margin=new Thickness(8,0,10,0)};
        choice.SelectionChanged+=(_,_)=>{e["stratigrafia"]=choice.SelectedIndex;InvalidateShared();};
        options=new InputForm(e,[new("passo","Passo iniziale FEM","m"),new("punta","Vincolo alla punta",Choices:["Libera","Cerniera","Incastro"])],_=>InvalidateShared(),compact:true);
        options.MaxWidth=540;options.HorizontalAlignment=HorizontalAlignment.Left;
        var controls=Ui.Stack(Ui.Bar(Ui.Text("Risposta alla forza assegnata",18,true),choice,Ui.Button("Modifica dati e strati",()=>editInputs?.Invoke()),Ui.Button("Info",ShowInfo,inspection:true)),options);
        var toggles=new WrapPanel();foreach(var (key,label) in new[]{("strati","Stratigrafia"),("falda","Falda"),("kh","kh per strato"),("mesh","Nodi / discretizzazione"),("molle","Molle equivalenti"),("carichi","Carichi / vincoli"),("sisma","Zona sismica 10D"),("k","k distribuita"),("y","y"),("theta","θ"),("q","q"),("N","N"),("V","V"),("M","M"),("tratti","Tratti")})
        {var box=new CheckBox{Content=label,Tag=key,IsChecked=view[key]==null?key!="molle":view.B(key),Margin=new Thickness(0,5,14,5)};box.Click+=(_,_)=>{view[key]=box.IsChecked==true;drawing.InvalidateVisual();Modified?.Invoke();};toggles.Children.Add(box);}
        controls.Children.Add(toggles);controls.Children.Add(Ui.Text("Selezionare una quota; ripetere il clic alle interfacce per leggere i due lati. K* è illustrativa: il FEM usa la matrice di fondazione consistente.",11,color:Ui.Muted));header.Content=controls;
    }
    void Info(string title,string content){var text=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Padding=new Thickness(18),FontSize=14,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Text=content};Ui.Dialog(this,title,text,900,650).ShowDialog();}
    void ShowReinforcement()
    {
        if(Result==null){Info("Tavola armature",status.Text);return;}
        var sheet=new PileReinforcementDrawing{HorizontalAlignment=HorizontalAlignment.Left};sheet.Set(Result);
        var zoom=new Slider{Minimum=35,Maximum=150,Value=75,Width=180,TickFrequency=25,Margin=new Thickness(8)};
        var label=Ui.Text("75%",12);sheet.LayoutTransform=new System.Windows.Media.ScaleTransform(.75,.75);
        zoom.ValueChanged+=(_,_)=>{double factor=zoom.Value/100;sheet.LayoutTransform=new System.Windows.Media.ScaleTransform(factor,factor);label.Text=$"{zoom.Value:0}%";};
        var export=Ui.Button("Esporta PNG…",()=>{var save=new SaveFileDialog{Filter="Tavola PNG|*.png",FileName="armature-palo.png"};if(save.ShowDialog()==true)System.IO.File.WriteAllBytes(save.FileName,sheet.Png((int)Math.Ceiling(sheet.SheetSize.Width*2),(int)Math.Ceiling(sheet.SheetSize.Height*2)));},inspection:true);
        var bar=Ui.Bar(Ui.Text("Zoom",12),zoom,label,export);
        var view=new ScrollViewer{Content=sheet,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        Ui.Dialog(this,"Armature del palo · elevazione e distinta",Ui.Dock(view,top:bar),1250,900).ShowDialog();
    }
    void ShowInfo()=>Info("Info · risposta elastica del palo",ElasticHorizontalPile.Limits+"\n\n"+ViggianiHorizontalSoil.Source+"\n\n"+ViggianiHorizontalSoil.StratificationConvention+"\n\n"+ElasticHorizontalPile.Signs+"\n\nFEM Euler–Bernoulli: due gdl/nodo, matrice consistente ∫k NᵀN dx. kh=nh z/D; k=kh D=nh z. 1 N/cm³=1000 kN/m³. K* nodale illustrativa = integrale di k sulle mezze lunghezze degli elementi adiacenti [kN/m]; non sostituisce la matrice del solutore.\n\nCtesta=H(Llibero−e_da_pc): nessun doppio conteggio del braccio. La media degli estremi della singola riga è un valore iniziale del software, non un valore consigliato dall'autore. Fonti e modifiche sono registrate.\n\nEJ: sezione circolare integra lorda con Ecm da Model; CHS solo acciaio. Override motivato nelle opzioni della sezione. Broms è una capacità limite distinta. N=Ntesta+w x: peso proprio dai materiali, senza galleggiamento automatico, attrito laterale o secondo ordine. Le verifiche N–M e taglio sono distinte dai dettagli costruttivi. MRd tratteggiato è interrotto nelle zone prive di sviluppo delle barre. Le fasce colorate richiedono dettagli locali; giunti e confinamento restano preliminari. Minimi pali NTC §7.2.5 selezionabili; zone dissipative, confinamento dei giunti e SLE richiedono completamento. Le proposte non sostituiscono automaticamente i dati personalizzati.");
    internal void InvalidateShared()
    {
        if(disposed)return;revision++;calculationCancellation?.Cancel();Result=null;reinforcementDrawing.Set(null);reinforcementSummary.Text="Verifiche e armature da aggiornare";sectionPreview.Outline=[];sectionPreview.Bars=[];sectionPreview.InvalidateVisual();sectionDetail.Text="Sezione da verificare";csv.IsEnabled=false;timer.Stop();if(surveyCount!=parent.Array("stratigrafie").Count){if(surveyCount>=0 && !ElasticHorizontalPile.NeedsMigration(parent))parent["elastico"]!["stratigrafia"]=-1;BuildHeader();}
        bool retain=false;try{retain=Response!=null&&Response.S("chiave_risposta")==ElasticHorizontalPile.ResponseKey(parent);}catch(ArgumentException){}
        reinforcementEditor.InvalidateResults();
        if(retain)drawing.Set(Response,preserveSelection:true);
        else{Response=null;drawing.Set(null);table.ItemsSource=null;selection.Text=summary.Text="";}
        try{var s=ElasticHorizontalPile.SectionStiffness(parent);section.Text=$"EJ={s.EI:G7} kNm² · E={s.ModulusMpa:G7} MPa · J={s.InertiaMm4:G7} mm⁴\n{s.Assumption}"+(s.OverrideReason==""?"":" · OVERRIDE: "+s.OverrideReason);}catch(ArgumentException ex){section.Text="EJ da completare nei dati iniziali: "+ex.Message;}
        CalculationState(retain?"FEM conservato (N, V, M invariati) · verifiche di sezione in coda":"Dati modificati · analisi FEM e convergenza in coda",true);if(!ElasticHorizontalPile.NeedsMigration(parent))timer.Start();Modified?.Invoke();
    }
    internal void Commit(){options?.Commit();reinforcementEditor.Commit();}
    async void Tick(object? sender,EventArgs e){if(busy)return;timer.Stop();await CalculateAsync();}
    internal async Task CalculateAsync()
    {
        if(busy||disposed)return;Commit();timer.Stop();busy=true;int request=revision;var snapshot=(JsonObject)parent.DeepClone();
        using var cancellation=new CancellationTokenSource();calculationCancellation=cancellation;
        try
        {
            if(Response==null)
            {
                CalculationState("1/2 · FEM su Winkler: spostamenti, N–V–M, equilibrio e confronto delle mesh",true);
                var response=await Task.Run(()=>ElasticHorizontalPile.CalculateResponse(snapshot,cancellation.Token));if(disposed||request!=revision)return;
                Response=response;ResponseCalculations++;PresentResponse();
            }
            CalculationState("2/2 · FEM aggiornato · preparazione del calcolo parallelo MRd",true);
            var updates=new Progress<PileCalculationProgress>(p=>{if(!disposed&&request==revision&&busy&&Result==null)CalculationState("2/2 · "+p.Phase,true,p.Completed,p.Total);});
            var currentResponse=(JsonObject)Response.DeepClone();
            var result=await Task.Run(()=>ElasticHorizontalPile.CompleteReinforcement(snapshot,currentResponse,resistanceCache,cancellation.Token,progress:updates));
            if(disposed||request!=revision)return;Result=result;Present();
        }
        catch(OperationCanceledException){}
        catch(Exception ex){if(!disposed&&request==revision){Result=null;drawing.Set(Response,preserveSelection:true);reinforcementDrawing.Set(null,"Distinta non disponibile: "+ex.Message);csv.IsEnabled=false;CalculationState("Calcolo non disponibile: "+ex.Message,false);}}
        finally{if(ReferenceEquals(calculationCancellation,cancellation))calculationCancellation=null;busy=false;if(!disposed&&request!=revision)timer.Start();}
    }
    void Present()
    {
        var r=Result!["risposta"]!;drawing.Set(Result,preserveSelection:true);csv.IsEnabled=true;status.Text=$"Stratigrafia {Result["dati_condivisi"].D("stratigrafia")} · {r.D("Elements")} elementi · "+(r.B("MeshConverged")?"convergenza ≤ 0,1%":"RAFFINARE LA MESH")+$" · y testa {r.D("HeadDisplacement"):G6} m · θ testa {r.D("HeadRotation"):G6} rad";
        reinforcementDrawing.Set(Result);if(!reinforcementEditor.IsKeyboardFocusWithin)reinforcementEditor.Refresh();
        CalculationState(status.Text,false);reinforcementEditor.UpdateResults();ShowSection(selectedPoint);
        reinforcementSummary.Text=string.Join("\n",Result["armature"]!.Array("tratti").Select(t=>{var c=t!["critica"];var a=c?["Action"];return $"{t.S("id")} · {t.D("inizio"):G5}–{t.D("fine"):G5} m · {t.S("stato")} {t.S("errore")}\nCritica x={a.D("Depth"):G5} m: N={a.D("N"):G5} kN, V={a.D("V"):G5} kN, M={a.D("M"):G5} kNm; MRd nominale +={c?["MRdPositive"]}, MRd−={c?["MRdNegative"]} kNm; VRd={c?["VRd"]} kN. {c.S("Message")}";}));
    }
    void PresentResponse()
    {
        if(Response==null)return;var r=Response["risposta"]!;drawing.Set(Response);
        var lines=new List<string>{$"Residui ΣF={r.D("ForceResidual"):G4} kN, ΣM={r.D("MomentResidual"):G4} kNm; reazioni C testa={r.D("HeadReactionMoment"):G5} kNm, H punta={r.D("TipReactionForce"):G5} kNm, C punta={r.D("TipReactionMoment"):G5} kNm.",$"Mesh {r.D("ComparisonElements")} / {r.D("Elements")} · Δy={r["RelativeMeshChanges"].D("y"):P4}, ΔM={r["RelativeMeshChanges"].D("M"):P4}, ΔV={r["RelativeMeshChanges"].D("V"):P4}"};
        foreach(var (key,unit) in new[]{("y","m"),("theta","rad"),("N","kN"),("V","kN"),("M","kNm"),("q","kN/m")}){var ex=r["Extrema"]![key]!;lines.Add($"{key} [{unit}]: min {ex.D("Minimum"):G6} a x={ex.D("MinimumDepth"):G5}; max {ex.D("Maximum"):G6} a x={ex.D("MaximumDepth"):G5}; |max|={ex.D("AbsoluteMaximum"):G6} a x={ex.D("AbsoluteMaximumDepth"):G5} m.");}summary.Text=string.Join("\n",lines);
        table.ItemsSource=r.Array("Points").Select(p=>new{X_m=p.D("Depth"),Z_m=p.D("GroundDepth"),Strato=p.D("Layer"),Lato=p.S("Side"),Kh_kNm3=p.D("Kh"),K_kNm2=p.D("DistributedStiffness"),Y_m=p.D("Displacement"),Theta_rad=p.D("Rotation"),N_kN=p.D("AxialForce"),V_kN=p.D("Shear"),M_kNm=p.D("Moment"),Q_kNm=p.D("SoilReaction")}).ToArray();
    }
    void Export(){Commit();if(Result==null)return;var save=new SaveFileDialog{Filter="CSV|*.csv",FileName="Palo_risposta_elastica.csv"};if(save.ShowDialog(Window.GetWindow(this))==true)Archivio.ScriviAtomico(save.FileName,Encoding.UTF8.GetBytes(ElasticHorizontalPile.Csv(Result)));}
    void ShowSection(JsonNode? p)
    {
        sectionPreview.Outline=[];sectionPreview.Bars=[];sectionPreview.CircularLinkRadius=0;sectionDetail.Text="Nessuna quota selezionata. Fare clic sul profilo o su un diagramma: la linea arancione identifica la sezione trasversale.";
        if(p!=null&&Result?["armature"] is JsonNode arm){double x=p.D("Depth");var matching=arm.Array("tratti").Where(t=>x>=t.D("inizio")&&x<=t.D("fine")).ToArray();var s=p.S("Side")=="Below"?matching.LastOrDefault():matching.FirstOrDefault();if(s!=null){sectionPreview.Outline=s.Array("contorno_sezione").Select(a=>a!.AsArray().Select(v=>J.Number(v)??0).ToArray()).ToList();sectionPreview.Bars=s.Array("barre_sezione").Select(a=>{var b=a!.AsArray().Select(v=>J.Number(v)??0).ToArray();return new Barra(b[0],b[1],b[2],b[3]);}).ToList();var section=s["sezione"]!;sectionPreview.CircularLinkRadius=s.D("staffa_raggio_mm");sectionPreview.LinkDiameter=section.D("transverse_bar_diameter_mm");sectionPreview.LinkCaption=section.S("tipo_trasversale")=="Spirale"?"Spirale":"Staffa";sectionPreview.ShowAxes=true;sectionPreview.Caption=$"Sez. {s.S("id")} · x={x:0.000} m dalla testa";sectionDetail.Text=$"{s.S("id")} · x={x:G6} m DALLA TESTA · z={p.D("GroundDepth"):G6} m DAL PIANO CAMPAGNA\nLato {p.S("Side")} · riferimento: linea arancione sui diagrammi · {s.S("stato")}\n{section.D("longitudinal_bar_count")} φ{section.D("longitudinal_bar_diameter_mm")} · {section.S("tipo_trasversale","Staffe singole")} φ{section.D("transverse_bar_diameter_mm")}/{section.D("transverse_spacing_mm")} mm\nCopriferro {section.D("cover_mm")} mm\n"+s["dettaglio"].S("Status");}}
        sectionPreview.InvalidateVisual();
    }
    public void Dispose(){disposed=true;calculationCancellation?.Cancel();resistanceCache.Clear();timer.Stop();timer.Tick-=Tick;}
}
