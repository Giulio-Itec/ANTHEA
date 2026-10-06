using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;
namespace X.Desktop;
internal sealed partial class PileReinforcementEditor : ContentControl
{
    readonly JsonObject root;readonly Action changed;readonly Action? sharedSectionChanged;readonly Func<JsonObject?> result;InputForm? details;readonly TextBlock message=Ui.Text("",12);
    readonly TextBlock phase=Ui.Text("",12);readonly ProgressBar progress=new(){Height=7,Maximum=100,Visibility=Visibility.Collapsed,Margin=new Thickness(0,4,0,8)};readonly List<Button> resultCommands=new();
    string calculationState="Dati in attesa del ricalcolo automatico";
    internal void SetCalculationState(string text,bool running,int completed=0,int total=0){calculationState=text;phase.Text=text;progress.Visibility=running?Visibility.Visible:Visibility.Collapsed;progress.IsIndeterminate=running&&total==0;progress.Value=total>0?100d*completed/total:0;foreach(var b in resultCommands)b.IsEnabled=result()!=null;if(!running&&result()==null)UpdateResults();}
    Button ResultButton(string title,Action action){var button=Ui.Button(title,action);button.IsEnabled=result()!=null;button.ToolTip="Disponibile quando MRd e verifiche sono aggiornati; avanzamento sopra i tratti.";resultCommands.Add(button);return button;}
    int selected;string checkMode="Riepilogo";readonly ContentControl summary=new();readonly Dictionary<string,TextBlock> bills=new();readonly Dictionary<string,bool> expandedBills=new();
    internal static Brush SegmentColor(int index)=>Ui.Brush(new[]{"#DBEAFE","#DCFCE7","#F3E8FF","#FFEDD5","#CFFAFE","#FCE7F3"}[index%6]);
    internal PileReinforcementEditor(JsonObject root,Action changed,Func<JsonObject?> result,Action? sharedSectionChanged=null){this.root=root;this.changed=changed;this.result=result;this.sharedSectionChanged=sharedSectionChanged;Build();}
    FrameworkElement SeismicPanel(JsonObject settings)
    {
        InputForm? form=null;Button? approval=null;
        string[] conditional=["sisma_lunghezza","sisma_azioni","sisma_elastico"];
        void Visibility(){if(approval!=null)approval.IsEnabled=settings.B("sisma_testa");if(form!=null)foreach(var key in conditional)form.ShowField(key,settings.B("sisma_testa"));}
        form=new InputForm(settings,[new("sisma_testa","Controlla zona dissipativa presso la testa · capacità non esclusa",Bool:true),new("sisma_lunghezza","Estensione dalla testa · vuoto = 10D","m"),new("sisma_azioni","N e V assegnati appartengono alla combinazione sismica da verificare",Bool:true),new("sisma_elastico","M assegnato è elastico non ridotto (q=1), con N sismico concomitante",Bool:true)],_=>{Visibility();changed();},compact:true);

        approval=ApproveButton(true);Visibility();
        return new Expander{Header="Sisma · testa del palo · NTC §7.2.5",IsExpanded=settings.B("sisma_testa"),Content=Ui.Stack(Ui.Text("Zona minima 10D; As ≥ 1% Ac; staffe singole, φ ≥ 8 mm, s ≤ 6φL. Controlla ogni tratto interessato, anche se la zona attraversa più armature. Le quote e le armature assegnate non vengono cambiate.",12),form,Ui.Text("Senza valutazione specifica di duttilità: VRd ≥ 1,3|VEd|; in zona dissipativa σc < 0,45fcd; |Mel| < 1,5MRd(N). Confermare la provenienza delle azioni: un FEM elastico sotto un carico qualsiasi non è automaticamente un'analisi sismica q=1. Risultati nel selettore «Sisma · testa palo» a destra. Zone profonde, nodo palo-plinto, interazione cinematica e dettagli esecutivi restano separati.",12),Ui.Bar(approval,Ui.Button("NTC §7.2.5 · finestra interna",()=>CreateNtcWindow().ShowDialog())))};
    }
    internal void Commit(){foreach(var form in Ui.Descendants<InputForm>(this))form.Commit();}
    internal void Refresh()=>Build();
    internal void InvalidateResults(){foreach(var bill in bills.Values)bill.Text="Distinta in aggiornamento · vedere avanzamento sopra i tratti.";UpdateSummary();}
    // Update output labels even while an input retains focus; rebuilding would interrupt editing.
    internal void UpdateResults(){foreach(var (id,bill) in bills)bill.Text=BillFor(id);UpdateSummary();}
    void Build()
    {
        if(message.Parent is Panel old)old.Children.Remove(message);if(phase.Parent is Panel owner)owner.Children.Remove(phase);if(progress.Parent is Panel pp)pp.Children.Remove(progress);resultCommands.Clear();
        if(ElasticHorizontalPile.NeedsMigration(root))return;ElasticHorizontalPile.PrepareReinforcement(root);
        var e=root["elastico"]!.AsObject();var rows=e.Array("tratti");bills.Clear();selected=Math.Clamp(selected,0,Math.Max(0,rows.Count-1));var stack=Ui.Stack(Ui.Text("Tratti di armatura",16,true),Ui.Text("Quote dalla testa [m]. Le barre terminano alla fine del proprio tratto. Dal secondo tratto l’inizio arretra di l₀; nessuna fusione automatica. Il primo tratto usa la sezione principale.",12));
        phase.Text=calculationState;stack.Children.Add(phase);stack.Children.Add(progress);
        stack.Children.Add(Ui.Bar(ApproveButton(false),Ui.Button("NTC §7.2.5",()=>CreateNtcWindow().ShowDialog())));
        var transverse=Ui.Choice(new[]{"Da definire","Staffe singole","Spirale"},e["dettagli"].S("sisma_staffe"));transverse.Width=160;transverse.SelectionChanged+=(_,_)=>{e["dettagli"]!["sisma_staffe"]=transverse.SelectedItem?.ToString();changed();Build();};stack.Children.Add(Ui.Bar(Ui.Text("Armatura trasversale principale",12),transverse));
        stack.Children.Add(Ui.Bar(ResultButton("Proponi suddivisione…",Propose),Ui.Button("Info sezioni",()=>Ui.Dialog(this,"Perimetro delle verifiche",Ui.Text("NTC 2018 §7.2.5 · G.U. 20/02/2018, PDF 217, pagina stampata 213: per i pali As≥0,3% Ac, φst≥8 mm e s≤8φL per tutta la lunghezza. Se non si esclude il raggiungimento della capacità, il testo richiede zone dissipative (10D presso testa / 5D ai contatti profondi), As≥1%, staffe singole s≤6φL e condizioni ulteriori su duttilità e azioni. I controlli di testa sono attivabili nelle ipotesi Sisma e leggibili nel riepilogo. Le azioni sismiche devono essere assegnate e confermate; le zone profonde restano da valutare. Regole Pilastro: default modificabile, aggiuntivo ai minimi pali.\n\nN–M tramite il dominio resistente Checker; taglio tramite lo stesso motore del modulo c.a. Staffe chiuse a 90°, due bracci. La proposta dei tratti conserva inizialmente l'armatura principale; personalizzare i tratti e controllare gli esiti. Ancoraggi rettilinei a fyd e sovrapposizioni sono proposte da completare con disposizione, confinamento e requisiti esecutivi/sismici pertinenti. MRd tratteggiato = capacità nominale, senza certificare i dettagli costruttivi.",14),900,520).ShowDialog())));
        double start=0;double total=root["generali"].D("lunghezza")+root["generali"].D("tratto_libero");
        for(int i=0;i<rows.Count;i++)
        {
            int index=i;var row=rows[i]!.AsObject();double endValue=J.Number(row["fine"])??total;
            var card=Ui.Stack(Ui.Text($"{row.S("id")} · {start:G4} → {endValue:G4} m · L={endValue-start:G4} m",14,true));start=endValue;
            var line=new WrapPanel();
            FrameworkElement Field(string title,FrameworkElement input,double width=115){input.Width=width;var field=Ui.Stack(Ui.Text(title,11,true),input);field.Margin=new Thickness(0,4,10,4);return field;}
            var end=new TextBox{Text=row.S("fine"),ToolTip="Quota di taglio finale dalla testa, in metri; vuoto = punta. La barra successiva inizia a questa quota meno l₀."};end.TextChanged+=(_,_)=>{row["fine"]=string.IsNullOrWhiteSpace(end.Text)?null:JsonValue.Create(end.Text);changed();};line.Children.Add(Field("Fine tratto / barre [m]",end));
            var inherit=new CheckBox{Content="Usa principale",IsChecked=row.B("collegato"),IsEnabled=i>0,VerticalAlignment=VerticalAlignment.Center};inherit.Click+=(_,_)=>{if(inherit.IsChecked!=true)foreach(var key in ElasticHorizontalPile.ReinforcementKeys)if(row[key]==null)row[key]=root["sezione"]![key]?.DeepClone();row["collegato"]=inherit.IsChecked==true;changed();Build();};line.Children.Add(Field("Origine armatura",inherit,140));
            var labels=new[]{"Numero barre","φ barre [mm]","φ staffe [mm]","Passo staffe [mm]"};var adoptedSection=ElasticHorizontalPile.SegmentSection(root,row);
            foreach(var (key,j) in ElasticHorizontalPile.ReinforcementKeys.Select((key,j)=>(key,j))){var box=new TextBox{Text=adoptedSection.S(key),IsReadOnly=row.B("collegato"),Background=row.B("collegato")?Ui.Bg:Brushes.White,ToolTip=row.B("collegato")?"Valore condiviso: modificare la sezione principale o scollegare questo tratto.":labels[j]+(row[key]==null?" · campo non assegnato: valore dalla sezione principale":"")};box.TextChanged+=(_,_)=>{row[key]=box.Text;changed();};line.Children.Add(Field(labels[j],box,95));}
            var kind=Ui.Choice(new[]{"Da definire","Staffe singole","Spirale"},ElasticHorizontalPile.TransverseKind(root,row));kind.IsEnabled=!row.B("collegato");kind.SelectionChanged+=(_,_)=>{row["tipo_trasversale"]=kind.SelectedItem?.ToString();changed();};line.Children.Add(Field("Tipo trasversale",kind,145));
            card.Children.Add(line);var commands=Ui.Bar(Ui.Button("Verifiche →",()=>{selected=index;UpdateSummary();}),Ui.Button("Apri verificatore c.a.",()=>OpenConcrete(index)),ResultButton("Dimensiona…",()=>Design(index)),Ui.Button("Dividi…",()=>Split(index)));
            if(i>0)commands.Children.Add(Ui.Button("Unisci ←",()=>{rows[index-1]!["fine"]=row["fine"]?.DeepClone();rows.RemoveAt(index);changed();Build();}));card.Children.Add(commands);
            string id=row.S("id");var bill=Ui.Text(BillFor(id),11);bills.Add(id,bill);
            var billPanel=new Expander{Header="Distinta ferri · barre e staffe",Content=bill,IsExpanded=!expandedBills.TryGetValue(id,out bool open)||open};
            billPanel.Expanded+=(_,_)=>expandedBills[id]=true;billPanel.Collapsed+=(_,_)=>expandedBills[id]=false;
            commands.Children.Add(Ui.Button("Distinta ferri",()=>{selected=index;checkMode="Distinta ferri";billPanel.IsExpanded=true;UpdateResults();billPanel.BringIntoView();}));card.Children.Add(billPanel);
            stack.Children.Add(new Border{Background=SegmentColor(i),CornerRadius=new CornerRadius(5),Padding=new Thickness(10),Margin=new Thickness(0,7,0,0),Child=card});
        }
        details=new InputForm(e["dettagli"]!.AsObject(),[new("elemento","Regole di dettaglio adottate",Choices:["Pilastro","Trave","Solo controlli comuni"]),new("barre_trattenute","Confermo disposizione staffe a trattenimento delle barre compresse",Bool:true),new("zone_estremita","Confermo dettagli nelle zone di estremità",Bool:true),new("azioni_progetto","H e N sono azioni di progetto; peso unitario adottato coerente",Bool:true),new("taglio_confermato","Confermo taglio circolare equivalente, staffe chiuse a 90°",Bool:true),new("z_d","Braccio resistente z/d (≤0,9)"),new("aderenza_buona","Condizioni di buona aderenza",Bool:true),new("percentuale_sovrapposta","Barre sovrapposte nella sezione","%"),new("distanza_barre","Distanza libera barre sovrapposte","mm"),new("lunghezza_barra","Lunghezza commerciale delle barre","m"),new("lunghezza_gabbia","Lunghezza massima gabbia da proporre","m"),new("lunghezza_minima","Lunghezza minima desiderata tratto","m")],_=>changed(),compact:true);
        stack.Children.Add(new Expander{Header="Ipotesi di verifica e vincoli per la proposta",Content=details});
        stack.Children.Add(SeismicPanel(e["dettagli"]!.AsObject()));
        var extra=new InputForm(e["dettagli"]!.AsObject(),[new("fattore_sovrapposizione","Sovrapposizione iniziale: moltiplicatore φ","φ"),new("aggregato","Diametro massimo aggregato","mm"),new("copriferro_override","Override esplicito cmin,dur (altrimenti usa esposizione principale)",Bool:true),new("cmin_dur","cmin,dur manuale · solo se override attivo","mm"),new("delta_c","Tolleranza di copriferro Δcdev","mm")],_=>{changed();},compact:true);
        extra.ShowField("cmin_dur",e["dettagli"].B("copriferro_override"));
        ((CheckBox)extra.Editors["copriferro_override"]).Click+=(_,_)=>extra.ShowField("cmin_dur",e["dettagli"].B("copriferro_override"));
        stack.Children.Add(new Expander{Header="Sovrapposizioni, interferro e copriferro",Content=Ui.Stack(Ui.Text("Esposizione condivisa: "+root["sezione"].S("esposizione")+". Dettagli Pilastro per impostazione iniziale modificabile. Default l₀=60φ arrotondato al decimetro superiore, almeno pari alla lunghezza richiesta. Primo gruppo: 0 → fine. Successivi: inizio tratto − l₀ → fine. I gruppi sono spezzati solo oltre la lunghezza commerciale disponibile (6 / 8 / 10 / 12 m). Ancoraggio lbd e traslazione aₗ non allungano automaticamente i pezzi; sviluppi esterni del vecchio schema conservati ma non utilizzati.",12),extra)});stack.Children.Add(message);
        var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition());layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(325)});layout.Children.Add(stack);Grid.SetColumn(summary,1);if(summary.Parent is Panel previous)previous.Children.Remove(summary);summary.Margin=new Thickness(16,0,0,0);summary.VerticalAlignment=VerticalAlignment.Top;layout.Children.Add(summary);Content=layout;UpdateSummary();
        var minimum=new CheckBox{Content="Controlla minimi pali NTC §7.2.5 (As 0,3%, φst 8 mm, s≤8φL; zone dissipative escluse)",IsChecked=e["dettagli"].B("minimi_pali"),Margin=new Thickness(4)};minimum.Click+=(_,_)=>{e["dettagli"]!["minimi_pali"]=minimum.IsChecked==true;changed();};stack.Children.Insert(stack.Children.Count-1,minimum);
    }
    static string Bill(JsonNode run)=>$"{run.S("Id")} · {run.D("Count"):0} Ø{run.D("Diameter"):0} · tratti {run.S("Segment")}\n{run.S("StartKind")} → {run.S("EndKind")}\n"+string.Join("\n",run.Array("Pieces").Select(p=>$"{p.S("Id")}: {p.D("Count")} φ{p.D("Diameter")} · x={p.D("Start"):0.00}–{p.D("End"):0.00} m · taglio {p.D("CuttingLength"):0.00} m · commerciale {p.D("StockLength"):0.0} m"))+"\n"+string.Join("\n",run.Array("Joints").Select(JointText))+"\n"+run.S("Status");
    static string JointText(JsonNode? j)=>$"{j.S("Id")} · {j.S("Kind")} · {j.D("MatchedCount"):0}/{j.D("Count"):0} coppie allineate · {j.S("UpperRun")} ↔ {j.S("LowerRun")}\nx={j.D("Start"):0.00}–{j.D("End"):0.00} m · iniziale {j.D("InitialLength"):0.00} / richiesta {j.D("RequiredLength"):0.00} / adottata {j.D("AdoptedLength"):0.00} / effettiva {j.D("ActualLength"):0.00} m\n{j.S("Note")}";
    static string LinkBill(JsonNode? links)=>links==null?"":$"\n{links.S("tipo","Staffe singole")}: {links.D("quantita")} × φ{links.D("diametro_mm")} · passo ≤ {links.D("passo_massimo_mm")} mm · effettivo {links.D("passo_effettivo_mm"):0.#} mm"+(links.S("tipo")=="Spirale"?$" · {links.D("spire"):0} spire":"")+$"\nL geometrica totale {links.D("lunghezza_geometrica_m"):0.00} m (esclusi ganci/ancoraggi).\n{links.S("stato")}";
    string BillFor(string id)
    {
        var current=result();if(current==null)return calculationState.StartsWith("Calcolo non disponibile")?"Distinta non disponibile · "+calculationState:"Distinta in aggiornamento · vedere avanzamento sopra i tratti.";
        var arm=current["armature"];var record=arm?.Array("tratti").FirstOrDefault(t=>t.S("id")==id);
        var runs=arm?.Array("distinta").Where(r=>r.S("Segment").Split(" / ").Contains(id)).ToArray();
        if(runs?.Length>0)return string.Join("\n\n",runs.Select(r=>Bill(r!)))+LinkBill(record?["distinta_staffe"]);
        string reason=record.S("errore",record.S("stato",arm.S("stato","Dati del tratto non disponibili.")));
        return "Distinta non disponibile: "+reason+LinkBill(record?["distinta_staffe"]);
    }
    static string Number(JsonNode? row,string key)=>J.Number(row?[key])?.ToString("G5")??"—";
    internal void UpdateSummary()
    {
        var rows=root["elastico"]!.Array("tratti");if(rows.Count==0)return;selected=Math.Clamp(selected,0,rows.Count-1);
        var choose=new ComboBox{ItemsSource=rows.Select(r=>r.S("id")).ToArray(),SelectedIndex=selected,Margin=new Thickness(0,6,0,6)};
        choose.SelectionChanged+=(_,_)=>{selected=choose.SelectedIndex;UpdateSummary();};
        var mode=Ui.Choice(new[]{"Riepilogo","N–M","Taglio","Interferro","Minimi armatura","Dettagli pilastro / trave","Sisma · testa palo","Copriferro","Sviluppo / sovrapposizioni","Distinta ferri"},checkMode);mode.SelectionChanged+=(_,_)=>{checkMode=mode.SelectedItem?.ToString()??"N–M";UpdateSummary();};
        var content=Ui.Stack(Ui.Text("Sezione e verifiche",15,true));
        var all=result()?["armature"]?.Array("tratti");
        if(all!=null){int failed=all.Count(r=>r?["riepilogo"].S("colore")=="rosso"),waiting=all.Count(r=>r?["riepilogo"].S("colore")=="ambra");content.Children.Add(Ui.Text($"PALO · {all.Count} tratti\n{failed} non verificati · {waiting} incompleti · {all.Count-failed-waiting} con controlli soddisfatti",12,true,failed>0?Brushes.Firebrick:waiting>0?Brushes.DarkGoldenrod:Brushes.DarkGreen));}
        content.Children.Add(choose);content.Children.Add(mode);
        var record=result()?["armature"]?.Array("tratti").FirstOrDefault(r=>r.S("id")==rows[selected].S("id"));
        if(record==null)content.Children.Add(Ui.Text("Verifiche da aggiornare. I diagrammi FEM validi rimangono disponibili.",12));
        else
        {
            var preview=new SectionDrawing{Width=205,Height=175,Caption=$"{record.S("id")} · sezione costante x={record.D("inizio"):0.00}–{record.D("fine"):0.00} m",CircularLinkRadius=record.D("staffa_raggio_mm"),LinkDiameter=record["sezione"].D("transverse_bar_diameter_mm"),LinkCaption=record["sezione"].S("tipo_trasversale")=="Spirale"?"Spirale":"Staffa",ShowAxes=true,Outline=record.Array("contorno_sezione").Select(a=>a!.AsArray().Select(v=>J.Number(v)??0).ToArray()).ToList(),Bars=record.Array("barre_sezione").Select(a=>{var b=a!.AsArray().Select(v=>J.Number(v)??0).ToArray();return new Barra(b[0],b[1],b[2],b[3]);}).ToList()};content.Children.Add(preview);
            content.Children.Add(Ui.Text("Dettagli: "+record.S("comportamento_dettagli")+" · esposizione "+record["durabilita"].S("esposizione")+" · cmin,dur "+Number(record["durabilita"],"cmin_dur")+" mm",11));
            AddVerdict(content,record);
            if(checkMode=="N–M"||checkMode=="Taglio")
            {
                string key=checkMode=="N–M"?"BendingRatio":"ShearRatio";var c=record.Array("verifiche").OrderByDescending(c=>J.Number(c?[key])??double.PositiveInfinity).FirstOrDefault();var a=c?["Action"];
                content.Children.Add(Ui.Text($"Quota critica x={a.D("Depth"):0.00} m\nN={a.D("N"):G5} kN · V={a.D("V"):G5} kN\nM={a.D("M"):G5} kNm\nη={Number(c,key)}\n"+(checkMode=="N–M"?$"MRd+={Number(c,"MRdPositive")}\nMRd−={Number(c,"MRdNegative")} kNm":$"VRd={Number(c,"VRd")} kN"),12));
            }
            else if(checkMode=="Sisma · testa palo")
            {
                var seismic=record["sisma_testa"];
                content.Children.Add(Ui.Text(seismic.B("Active")?$"Zona di testa: x=0–{seismic.D("HeadEnd"):0.00} m\n10D = {seismic.D("RequiredHeadLength"):0.00} m\n{seismic.S("Status")}":"Controlli sismici non attivi. Abilitarli nelle ipotesi Sisma · testa del palo.",12,true));
                foreach(var c in seismic.Array("Checks"))
                {
                    string state=c?["Passed"]==null?"Da completare":c.B("Passed")?"Soddisfatto":"Non soddisfatto";
                    content.Children.Add(Ui.Text($"{c.S("Title")} · {state}\nValore {Number(c,"Actual")} / limite {Number(c,"Limit")} {c.S("Unit")}"+(c?["Depth"]==null?"":$" · x={c.D("Depth"):0.00} m")+$"\n{c.S("Criterion")}",12,color:c?["Passed"]!=null&&!c.B("Passed")?Brushes.Firebrick:Ui.Navy));
                }
                content.Children.Add(Ui.Text(seismic.S("Scope"),11));
            }
            else if(checkMode=="Distinta ferri")content.Children.Add(Ui.Text(BillFor(record.S("id")),12));
            else if(checkMode=="Sviluppo / sovrapposizioni")
            {
                foreach(var run in record.Array("dettagli_barre"))
                {
                    content.Children.Add(Ui.Text($"{run.S("Id")} · {run.D("Count"):0} Ø{run.D("Diameter"):0}\n{run.S("StartKind")} → {run.S("EndKind")}\nlbd={run.D("Anchorage"):0.00} m · aₗ={run.D("Shift"):0.00} m\n{run.S("Status")}",12));
                    foreach(var joint in run!.Array("Joints"))content.Children.Add(Ui.Text(JointText(joint),11));
                }
            }
            else if(checkMode!="Riepilogo")foreach(var c in record.Array("controlli_costruttivi").Where(c=>checkMode=="Interferro"?c.S("Key")=="ClearSpacing":checkMode=="Minimi armatura"?c.S("Key").StartsWith("Pile")||c.S("Key").Contains("Longitudinal"):checkMode=="Dettagli pilastro / trave"?!c.S("Key").Contains("Cover")&&c.S("Key")!="ClearSpacing":c.S("Key").Contains("Cover")))
            {
                string name=ElasticHorizontalPile.ConstructionLabel(c.S("Key"));
                string verdict=c?["Passed"]==null?"Da completare":c.B("Passed")?"Soddisfatto":"Non soddisfatto";
                content.Children.Add(Ui.Text($"{name} · {verdict}\nValore {Number(c,"Actual")} / limite {Number(c,"Limit")} {c.S("Unit")}\n{c.S("Explanation")}\n{c.S("Reference")}",12));
            }

            content.Children.Add(Ui.Text(record.S("errore"),12,color:Brushes.Firebrick));
        }
        content.Children.Add(Ui.Button("Apri verificatore c.a.",()=>OpenConcrete(selected)));content.Children.Add(Ui.Text("Controlli distinti. Giunti, confinamento, SLE e dettagli esecutivi da completare nel campo applicabile.",11,color:Ui.Muted));summary.Content=Ui.Paper(content,12);
    }
    void OpenConcrete(int index)
    {
        try
        {
            Commit();var sheet=ElasticHorizontalPile.CreateConcreteSheet(root,result(),index);var workspace=new ConcreteWorkspace(sheet);var row=root["elastico"]!.Array("tratti")[index]!.AsObject();
            var note=Ui.Text("Scheda di approfondimento: puoi modificare le azioni (N negativo a compressione). Alla chiusura vengono conservate per questo tratto. L'applicazione trasferisce solo l'armatura al palo; geometria, materiali e carichi del foglio restano indipendenti.",12);
            if(ElasticHorizontalPile.TransverseKind(root,row)=="Spirale")note.Text+=" Il foglio c.a. usa il modello di taglio a staffe singole: non certifica la resistenza della spirale.";
            var apply=Ui.Button(index==0?"Applica armatura alla sezione principale":"Applica armatura al tratto",()=>{try{workspace.Commit();ElasticHorizontalPile.ApplyConcreteSheetReinforcement(root,sheet,index);if(index==0)sharedSectionChanged?.Invoke();changed();note.Text="Armatura applicata. FEM conservato; verifiche del palo in aggiornamento.";}catch(ArgumentException ex){note.Text=ex.Message;}});
            var dialog=Ui.Dialog(this,$"Verificatore c.a. · {row.S("id")}",Ui.Dock(workspace,Ui.Stack(note,apply)),1280,900);
            dialog.Closed+=(_,_)=>{workspace.Commit();workspace.Dispose();row["foglio_cls"]=sheet.DeepClone();changed();Build();};dialog.ShowDialog();
        }
        catch(ArgumentException ex){message.Text=ex.Message;}
    }
    void Split(int index)
    {
        var edit=new TextBox{Text="",MinWidth=170};var dialog=Ui.Dialog(this,"Dividi il tratto",Ui.Stack(Ui.Text("Quota dalla testa [m]",13),edit),420,220);
        ((StackPanel)dialog.Content).Children.Add(Ui.Button("Dividi",()=>{try{var segments=ElasticHorizontalPile.ReadSegments(root);var cut=J.Number(JsonValue.Create(edit.Text))??throw new ArgumentException("Quota numerica richiesta.");var division=GPC.Checkers.Geotechnics.Piles.PileSegments.Split(segments,segments[index].Id,cut);GPC.Checkers.Geotechnics.Piles.PileSegments.Validate(division,segments[^1].End);var rows=root["elastico"]!.Array("tratti");var next=(JsonObject)rows[index]!.DeepClone();next["id"]="T-"+Guid.NewGuid().ToString("N")[..6];rows[index]!["fine"]=cut;rows.Insert(index+1,next);changed();Build();dialog.Close();}catch(ArgumentException ex){edit.ToolTip=ex.Message;message.Text=ex.Message;}}));dialog.ShowDialog();
    }
    void Propose()
    {
        if(result() is not JsonObject r){message.Text=""+calculationState+"";return;}
        try{var proposal=ElasticHorizontalPile.ProposeSegments(root,r);var dialog=Ui.Dialog(this,"Proposta di suddivisione",Ui.Stack(Ui.Text("Quote su griglia di 0,5 m (punta esatta), tratti ≥3 m. Preferenza per barre 6/8/10/12 m comprensive dei giunti; arretramento per una sovrapposizione nei tratti successivi al primo, con domanda N–M–V. Il cambio di sezione cerca il primo attraversamento discendente di |M|max/2 dopo il massimo; oltre il cambio l’armatura principale è copiata come primo tentativo personalizzabile, da dimensionare. Le quote finali sono quote di taglio; l’inizio dei gruppi successivi arretra di l₀. Dettagli ancora preliminari.",13),Ui.Text(proposal.ToJsonString(J.Options),12)),700,500);
            ((StackPanel)dialog.Content).Children.Add(Ui.Button("Applica e archivia i tratti precedenti",()=>{var e=root["elastico"]!.AsObject();if(e["storico_tratti"] is not JsonArray)e["storico_tratti"]=new JsonArray();e.Array("storico_tratti").Add(e["tratti"]!.DeepClone());e["tratti"]=proposal;e["origine_tratti"]="Proposta applicata esplicitamente dall'utente";changed();Build();dialog.Close();}));dialog.ShowDialog();}
        catch(ArgumentException ex){message.Text=ex.Message;}
    }
    void Design(int index)
    {
        if(result() is not JsonObject r){message.Text=""+calculationState+"";return;}
        var counts=new TextBox{Text="6;8;10;12;14;16;20;24",Margin=new Thickness(5)};var spacings=new TextBox{Text="100;150;200",Margin=new Thickness(5)};var diameters=new TextBox{Text="16;20;24;28;32",Margin=new Thickness(5)};var links=new TextBox{Text="8;10;12",Margin=new Thickness(5)};var status=Ui.Text("Ricerca nel catalogo esplicito: As crescente, poi staffe per metro. Sono controllati N–M–V, interferro e minimi disponibili. Dettagli costruttivi preliminari.",12);JsonObject? proposal=null;var snapshot=(JsonObject)root.DeepClone();string signature=root.ToJsonString();
        var designProgress=new ProgressBar{Height=8,Maximum=100,Visibility=Visibility.Collapsed};
        using var cancellation=new System.Threading.CancellationTokenSource();
        var apply=Ui.Button(index==0?"Applica alla sezione principale":"Applica al tratto",()=>{if(proposal==null)return;if(signature!=root.ToJsonString()){status.Text="Input cambiati: ripetere la ricerca.";return;}var target=index==0?root["sezione"]!.AsObject():root["elastico"]!.Array("tratti")[index]!.AsObject();foreach(string key in ElasticHorizontalPile.ReinforcementKeys)target[key]=proposal[key]!.DeepClone();if(index>0)target["collegato"]=false;target["proposta_armatura"]=proposal.DeepClone();if(index==0)sharedSectionChanged?.Invoke();changed();Build();});apply.IsEnabled=false;
        var run=Ui.Button("Trova proposta",()=>{});run.Click+=async(_,_)=>{run.IsEnabled=false;apply.IsEnabled=false;try{double[] Values(TextBox box)=>box.Text.Split(';').Select(s=>J.Number(JsonValue.Create(s))??throw new ArgumentException("Catalogo non numerico.")).ToArray();var ns=counts.Text.Split(';').Select(int.Parse).ToArray();var ss=Values(spacings);var ds=Values(diameters);var ts=Values(links);status.Text="Ricerca con Checker…";designProgress.Visibility=Visibility.Visible;designProgress.IsIndeterminate=true;var updates=new Progress<PileCalculationProgress>(p=>{if(cancellation.IsCancellationRequested||run.IsEnabled)return;status.Text=p.Phase;designProgress.IsIndeterminate=p.Total==0;designProgress.Value=p.Total>0?100d*p.Completed/p.Total:0;});proposal=await Task.Run(()=>ElasticHorizontalPile.DesignSegment(snapshot,r,index,ns,ds,ts,ss,cancellation.Token,updates));status.Text=proposal.ToJsonString(J.Options);apply.IsEnabled=true;}catch(OperationCanceledException){status.Text="Ricerca annullata.";}catch(Exception ex){status.Text=ex.Message;}finally{run.IsEnabled=true;designProgress.Visibility=Visibility.Collapsed;}};
        var dialog=Ui.Dialog(this,"Dimensionamento del tratto · proposta da applicare",Ui.Stack(Ui.Text("Quantità barre candidate (separate da ;)",12),counts,Ui.Text("Diametri longitudinali [mm]",12),diameters,Ui.Text("Diametri staffe [mm]",12),links,Ui.Text("Passi staffe [mm]",12),spacings,Ui.Bar(run,apply),designProgress,status),740,650);dialog.Closed+=(_,_)=>cancellation.Cancel();dialog.ShowDialog();
    }
}
