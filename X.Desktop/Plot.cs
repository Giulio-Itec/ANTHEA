using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json.Nodes;
using X.Core;

namespace X.Desktop;

public sealed record Serie(string Name,List<double[]> Points,Color Color,bool Dashed=false,bool Highlighted=false);
public sealed record PlotMarker(double X,double Y,string Label,Color Color);
public sealed partial class Plot:Control
{
    public List<Serie> Series { get; set; }=[];
    public string Title { get; set; }="Premere Calcola";
    public string XLabel { get; set; }="Resistenza / azione [kN]";
    public string YLabel { get; set; }="Profondità z [m]";
    public bool InvertY { get; set; }=true;
    public bool EqualScale { get; set; }
    public double? XMinimum {get;set;}
    public List<PlotMarker> Markers{get;set;}=[];
    public string Note{get;set;}="";
    private float zoom=1;private PointF offset;private Point? drag;
    public Plot(){DoubleBuffered=true;BackColor=Color.White;Dock=DockStyle.Fill;ResizeRedraw=true;MouseWheel+=(_,e)=>{zoom=Math.Clamp(zoom*(e.Delta>0?1.2f:1/1.2f),1,20);Invalidate();};MouseDown+=(_,e)=>{if(e.Button==MouseButtons.Left){drag=e.Location;Capture=true;}};MouseMove+=(_,e)=>{if(drag is Point p){offset.X+=e.X-p.X;offset.Y+=e.Y-p.Y;drag=e.Location;Invalidate();}};MouseUp+=(_,_)=>{drag=null;Capture=false;};DoubleClick+=(_,_)=>ResetView();}
    public void ResetView(){zoom=1;offset=PointF.Empty;Invalidate();}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Render(e.Graphics,ClientRectangle);}
    public void Render(Graphics g,Rectangle bounds)
    {
        if(CapacityStyle){RenderCapacity(g,bounds);return;}
        g.Clear(Color.White);g.SmoothingMode=SmoothingMode.AntiAlias;using var titleFont=new Font("Segoe UI",12,FontStyle.Bold);using var font=new Font("Segoe UI",8);using var ink=new SolidBrush(Ui.Navy);g.DrawString(Title,titleFont,ink,18,12);
        if(bounds.Width<200||bounds.Height<180)return;int legendColumns=bounds.Width>750?3:2;int legendHeight=(int)Math.Ceiling(Series.Count/(double)legendColumns)*18;var area=new RectangleF(70,65,bounds.Width-115,Math.Max(60,bounds.Height-125-legendHeight));
        if(Note!=""){area.Y+=30;area.Height=Math.Max(40,area.Height-30);using var noteFont=new Font("Segoe UI",13,FontStyle.Regular,GraphicsUnit.Pixel);g.DrawString(Note,noteFont,ink,new RectangleF(18,39,bounds.Width-30,34));}
        var points=Series.SelectMany(s=>s.Points).Concat(Markers.Select(m=>new[]{m.X,m.Y})).Where(p=>p.Length>=2&&double.IsFinite(p[0])&&double.IsFinite(p[1])).ToArray();if(points.Length==0){g.DrawString("Nessun risultato da visualizzare",font,Brushes.Gray,70,80);return;}
        double xmin=Math.Min(0,points.Min(p=>p[0])),xmax=points.Max(p=>p[0]),ymin=Math.Min(0,points.Min(p=>p[1])),ymax=points.Max(p=>p[1]);
        if(XMinimum is double minimum)xmin=minimum;
        if(xmax<=xmin)xmax=xmin+1;if(ymax<=ymin)ymax=ymin+1;double dx=xmax-xmin,dy=ymax-ymin;if(XMinimum is null)xmin-=dx*.04;xmax+=dx*.04;if(!InvertY){ymin-=dy*.05;ymax+=dy*.05;}
        if(EqualScale){double ratio=area.Width/area.Height;if((xmax-xmin)/(ymax-ymin)>ratio){double mid=(ymin+ymax)/2,span=(xmax-xmin)/ratio;ymin=mid-span/2;ymax=mid+span/2;}else{double mid=(xmin+xmax)/2,span=(ymax-ymin)*ratio;xmin=mid-span/2;xmax=mid+span/2;}}
        PointF P(double x,double y)=>new(area.Left+(float)((x-xmin)/(xmax-xmin))*area.Width*zoom+offset.X,area.Top+(float)(InvertY?(y-ymin)/(ymax-ymin):1-(y-ymin)/(ymax-ymin))*area.Height*zoom+offset.Y);
        using var grid=new Pen(Color.FromArgb(224,231,239));using var border=new Pen(Color.FromArgb(140,155,174));
        for(int i=0;i<=5;i++)
        {
            double x=xmin+(xmax-xmin)*i/5,y=ymin+(ymax-ymin)*i/5;var px=P(x,ymin);var py=P(xmin,y);
            if(px.X>=area.Left&&px.X<=area.Right){g.DrawLine(grid,px.X,area.Top,px.X,area.Bottom);g.DrawString(x.ToString("0.##"),font,Brushes.DimGray,px.X-14,area.Bottom+8);}
            if(py.Y>=area.Top&&py.Y<=area.Bottom){g.DrawLine(grid,area.Left,py.Y,area.Right,py.Y);g.DrawString(y.ToString("0.##"),font,Brushes.DimGray,8,py.Y-7);}
        }
        var state=g.Save();g.SetClip(area);
        foreach(var s in Series)
        {
            var pp=s.Points.Where(p=>p.Length>=2&&double.IsFinite(p[0])&&double.IsFinite(p[1])).Select(p=>P(p[0],p[1])).ToArray();using var pen=new Pen(s.Color,s.Highlighted?3.5f:s.Name.StartsWith("Ed")?1.7f:2.1f){DashStyle=s.Dashed?DashStyle.Dash:DashStyle.Solid};
            if(pp.Length>1)g.DrawLines(pen,pp);else if(pp.Length==1)g.FillEllipse(new SolidBrush(s.Color),pp[0].X-4,pp[0].Y-4,8,8);
        }
        foreach(var marker in Markers)
        {
            var p=P(marker.X,marker.Y);using var guide=new Pen(marker.Color,1){DashStyle=DashStyle.Dash};using var fill=new SolidBrush(marker.Color);
            g.DrawLine(guide,area.Left,p.Y,p.X,p.Y);g.DrawLine(guide,p.X,p.Y,p.X,area.Bottom);g.FillEllipse(fill,p.X-5,p.Y-5,10,10);g.DrawEllipse(Pens.White,p.X-5,p.Y-5,10,10);
            using var markerFont=new Font("Segoe UI",13,FontStyle.Bold,GraphicsUnit.Pixel);var size=g.MeasureString(marker.Label,markerFont);float labelX=Math.Clamp(p.X+9,area.Left,Math.Max(area.Left,area.Right-size.Width)),labelY=Math.Clamp(p.Y-25,area.Top,Math.Max(area.Top,area.Bottom-size.Height));
            g.FillRectangle(Brushes.White,labelX,labelY,size.Width,size.Height);g.DrawString(marker.Label,markerFont,fill,labelX,labelY);
        }
        g.Restore(state);g.DrawRectangle(border,area.X,area.Y,area.Width,area.Height);g.DrawString(XLabel,font,ink,area.Left,area.Bottom+30);g.DrawString(YLabel,font,ink,18,Note==""?43:73);
        for(int i=0;i<Series.Count;i++){float x=18+(i%legendColumns)*(bounds.Width-36f)/legendColumns,y=area.Bottom+53+(i/legendColumns)*18;using var pen=new Pen(Series[i].Color,2){DashStyle=Series[i].Dashed?DashStyle.Dash:DashStyle.Solid};g.DrawLine(pen,x,y+6,x+20,y+6);g.DrawString(Series[i].Name,font,Brushes.DimGray,x+25,y);}
    }
    public byte[] Png(){using var bitmap=new Bitmap(1200,750);using(var g=Graphics.FromImage(bitmap))Render(g,new Rectangle(0,0,1200,750));using var stream=new MemoryStream();bitmap.Save(stream,ImageFormat.Png);return stream.ToArray();}
}

public sealed class SectionDrawing:Control
{
    public List<double[]> Outline {get;set;}=[];public List<Barra> Bars {get;set;}=[];public double[]? Plane {get;set;}
    public SectionDrawing(){DoubleBuffered=true;BackColor=Color.White;Dock=DockStyle.Fill;ResizeRedraw=true;}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;if(Outline.Count<3)return;
        double xmin=Outline.Min(p=>p[0]),xmax=Outline.Max(p=>p[0]),ymin=Outline.Min(p=>p[1]),ymax=Outline.Max(p=>p[1]),scale=Math.Min((Width-60)/(xmax-xmin),(Height-75)/(ymax-ymin));if(scale<=0)return;
        PointF P(double x,double y)=>new((float)(Width/2+(x-(xmin+xmax)/2)*scale),(float)((Height-30)/2-(y-(ymin+ymax)/2)*scale));
        var outline=Outline.Select(p=>P(p[0],p[1])).ToArray();using var path=new GraphicsPath();path.AddPolygon(outline);using var concrete=new SolidBrush(Color.FromArgb(229,231,235));g.FillPath(concrete,path);
        if(Plane is {Length:3} q)
        {
            // Ritaglio della regione compressa sul poligono: solo rappresentazione del piano già calcolato.
            double Stress(double[] p)=>q[0]+q[1]*p[1]-q[2]*p[0];var clipped=new List<double[]>();
            for(int i=0;i<Outline.Count;i++){var a=Outline[i];var b=Outline[(i+1)%Outline.Count];double sa=Stress(a),sb=Stress(b);if(sa>=0)clipped.Add(a);if((sa>=0)!=(sb>=0)){double t=sa/(sa-sb);clipped.Add([a[0]+t*(b[0]-a[0]),a[1]+t*(b[1]-a[1])]);}}
            if(clipped.Count>=3){using var red=new SolidBrush(Color.FromArgb(252,165,165));g.FillPolygon(red,clipped.Select(p=>P(p[0],p[1])).ToArray());}
        }
        using var border=new Pen(Ui.Navy,1.5f);g.DrawPath(border,path);
        foreach(var bar in Bars){var p=P(bar.X,bar.Y);float diameter=Math.Max(4,(float)(bar.Diametro*scale));bool tension=Plane is {Length:3} plane&&plane[0]+plane[1]*bar.Y-plane[2]*bar.X<0;using var steel=new SolidBrush(tension?Color.FromArgb(37,99,235):Ui.Navy);g.FillEllipse(steel,p.X-diameter/2,p.Y-diameter/2,diameter,diameter);g.DrawEllipse(Pens.White,p.X-diameter/2,p.Y-diameter/2,diameter,diameter);}
        using var caption=new Font("Segoe UI",9);g.DrawString(Plane is null?"Geometria della sezione · nessun risultato di verifica":"Sezione [mm] · CLS compresso · barre blu: tese",caption,Brushes.DimGray,new RectangleF(10,Height-32,Width-20,30));
    }
}
