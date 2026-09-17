using System.Text.Json.Nodes;
using X.Core;

namespace X.Desktop;

// Presentazione: stessa organizzazione a schede simultanee del programma Tkinter.
// Nessuna formula o modifica ai dati di calcolo è contenuta nel layout.
public sealed partial class FoglioEditor
{
    private readonly Panel originalCanvas=new(){BackColor=Ui.Bg};
    private readonly Panel originalScroll=new(){Dock=DockStyle.Fill,AutoScroll=true,BackColor=Ui.Bg};
    private readonly List<Panel> originalCards=[];
    private readonly DataGridView verification=Ui.Grid();
    private readonly StratigraphyDrawing stratigraphy=new();
    private int expandedCard=-1;

    private Control Take(TabControl source,string title)
    {
        var tab=source.TabPages.Cast<TabPage>().First(t=>t.Text==title);
        var body=tab.Controls[0];tab.Controls.Remove(body);source.TabPages.Remove(tab);tab.Dispose();return body;
    }
    private Panel Card(string title,Control body,bool expandable=false)
    {
        var card=new PaperPanel{Padding=new Padding(Module=="str_palo"?8:16)};
        var header=new TableLayoutPanel{Dock=DockStyle.Top,Height=36,BackColor=Color.White,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        header.ColumnStyles.Add(new(SizeType.Percent,100));header.ColumnStyles.Add(new(SizeType.Absolute,expandable?88:0));
        header.Controls.Add(new Label{Text=title,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Color.FromArgb(17,24,39)},0,0);
        if(expandable){int index=originalCards.Count;var expand=Ui.Button("Estendi",()=>{expandedCard=expandedCard==index?-1:index;LayoutCards();});expand.Dock=DockStyle.Fill;expand.Font=new Font("Segoe UI",8);expand.Margin=Padding.Empty;expand.AutoSize=false;header.Controls.Add(expand,1,0);}
        body.Dock=DockStyle.Fill;card.Controls.Add(body);card.Controls.Add(header);originalCards.Add(card);originalCanvas.Controls.Add(card);return card;
    }
    private void RestoreOriginalLayout()
    {
        SuspendLayout();
        // Spostiamo gli stessi editor e relativi binding, senza duplicare gli input.
        if(Module=="str_palo")
        {
            Card("Geometria",Take(inputs,"Geometria"));Card("Materiali",Take(inputs,"Materiali"));Card("Armatura",Take(inputs,"Armatura"));
            Card("Disegno sezione",Take(outputs,"Sezione"));Card("Dominio",Take(outputs,"Domini"));
            var combos=(TabControl)Take(inputs,"Azioni SLU / SLV / SLE");
            foreach(var name in new[]{"SLU","SLV","SLE"})
            {
                var body=Take(combos,name);var bar=body.Controls.OfType<FlowLayoutPanel>().First();bar.Dock=DockStyle.Bottom;bar.Height=34;bar.BackColor=Color.White;
                foreach(var button in bar.Controls.OfType<Button>()){button.Font=new Font("Segoe UI",8);button.FlatAppearance.BorderSize=0;button.Padding=Padding.Empty;button.Height=27;}
                var grid=comboGrids[name];grid.CellBorderStyle=DataGridViewCellBorderStyle.None;grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.White;grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Color.White;grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",8,FontStyle.Bold);grid.ColumnHeadersDefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;grid.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;grid.DefaultCellStyle.Font=new Font("Segoe UI",9);grid.Columns[0].HeaderText="Combo";grid.DefaultCellStyle.BackColor=Color.FromArgb(248,250,252);grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(234,242,250);grid.ClearSelection();Card(name,body);
            }
            combos.Dispose();
            // I dettagli rimangono accessibili, senza sostituire le schede di lavoro.
            var detailButton=Ui.Button("Tabelle e dettagli",()=>ShowDetails());detailButton.Dock=DockStyle.Bottom;originalCards[7].Controls.Add(detailButton);detailButton.BringToFront();
        }
        else
        {
            Card("Dati generali",Take(inputs,"Dati generali"));Card("Efficienza",Take(inputs,"Efficienza"));Card("Coefficienti normativa",Take(inputs,"Coefficienti"));
            foreach(var h in new[]{"Cond.","NEd","Rd","Util."})verification.Columns.Add(h,h);
            verification.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;verification.ColumnHeadersDefaultCellStyle.BackColor=Ui.Navy;verification.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;verification.Font=new Font("Segoe UI",8);verification.RowTemplate.Height=40;
            foreach(var name in Micro?new[]{"C","T"}:new[]{"Dr. C","Dr. T","N.dr. C","N.dr. T"})verification.Rows.Add(name,"—","—","—");
            var check=new Panel();var note=new Label{Text="Valori in kN · Util. = NEd / Rd",Dock=DockStyle.Bottom,Height=32,Font=new Font("Segoe UI",8),ForeColor=Ui.Muted};check.Controls.Add(verification);check.Controls.Add(note);Card("Verifica",check);
            Card("Stratigrafia",Take(inputs,"Stratigrafie"),true);
            var profile=Take(outputs,"Profilo");profile.Dispose();stratigraphy.Data=Data;stratigraphy.Micro=Micro;Card("Profilo stratigrafico",stratigraphy,true);
            // Il selettore delle curve è sotto il grafico, non una colonna laterale.
            visible.Dock=DockStyle.Bottom;visible.Height=125;visible.MultiColumn=true;visible.ColumnWidth=185;visible.Font=new Font("Segoe UI",8);
            outputs.TabPages[0].Text="Grafico";Card("Grafici capacità portante",outputs,true);ConfigureCapacityPresentation();
        }
        foreach(var retained in new Control[]{inputs,outputs,status,calculate,warnings})
            if(retained.Parent is not null&&retained.Parent!=originalCards.LastOrDefault())
                if(retained!=outputs||Module=="str_palo")retained.Parent.Controls.Remove(retained);
        var old=Controls.Cast<Control>().ToArray();Controls.Clear();
        foreach(var c in old)c.Dispose();
        // Le pagine residue della sezione sono conservate per la finestra dettagli.
        originalScroll.Controls.Add(originalCanvas);Controls.Add(originalScroll);
        var footer=new Panel{Dock=DockStyle.Bottom,Height=42,Padding=new Padding(24,4,24,4),BackColor=Ui.Bg};
        status.Dock=DockStyle.Fill;calculate.Dock=DockStyle.Right;calculate.Width=120;calculate.AutoSize=false;footer.Controls.Add(status);footer.Controls.Add(calculate);Controls.Add(footer);
        warnings.Dock=DockStyle.Bottom;warnings.Height=58;warnings.Visible=false;Controls.Add(warnings);warnings.TextChanged+=(_,_)=>warnings.Visible=!string.IsNullOrWhiteSpace(warnings.Text);
        originalScroll.Resize+=(_,_)=>LayoutCards();ResumeLayout();LayoutCards();
    }
    private void LayoutCards()
    {
        int w=Math.Max(Module=="str_palo"?1100:1440,originalScroll.ClientSize.Width-(Module=="str_palo"?0:20)),margin=Module=="str_palo"?5:Math.Max(24,(int)(w*.03)),gap=Module=="str_palo"?10:20;
        int h=Math.Max(Module=="str_palo"?650:760,originalScroll.ClientSize.Height-4);
        if(expandedCard==6&&Module!="str_palo")
        {
            originalCanvas.Size=new Size(w,h);for(int i=0;i<originalCards.Count;i++)originalCards[i].Visible=i==expandedCard;
            originalCards[expandedCard].SetBounds(16,12,w-32,h-24);return;
        }
        foreach(var card in originalCards)card.Visible=true;
        if(Module=="str_palo")
        {
            int left=(int)((w-2*margin-gap)*.60),right=w-2*margin-gap-left;
            int top=430,third=(left-2*gap)/3,bottom=h-top-gap-2*margin;
            for(int i=0;i<3;i++)originalCards[i].SetBounds(margin+i*(third+gap),margin,third,top);
            int half=(left-gap)/2;originalCards[3].SetBounds(margin,margin+top+gap,half,bottom);originalCards[4].SetBounds(margin+half+gap,margin+top+gap,left-half-gap,bottom);
            int rh=(h-2*margin-2*gap)/3;for(int i=0;i<3;i++)originalCards[5+i].SetBounds(margin+left+gap,margin+i*(rh+gap),right,rh);
        }
        else
        {
            int top=Math.Max(320,Math.Max((generalForm?.ContentHeight??300)+80,(normativeForm?.ContentHeight??260)+104)),available=w-2*margin-3*gap,x=margin;double[] ratios=[.39,.17,.24,.20];
            for(int i=0;i<4;i++){int cw=i==3?w-margin-x:(int)(available*ratios[i]);originalCards[i].SetBounds(x,24,cw,top);x+=cw+gap;}
            int y=24+top+gap;h=Math.Max(h,y+680);available=w-2*margin-2*gap;x=margin;double[] lower=[.47,.20,.33];
            if(expandedCard==4)lower=[.66,.15,.19];else if(expandedCard==5)lower=[.16,.65,.19];
            for(int i=0;i<3;i++){int cw=i==2?w-margin-x:(int)(available*lower[i]);originalCards[i+4].SetBounds(x,y,cw,h-y-24);x+=cw+gap;}
        }
        originalCanvas.Size=new Size(w,h);
    }
    private void ShowDetails()
    {
        using var dialog=new Form{Text="Tabelle e dettagli",Width=1050,Height=650,StartPosition=FormStartPosition.CenterParent};
        outputs.Dock=DockStyle.Fill;dialog.Controls.Add(outputs);dialog.FormClosing+=(_,_)=>dialog.Controls.Remove(outputs);dialog.ShowDialog(this);
    }
    private void UpdateVerification()
    {
        if(Module=="str_palo"&&Result is null)foreach(var grid in comboGrids.Values)foreach(DataGridViewRow row in grid.Rows)for(int i=4;i<grid.Columns.Count;i++)row.Cells[i].Value="—";
        foreach(DataGridViewRow row in verification.Rows)for(int i=1;i<4;i++)row.Cells[i].Value="—";
        if(Result is null||!Result.B("copertura_completa"))return;
        int index=0;foreach(var (key,curve) in Result["curve"]!.AsObject())
        {
            if(index>=verification.Rows.Count)break;
            double rd=curve!.Array("progetto")[^1]![1]!.GetValue<double>();var action=Result["azioni"]!.Array(key.Contains("trazione")?"trazione":"compressione").LastOrDefault();double? ed=J.Number(action?[1]);
            var cells=verification.Rows[index++].Cells;cells[1].Value=ed?.ToString("N1")??"—";cells[2].Value=rd.ToString("N1");cells[3].Value=ed.HasValue&&rd>0?(100*ed.Value/rd).ToString("N1")+"%":"—";
        }
    }
    internal void VerifyLayout(string directory,string name)
    {
        if(originalCards.Count!=(Module=="str_palo"?8:7))throw new InvalidOperationException("Numero di schede del layout errato.");
        if(calculate.IsDisposed||status.IsDisposed||outputs.IsDisposed||warnings.IsDisposed)throw new InvalidOperationException("Controllo necessario rimosso durante la ricomposizione del layout.");
        foreach(var card in originalCards)if(card.Width<150||card.Height<150)throw new InvalidOperationException("Scheda troppo piccola.");
        if(Module!="str_palo")
        {
            expandedCard=6;LayoutCards();PerformLayout();
            using var picture=new Bitmap(Width,Height);DrawToBitmap(picture,ClientRectangle);picture.Save(Path.Combine(directory,name+"_grafico_esteso.png"));
            if(originalCards.Take(6).Any(c=>c.Visible)||!originalCards[6].Visible)throw new InvalidOperationException("Espansione grafico non corretta.");
            expandedCard=-1;LayoutCards();if(originalCards.Any(c=>!c.Visible))throw new InvalidOperationException("Ripristino schede non corretto.");
            var saved=(JsonObject)Data.DeepClone();capacityView.SelectedIndex=1;UpdateVisible();capacityView.SelectedIndex=0;UpdateVisible();if(!JsonNode.DeepEquals(saved,Data))throw new InvalidOperationException("Il selettore grafico ha modificato gli input.");
        }
        var before=(JsonObject)Data.DeepClone();string key=Module=="str_palo"?"diameter_mm":"diametro";string value=generalForm!.Editors[key].Text;
        generalForm.Set(key,value+" ");if(Result is not null)throw new InvalidOperationException("Risultati non invalidati dopo modifica input.");generalForm.Set(key,value);
        if(!JsonNode.DeepEquals(before,Data))throw new InvalidOperationException("I controlli grafici non conservano i dati dopo ripristino input.");
    }
}

public sealed class StratigraphyDrawing:Control
{
    public JsonObject? Data{get;set;}public bool Micro{get;set;}
    public StratigraphyDrawing(){Dock=DockStyle.Fill;DoubleBuffered=true;ResizeRedraw=true;BackColor=Color.White;}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);if(Data is null)return;var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var sets=Data.Array("stratigrafie");var gen=Data["generali"];double length=gen.D("lunghezza"),angle=Micro?gen.D("inclinazione")*Math.PI/180:0;
        double total=Math.Max(length*Math.Cos(angle),sets.Select(s=>s!.AsArray().Sum(r=>Math.Max(0,r.D("spessore")))).DefaultIfEmpty(0).Max());
        if(total<=0){g.DrawString("Inserire la stratigrafia",Font,Brushes.Gray,12,35);return;}
        float top=42,bottom=Height-45,scale=(bottom-top)/(float)total;if(scale<=0)return;int count=Math.Max(1,sets.Count);float band=(Width-56f)/count;
        if(Micro&&length*Math.Sin(angle)>0)scale=Math.Min(scale,(band-20)/(float)(length*Math.Sin(angle)));
        Color[] colors=new[]{"#F4C95D","#DFA06E","#A8C686","#8FB8DE","#C6A0D5","#C9B79C","#F2A7B5","#86C5C9","#E4B363","#A9BCD0","#B8D8BA","#D6B5D8"}.Select(ColorTranslator.FromHtml).ToArray();
        using var font=new Font("Segoe UI",8);using var border=new Pen(Color.FromArgb(155,161,168));
        for(int j=0;j<sets.Count;j++)
        {
            float x=38+j*band;double z=0;int layer=0;g.DrawString($"Stratigrafia {j+1}",font,Brushes.DimGray,x,15);
            foreach(var row in sets[j]!.AsArray())
            {
                double h=row.D("spessore");if(h<=0)continue;float y=top+(float)z*scale,ph=(float)h*scale;using var fill=new SolidBrush(colors[layer++%colors.Length]);g.FillRectangle(fill,x,y,Math.Max(1,band-10),ph);g.DrawRectangle(border,x,y,Math.Max(1,band-10),ph);
                g.DrawString(z.ToString("0.##"),font,Brushes.DimGray,0,y-6);
                string name=row.S("strato",((char)('A'+(layer-1)%26)).ToString());
                string description=Micro?$"Strato {name}\n{row.S("terreno")}\nS = {row.S("spessore")} m\nα = {row.S("alpha")}":$"Strato {name}\n{row.S("tipologia")} · {row.S("addensamento")}\nS={row.S("spessore")} m · φ′={row.S("angolo_attrito")}°\nγ={row.S("peso_specifico")} · γsat={row.S("peso_specifico_saturo")} kN/m³\nc′={row.S("coesione_efficace")} · Cu={row.S("coesione_non_drenata")} kPa";
                if(ph>25){using var compact=new Font("Segoe UI",band<180?6:7);using var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};g.DrawString(description,compact,Brushes.Black,new RectangleF(x+4,y+3,band-18,ph-6),format);}z+=h;
            }
            g.DrawString(z.ToString("0.##"),font,Brushes.DimGray,0,top+(float)z*scale-6);
            // Le coordinate del palo sono esclusivamente una rappresentazione degli input.
            if(Micro){float px=x+5;using var pile=new Pen(Color.FromArgb(150,153,157),Math.Clamp((float)(gen.D("diametro")*scale),6,22));g.DrawLine(pile,px,top,px+(float)(length*Math.Sin(angle))*scale,top+(float)(length*Math.Cos(angle))*scale);}
        }
        if(!Micro&&gen.B("presenza_falda")){float y=top+(float)gen.D("profondita_falda")*scale;using var water=new Pen(Color.DodgerBlue,2){DashStyle=System.Drawing.Drawing2D.DashStyle.Dash};g.DrawLine(water,28,y,Width-10,y);g.DrawString("Falda",font,Brushes.DodgerBlue,38,y-16);}
        g.DrawString("Profondità z [m]",font,Brushes.DimGray,20,Height-25);
    }
}
