using System.IO;
using System.Numerics;
using System.Windows;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void DeformationRenderingChecks(string output)
    {
        var sample = Sample.Model();
        var displacement = sample.Nodes.Select(n => new ModelDisplacement(n.Id, n.Id == 5 ? .003 : 0, n.Id == 5 ? .004 : 0, n.Id == 5 ? .012 : 0)).ToArray();
        sample.Results = [sample.Results[0] with { Name = "A", Displacements = displacement },
            new("Traslazione", false, []) { Displacements = sample.Nodes.Select(n => new ModelDisplacement(n.Id, 0, 0, .1)).ToArray() },
            new("Parziale", false, []) { Displacements = displacement.Where(d => d.Node != 5).ToArray() },
            new("Inviluppo(max)", true, []) { Displacements = displacement }, new("Assente", false, [])];
        string fingerprint = sample.Fingerprint();
        using var control = new ModelViewerControl(sample, [], false, _ => { }, (_, _, _, _) => { });
        var host = new Window { Title = "ANTHEA · prova deformate", Content = control, Width = 1500, Height = 950, ShowInTaskbar = false, ShowActivated = false };
        host.Show(); Wait(400);
        try
        {
            var state = (ModelViewerViewModel)control.DataContext;
            var viewport = Children<Viewport3DX>(control).Single();
            var renderer = (ModelSceneRenderer)control.GetType().GetField("renderer", Flags)!.GetValue(control)!;
            state.ShowInspector = false; state.SelectedResultType = 1; state.SelectedComponent = "|u|";
            Check(state.HasResults && state.IsDisplacementResult && state.Family == "*" && state.Axes == "GLOBAL", "nodal displacement field includes every element family in global axes");
            Check(Math.Abs(state.ActiveValues.Single(v => v.Node == 5).Value - 13) < 1e-12 && state.ResultUnit == "|u| [mm]", "independent 3-4-12 displacement vector has graphical magnitude 13 mm");
            Check(!state.CanAverageContours && !state.IsBeamFamily && !state.LinkCommand.CanExecute(null), "nodal fields disable force averaging, beam diagrams and force links");
            state.ShowTable = true; state.SelectedTableKind = 5;
            Check(state.TableRows!.Count == 6 && !state.TableRows.Table!.Columns.Contains("Elemento"), "nodal result table lists shared nodes once");
            state.SelectNodes([5]); state.TableOnlySelected = true;
            Check(state.TableRows.Count == 1 && (int)state.TableRows[0]["Nodo"] == 5, "nodal table selection filter follows the selected node");
            state.SelectNodes([1]);
            Check(state.TableRows.Count == 1 && (int)state.TableRows[0]["Nodo"] == 1, "changing only the selected node invalidates the table cache");
            state.TableOnlySelected = false; state.ShowTable = false; state.ClearSelectionCommand.Execute(null);
            state.ShowDeformation = true; state.SelectedDeformationScale = 2; state.DeformationScale = 100;
            Check(Vector3.Distance(renderer.PointForNode(5), new(2.3f, -.6f, 1.2f)) < 1e-6, "deformed picking coordinates use imported translation and manual factor");
            Check(renderer.SelectionShape(sample.Elements[1]).Contains(renderer.PointForNode(5)), "rectangle selection follows the deformed geometry");
            Check(viewport.Items.OfType<MeshGeometryModel3D>().Any(m => m.Material is ColorStripeMaterial && m.Geometry!.Positions!.Any(p => Math.Abs(p.Z - 1.2) < 1e-6)), "nodal contour is drawn on the displaced plate mesh");
            Check(viewport.Items.OfType<LineGeometryModel3D>().Any(l => l.Color.A < 255 && l.Geometry!.Positions!.Count > 0 && !l.IsHitTestVisible), "undeformed reference is transparent and cannot intercept picking");
            var reference = viewport.Items.OfType<LineGeometryModel3D>().Single(l => l.Color.A < 255 && l.Color.R == 183);
            int outlineCount = reference.Geometry!.Positions!.Count;
            state.ShowUndeformedMesh = true;
            Check(viewport.Items.OfType<LineGeometryModel3D>().Single(l => l.Color.A < 255 && l.Color.R == 183).Geometry!.Positions!.Count > outlineCount, "undeformed mesh is optional above the uncluttered reference outline");
            state.ShowUndeformedMesh = false;
            double originalScalar = state.ActiveValues.Single(v => v.Node == 5).Value;
            state.SelectedDeformationScale = 0;
            Check(Math.Abs(state.EffectiveDeformationScale - .32 / .013) < 1e-10, "automatic factor makes maximum motion eight percent of original extent");
            state.SelectedDeformationScale = 1;
            Check(state.EffectiveDeformationScale == 1 && Math.Abs(renderer.PointForNode(5).Z - .012) < 1e-8, "real displacement uses factor one");
            Check(state.ActiveValues.Single(v => v.Node == 5).Value == originalScalar, "shape magnification never scales the displayed result value");
            state.SelectedComponent = "uy"; state.DisplacementUnit = "cm";
            Check(Math.Abs(state.ActiveValues.Single(v => v.Node == 5).Value - .4) < 1e-12, "signed component changes units independently of force units");
            state.SelectedColorScale = 3; state.ColorScaleLower = "-1"; state.ColorScaleUpper = "1"; state.ApplyColorScaleCommand.Execute(null);
            state.DisplacementUnit = "mm";
            Check(state.ColorMinimum == -10 && state.ColorMaximum == 10, "manual displacement scale follows unit changes without changing physical limits");
            state.ShowUndeformed = false;
            Check(!viewport.Items.OfType<LineGeometryModel3D>().Any(l => l.Color.A < 255 && l.Color.R == 183), "reference geometry can be completely disabled");
            state.SelectedCase = "Traslazione"; state.SelectedComponent = "uz"; state.SelectedRepresentation = 1;
            state.SelectedDeformationScale = 2; state.DeformationScale = 2; state.ShowUndeformed = true;
            var plateVertices = viewport.Items.OfType<MeshGeometryModel3D>().Where(m => m.Material is ColorStripeMaterial).SelectMany(m => m.Geometry!.Positions!).Where(p => Math.Abs(p.X) > .2).ToArray();
            Check(plateVertices.Any(p => Math.Abs(p.Z + .65) < 1e-6) && plateVertices.Any(p => Math.Abs(p.Z + .35) < 1e-6), "plate offset and thickness translate with the deformed surface: -0.65 and -0.35 m");
            var saved = state.CaptureView("Deformata"); ModelViewPresetStore.Validate([saved]);
            state.SelectedResultType = 0; state.ShowDeformation = false; state.ShowUndeformed = false; state.DisplacementUnit = "m";
            state.ApplyView(saved);
            Check(state.IsDisplacementResult && state.SelectedComponent == "uz" && state.DisplacementUnit == "mm" && state.ShowDeformation && state.ShowUndeformed && state.EffectiveDeformationScale == 2, "named view restores result type, units, shape, ghost and scale together");
            state.AnimationDuration = 1; state.DeformationPhase = 0; state.ToggleAnimationCommand.Execute(null);
            Check(state.IsAnimationPlaying && renderer.AnimationVertexCount > 0, "animation prepares a reusable set of geometry buffers");
            var animatedMesh = viewport.Items.OfType<MeshGeometryModel3D>().First(m => m.Material is ColorStripeMaterial);
            var animatedGeometry = animatedMesh.Geometry;
            state.AdvanceAnimation(TimeSpan.FromSeconds(.25));
            Check(Math.Abs(state.DeformationPhase - .5) < 1e-12 && Math.Abs(renderer.PointForNode(5).Z - .1) < 1e-6, "one quarter of a graphical cycle reaches half of the requested translation");
            Check(ReferenceEquals(animatedMesh.Geometry, animatedGeometry) && viewport.Items.Contains(animatedMesh), "animation updates existing GPU geometry without rebuilding scene objects");
            Check(animatedGeometry!.Positions!.Any(p => Math.Abs(p.Z + .75) < 1e-6), "animated extruded plate includes its original offset at the intermediate frame");
            Check(state.ActiveValues.All(v => v.Value == 100), "animation leaves imported displacement values and legend constant");
            state.PauseAnimation();
            Check(Math.Abs(renderer.PointForNode(5).Z - .1) < 1e-6 && renderer.AnimationVertexCount == 0, "pausing restores full static geometry at the current graphical phase");
            state.ResetDeformationFrameCommand.Execute(null);
            Check(state.DeformationPhase == 1 && Math.Abs(renderer.PointForNode(5).Z - .2) < 1e-6, "reset returns to the complete deformation");
            state.ClipEnabled = true;
            Check(!state.ToggleAnimationCommand.CanExecute(null) && state.CanDeform, "clipping disables animation while retaining the static deformation");
            state.ClipEnabled = false; state.ToggleAnimationCommand.Execute(null); Wait(140);
            Check(state.IsAnimationPlaying && state.DeformationPhase < 1, "WPF timer advances the graphical deformation");
            state.SelectedCase = "A";
            Check(!state.IsAnimationPlaying && renderer.AnimationVertexCount == 0, "changing the result case stops the animation and invalidates its buffers");
            state.ResetDeformationFrameCommand.Execute(null);
            state.SelectedCase = "Parziale";
            Check(state.DeformationMessage.Contains("2/3") && renderer.SelectionShape(sample.Elements[1]).All(p => p.Z == 0), "missing nodal displacement leaves the entire incomplete element in its original position");
            state.SelectedCase = "Inviluppo(max)";
            Check(!state.CanDeform && !state.Components.Contains("|u|") && renderer.PointForNode(5).Z == 0, "non-simultaneous envelope cannot fabricate a deformed shape or resultant magnitude");
            state.SelectedCase = "Assente";
            Check(!state.HasDisplacements && !state.HasResults && state.DeformationMessage.Contains("non acquisiti"), "case without displacement clears the prior field and explains availability");
            state.SelectedCase = "A"; state.SelectedComponent = "|u|"; state.SelectedDeformationScale = 0;
            state.SelectedRepresentation = 0; state.ShowMesh = true; state.ShowExtrema = true; state.SelectedPanel = 1;
            state.CameraPresetCommand.Execute("iso-ne"); Wait(350); renderer.Fit(); Wait(200);
            Capture(control, viewport, Path.Combine(output, "deformata-collaudo.png"));
            state.ShowDeformation = false;
            Check(renderer.PointForNode(5) == new Vector3(2, -1, 0), "turning deformation off restores exact original centered geometry");
            Check(sample.Fingerprint() == fingerprint, "deformation, units, contours and presets preserve the imported snapshot bit for bit");
        }
        finally { host.Close(); }
    }
}
