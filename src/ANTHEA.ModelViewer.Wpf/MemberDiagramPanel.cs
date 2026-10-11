using System.Windows;
using System.Windows.Controls;

namespace ANTHEA.ModelViewer.Wpf;

/// <summary>Responsive chart layout; compact windows never receive unreadable three-column plots.</summary>
public sealed class MemberDiagramPanel : Panel
{
    int Columns(double width)=>InternalChildren.Count<=1?1:Math.Clamp((int)(width/300),1,3);
    protected override Size MeasureOverride(Size available)
    {
        double width=double.IsFinite(available.Width)?available.Width:900;int columns=Columns(width);
        double height=0;
        foreach(UIElement child in InternalChildren){child.Measure(new Size(width/columns,double.PositiveInfinity));height=Math.Max(height,child.DesiredSize.Height);}
        return new Size(width,height*Math.Ceiling((double)InternalChildren.Count/columns));
    }
    protected override Size ArrangeOverride(Size size)
    {
        int columns=Columns(size.Width);double height=InternalChildren.Cast<UIElement>().Select(c=>c.DesiredSize.Height).DefaultIfEmpty(0).Max();
        for(int i=0;i<InternalChildren.Count;i++)InternalChildren[i].Arrange(new Rect(i%columns*size.Width/columns,i/columns*height,size.Width/columns,height));
        return size;
    }
}
