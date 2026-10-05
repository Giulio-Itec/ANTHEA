using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using Anthea.Calculations;

static partial class Program
{
    static int DrawingChecks(string directory)
    {
        var app=new TestApp();app.LoadStyles();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        var type=typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.PileReinforcementDrawing")!;
        var drawing=(FrameworkElement)Activator.CreateInstance(type,true)!;
        var set=type.GetMethod("Set",flags)!;var png=type.BaseType!.GetMethod("Png",flags)!;
        void Render(string name,JsonObject result)
        {
            string before=result.ToJsonString();set.Invoke(drawing,[result,null]);
            var pieces=((System.Collections.IEnumerable)type.GetProperty("Pieces",flags)!.GetValue(drawing)!).Cast<object>().ToArray();
            var enginePieces=result["armature"]!.Array("distinta").SelectMany(r=>r!.Array("Pieces")).ToArray();
            Check(pieces.Length==enginePieces.Length,name+": one drawn mark per actual cutting piece, no duplicate continuous groups");
            Check(pieces.Select(p=>p.GetType().GetProperty("Mark")!.GetValue(p)).Distinct().Count()==pieces.Length,name+": unique marks link elevation to schedule");
            Check(pieces.Select(p=>(JsonNode)p.GetType().GetProperty("Bar")!.GetValue(p)!).Zip(enginePieces).All(pair=>ReferenceEquals(pair.First,pair.Second)),name+": dimensions quantities and joints use native result objects");
            var size=(Size)type.GetProperty("SheetSize",flags)!.GetValue(drawing)!;
            Check(size.Width>=1140&&size.Height>900,name+": complete sheet reserves room for sections and cutting table");
            File.WriteAllBytes(Path.Combine(directory,name+".png"),(byte[])png.Invoke(drawing,[(int)size.Width,(int)size.Height])!);
            File.WriteAllBytes(Path.Combine(directory,name+"-reduced.png"),(byte[])png.Invoke(drawing,[(int)(size.Width*.65),(int)(size.Height*.65)])!);
            Check(result.ToJsonString()==before,name+": resizing and drawing leave calculations unchanged");
            File.WriteAllText(Path.Combine(directory,name+".json"),result.ToJsonString(J.Options));
        }
        var root=Input();root["elastico"]!["dettagli"]!["lunghezza_barra"]=6;
        var single=ElasticHorizontalPile.CalculateShared(root);
        Check(single["armature"]!.Array("distinta").SelectMany(r=>r!.Array("Pieces")).Any(p=>p.D("LapWithPrevious")>0),"reference sheet includes a real splice between stock bars");
        Render("tavola-palo-continuo",single);
        root["elastico"]!["tratti"]=new JsonArray(ElasticHorizontalPile.NewSegment("T1",3),ElasticHorizontalPile.NewSegment("T2",6),ElasticHorizontalPile.NewSegment("T3",9),ElasticHorizontalPile.NewSegment("T4",null));
        root["elastico"]!["tratti"]![2]!["collegato"]=false;root["elastico"]!["tratti"]![2]!["longitudinal_bar_count"]=8;
        root["elastico"]!["tratti"]![3]!["collegato"]=false;root["elastico"]!["tratti"]![3]!["longitudinal_bar_count"]=8;
        root["elastico"]!["dettagli"]!["lunghezza_barra"]=12;
        var four=ElasticHorizontalPile.CalculateShared(root);
        Check(four["armature"]!.Array("distinta").Select(r=>r.D("Count")).SequenceEqual(new[]{16d,16,8,8}),"four zones remain four user-defined reinforcement groups");
        Check(four["armature"]!.Array("giunti").Count==3&&four["armature"]!.Array("distinta").Select(r=>r.D("End")).SequenceEqual(new[]{3d,6,9,12}),"every user boundary stays a cutting end with an incoming lap");
        Render("tavola-quattro-zone",four);
        root["elastico"]!["tratti"]![2]!["longitudinal_bar_diameter_mm"]=20;root["elastico"]!["tratti"]![3]!["longitudinal_bar_diameter_mm"]=20;
        var diameterChange=ElasticHorizontalPile.CalculateShared(root);
        Check(diameterChange["armature"]!.Array("giunti").Count==3&&diameterChange["armature"]!.Array("giunti")[1].D("Count")==8,"diameter change retains all three cutting-boundary joints");
        Check(diameterChange["armature"]!.Array("giunti").All(j=>Math.Abs(j.D("ActualLength")-j.D("AdoptedLength"))<1e-9),"drawn change joint has exactly the adopted length");
        Render("tavola-cambio-diametro",diameterChange);
        root["elastico"]!["tratti"]![2]!["longitudinal_bar_diameter_mm"]=24;root["elastico"]!["tratti"]![3]!["longitudinal_bar_diameter_mm"]=24;
        root["elastico"]!["dettagli"]!["ancoraggio_testa"]=2;root["elastico"]!["dettagli"]!["ancoraggio_punta"]=2;
        var anchored=ElasticHorizontalPile.CalculateShared(root);Render("tavola-quote-conservate",anchored);
        Check(anchored["armature"]!.Array("distinta").All(r=>r.D("Start")>=0&&r.D("End")<=12),"legacy exterior allowances do not add unrequested cutting length");
        File.WriteAllText(Path.Combine(directory,"drawing-checks.txt"),$"PASS {checks} checks");return 0;
    }
}
