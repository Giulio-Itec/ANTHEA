using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void OverlayDetailRenderingChecks(ModelViewerControl control, Viewport3DX viewport, string output)
    {
        static string[] Labels(Viewport3DX view) => view.Items.OfType<BillboardTextModel3D>()
            .SelectMany(b => (b.Geometry as BillboardText3D)?.TextInfo ?? []).Select(t => t.Text).ToArray();
        static LineGeometryModel3D? Directions(Viewport3DX view) => view.Items.OfType<LineGeometryModel3D>()
            .SingleOrDefault(l => l.Color == Color.FromRgb(103,225,227));
        var sample = Sample.Model();
        sample.Boundaries = [new("CONS/1", "RESTRAIN", [1], null, "DX, RZ"),
            new("RIGD/2/1", "CONSTRAINT", [2,3], null, "DX, DY"), new("RIGD/2/2", "CONSTRAINT", [2,5], null, "DX, DY"),
            new("FRLS/12/1", "RELEASE", [4], 12, "DX: Continuous, RZ: Released")];
        using(var small = new ModelViewerControl(sample, [], false, _ => {}, (_,_,_,_) => {}))
        {
            var host = new Window { Content = small, Width = 1500, Height = 950, ShowActivated = false, ShowInTaskbar = false };
            host.Show(); Wait(350);
            try
            {
                var vm = (ModelViewerViewModel)small.DataContext; var scene = Children<Viewport3DX>(small).Single();
                vm.ShowInspector = false; vm.ShowResults = false; vm.SelectedPanel = 0;
                vm.CameraPresetCommand.Execute("top"); Wait(350);
                vm.ShowRestraints = vm.ShowConstraints = vm.ShowReleases = vm.ShowBoundaryLabels = true;
                vm.ShowElementDirections = true; vm.AvoidLabelOverlap = false; Wait(120);
                Check(Labels(scene).Any(t => t.Contains("RESTRAIN - N 1") && t.Contains("DX, RZ")), "partial support label preserves only the imported DOFs");
                Check(Labels(scene).Count(t => t.Contains("CONSTRAINT - N 2")) == 1 && Labels(scene).Any(t => t.Contains("2 collegamenti")), "rigid links with the same master and DOFs share one explicitly counted label");
                Check(Labels(scene).Any(t => t.Contains("DX: Continuous") && t.Contains("RZ: Released")), "release labels retain individual imported states without converting springs or continuity to hinges");
                var arrows = Directions(scene)!; var positions = arrows.Geometry!.Positions!;
                Check(!arrows.IsHitTestVisible && positions.Count == sample.Elements.Length * 6, "one non-pickable connectivity arrow is drawn per visible element");
                Check(positions[1].X > positions[0].X && Math.Abs(positions[1].Y - positions[0].Y) < 1e-6 && positions[13].Y > positions[12].Y, "plate N1-to-N2 and beam I-to-J arrows follow imported connectivity");
                vm.SelectElement(12); vm.OverlaysOnlySelection = true;
                Check(Directions(scene)!.Geometry!.Positions!.Count == 6 && !Labels(scene).Any(t => t.Contains("CONSTRAINT")), "selection scope excludes unrelated arrows and boundary labels");
                vm.InspectBoundariesCommand.Execute(null);
                Check(vm.TableRows!.Count == 2 && vm.TableCsv().Contains("DX: Continuous"), "the boundary table retains full release information within the selected scope");
                vm.ShowTable = false; vm.OverlaysOnlySelection = false; vm.ShowBoundaryLabels = false;
                var support = scene.Items.OfType<LineGeometryModel3D>().Single(l => l.Color == Color.FromRgb(111,206,157) && l.Geometry?.Positions?.Count > 0);
                float original = Vector3.Distance(support.Geometry!.Positions![0], support.Geometry.Positions[1]); vm.OverlayScale = 2;
                support = scene.Items.OfType<LineGeometryModel3D>().Single(l => l.Color == Color.FromRgb(111,206,157) && l.Geometry?.Positions?.Count > 0);
                Check(Math.Abs(Vector3.Distance(support.Geometry!.Positions![0], support.Geometry.Positions[1]) / original - 2) < 1e-5, "symbol scale doubles support geometry without changing model coordinates");
                vm.DisableOverlaysCommand.Execute(null);
                Check(Directions(scene) == null && Labels(scene).Length == 0, "master switch removes every added symbol and label");
            }
            finally {host.Close();}
        }
        var state = (ModelViewerViewModel)control.DataContext; string fingerprint = state.Snapshot!.Fingerprint();
        state.ShowResults = false; state.SelectedPanel = 0; state.ShowInspector = false; state.ShowMesh = false;
        state.ShowRestraints = state.ShowConstraints = state.ShowBoundaryLabels = true;
        state.ShowElementDirections = true; state.OverlayScale = .7;
        Wait(150);
        var expander = Children<Expander>(control).Single(e => e.Header?.ToString() == "Assi e condizioni al contorno");
        expander.IsExpanded = true; expander.BringIntoView(); SetAppearance("VeryDark"); Wait(200);
        var tools = (ScrollViewer)control.FindName("ModelOverlaysScroll"); tools.ScrollToBottom(); Wait(100);
        Check(((TreeView)control.FindName("ModelTree")).ActualHeight > 100 && tools.ScrollableHeight > 0, "expanded overlay controls scroll while the model tree keeps its viewport");
        Check(Labels(viewport).Length is >0 and <=40, "real rigid-link model keeps boundary labels bounded");
        Capture(control, viewport, Path.Combine(output, "spalla-vincoli-versi.png"));
        state.InspectBoundariesCommand.Execute(null); Wait(100);
        Check(state.TableRows!.Count == state.Snapshot.Boundaries!.Length, "all imported boundary records remain available in the real-model table");
        Capture(control, viewport, Path.Combine(output, "spalla-vincoli-tabella.png"));
        state.ShowTable = false; state.ShowResults = true; state.SelectedCase = state.Cases.First(c => state.Snapshot.Results.Any(r => r.Name == c && r.Displacements?.Length > 0));
        state.ShowDeformation = true;
        var renderer = (ModelSceneRenderer)control.GetType().GetField("renderer", Flags)!.GetValue(control)!;
        Check(renderer.PrepareAnimation(ModelViewerControl.Options(state)), "deformation animation accepts connectivity arrows and suspends DOF labels");
        Check(Labels(viewport).Length == 0, "animation does not leave static boundary labels at the wrong coordinates");
        renderer.RenderAnimationFrame(.25); state.DisableOverlaysCommand.Execute(null);
        Check(state.Snapshot.Fingerprint() == fingerprint, "overlay details leave the imported engineering model unchanged");
    }
}
