using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using X.Desktop.Themes;

namespace X.Desktop.Views.Climate;

/// <summary>Theme-aware rendering of the packaged, original climate SVG figures. No external files or SVG scripts.</summary>
public sealed class ActionReferenceSketch:FrameworkElement
{
    private XElement? svg;
    private double width=864,height=346;
    private readonly Dictionary<string,XElement> definitions=[];
    private readonly Dictionary<XElement,Geometry> paths=[];
    public static readonly DependencyProperty SourceProperty=DependencyProperty.Register(nameof(Source),typeof(string),typeof(ActionReferenceSketch),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender|FrameworkPropertyMetadataOptions.AffectsMeasure,(d,_)=>((ActionReferenceSketch)d).Load()));
    public string? Source{get=>(string?)GetValue(SourceProperty);set=>SetValue(SourceProperty,value);}
    public int PrimitiveCount=>paths.Count+(svg?.Descendants().Count(e=>e.Name.LocalName=="text")??0);
    private static double Number(string value)=>double.Parse(value.Replace("px","",StringComparison.Ordinal),CultureInfo.InvariantCulture);
    private void Load()
    {
        svg=null;paths.Clear();definitions.Clear();if(Source is null)return;
        string name=Source.Split('/').Last().Replace(".png",".svg",StringComparison.Ordinal);
        if(!name.StartsWith("clima-",StringComparison.Ordinal)||!name.EndsWith(".svg",StringComparison.Ordinal))throw new ArgumentException("Figura Azioni non riconosciuta.");
        using var stream=typeof(ActionReferenceSketch).Assembly.GetManifestResourceStream("Wiki.figures."+name)??throw new InvalidOperationException("Figura Azioni mancante: "+name);
        using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Ignore,XmlResolver=null});
        svg=XElement.Load(reader);var view=svg.Attribute("viewBox")!.Value.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(Number).ToArray();width=view[2];height=view[3];
        foreach(var element in svg.Descendants())
        {
            if(element.Attribute("id") is { } id)definitions[id.Value]=element;
            if(element.Name.LocalName=="path"){var geometry=Geometry.Parse(element.Attribute("d")!.Value);geometry.Freeze();paths[element]=geometry;}
        }
        System.Windows.Automation.AutomationProperties.SetName(this,"Convenzioni di riferimento · "+name);
    }
    protected override Size MeasureOverride(Size available)
    {double w=double.IsFinite(available.Width)?available.Width:width;return new(w,w*height/width);}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);ActionDrawingStyle.Surface(this,dc);if(svg is null)return;
        double scale=Math.Min(ActualWidth/width,ActualHeight/height);dc.PushTransform(new ScaleTransform(scale,scale));
        foreach(var element in svg.Elements())Draw(element,dc);dc.Pop();
    }
    private static Dictionary<string,string> Style(XElement e)=>((string?)e.Attribute("style")??"").Split(';',StringSplitOptions.RemoveEmptyEntries)
        .Select(x=>x.Split(':',2)).Where(x=>x.Length==2).ToDictionary(x=>x[0].Trim(),x=>x[1].Trim());
    private void Draw(XElement e,DrawingContext dc)
    {
        string kind=e.Name.LocalName;if(kind is "defs" or "metadata" or "title")return;
        int pushed=0;var style=Style(e);
        double N(string key,double fallback=0)=>e.Attribute(key) is { } a?Number(a.Value):fallback;
        if(e.Attribute("transform") is { } transform)
            foreach(Match m in Regex.Matches(transform.Value,@"(translate|rotate)\(([^)]+)\)"))
            {
                var v=m.Groups[2].Value.Split(new[]{' ',','},StringSplitOptions.RemoveEmptyEntries).Select(Number).ToArray();
                dc.PushTransform(m.Groups[1].Value=="translate"?new TranslateTransform(v[0],v.Length>1?v[1]:0):new RotateTransform(v[0],v.Length>1?v[1]:0,v.Length>2?v[2]:0));pushed++;
            }
        if(style.TryGetValue("opacity",out var opacity)){dc.PushOpacity(Number(opacity));pushed++;}
        if(e.Attribute("clip-path") is { } clip&&definitions.TryGetValue(clip.Value.Replace("url(#","",StringComparison.Ordinal).TrimEnd(')'),out var target)&&target.Elements().FirstOrDefault() is { } rect)
        {dc.PushClip(new RectangleGeometry(new Rect(Number(rect.Attribute("x")!.Value),Number(rect.Attribute("y")!.Value),Number(rect.Attribute("width")!.Value),Number(rect.Attribute("height")!.Value))));pushed++;}
        var stroke=ActionFigurePalette.FromOriginal(style.GetValueOrDefault("stroke","none"));var pen=stroke is null?null:new Pen(stroke,Number(style.GetValueOrDefault("stroke-width","1")));
        if(pen is not null&&style.TryGetValue("stroke-dasharray",out var dash)&&dash!="none")pen.DashStyle=new DashStyle(dash.Split(',').Select(Number).Select(v=>v/pen.Thickness),0);
        var fill=ActionFigurePalette.FromOriginal(style.GetValueOrDefault("fill","currentColor"));
        switch(kind)
        {
            case "g":foreach(var child in e.Elements())Draw(child,dc);break;
            case "path":dc.DrawGeometry(fill,pen,paths[e]);break;
            case "use":
                string id=e.Attributes().Single(a=>a.Name.LocalName=="href").Value.TrimStart('#');
                if(definitions.TryGetValue(id,out var definition)&&paths.TryGetValue(definition,out var path)){dc.PushTransform(new TranslateTransform(N("x"),N("y")));dc.DrawGeometry(fill,pen,path);dc.Pop();}
                break;
            case "text":
                var typeface=new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,style.GetValueOrDefault("font-weight")=="700"?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal);
                var text=new FormattedText(e.Value,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,typeface,Number(style.GetValueOrDefault("font-size","12")),fill??ActionDrawingStyle.Ink,VisualTreeHelper.GetDpi(this).PixelsPerDip);
                double x=N("x");if(style.GetValueOrDefault("text-anchor")=="middle")x-=text.Width/2;else if(style.GetValueOrDefault("text-anchor")=="end")x-=text.Width;
                dc.DrawText(text,new(x,N("y")-text.Baseline));break;
        }
        while(pushed>0){dc.Pop();pushed--;}
    }
}
