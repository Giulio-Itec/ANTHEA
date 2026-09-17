using System.Text.Json.Nodes;
using X.Core;

namespace X.Desktop;

public sealed partial class MainForm:Form
{
    private JsonObject document=Archivio.Documento("geo_palo_verticale");private JsonObject? currentSheet;private string? path;private bool dirty,refreshing;
    private readonly TreeView tree=new(){Dock=DockStyle.Fill,HideSelection=false,BorderStyle=BorderStyle.None,BackColor=Color.FromArgb(234,239,246),ItemHeight=29};
    private readonly Panel content=new(){Dock=DockStyle.Fill};private readonly Label heading=new(){Dock=DockStyle.Top,Height=54,Padding=new Padding(15,0,0,0),TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",16,FontStyle.Bold),ForeColor=Ui.Navy,BackColor=Color.White};
    private FoglioEditor? editor;
    public MainForm(string? initialPath=null)
    {
        Text="ANTHEA";Font=new Font("Segoe UI",9);BackColor=Ui.Bg;Width=1550;Height=960;MinimumSize=new Size(1050,700);StartPosition=FormStartPosition.CenterScreen;
        var menu=new MenuStrip{BackColor=Color.White};var file=new ToolStripMenuItem("File");menu.Items.Add(file);
        void Item(ToolStripMenuItem parent,string text,Action action,Keys keys=Keys.None){var item=new ToolStripMenuItem(text,null,(_,_)=>Safe(action)){ShortcutKeys=keys};parent.DropDownItems.Add(item);}
        Item(file,"Nuovo palo",()=>NewCalculation("geo_palo_verticale"),Keys.Control|Keys.N);
        Item(file,"Nuovo micropalo",()=>NewCalculation("geo_micropalo_verticale"));Item(file,"Nuova sezione in c.a.",()=>NewCalculation("str_palo"));Item(file,"Nuovo archivio progetti",NewProjects);
        file.DropDownItems.Add(new ToolStripSeparator());Item(file,"Apri…",Open,Keys.Control|Keys.O);Item(file,"Salva",()=>Save(false),Keys.Control|Keys.S);Item(file,"Salva con nome…",()=>Save(true),Keys.Control|Keys.Shift|Keys.S);
        Item(file,"Esporta foglio selezionato…",ExportSheet);file.DropDownItems.Add(new ToolStripSeparator());Item(file,"Report Word…",ExportReport);Item(file,"Risultati JSON…",ExportJson);Item(file,"Esci",Close,Keys.Alt|Keys.F4);
        var projects=new ToolStripMenuItem("Progetti");menu.Items.Add(projects);Item(projects,"Aggiungi progetto",AddProject);Item(projects,"Aggiungi struttura",AddStructure);Item(projects,"Aggiungi palo",()=>AddSheet("geo_palo_verticale"));Item(projects,"Aggiungi micropalo",()=>AddSheet("geo_micropalo_verticale"));Item(projects,"Aggiungi sezione in c.a.",()=>AddSheet("str_palo"));Item(projects,"Rinomina",Rename);Item(projects,"Elimina",Delete);
        var info=new ToolStripMenuItem("Informazioni");menu.Items.Add(info);Item(info,"Metodi e unità",()=>MessageBox.Show(this,"ANTHEA — conversione C# / .NET 8\n\nGeotecnica: m, kN, kPa, kN/m³, gradi.\nSezioni: mm, MPa, kN, kNm; Mx = ΣF·y, My = −ΣF·x.\n\nNq parametrizzato NQ-2026-09-09; micropali Bustamante–Doix secondo la fonte Python.\nPunta coesiva non drenata sotto falda: Ab·(Nc·Cu + σv totale).\nSLV al primo snervamento dell'acciaio; SLE metodo n.\n\nAvvisi, ipotesi e limiti sono riportati nei risultati.\nConsultare LEGGIMI.md e il confronto numerico nella cartella del programma.","ANTHEA",MessageBoxButtons.OK,MessageBoxIcon.Information));
        var toolbar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=47,BackColor=Ui.Bg,WrapContents=false};toolbar.Controls.AddRange([Ui.Button("Palo",()=>Safe(()=>NewCalculation("geo_palo_verticale"))),Ui.Button("Micropalo",()=>Safe(()=>NewCalculation("geo_micropalo_verticale"))),Ui.Button("Sezione in c.a.",()=>Safe(()=>NewCalculation("str_palo"))),Ui.Button("Apri",()=>Safe(Open)),Ui.Button("Salva",()=>Safe(()=>Save(false)),true),Ui.Button("Report Word",()=>Safe(ExportReport))]);
        var layout=new SplitContainer{Dock=DockStyle.Fill,FixedPanel=FixedPanel.Panel1,SplitterWidth=5};layout.Size=new Size(1500,850);layout.SplitterDistance=220;layout.Panel1MinSize=160;layout.Panel2MinSize=820;
        var treeButtons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=145,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.FromArgb(234,239,246)};treeButtons.Controls.AddRange([Ui.Button("+ Progetto",()=>Safe(AddProject)),Ui.Button("+ Struttura",()=>Safe(AddStructure)),Ui.Button("Rinomina",()=>Safe(Rename))]);layout.Panel1.Controls.Add(tree);layout.Panel1.Controls.Add(treeButtons);layout.Panel2.Controls.Add(content);layout.Panel2.Controls.Add(heading);
        Controls.Add(layout);Controls.Add(toolbar);Controls.Add(menu);MainMenuStrip=menu;
        tree.BeforeSelect+=(_,e)=>{if(!refreshing&&editor?.Busy==true){e.Cancel=true;return;}if(!refreshing)Commit();};tree.AfterSelect+=(_,e)=>{if(!refreshing&&e.Node?.Tag is JsonObject sheet&&sheet.ContainsKey("modulo_id"))ShowSheet(sheet);};
        var context=new ContextMenuStrip();context.Items.Add("Aggiungi palo",null,(_,_)=>Safe(()=>AddSheet("geo_palo_verticale")));context.Items.Add("Aggiungi micropalo",null,(_,_)=>Safe(()=>AddSheet("geo_micropalo_verticale")));context.Items.Add("Aggiungi sezione",null,(_,_)=>Safe(()=>AddSheet("str_palo")));context.Items.Add("Rinomina",null,(_,_)=>Safe(Rename));context.Items.Add("Elimina",null,(_,_)=>Safe(Delete));tree.ContextMenuStrip=context;tree.NodeMouseClick+=(_,e)=>{if(e.Button==MouseButtons.Right)tree.SelectedNode=e.Node;};
        FormClosing+=(_,e)=>{if(editor?.Busy==true){e.Cancel=true;MessageBox.Show(this,"Attendere il completamento del calcolo.");return;}e.Cancel=!ConfirmDiscard();};
        InstallOriginalShell(menu);RefreshTree();ShowHome();if(initialPath is not null)Shown+=(_,_)=>Safe(()=>LoadFile(initialPath));
    }
    private void Safe(Action action){try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Operazione non completata",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    private static string ModuleName(string module)=>module switch{"geo_palo_verticale"=>"Palo · capacità portante","geo_micropalo_verticale"=>"Micropalo · Bustamante–Doix","str_palo"=>"Sezione in c.a. · SLU / SLV / SLE",_=>module};
    private void MarkDirty(){dirty=true;UpdateTitle();}
    private void UpdateTitle()=>Text="ANTHEA — "+(path is null?"Nuovo documento":Path.GetFileName(path))+(dirty?" *":"");
    private void Commit(){if(editor is null||currentSheet is null)return;editor.Commit();currentSheet["dati"]=editor.Data.DeepClone();}
    private void ShowSheet(JsonObject sheet)
    {
        if(editor?.Busy==true)return;editor?.Dispose();content.Controls.Clear();currentSheet=sheet;string module=sheet.S("modulo_id");
        editor=new FoglioEditor(module,sheet["dati"] as JsonObject??Archivio.NuovoFoglio(module));editor.Modified+=MarkDirty;content.Controls.Add(editor);heading.Text=sheet.S("nome",ModuleName(module));ShowModuleView();
    }
    private void RefreshTree(JsonObject? selected=null)
    {
        refreshing=true;tree.BeginUpdate();tree.Nodes.Clear();
        if(document.S("tipo")=="calcolo")tree.Nodes.Add(new TreeNode(ModuleName(document.S("modulo_id"))){Tag=document});
        else foreach(var p in document.Array("progetti"))
        {
            var pn=new TreeNode(p.S("nome","Progetto")){Tag=p};tree.Nodes.Add(pn);foreach(var s in p!.Array("strutture")){var sn=new TreeNode(s.S("nome","Struttura")){Tag=s};pn.Nodes.Add(sn);foreach(var f in s!.Array("fogli"))sn.Nodes.Add(new TreeNode(f.S("nome",ModuleName(f.S("modulo_id")))){Tag=f});}
        }
        tree.ExpandAll();IEnumerable<TreeNode> Walk(TreeNodeCollection nodes){foreach(TreeNode n in nodes){yield return n;foreach(var child in Walk(n.Nodes))yield return child;}}
        tree.SelectedNode=Walk(tree.Nodes).FirstOrDefault(n=>ReferenceEquals(n.Tag,selected??currentSheet))??tree.Nodes.Cast<TreeNode>().FirstOrDefault();tree.EndUpdate();refreshing=false;
    }
    private bool ConfirmDiscard()
    {
        Commit();if(!dirty)return true;var answer=MessageBox.Show(this,"Salvare le modifiche al documento corrente?","ANTHEA",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);if(answer==DialogResult.Cancel)return false;if(answer==DialogResult.Yes)return Save(false);return true;
    }
    private void NewCalculation(string module){if(editor?.Busy==true||!ConfirmDiscard())return;document=Archivio.Documento(module);path=null;dirty=false;currentSheet=null;RefreshTree();ShowSheet(document);UpdateTitle();}
    private void NewProjects(){if(editor?.Busy==true||!ConfirmDiscard())return;document=J.Obj(("formato","X"),("versione",1),("tipo","progetti"),("progetti",new JsonArray()));path=null;dirty=false;editor?.Dispose();editor=null;currentSheet=null;content.Controls.Clear();heading.Text="Progetti";RefreshTree();ShowProjects();AddProject();}
    private void AddProject()
    {
        if(document.S("tipo")!="progetti"){NewProjects();return;}
        var name=Ui.Ask(this,"Nome progetto","Progetto "+(document.Array("progetti").Count+1));if(string.IsNullOrWhiteSpace(name))return;Commit();var p=J.Obj(("id",Guid.NewGuid().ToString("N")),("nome",name),("strutture",new JsonArray()));document.Array("progetti").Add(p);MarkDirty();RefreshTree(p);
    }
    private TreeNode? SelectedProject(){var node=tree.SelectedNode;while(node?.Parent is not null)node=node.Parent;return node;}
    private void AddStructure()
    {
        if(document.S("tipo")!="progetti"){MessageBox.Show(this,"Creare o aprire un archivio progetti dal menu File.");return;}
        var project=SelectedProject()?.Tag as JsonObject;if(project is null){AddProject();return;}var name=Ui.Ask(this,"Nome struttura","Struttura "+(project.Array("strutture").Count+1));if(string.IsNullOrWhiteSpace(name))return;Commit();var s=J.Obj(("id",Guid.NewGuid().ToString("N")),("nome",name),("fogli",new JsonArray()));project.Array("strutture").Add(s);MarkDirty();RefreshTree(s);
    }
    private void AddSheet(string module)
    {
        if(editor?.Busy==true)return;if(document.S("tipo")!="progetti"){NewCalculation(module);return;}
        var node=tree.SelectedNode;if(node?.Tag is JsonObject f&&f.ContainsKey("modulo_id"))node=node.Parent;
        if(node?.Tag is not JsonObject s||!s.ContainsKey("fogli")){MessageBox.Show(this,"Selezionare una struttura nell'albero dei progetti.");return;}
        var name=Ui.Ask(this,"Nome foglio",ModuleName(module));if(string.IsNullOrWhiteSpace(name))return;Commit();var sheet=J.Obj(("id",Guid.NewGuid().ToString("N")),("nome",name),("modulo_id",module),("dati",Archivio.NuovoFoglio(module)));s.Array("fogli").Add(sheet);MarkDirty();RefreshTree(sheet);ShowSheet(sheet);
    }
    private void Rename(){if(tree.SelectedNode?.Tag is not JsonObject target)return;var name=Ui.Ask(this,"Rinomina",target.S("nome",tree.SelectedNode.Text));if(string.IsNullOrWhiteSpace(name))return;target["nome"]=name;MarkDirty();RefreshTree(target);if(ReferenceEquals(target,currentSheet))heading.Text=name;}
    private void Delete()
    {
        if(document.S("tipo")!="progetti"||tree.SelectedNode?.Tag is not JsonObject target||editor?.Busy==true)return;
        if(MessageBox.Show(this,"Eliminare l'elemento selezionato e i suoi contenuti dal documento? Il file su disco resta invariato fino al salvataggio.","Elimina",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        Commit();if(target.Parent is JsonArray list)list.Remove(target);editor?.Dispose();editor=null;currentSheet=null;content.Controls.Clear();heading.Text="Progetti";MarkDirty();RefreshTree();
    }
    private void Open(){using var dialog=new OpenFileDialog{Filter="File ANTHEA|*.programma;*.anthea|Tutti i file|*.*"};if(dialog.ShowDialog(this)==DialogResult.OK)LoadFile(dialog.FileName);}
    public void LoadFile(string filename)
    {
        var loaded=Archivio.Leggi(filename);if(editor?.Busy==true||!ConfirmDiscard())return;document=loaded;path=filename;dirty=false;currentSheet=null;editor?.Dispose();editor=null;content.Controls.Clear();RefreshTree();
        if(document.S("tipo")=="calcolo")ShowSheet(document);else{heading.Text="Progetti — selezionare un foglio";ShowProjects();}UpdateTitle();
    }
    private bool Save(bool asNew)
    {
        Commit();string? target=path;if(asNew||target is null){using var save=new SaveFileDialog{Filter="File ANTHEA|*.programma",FileName=path is null?"Calcolo.programma":Path.GetFileName(path)};if(save.ShowDialog(this)!=DialogResult.OK)return false;target=save.FileName;}
        try{Archivio.Scrivi(target,document);path=target;dirty=false;UpdateTitle();return true;}catch(Exception ex){MessageBox.Show(this,ex.Message,"Salvataggio non riuscito",MessageBoxButtons.OK,MessageBoxIcon.Error);return false;}
    }
    private void ExportSheet(){Commit();if(currentSheet is null)return;using var save=new SaveFileDialog{Filter="File ANTHEA|*.programma",FileName="Foglio.programma"};if(save.ShowDialog(this)==DialogResult.OK)Archivio.Scrivi(save.FileName,J.Obj(("formato","X"),("versione",1),("tipo","calcolo"),("modulo_id",currentSheet["modulo_id"]),("dati",currentSheet["dati"])));}
    private void ExportJson(){Commit();if(editor?.Result is null){MessageBox.Show(this,"Premere Calcola prima di esportare i risultati.");return;}using var save=new SaveFileDialog{Filter="Risultati JSON|*.json",FileName="Risultati.json"};if(save.ShowDialog(this)==DialogResult.OK)editor.ExportResult(save.FileName);}
    private void ExportReport()
    {
        Commit();
        if(editor?.Result is null){MessageBox.Show(this,"Premere Calcola prima di esportare il report.");return;}if(editor.Module=="str_palo"){MessageBox.Show(this,"Report Word della sezione non disponibile, come nella versione Python. È possibile esportare i risultati JSON dal menu File.");return;}
        using var options=new Form{Text="Contenuti del report Word",Width=440,Height=480,StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false};var list=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true};foreach(var (key,label) in ReportWord.Sezioni)list.Items.Add(label,!key.StartsWith("grafico_"));var ok=new Button{Dock=DockStyle.Bottom,Height=40,Text="Esporta",DialogResult=DialogResult.OK};options.Controls.Add(list);options.Controls.Add(ok);if(options.ShowDialog(this)!=DialogResult.OK)return;var selected=ReportWord.Sezioni.Where((_,i)=>list.GetItemChecked(i)).Select(s=>s.Key).ToHashSet();
        using var save=new SaveFileDialog{Filter="Documento Word|*.docx",FileName="Relazione.docx"};if(save.ShowDialog(this)==DialogResult.OK)editor.ExportReport(save.FileName,heading.Text,selected);
    }
    public async Task Smoke(string directory,JsonArray cases)
    {
        Directory.CreateDirectory(directory);var log=new List<string>();
        void Capture(string name){PerformLayout();Update();using var picture=new Bitmap(Width,Height);DrawToBitmap(picture,new Rectangle(0,0,Width,Height));picture.Save(Path.Combine(directory,name+".png"));}
        ShowHome();Capture("home");ShowModules();Capture("moduli");ShowProjects();Capture("progetti");
        foreach(var (kind,name) in new[]{("palo","palo_storico_0"),("micropalo","micropalo_IRS_45_Feld"),("sezione","")})
        {
            string module=kind=="palo"?"geo_palo_verticale":kind=="micropalo"?"geo_micropalo_verticale":"str_palo";
            JsonObject data=kind=="sezione"?SezioneCA.DefaultData():cases.First(c=>c.S("nome")==name)!["input"]!.AsObject();
            document=J.Obj(("formato","X"),("versione",1),("tipo","calcolo"),("modulo_id",module),("dati",data));dirty=false;ShowSheet(document);RefreshTree();await editor!.CalculateAsync();
            if(editor.Result is null||editor.Result.S("errore")!="")throw new Exception(kind+": "+editor.Result?.S("errore"));
            PerformLayout();Update();using var bmp=new Bitmap(Width,Height);DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(Path.Combine(directory,kind+".png"));
            Commit();Archivio.Scrivi(Path.Combine(directory,kind+".programma"),document);var loaded=Archivio.Leggi(Path.Combine(directory,kind+".programma"));if(!JsonNode.DeepEquals(loaded,document))throw new Exception("Round trip archivio "+kind);editor.ExportResult(Path.Combine(directory,kind+".json"));
            if(kind!="sezione")editor.ExportReport(Path.Combine(directory,kind+".docx"),kind,ReportWord.Sezioni.Select(s=>s.Key).ToHashSet());
            log.Add(kind+": calcolo, rendering, archivio, export OK");
            editor.VerifyLayout(directory,kind);log.Add(kind+": schede simultanee, controlli conservati, espansione/ripristino OK");
        }
        File.WriteAllLines(Path.Combine(directory,"smoke.txt"),log);dirty=false;
    }
}
