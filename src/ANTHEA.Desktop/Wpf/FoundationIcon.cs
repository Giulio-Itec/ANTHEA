using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

internal sealed class FoundationIcon : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);var outline=new Pen(Appearance.Ink,2);var arrow=new Pen(Appearance.Accent,2);
        dc.DrawLine(outline,new Point(6,39),new Point(74,39));
        dc.DrawRectangle(Appearance.Paper,outline,new Rect(15,41,50,16));
        dc.DrawRectangle(Appearance.Paper,outline,new Rect(33,23,14,18));
        dc.DrawLine(arrow,new Point(40,4),new Point(40,31));
        dc.DrawLine(arrow,new Point(40,31),new Point(35,25));dc.DrawLine(arrow,new Point(40,31),new Point(45,25));
        for(int x=8;x<72;x+=12)dc.DrawLine(outline,new Point(x,67),new Point(x+8,60));
    }
}
