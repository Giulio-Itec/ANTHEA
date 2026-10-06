using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Calculations;
using GPC.Checkers.Geotechnics.Piles;
using X.Core;

internal static class Program
{
    static int checks;
    static void Check(bool valid, string name) { if (!valid) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
    static void Equal(double? actual, double expected, string name, double tolerance = 1e-10) => Check(actual.HasValue && Math.Abs(actual.Value - expected) < tolerance, name + $": {actual} != {expected}");
    static LateralGroupInput Grid(int nx = 3, int ny = 3, double sx = 3, double sy = 3) => HorizontalPileGroup.Read(J.Obj(("versione", 1), ("diametro", 1), ("angolo", 0), ("metodo", 4), ("estrapolazioni", false), ("file_proiettate", false), ("pali", HorizontalPileGroup.Grid(nx, ny, sx, sy))));
    static LateralGroupResult Calc(LateralGroupInput input, LateralGroupMethod method) => LateralPileGroup.Calculate(input, method);
    [STAThread] static int Main(string[] args)
    {
        string folder = Path.GetFullPath(args.Length > 0 ? args[0] : "supporto/artefatti/efficienza-orizzontale"); Directory.CreateDirectory(folder);
        try
        {
            var g = Grid();
            Equal(Calc(g, LateralGroupMethod.Davisson).Factor, .25, "Davisson 3D");
            foreach (var (x, y) in new[] { (4d, .4), (5d, .55), (6d, .7), (7d, .85), (8d, 1d), (12d, 1d) }) Equal(Calc(Grid(sx: x), LateralGroupMethod.Davisson).Factor, y, "Davisson table");
            Equal(Calc(Grid(sx: 4.5), LateralGroupMethod.Davisson).Factor, .475, "Davisson interpolation");
            Check(!Calc(Grid(sx: 2), LateralGroupMethod.Davisson).Available, "Davisson no silent extrapolation");
            Check(!Calc(Grid(sy: 2), LateralGroupMethod.Davisson).Available, "Davisson transverse applicability");
            Equal(Calc(g, LateralGroupMethod.Aashto).Factor, .5, "AASHTO 3D independent mean");
            Equal(Calc(Grid(sx: 4), LateralGroupMethod.Aashto).Factor, .675, "AASHTO interpolation");
            Equal(Calc(Grid(sx: 5), LateralGroupMethod.Aashto).Factor, .85, "AASHTO 5D");
            foreach (var (x, y) in new[] { (3d, 1.55 / 3), (4d, 2d / 3), (5d, .85), (6d, 1d), (9d, 1d) }) Equal(Calc(Grid(sx: x), LateralGroupMethod.Fhwa).Factor, y, "FHWA table");
            Check(!Calc(g, LateralGroupMethod.Rollins).Available, "Rollins experimental range");
            g.AllowExtrapolation = true;
            var rollins = Calc(g, LateralGroupMethod.Rollins);
            Equal(rollins.Piles.First(p => p.Row == 1).Factor, .7856391950537085, "Rollins lead numeric");
            Equal(rollins.Piles.First(p => p.Row == 2).Factor, .5712783901074171, "Rollins second numeric");
            Equal(rollins.Piles.First(p => p.Row == 3).Factor, .4091673732008658, "Rollins third numeric");
            Check(rollins.Warnings.Any(w => w.Contains("Estrapolazione")), "Rollins warning");
            var pair = Grid(2, 1); var pairResult = Calc(pair, LateralGroupMethod.ReeseVanImpe);
            Equal(pairResult.Piles.Single(p => p.Row == 1).Factor, .7 * Math.Pow(3, .26), "Leading pair");
            Equal(pairResult.Piles.Single(p => p.Row == 2).Factor, .48 * Math.Pow(3, .38), "Trailing pair");
            var side = Calc(Grid(1, 2), LateralGroupMethod.ReeseVanImpe); Equal(side.Factor, .64 * Math.Pow(3, .34), "Side by side");
            foreach (var (spacing, lead, trail) in new[] { (4d, 1d, .48 * Math.Pow(4, .38)), (7d, 1d, 1d) }) { var r = Calc(Grid(2, 1, spacing), LateralGroupMethod.ReeseVanImpe); Equal(r.Piles.Single(p => p.Row == 1).Factor, lead, "Leading threshold"); Equal(r.Piles.Single(p => p.Row == 2).Factor, trail, "Trailing threshold"); }
            Equal(Calc(Grid(1, 2, sy: 3.75), LateralGroupMethod.ReeseVanImpe).Factor, 1, "Side threshold");
            var reese = Calc(g, LateralGroupMethod.ReeseVanImpe); var caltrans = Calc(g, LateralGroupMethod.Caltrans);
            foreach (var p in caltrans.Piles) Equal(p.Factor, reese.Piles.Single(r => r.Id == p.Id).Factor * (p.Row == 1 ? .9 : p.Row == 2 ? 1 : .8), "Caltrans independent alpha at 3D");
            var four = Calc(Grid(4, 2, 4), LateralGroupMethod.Caltrans); Equal(four.Piles.First(p => p.Row == 4).Alpha, .85, "Caltrans fourth row interpolation");
            var irregular = Grid(); irregular.Piles = irregular.Piles.Take(8).ToArray(); Check(!Calc(irregular, LateralGroupMethod.Aashto).Available, "Irregular row blocked"); Check(Calc(irregular, LateralGroupMethod.ReeseVanImpe).Available, "Irregular pair available");
            irregular.AcceptProjectedRows = true; irregular.RepresentativeParallelSpacing = 3; irregular.RepresentativeTransverseSpacing = 3; Check(Calc(irregular, LateralGroupMethod.Davisson).Available, "Explicit extension");
            var reverse = Grid(); reverse.LoadAngleDegrees = 180;
            var reversed = Calc(reverse, LateralGroupMethod.ReeseVanImpe); Equal(reversed.Factor, reese.Factor!.Value, "Reverse symmetric group"); Equal(reversed.Piles[0].Factor, reese.Piles[2].Factor, "Reverse leading trailing");
            var rotate = Grid(); double a = .37; rotate.Piles = rotate.Piles.Select(p => new GroupPile { Id = p.Id, X = p.X * Math.Cos(a) - p.Y * Math.Sin(a) + 10000, Y = p.X * Math.Sin(a) + p.Y * Math.Cos(a) - 8000 }).ToArray(); rotate.LoadAngleDegrees = a * 180 / Math.PI;
            foreach (var m in new[] { LateralGroupMethod.Davisson, LateralGroupMethod.Aashto, LateralGroupMethod.Fhwa, LateralGroupMethod.ReeseVanImpe, LateralGroupMethod.Caltrans }) Equal(Calc(rotate, m).Factor, Calc(Grid(), m).Factor!.Value, "Rigid rotation and translation " + m, 1e-9);
            foreach (var m in Enum.GetValues<LateralGroupMethod>()) Equal(Calc(Grid(1, 1), m).Factor, 1, "Isolated pile " + m);
            var invalid = Grid(2, 1, .5); try { Calc(invalid, LateralGroupMethod.ReeseVanImpe); throw new Exception("Overlap accepted"); } catch (ArgumentException) { checks++; }
            invalid = Grid(); invalid.Diameter = double.NaN; try { Calc(invalid, LateralGroupMethod.Davisson); throw new Exception("NaN accepted"); } catch (ArgumentException) { checks++; }
            var data = HorizontalPileGroup.Defaults(); string original = data.ToJsonString(); var result = CalculationService.Calculate(HorizontalPileGroup.Module, data); Check(data.ToJsonString() == original, "Adapter does not mutate input"); Check(result.Array("confronto").Count == 12, "Six methods in X and Y");
            var document = Archivio.Documento(HorizontalPileGroup.Module); string archive = Path.Combine(folder, "palificata.anthea"); Archivio.Scrivi(archive, document); Check(JsonNode.DeepEquals(document, Archivio.Leggi(archive)), "Archive roundtrip");
            foreach(var (kind,expected) in new[]{(PileLayoutKind.Rectangular,9),(PileLayoutKind.Staggered,8),(PileLayoutKind.Triangular,6),(PileLayoutKind.Pentagonal,6),(PileLayoutKind.Hexagonal,7)})
            {
                var layout=PileGroupLayout.Generate(new PileLayoutOptions{Kind=kind});Check(layout.Count==expected,"layout count "+kind);Equal(layout.Average(p=>p.X),0,"center x "+kind);Equal(layout.Average(p=>p.Y),0,"center y "+kind);
                var cap=PileGroupLayout.CapOutline(layout,1,1,false);Check(cap.Count>=3,"cap outline "+kind);
            }
            var rectangle=PileGroupLayout.Generate(new PileLayoutOptions{Diameter=2});var boundary=PileGroupLayout.CapOutline(rectangle,2,1,true);Equal(boundary.Max(p=>p.X)-rectangle.Max(p=>p.X),2,"axis-edge margin based on D");
            var custom=HorizontalPileGroup.Defaults();custom["direzioni"]!["custom"]=true;Check(HorizontalPileGroup.Calculate(custom).Array("confronto").Count==18,"custom direction adds six results");
            custom["direzioni"]!["X-"]=true;custom["direzioni"]!["Y-"]=true;var negative=HorizontalPileGroup.Calculate(custom);Check(negative.Array("confronto").Count==30,"five directions thirty method results");
            var negativeDirections=HorizontalPileGroup.Directions(custom);Check(negativeDirections.Single(d=>d.Id=="X-").Angle==180&&negativeDirections.Single(d=>d.Id=="Y-").Angle==270,"negative direction angles");
            foreach(var (positiveId,negativeId) in new[]{("X","X-"),("Y","Y-")}){var positiveRow=negative.Array("confronto").Single(r=>r.S("DirectionId")==positiveId&&r.D("Method")==4)!;var negativeRow=negative.Array("confronto").Single(r=>r.S("DirectionId")==negativeId&&r.D("Method")==4)!;Equal(negativeRow.D("Factor"),positiveRow.D("Factor"),"symmetric reversed direction mean");Check(positiveRow.Array("Piles").First().D("Row")!=negativeRow.Array("Piles").First().D("Row"),"reverse direction changes leading row");}
            HorizontalPileGroup.Select(negative,4,"Y-");Check(negative["selezionato"].D("Angle")==270,"negative result selected");
            custom["generatore"]!["tipo"]="Generica";var originalPiles=custom["pali"]!.ToJsonString();HorizontalPileGroup.Regenerate(custom);Check(custom["pali"]!.ToJsonString()==originalPiles,"generic coordinates preserved");
            File.WriteAllText(Path.Combine(folder, "risultati.json"), result.ToJsonString(J.Options));
            File.WriteAllBytes(Path.Combine(folder, "esempio-palificata.docx"), ReportPileGroup.Create("Palificata 3 × 3 a 3D", result));
            var app = new TestApp(); app.LoadStyles(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var type = typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.HorizontalPileGroupWorkspace")!;
            foreach (int selected in new[] { 4 })
            {
                Console.WriteLine("UI begin " + selected); var uiData = HorizontalPileGroup.Defaults(); uiData["metodo"] = selected;
                var view = (FrameworkElement)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { uiData }, null)!;
                Console.WriteLine("UI constructed"); view.Measure(new Size(1400, 1050)); view.Arrange(new Rect(0, 0, 1400, 1050)); view.UpdateLayout();
                var bitmap = new RenderTargetBitmap(1400, 1050, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(Path.Combine(folder, "ui-" + selected + ".png"))) encoder.Save(file);
                var uiResult = (JsonObject?)type.GetProperty("Result", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view); Check(uiResult is not null, "UI calculated");
                var cmp=(DataGrid)type.GetField("comparison",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(view)!;cmp.SelectedIndex=6;var selectedResult=(JsonObject)type.GetProperty("Result",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(view)!;Check(selectedResult["selezionato"].S("DirectionId")=="Y"&&selectedResult["selezionato"].D("Method")==0,"selection drives displayed method and direction");
                view.UpdateLayout(); Console.WriteLine("selected layout updated"); Console.WriteLine("dispose begin"); ((IDisposable)view).Dispose(); Console.WriteLine("dispose end");
            }
            File.WriteAllText(Path.Combine(folder, "checks.txt"), $"PASS: {checks} checks; reference values, symmetry, rotation, limits, archive, report, WPF offscreen.");
            Console.WriteLine($"PASS: {checks} checks"); return 0;
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(folder, "failure.txt"), ex.ToString()); Console.Error.WriteLine(ex); return 1; }
    }
}

// The extracted App.xaml dictionary needs the root namespaces: x, and local (with its assembly) for the Appearance triggers added in d7ca52a.
internal sealed class TestApp : Application { internal void LoadStyles() { var document=System.Xml.Linq.XDocument.Load("X.Desktop/App.xaml"); var dictionary=document.Root!.Elements().Single().Elements().Single(); dictionary.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns+"x","http://schemas.microsoft.com/winfx/2006/xaml"); dictionary.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns+"local","clr-namespace:X.Desktop;assembly=ANTHEA"); foreach(var source in dictionary.Descendants().SelectMany(e=>e.Attributes("Source"))) source.Value="/ANTHEA;component/"+source.Value; Resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Parse(dictionary.ToString()); } }
