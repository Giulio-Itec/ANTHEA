using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using X.Core;
namespace X.Desktop;
internal sealed class ElasticPileDrawing : FrameworkElement
{
    internal static double NiceStep(double value){if(!double.IsFinite(value)||value<=0)return 1;double decade=Math.Pow(10,Math.Floor(Math.Log10(value))),scaled=value/decade;return (scaled<=1?1:scaled<=2?2:scaled<=5?5:10)*decade;}
    JsonObject? result;JsonNode? selected;double retainedMomentMaximum;const double Profile=310;
    double Top=>result?["input"]?["zona_sismica"]!=null&&On("sisma")?112:65;
    internal JsonObject Options{get;set;}=new();
    internal event Action<JsonNode?>? Selected;
    bool On(string key)=>Options[key]==null?key!="molle":Options.B(key);
    internal void Set(JsonObject? value,bool preserveSelection=false){result=value;if(!preserveSelection){selected=null;retainedMomentMaximum=0;Selected?.Invoke(null);}InvalidateVisual();}
    internal void SelectDepth(double depth)
    {
        if(result==null)return;var points=result["risposta"]!.Array("Points");var nearest=points.OrderBy(p=>Math.Abs(p.D("Depth")-depth)).First();
        var pair=points.Where(p=>Math.Abs(p.D("Depth")-nearest.D("Depth"))<1e-9).ToArray();selected=pair.Length>1&&ReferenceEquals(selected,pair[0])?pair[1]:pair[0];Selected?.Invoke(selected);InvalidateVisual();
    }
    protected override void OnMouseDown(MouseButtonEventArgs e){base.OnMouseDown(e);if(result!=null)SelectDepth(Math.Clamp((e.GetPosition(this).Y-Top)/(ActualHeight-Top-35),0,1)*result["input"].D("lunghezza"));}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);dc.DrawRectangle(Brushes.White,null,new Rect(0,0,ActualWidth,ActualHeight));if(result==null)return;
        var input=result["input"]!;var response=result["risposta"]!;var rows=response.Array("Points");double length=input.D("lunghezza"),free=input.D("libero"),bottom=ActualHeight-35;
        double Y(double x)=>Top+(bottom-Top)*x/length;
        void Text(string s,double x,double y,int size=11,double width=240,Brush? brush=null)=>dc.DrawText(new FormattedText(s,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,brush??Ui.Navy,VisualTreeHelper.GetDpi(this).PixelsPerDip){MaxTextWidth=width},new Point(x,y));
        var seismic=On("sisma")?input["zona_sismica"]:null;var seismicBrush=Ui.Brush("#6D28D9");
        if(seismic!=null)
        {
            double zoneEnd=seismic.D("AdoptedEnd");
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(20,124,58,237)),null,new Rect(36,Y(0),Math.Max(1,ActualWidth-36),Y(zoneEnd)-Y(0)));
            dc.DrawRectangle(Ui.Brush("#F3E8FF"),null,new Rect(36,63,Math.Max(1,ActualWidth-44),26));
            string extra=seismic.D("NominalLength")>length?" · palo più corto: zona su tutta la lunghezza":Math.Abs(zoneEnd-seismic.D("MinimumEnd"))>1e-9?$" · zona assegnata 0–{zoneEnd:0.00} m":"";
            Text($"ZONA SISMICA DI TESTA · 10D = {seismic.D("NominalLength"):0.00} m{extra}"+(seismic.B("MeetsMinimum")?"":" · ESTENSIONE INSUFFICIENTE"),46,67,13,Math.Max(1,ActualWidth-70),seismicBrush);
        }
        Text("x dalla testa [m] ↓",0,30);double depth=free;
        foreach(var (layer,i) in input.Array("strati").Select((p,i)=>(p!,i)))
        {
            double end=Math.Min(length,depth+layer.D("spessore"));if(end<=depth)break;
            if(On("strati")){dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(50,(byte)(100+i*31%100),150,190)),null,new Rect(40,Y(depth),Profile-50,Y(end)-Y(depth)));if(Y(end)-Y(depth)>=18)Text($"{i+1} · {layer.S("nome")}",90,Y(depth)+3,11,200);}
            depth=end;
        }
        if(On("kh"))foreach(var p in result.Array("parametri_terreno"))
        {
            double z=p.D("Top")+free,end=Math.Min(length,p.D("Bottom")+free);if(z>=length||Y(end)-Y(z)<70)continue;
            var segment=rows.Where(r=>r.D("Layer")==p.D("Layer")&&r.D("Depth")>=z-1e-9&&r.D("Depth")<=end+1e-9).ToArray();
            // Segment endpoint kh comes from the solver, selecting the correct side of each discontinuity.
            var first=segment.FirstOrDefault(r=>Math.Abs(r.D("Depth")-z)<1e-9&&r.S("Side")=="Below")??segment.FirstOrDefault();
            var last=segment.LastOrDefault(r=>Math.Abs(r.D("Depth")-end)<1e-9&&r.S("Side")=="Above")??segment.LastOrDefault();
            if(first!=null&&last!=null)Text($"kh {first.D("Kh"):G4} → {last.D("Kh"):G4} kN/m³\n{ElasticHorizontalPile.ParameterLabel(p.S("Law"))}={p.D("AdoptedValue"):G4} {p.S("Unit")}\n{ElasticHorizontalPile.MethodLabel(p.S("Method"))}",90,Y(z)+22,10,200);
        }
        dc.DrawLine(new Pen(Ui.Navy,8),new Point(76,Y(0)),new Point(76,Y(length)));dc.DrawLine(new Pen(Brushes.SaddleBrown,1),new Point(36,Y(free)),new Point(ActualWidth,Y(free)));Text("p.c.",2,Y(free)+4);
        if(On("falda")&&input.B("falda")&&free+input.D("z_falda")<=length){double y=Y(free+input.D("z_falda"));dc.DrawLine(new Pen(Brushes.SteelBlue,1){DashStyle=DashStyles.Dash},new Point(35,y),new Point(ActualWidth,y));Text("falda",92,y-15);}
        foreach(var node in response.Array("Nodes"))
        {
            double y=Y(node.D("Depth"));if(On("mesh"))dc.DrawEllipse(Brushes.White,new Pen(Ui.Blue,.8),new Point(76,y),2.5,2.5);
            if(On("molle")&&node.D("EquivalentSpring")>0)dc.DrawLine(new Pen(Ui.Muted,.8),new Point(40,y),new Point(71,y));
        }
        if(On("carichi")){Text($"H={input.D("H"):G5} kN\nC testa={response.D("AppliedHeadMoment"):G5} kNm",90,0);Text("Testa: "+input.S("vincolo"),90,42);Text("Punta: "+input.S("punta"),70,bottom+10);}
        double depthStep=NiceStep(length/6);for(double x=0;x<=length;x+=depthStep){Text(x.ToString("G4"),0,Y(x)-5);dc.DrawLine(new Pen(Brushes.LightGray,.5),new Point(Profile,Y(x)),new Point(ActualWidth,Y(x)));}
        var segments=result["armature"]?.Array("tratti")??new JsonArray();
        var diagrams=new[]{("k","DistributedStiffness","k [kN/m²]"),("y","Displacement","y [m]"),("theta","Rotation","θ [rad]"),("q","SoilReaction","q [kN/m]"),("N","AxialForce","N [kN]"),("V","Shear","V [kN]"),("M","Moment","M [kNm]")}.Where(p=>On(p.Item1)).ToArray();double w=(ActualWidth-Profile)/Math.Max(1,diagrams.Length+(On("tratti")?1:0));
        for(int j=0;j<diagrams.Length;j++)
        {
            var (key,property,label)=diagrams[j];double center=Profile+w*(j+.5),max=rows.Max(p=>Math.Abs(p.D(property)));
            if(key=="M")
            {
                foreach(var check in segments.SelectMany(s=>s!.Array("verifiche")))max=Math.Max(max,Math.Max(Math.Abs(check.D("MRdPositive")),Math.Abs(check.D("MRdNegative"))));
                if(result["armature"]==null)max=Math.Max(max,retainedMomentMaximum);else retainedMomentMaximum=max;
            }
            if(max==0)max=1;double step=NiceStep(max/2);max=Math.Ceiling(max/step)*step;Text(label,center-w/2+7,5,12,w-10);
            for(double tick=-max;tick<=max+step*.01;tick+=step){double tx=center+tick/max*w*.4;dc.DrawLine(new Pen(tick==0?Brushes.Gray:Brushes.LightGray,tick==0?1:.5),new Point(tx,Top),new Point(tx,bottom));Text(tick.ToString(step>=1?"0":"0."+new string('#',Math.Min(12,(int)Math.Ceiling(-Math.Log10(step))))),tx-18,Top-17,9,45);}
            Point? previous=null;double? previousX=null;foreach(var row in rows){double x=row.D("Depth");var point=new Point(center+row.D(property)/max*w*.4,Y(x));if(previous.HasValue){var pen=new Pen(key=="M"?Brushes.DarkRed:Ui.Blue,1.5);if(previousX==x)pen.DashStyle=DashStyles.Dot;dc.DrawLine(pen,previous.Value,point);}previous=point;previousX=x;}
            if(key=="M")
            {
                Text("MRd± ⋯ · dettagli prelim.",center-w/2+7,24,9,w-10);
                foreach(var segment in segments)
                {
                    var detail=segment?["dettaglio"];if(detail!=null)
                    {
                        double a=segment.D("inizio"),b=segment.D("fine");
                        foreach(var zone in new[]{(a,Math.Min(b,detail.D("CapacityStart"))),(Math.Max(a,detail.D("CapacityEnd")),b)})if(zone.Item2>zone.Item1)dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(30,220,100,70)),null,new Rect(center-w/2+3,Y(zone.Item1),w-6,Y(zone.Item2)-Y(zone.Item1)));
                    }
                    foreach(string branch in new[]{"UsableMRdPositive","UsableMRdNegative"}){Point? last=null;foreach(var check in segment!.Array("verifiche")){if(J.Number(check![branch]) is not double mr){last=null;continue;}double z=check["Action"].D("Depth");var p=new Point(center+mr/max*w*.4,Y(z));if(last.HasValue)dc.DrawLine(new Pen(Brushes.SeaGreen,1.2){DashStyle=DashStyles.Dash},last.Value,p);last=p;}}
                }
            }
        }
        if(On("tratti")){double left=Profile+w*diagrams.Length;Text("Tratti di verifica",left+8,5,12,w-12);foreach(var (s,i) in segments.Select((s,i)=>(s!,i))){double a=Y(s.D("inizio")),b=Y(s.D("fine"));if(b<=a)continue;var brush=PileReinforcementEditor.SegmentColor(i);dc.DrawRectangle(brush,new Pen(Ui.Muted,.8),new Rect(left+8,a,w-16,b-a));if(b-a>32)Text(s.S("id")+" · "+(s.B("collegato")?"principale":"personalizzata")+"\n"+s.S("stato"),left+12,a+5,10,w-24);dc.DrawLine(new Pen(Ui.Muted,.5){DashStyle=DashStyles.Dash},new Point(Profile,a),new Point(ActualWidth,a));}}
        if(seismic!=null)
        {
            double minimumY=Y(seismic.D("MinimumEnd")),adoptedY=Y(seismic.D("AdoptedEnd"));
            dc.DrawRectangle(null,new Pen(seismicBrush,2.5),new Rect(61,Y(0),30,adoptedY-Y(0)));
            void Marker(double y,string label,DashStyle style)
            {
                dc.DrawLine(new Pen(seismicBrush,2){DashStyle=style},new Point(36,y),new Point(ActualWidth,y));
                double left=Math.Max(Profile+8,ActualWidth-310);dc.DrawRectangle(Brushes.White,new Pen(seismicBrush,.7),new Rect(left,y-20,295,20));Text(label,left+5,y-18,11,285,seismicBrush);
            }
            Marker(minimumY,$"Limite minimo 10D · x={seismic.D("MinimumEnd"):0.00} m"+(seismic.D("NominalLength")>length?" (punta)":""),DashStyles.Dash);
            if(Math.Abs(seismic.D("AdoptedEnd")-seismic.D("MinimumEnd"))>1e-9)
            {
                // Nearby limits share one caption, while both exact horizontal lines remain visible.
                if(Math.Abs(adoptedY-minimumY)<25){dc.DrawLine(new Pen(seismicBrush,1.5){DashStyle=DashStyles.Dot},new Point(36,adoptedY),new Point(ActualWidth,adoptedY));Text($"Zona assegnata: x={seismic.D("AdoptedEnd"):0.00} m",Profile+10,minimumY+4,11,280,seismicBrush);}
                else Marker(adoptedY,$"Fine zona assegnata · x={seismic.D("AdoptedEnd"):0.00} m",DashStyles.Dot);
            }
        }
        if(selected!=null)
        {
            double y=Y(selected.D("Depth"));dc.DrawLine(new Pen(Brushes.DarkOrange,1.5){DashStyle=DashStyles.Dash},new Point(35,y),new Point(ActualWidth,y));
            var node=response.Array("Nodes").OrderBy(n=>Math.Abs(n.D("Depth")-selected.D("Depth"))).First();
            ToolTip=$"x={selected.D("Depth"):G6} m · {selected.S("Side")}\nNodo più vicino: x={node.D("Depth"):G5} m; K*={node.D("EquivalentSpring"):G6} kN/m\nTratto tributario {node.D("TributaryTop"):G5}–{node.D("TributaryBottom"):G5} m.\nRipetere il clic all’interfaccia per leggere l’altro lato.";
            if(On("molle"))Text($"Nodo x={node.D("Depth"):G4} m\nK*={node.D("EquivalentSpring"):G5} kN/m",90,Math.Min(bottom-35,y+4),10,200);
        }
    }
}
