using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ANTHEA.ModelWorkspace;

namespace ANTHEA.ModelViewer.Wpf;

/// <summary>Screen-space section drawing only: all physical quantities and outlines come from the read-only catalog.</summary>
public sealed class ModelSectionPreview : FrameworkElement
{
    public static readonly DependencyProperty DetailProperty=DependencyProperty.Register(nameof(Detail),typeof(ModelSectionDetail),typeof(ModelSectionPreview),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public ModelSectionDetail? Detail {get=>(ModelSectionDetail?)GetValue(DetailProperty);set=>SetValue(DetailProperty,value);}
    public static readonly DependencyProperty InkProperty=DependencyProperty.Register(nameof(Ink),typeof(Brush),typeof(ModelSectionPreview),new FrameworkPropertyMetadata(Brushes.SlateGray,FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush Ink {get=>(Brush)GetValue(InkProperty);set=>SetValue(InkProperty,value);}
    static DependencyProperty Toggle(string name,bool initial)=>DependencyProperty.Register(name,typeof(bool),typeof(ModelSectionPreview),new FrameworkPropertyMetadata(initial,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ShowDimensionsProperty=Toggle(nameof(ShowDimensions),true);
    public static readonly DependencyProperty ShowLocalAxesProperty=Toggle(nameof(ShowLocalAxes),true);
    public static readonly DependencyProperty ShowPrincipalAxesProperty=Toggle(nameof(ShowPrincipalAxes),false);
    public static readonly DependencyProperty ShowCentroidProperty=Toggle(nameof(ShowCentroid),true);
    public static readonly DependencyProperty ShowInsertionProperty=Toggle(nameof(ShowInsertion),true);
    public bool ShowDimensions {get=>(bool)GetValue(ShowDimensionsProperty);set=>SetValue(ShowDimensionsProperty,value);}
    public bool ShowLocalAxes {get=>(bool)GetValue(ShowLocalAxesProperty);set=>SetValue(ShowLocalAxesProperty,value);}
    public bool ShowPrincipalAxes {get=>(bool)GetValue(ShowPrincipalAxesProperty);set=>SetValue(ShowPrincipalAxesProperty,value);}
    public bool ShowCentroid {get=>(bool)GetValue(ShowCentroidProperty);set=>SetValue(ShowCentroidProperty,value);}
    public bool ShowInsertion {get=>(bool)GetValue(ShowInsertionProperty);set=>SetValue(ShowInsertionProperty,value);}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);if(ActualWidth<60||ActualHeight<60)return;
        void Text(string value,Point p,Brush? brush=null,double size=11)=>dc.DrawText(new FormattedText(value,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,brush??Ink,VisualTreeHelper.GetDpi(this).PixelsPerDip),p);
        if(Detail is not {HasGeometry:true} detail){Text("Contorno non disponibile",new(16,20));return;}
        var points=detail.Contours.SelectMany(c=>c.Boundary).ToArray();if(points.Length==0)return;
        double ymin=points.Min(p=>p.Y),ymax=points.Max(p=>p.Y),zmin=points.Min(p=>p.Z),zmax=points.Max(p=>p.Z);
        var fit=points.AsEnumerable();if(ShowInsertion&&detail.Insertion is {} insertion)fit=fit.Append(insertion);
        var fitPoints=fit.ToArray();double minY=fitPoints.Min(p=>p.Y),maxY=fitPoints.Max(p=>p.Y),minZ=fitPoints.Min(p=>p.Z),maxZ=fitPoints.Max(p=>p.Z);
        double padding=ShowDimensions?64:25,scale=Math.Min(Math.Max(1,ActualWidth-padding*2)/Math.Max(.001,maxY-minY),Math.Max(1,ActualHeight-padding*2)/Math.Max(.001,maxZ-minZ));
        Point Map(SectionPoint p)=>new(ActualWidth/2+(p.Y-(minY+maxY)/2)*scale,ActualHeight/2-(p.Z-(minZ+maxZ)/2)*scale);
        Brush[] fills=[new SolidColorBrush(Color.FromRgb(102,165,221)),new SolidColorBrush(Color.FromRgb(200,181,126)),new SolidColorBrush(Color.FromRgb(133,192,167))];
        foreach(var contour in detail.Contours)
        {
            var geometry=new StreamGeometry{FillRule=FillRule.EvenOdd};
            using(var context=geometry.Open())
                foreach(var loop in new[]{contour.Boundary}.Concat(contour.Holes))
                {if(loop.Length<3)continue;context.BeginFigure(Map(loop[0]),true,true);context.PolyLineTo(loop.Skip(1).Select(Map).ToArray(),true,false);}
            geometry.Freeze();dc.DrawGeometry(fills[contour.Part%fills.Length],new Pen(Ink,1),geometry);
        }
        void Arrow(Point a,Point b,Brush brush,string name)
        {
            var pen=new Pen(brush,1.4);dc.DrawLine(pen,a,b);var v=b-a;if(v.Length<1)return;v.Normalize();var n=new Vector(-v.Y,v.X);
            dc.DrawLine(pen,b,b-v*8+n*3);dc.DrawLine(pen,b,b-v*8-n*3);Text(name,b+new Vector(4,-13),brush);
        }
        if(detail.Centroid is {} centroid)
        {
            Point c=Map(centroid);
            if(ShowLocalAxes){Arrow(c,c+new Vector(55,0),Brushes.SeaGreen,"Y");Arrow(c,c+new Vector(0,-55),Brushes.DodgerBlue,"Z");}
            if(ShowPrincipalAxes&&detail.PrincipalAngleRadians is double angle)
            {
                var axis=new Vector(Math.Cos(angle),-Math.Sin(angle));Arrow(c,c+axis*70,Brushes.DarkOrange,"1");Arrow(c,c+new Vector(axis.Y,-axis.X)*50,Brushes.DarkOrange,"2");
            }
            if(ShowCentroid){dc.DrawEllipse(Brushes.White,new Pen(Brushes.DarkSlateGray,1.5),c,4,4);Text("G",c+new Vector(-16,7));}
        }
        if(ShowInsertion&&detail.Insertion is {} reference)
        {
            var p=Map(reference);var pen=new Pen(Brushes.IndianRed,1.8);dc.DrawEllipse(null,pen,p,7,7);dc.DrawLine(pen,p-new Vector(11,0),p+new Vector(11,0));dc.DrawLine(pen,p-new Vector(0,11),p+new Vector(0,11));Text("O",p+new Vector(10,7),Brushes.IndianRed);
        }
        if(ShowDimensions)
        {
            var lower=Map(new(ymin,zmin));var upper=Map(new(ymax,zmax));var pen=new Pen(Ink,.8);
            double y=lower.Y+26,x=upper.X+26;
            dc.DrawLine(pen,new(lower.X,y-5),new(lower.X,y+5));dc.DrawLine(pen,new(upper.X,y-5),new(upper.X,y+5));dc.DrawLine(pen,new(lower.X,y),new(upper.X,y));
            Text((ymax-ymin).ToString("0.###")+" mm",new((lower.X+upper.X)/2-25,y+4));
            dc.DrawLine(pen,new(x-5,lower.Y),new(x+5,lower.Y));dc.DrawLine(pen,new(x-5,upper.Y),new(x+5,upper.Y));dc.DrawLine(pen,new(x,lower.Y),new(x,upper.Y));
            dc.PushTransform(new RotateTransform(-90,x+5,(lower.Y+upper.Y)/2));Text((zmax-zmin).ToString("0.###")+" mm",new(x+5,(lower.Y+upper.Y)/2-4));dc.Pop();
        }
    }
}
