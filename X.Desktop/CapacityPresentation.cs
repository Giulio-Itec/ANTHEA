using System.Drawing.Drawing2D;
using X.Core;

namespace X.Desktop;

public sealed partial class Plot
{
    public bool CapacityStyle{get;set;}
    public double CapacityDepth{get;set;}
    public int SondageCount{get;set;}
    public bool QuietCapacityEmpty{get;set;}
    public string CapacityEmptyMessage{get;set;}="Premere Calcola";
    private void RenderCapacity(Graphics g,Rectangle bounds)
    {
        // Scala e margini di pannello_grafici.py. Solo coordinate di presentazione.
        g.Clear(Color.White);g.SmoothingMode=SmoothingMode.AntiAlias;if(bounds.Width<100||bounds.Height<100)return;
        if(QuietCapacityEmpty&&Series.Count==0)
        {
            using var emptyFont=new Font("Segoe UI",8);using var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
            g.DrawString(CapacityEmptyMessage,emptyFont,Brushes.DimGray,bounds,format);return;
        }
        using var font=new Font("Segoe UI",7);using var strong=new Font("Segoe UI",7,FontStyle.Bold);using var ink=new SolidBrush(Color.FromArgb(17,24,39));
        var area=new RectangleF(47,28,Math.Max(30,bounds.Width-59),Math.Max(45,bounds.Height-84));
        var points=Series.SelectMany(s=>s.Points).Where(p=>p.Length>=2&&double.IsFinite(p[0])&&double.IsFinite(p[1])).ToArray();
        double maximum=points.Select(p=>p[0]).DefaultIfEmpty(0).Max(),axis=1;
        if(maximum>0){double power=Math.Pow(10,Math.Floor(Math.Log10(maximum)));axis=new[]{1d,2d,5d,10d}.First(n=>maximum/power<=n)*power;}
        double depth=CapacityDepth>0?CapacityDepth:points.Select(p=>p[1]).DefaultIfEmpty(1).Max();if(depth<=0)depth=1;
        string Number(double value)=>Math.Abs(value)>=1000?(value/1000).ToString("0.#")+"k":Math.Abs(value)>=10?value.ToString("0"):value.ToString("0.#");
        using var grid=new Pen(Color.FromArgb(243,244,246));
        for(int i=0;i<5;i++)
        {
            float f=i/4f,x=area.Left+f*area.Width,y=area.Top+f*area.Height;
            g.DrawLine(grid,x,area.Top,x,area.Bottom);g.DrawLine(grid,area.Left,y,area.Right,y);
            var tx=Number(f*axis);g.DrawString(tx,font,ink,x-g.MeasureString(tx,font).Width/2,area.Bottom+6);
            var ty=Number(f*depth);g.DrawString(ty,font,ink,area.Left-5-g.MeasureString(ty,font).Width,y-font.Height/2f);
        }
        using var border=new Pen(Color.FromArgb(17,24,39));g.DrawLine(border,area.Left,area.Top,area.Left,area.Bottom);g.DrawLine(border,area.Left,area.Bottom,area.Right,area.Bottom);
        g.DrawString(SondageCount==1?"1 sondaggio":$"{SondageCount} sondaggi · min e media",strong,ink,47,2);
        g.DrawString(YLabel.Contains("asse")?"s [m] lungo asse":"z [m]",font,ink,5,12);
        const string xlabel="Forza assiale [kN]";g.DrawString(xlabel,font,ink,area.Left+(area.Width-g.MeasureString(xlabel,font).Width)/2,area.Bottom+24);
        var state=g.Save();g.SetClip(area);
        foreach(var series in Series)
        {
            var line=series.Points.Where(p=>double.IsFinite(p[0])&&double.IsFinite(p[1])).Select(p=>new PointF(area.Left+(float)(p[0]/axis)*area.Width*zoom+offset.X,area.Top+(float)(p[1]/depth)*area.Height*zoom+offset.Y)).ToArray();
            using var pen=new Pen(series.Color,2){DashStyle=series.Dashed?DashStyle.Dash:DashStyle.Solid};if(line.Length>1)g.DrawLines(pen,line);
        }
        g.Restore(state);
        if(points.Length==0)g.DrawString("Nessuna curva attiva · premere Calcola",font,ink,new RectangleF(area.Left+15,area.Top+area.Height/2,area.Width-30,45));
    }
}

public sealed partial class FoglioEditor
{
    private readonly ComboBox capacityView=new(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Font=new Font("Segoe UI",8)};
    private readonly FlowLayoutPanel curveChoices=new(){Dock=DockStyle.Bottom,Height=120,AutoScroll=true,WrapContents=true,BackColor=Color.White};
    private readonly Label endValues=new(){Text="Valori a L [kN]",AutoSize=true,Font=new Font("Segoe UI",7,FontStyle.Bold),Padding=new Padding(0,8,0,0)};
    private void ConfigureCapacityPresentation()
    {
        plot.CapacityStyle=true;plot.QuietCapacityEmpty=PileCapacity;visible.Visible=false;
        if(PileCapacity){capacityView.Font=new Font("Segoe UI",15,FontStyle.Regular,GraphicsUnit.Pixel);curveChoices.Visible=false;endValues.Text="Valori a L non disponibili";}
        var page=outputs.TabPages[0];var wrapper=page.Controls[0];
        foreach(var bar in wrapper.Controls.OfType<FlowLayoutPanel>().ToArray()){wrapper.Controls.Remove(bar);bar.Dispose();}
        var graph=plot.Parent!;graph.Controls.Add(curveChoices);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=36,WrapContents=false,BackColor=Color.White};actions.Controls.Add(endValues);
        foreach(var (title,show) in new[]{("Tutte",true),("Nessuna",false)}){var button=Ui.Button(title,()=>SetVisible(show));button.Font=new Font("Segoe UI",7);button.Padding=Padding.Empty;button.Height=27;actions.Controls.Add(button);}graph.Controls.Add(actions);
        if(PileCapacity){actions.Height=32;outputs.Font=new Font("Segoe UI",8);foreach(TabPage tab in outputs.TabPages)tab.Padding=new Padding(2);}
        var context=new ContextMenuStrip();context.Items.Add("Adatta",null,(_,_)=>plot.ResetView());context.Items.Add("Salva PNG…",null,(_,_)=>SavePlot());plot.ContextMenuStrip=context;
        capacityView.Items.AddRange(Micro?new[]{"Tutte - progetto","Compressione","Trazione"}:new[]{"Tutte - progetto","Drenante · Compressione","Drenante · Trazione","Non drenante · Compressione","Non drenante · Trazione"});capacityView.SelectedIndex=0;
        capacityView.SelectedIndexChanged+=(_,_)=>{UpdateVisible();RebuildCurveChoices();};
        // Grafico e dettagli adiacenti, come nella sorgente. Abachi/JSON restano disponibili.
        var details=outputs.TabPages.Cast<TabPage>().First(t=>t.Text=="Tabelle e dettagli");outputs.TabPages.Remove(details);outputs.TabPages.Insert(1,details);
        var host=new Panel{Dock=DockStyle.Fill};var viewbar=new TableLayoutPanel{Dock=DockStyle.Top,Height=32,ColumnCount=2};viewbar.ColumnStyles.Add(new(SizeType.Percent,100));viewbar.ColumnStyles.Add(new(SizeType.Absolute,Micro?0:120));viewbar.Controls.Add(capacityView,0,0);if(!Micro)viewbar.Controls.Add(new Label{Text="Nq  Parametrizzata",Dock=DockStyle.Fill,Font=new Font("Segoe UI",8),TextAlign=ContentAlignment.MiddleRight},1,0);
        var parent=outputs.Parent!;parent.Controls.Remove(outputs);host.Controls.Add(outputs);host.Controls.Add(viewbar);parent.Controls.Add(host);host.BringToFront();
    }
    private bool CapacityIncludes(string key)
    {
        int selected=capacityView.SelectedIndex;if(selected<=0)return key.StartsWith("progetto_")||key.StartsWith("azione_");
        string condition=Micro?(selected==1?"compressione":"trazione"):new[]{"","drenante_compressione","drenante_trazione","non_drenante_compressione","non_drenante_trazione"}[selected];
        if(key.StartsWith("azione_"))return key.EndsWith(condition.EndsWith("trazione")?"trazione":"compressione");
        return key[(key.IndexOf('_')+1)..]==condition;
    }
    private void RebuildCurveChoices()
    {
        if(PileCapacity){curveChoices.Visible=allSeries.Count>0;curveChoices.Height=Math.Min(100,24*allSeries.Count);}
        foreach(Control control in curveChoices.Controls.Cast<Control>().ToArray())control.Dispose();curveChoices.Controls.Clear();
        endValues.Text=Result?.B("copertura_completa")==true?$"Valori a L={Data["generali"].S("lunghezza")} m [kN]":"Valori a L non disponibili";
        for(int i=0;i<allSeries.Count;i++)
        {
            if(!CapacityIncludes(seriesKeys[i]))continue;int index=i;var series=allSeries[i];string value=Result?.B("copertura_completa")==true&&series.Points.Count>0?series.Points[^1][0].ToString("N1"):"—";
            var check=new CheckBox{Text=series.Name+": "+value,ForeColor=series.Color,Font=new Font("Segoe UI",7),Width=240,Height=24,Checked=visible.GetItemChecked(i),Margin=new Padding(0),Tag=i};check.CheckedChanged+=(_,_)=>visible.SetItemChecked(index,check.Checked);curveChoices.Controls.Add(check);
        }
    }
}
