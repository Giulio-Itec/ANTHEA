namespace X.Desktop;

public sealed partial class MainForm
{
    private readonly Panel dashboard=new(){Dock=DockStyle.Fill,BackColor=Ui.Bg};
    private readonly Panel dashboardBody=new(){Dock=DockStyle.Fill,BackColor=Ui.Bg,Padding=new Padding(42,24,42,36)};
    private readonly Panel moduleView=new(){Dock=DockStyle.Fill,BackColor=Ui.Bg};
    private readonly Dictionary<string,Button> navigation=new();
    private readonly ToolTip shellTips=new();
    private void InstallOriginalShell(MenuStrip menu)
    {
        Size=new Size(1600,990);MinimumSize=new Size(1200,850);Text="ANTHEA";
        // Conserviamo i comandi File e le scorciatoie, non il vecchio telaio a due colonne.
        content.Parent?.Controls.Remove(content);heading.Parent?.Controls.Remove(heading);tree.Parent?.Controls.Remove(tree);Controls.Remove(menu);
        foreach(var old in Controls.Cast<Control>().ToArray())old.Dispose();Controls.Clear();
        for(int i=1;i<menu.Items.Count;i++)menu.Items[i].Visible=false;
        menu.Visible=true;Controls.Add(moduleView);Controls.Add(dashboard);Controls.Add(menu);
        var sidebar=new Panel{Dock=DockStyle.Left,Width=250,BackColor=Color.White,Padding=new Padding(20,22,20,22)};
        var nav=new FlowLayoutPanel{Dock=DockStyle.Top,Height=465,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        nav.Controls.Add(OriginalAssets.LogoBox(150));
        nav.Controls.Add(new Label{Text="Strumenti di calcolo",ForeColor=Ui.Muted,Size=new Size(208,48)});
        void Nav(string title,Action action){var b=Ui.Button(title,()=>Safe(action));b.AutoSize=false;b.Size=new Size(208,45);b.TextAlign=ContentAlignment.MiddleLeft;b.Padding=new Padding(16,0,0,0);b.FlatAppearance.BorderSize=0;b.Font=new Font("Segoe UI",10,FontStyle.Bold);navigation[title]=b;nav.Controls.Add(b);}
        Nav("Home",ShowHome);Nav("Moduli singoli",()=>ShowModules());Nav("Progetti",ShowProjects);
        var files=FileCommands(false);files.Width=210;files.Height=52;nav.Controls.Add(files);
        sidebar.Controls.Add(nav);sidebar.Controls.Add(new Label{Text="Moduli disponibili: 3 di 6",Dock=DockStyle.Bottom,Height=32,ForeColor=Ui.Muted});dashboard.Controls.Add(dashboardBody);dashboard.Controls.Add(sidebar);
        var top=new Panel{Dock=DockStyle.Top,Height=68,BackColor=Ui.Navy,Padding=new Padding(12,8,12,8)};
        var back=Ui.Button("← Torna ad ANTHEA",()=>{if(editor?.Busy==true)return;Commit();ShowHome();});back.AutoSize=false;back.Width=205;back.Font=new Font("Segoe UI",9,FontStyle.Bold);back.Dock=DockStyle.Left;back.BackColor=Ui.Navy;back.ForeColor=Color.White;back.FlatAppearance.BorderSize=0;
        var commands=FileCommands(true);commands.Dock=DockStyle.Left;commands.Width=172;
        var titleArea=new Panel{Dock=DockStyle.Fill,Padding=new Padding(12,0,0,0)};heading.Dock=DockStyle.Top;heading.Height=28;heading.Padding=Padding.Empty;heading.Font=new Font("Segoe UI",11,FontStyle.Bold);heading.BackColor=Ui.Navy;heading.ForeColor=Color.White;
        titleArea.Controls.Add(new Label{Text="Scheda di calcolo · input, profilo e risultati",Dock=DockStyle.Bottom,Height=22,ForeColor=Color.FromArgb(185,200,216),Font=new Font("Segoe UI",8)});titleArea.Controls.Add(heading);
        top.Controls.Add(titleArea);top.Controls.Add(commands);top.Controls.Add(back);moduleView.Controls.Add(content);moduleView.Controls.Add(top);
    }
    private FlowLayoutPanel FileCommands(bool dark)
    {
        var bar=new FlowLayoutPanel{WrapContents=false,Padding=new Padding(4,6,0,0),BackColor=dark?Ui.Navy:Color.White};
        foreach(var (label,action) in new (string,Action)[]{("Apri",Open),("Salva",()=>Save(false)),("Salva con nome",()=>Save(true)),("Report Word",ExportReport)})
        {if(!dark&&label=="Report Word")continue;var button=new FileIconButton(label,()=>Safe(action),dark);shellTips.SetToolTip(button,label);bar.Controls.Add(button);}return bar;
    }
    private void SelectNavigation(string name)
    {
        dashboard.Visible=true;moduleView.Visible=false;dashboard.BringToFront();
        foreach(var (key,button) in navigation){button.BackColor=key==name?Ui.Navy:Color.White;button.ForeColor=key==name?Color.White:Ui.Navy;}
        // Un eventuale albero dei progetti deve sopravvivere al cambio pagina.
        tree.Parent?.Controls.Remove(tree);
        foreach(var c in dashboardBody.Controls.Cast<Control>().ToArray())c.Dispose();dashboardBody.Controls.Clear();
    }
    private Label TextBlock(string text,float size,bool bold=false,Color? color=null,int height=40)=>new(){Text=text,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=color??Ui.Navy,Dock=DockStyle.Top,Height=height,TextAlign=ContentAlignment.MiddleLeft};
    private Panel LandingCard(string title,string description,string badge,string button,Action action)
    {
        var card=new PaperPanel{Dock=DockStyle.Fill,Padding=new Padding(34,32,34,32),Margin=new Padding(0,0,24,0)};
        var text=new Panel{Dock=DockStyle.Top,Height=230};var badgeRow=new Panel{Dock=DockStyle.Top,Height=36};var badgeLabel=new Label{Text=badge,AutoSize=true,Font=new Font("Segoe UI",9,FontStyle.Bold),Padding=new Padding(12,6,12,6),BackColor=Color.FromArgb(234,242,250),ForeColor=Ui.Navy};badgeRow.Controls.Add(badgeLabel);
        text.Controls.Add(badgeRow);text.Controls.Add(TextBlock(description,11,false,Ui.Muted,88));text.Controls.Add(TextBlock(title,24,true,Color.FromArgb(17,24,39),65));
        var buttonRow=new Panel{Dock=DockStyle.Bottom,Height=48};var open=Ui.Button(button,()=>Safe(action),true);open.Dock=DockStyle.Left;open.Width=185;open.AutoSize=false;buttonRow.Controls.Add(open);card.Controls.Add(text);card.Controls.Add(buttonRow);return card;
    }
    private void ShowHome()
    {
        SelectNavigation("Home");
        var hero=new Panel{Dock=DockStyle.Top,Height=250,BackColor=Ui.Navy,Padding=new Padding(24,14,24,14)};
        var presentation=new Panel{Dock=DockStyle.Fill,Padding=new Padding(30,20,0,0)};
        presentation.Controls.Add(TextBlock("Apri un modulo indipendente oppure organizza più verifiche all'interno di un progetto.",10,false,Color.FromArgb(185,200,216),65));
        presentation.Controls.Add(TextBlock("Strumenti di calcolo per l'ingegneria",24,true,Color.White,100));
        var logo=OriginalAssets.LogoBox(222);logo.Dock=DockStyle.Left;hero.Controls.Add(presentation);hero.Controls.Add(logo);
        var cards=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(0,20,0,0)};cards.ColumnStyles.Add(new(SizeType.Percent,50));cards.ColumnStyles.Add(new(SizeType.Percent,50));
        cards.Controls.Add(LandingCard("Moduli singoli","Usa un modulo direttamente, senza creare un progetto.","6 moduli predisposti","Apri i moduli",()=>ShowModules()),0,0);
        cards.Controls.Add(LandingCard("Progetti","Raggruppa i fogli per opera, spalla, pila o altra struttura.","Struttura gerarchica libera","Gestisci i progetti",ShowProjects),1,0);
        dashboardBody.Controls.Add(cards);dashboardBody.Controls.Add(hero);
    }
    private void ShowModules(string discipline="Tutti")
    {
        SelectNavigation("Moduli singoli");var list=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(14,0,0,0)};
        var filter=new FlowLayoutPanel{Dock=DockStyle.Left,Width=220,BackColor=Color.White,FlowDirection=FlowDirection.TopDown,Padding=new Padding(16),WrapContents=false};filter.Controls.Add(new Label{Text="DISCIPLINE",Font=new Font("Segoe UI",9,FontStyle.Bold),Width=185,Height=40});
        foreach(var name in new[]{"Tutti","Geotecnica","Strutture"}){var b=Ui.Button(name,()=>ShowModules(name),discipline==name);b.Width=185;b.AutoSize=false;filter.Controls.Add(b);}
        var modules=new[]{("Geotecnica","Palo","Capacità portante verticale","geo_palo_verticale"),("Geotecnica","Palo","Capacità portante orizzontale",""),("Geotecnica","Micropalo","Capacità portante verticale","geo_micropalo_verticale"),("Geotecnica","Micropalo","Capacità portante orizzontale",""),("Strutture","Sezione in c.a.","Verifiche SLU · SLV · SLE","str_palo"),("Strutture","Micropalo","Verifiche strutturali","")};int i=0;
        foreach(var area in new[]{"Geotecnica","Strutture"}.Reverse().Where(a=>discipline=="Tutti"||a==discipline))
        {
            var section=new Panel{Dock=DockStyle.Top,Height=area=="Geotecnica"?530:325,Padding=new Padding(0,0,0,24)};
            var groups=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};groups.ColumnStyles.Add(new(SizeType.Percent,50));groups.ColumnStyles.Add(new(SizeType.Percent,50));int column=0;
            foreach(var group in modules.Where(m=>m.Item1==area).GroupBy(m=>m.Item2))
            {
                var pane=new PaperPanel{Dock=DockStyle.Fill,Margin=new Padding(0,0,14,0),Padding=new Padding(12)};
                foreach(var (_,title,description,id) in group.Reverse())
                {
                    var card=new PaperPanel{Dock=DockStyle.Top,Height=205,Padding=new Padding(12)};
                    var actionRow=new Panel{Dock=DockStyle.Bottom,Height=42};var open=Ui.Button(id==""?"Dettagli":"Apri",()=>{if(id!="")Safe(()=>NewCalculation(id));else MessageBox.Show(this,"Modulo in preparazione, come nella versione originale.");},id!="");open.Dock=DockStyle.Right;open.Width=78;open.AutoSize=false;open.Font=new Font("Segoe UI",9,FontStyle.Bold);if(id=="")open.FlatAppearance.BorderSize=0;actionRow.Controls.Add(open);
                    var textArea=new Panel{Dock=DockStyle.Fill,Padding=new Padding(12,0,0,0)};textArea.Controls.Add(TextBlock(area+" · "+title,8,false,Ui.Muted,30));textArea.Controls.Add(TextBlock(description.Replace(" portante ","\nportante "),11,true,Color.FromArgb(17,24,39),78));
                    var badgeRow=new Panel{Dock=DockStyle.Top,Height=27};badgeRow.Controls.Add(new Label{Text=id==""?"In preparazione":"Disponibile",AutoSize=true,Padding=new Padding(6,3,6,3),Font=new Font("Segoe UI",7,FontStyle.Bold),ForeColor=id==""?Ui.Muted:Color.FromArgb(21,128,61),BackColor=id==""?Color.FromArgb(241,245,249):Color.FromArgb(220,252,231)});textArea.Controls.Add(badgeRow);
                    string iconId=id!=""?id:area=="Strutture"?"str_micropalo":title=="Palo"?"geo_palo_orizzontale":"geo_micropalo_orizzontale";
                    card.Controls.Add(textArea);card.Controls.Add(new OriginalModuleIcon{Module=iconId,Dock=DockStyle.Left,Width=80});card.Controls.Add(actionRow);
                    pane.Controls.Add(card);
                }
                var label=TextBlock(group.Key.ToUpperInvariant(),9,true,Color.White,34);label.BackColor=Ui.Navy;label.Padding=new Padding(8,0,0,0);pane.Controls.Add(label);groups.Controls.Add(pane,column++,0);
            }
            section.Controls.Add(groups);section.Controls.Add(TextBlock(area,16,true,height:42));list.Controls.Add(section);i++;
        }
        dashboardBody.Controls.Add(list);dashboardBody.Controls.Add(filter);dashboardBody.Controls.Add(TextBlock("Scegli disciplina, elemento e verifica. I dati restano indipendenti da un progetto.",10,false,Ui.Muted,48));dashboardBody.Controls.Add(TextBlock("Moduli singoli",24,true,height:62));
    }
    private void ShowProjects()
    {
        Commit();SelectNavigation("Progetti");tree.BackColor=Color.White;tree.Dock=DockStyle.Fill;
        var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};columns.ColumnStyles.Add(new(SizeType.Percent,60));columns.ColumnStyles.Add(new(SizeType.Percent,40));
        var hierarchy=new PaperPanel{Dock=DockStyle.Fill,Padding=new Padding(18),Margin=new Padding(0,0,12,0)};hierarchy.Controls.Add(tree);hierarchy.Controls.Add(TextBlock("Struttura del progetto",11,true,Color.FromArgb(17,24,39),45));
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=45,AutoScroll=true};buttons.Controls.AddRange([Ui.Button("+ Struttura",()=>Safe(AddStructure)),Ui.Button("Rinomina",()=>Safe(Rename)),Ui.Button("Elimina",()=>Safe(Delete))]);hierarchy.Controls.Add(buttons);
        var guide=new PaperPanel{Dock=DockStyle.Fill,Padding=new Padding(22),Margin=new Padding(12,0,0,0),AutoScroll=true};
        foreach(var (module,title) in new[]{("str_palo","Sezione in c.a. · SLU / SLV / SLE"),("geo_micropalo_verticale","Micropalo · Capacità portante verticale"),("geo_palo_verticale","Palo · Capacità portante verticale")})
        {
            var moduleCard=new PaperPanel{Dock=DockStyle.Top,Height=105,Padding=new Padding(10)};var icon=new OriginalModuleIcon{Module=module,Dock=DockStyle.Left,Width=80};var label=TextBlock(title,10,true,height:70);moduleCard.Controls.Add(label);moduleCard.Controls.Add(icon);
            foreach(var c in new Control[]{moduleCard,label,icon}){c.Cursor=Cursors.Hand;c.DoubleClick+=(_,_)=>Safe(()=>AddSheet(module));}guide.Controls.Add(moduleCard);
        }
        guide.Controls.Add(TextBlock("Seleziona una struttura e fai doppio clic su una scheda.\n\nPer rinominare o eliminare: clic destro sul nodo.",10,false,Ui.Muted,145));guide.Controls.Add(TextBlock("Schede di calcolo",11,true,Color.FromArgb(17,24,39),45));columns.Controls.Add(hierarchy,0,0);columns.Controls.Add(guide,1,0);
        var titleRow=new Panel{Dock=DockStyle.Top,Height=65};var add=Ui.Button("[+] Nuovo progetto",()=>Safe(AddProject),true);add.Dock=DockStyle.Right;add.Width=205;add.AutoSize=false;titleRow.Controls.Add(TextBlock("Progetti",24,true,height:62));titleRow.Controls.Add(add);
        dashboardBody.Controls.Add(columns);dashboardBody.Controls.Add(TextBlock("Organizza i fogli di calcolo per struttura e conserva una gerarchia ordinata.",10,false,Ui.Muted,55));dashboardBody.Controls.Add(titleRow);RefreshTree();
    }
    private void ShowModuleView(){dashboard.Visible=false;moduleView.Visible=true;moduleView.BringToFront();}
}
