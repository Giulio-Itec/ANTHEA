using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void ContourRenderingChecks(string output)
    {
        var sample = Sample.Model();
        sample.Results = [new("Media sintetica", false, sample.Elements.Where(e => e.Type == "PLATE")
            .SelectMany(e => e.Nodes.Select(n => new CornerResult(e.Id, n, [1, e.Id == 10 ? 10 : 30, 3, 4, 5, 6, 7, 8]))).ToArray())];
        sample.LocalFrames = [new(10, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)), new(11, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1))];
        using var control = new ModelViewerControl(sample, [], false, _ => { }, (_, _, _, _) => { });
        var host = new Window { Title = "ANTHEA · collaudo media ai vertici", Content = control, Width = 1400, Height = 940, ShowInTaskbar = false, ShowActivated = false };
        host.Show(); Wait(500);
        try
        {
            var state = (ModelViewerViewModel)control.DataContext; state.SelectedPanel = 1; state.ShowInspector = false;
            var viewport = Children<Viewport3DX>(control).Single();
            double[] SharedEdgeColors()
            {
                var mesh = (HelixToolkit.SharpDX.MeshGeometry3D)viewport.Items.OfType<MeshGeometryModel3D>().Single(m => m.Material is ColorStripeMaterial).Geometry!;
                return Enumerable.Range(0, mesh.Positions!.Count).Where(i => Math.Abs(mesh.Positions[i].X) < 1e-6)
                    .Select(i => (double)mesh.TextureCoordinates![i].X).ToArray();
            }
            var before = SharedEdgeColors();
            Check(before.Contains(0) && before.Contains(1), "GPU mesh retains both original values along the shared edge before smoothing");
            var selector = Children<ComboBox>(control).Single(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Valori del contouring");
            selector.SelectedIndex = 1; Wait(180);
            Check(state.IsAveragedContour && SharedEdgeColors().Length > 0 && SharedEdgeColors().All(v => Math.Abs(v - .5) < 1e-6), "MVVM selector gives both plates UV=0.5 along their entire shared edge: mean (10+30)/2=20");
            Check(state.ColorMinimum == 10 && state.ColorMaximum == 30 && state.ActiveValues.All(v => v.Value is 10 or 30), "GPU color limits agree with unchanged boundary extrema and original engineering data");
            selector.SelectedIndex = 0; Wait(120);
            Check(SharedEdgeColors().Contains(0) && SharedEdgeColors().Contains(1), "turning off averaging restores the original GPU discontinuity");
            selector.SelectedIndex = 1; Wait(120);
            Capture(control, viewport, Path.Combine(output, "media-sintetica.png"));
        }
        finally { host.Close(); }
    }

    static void RealContourAveragingChecks(ModelViewerControl control, Viewport3DX viewport, string output)
    {
        var state = (ModelViewerViewModel)control.DataContext;
        state.SelectedPanel = 1; state.SelectedMode = 2; state.SelectedComponent = "Myy"; state.ShowInspector = false; state.ShowMesh = false;
        state.SelectedColorScale = 3; state.ColorScaleLower = state.Minimum; state.ColorScaleUpper = state.Maximum; state.ApplyColorScaleCommand.Execute(null); Wait(120);
        string fingerprint = state.Snapshot!.Fingerprint();
        var raw = state.ActiveValues.ToArray();
        Capture(control, viewport, Path.Combine(output, "contouring-non-mediato.png"));
        var watch = Stopwatch.StartNew();
        state.SelectedContourValues = 1; watch.Stop();
        double prepareMilliseconds = watch.Elapsed.TotalMilliseconds;
        Wait(200);
        Check(state.Status.StartsWith("Scena pronta") && !state.ContourValues.SequenceEqual(raw), "real model renders a distinct averaged field");
        Check(state.Snapshot.Fingerprint() == fingerprint && state.ActiveValues.SequenceEqual(raw), "real-model smoothing preserves snapshot and imported values");
        Capture(control, viewport, Path.Combine(output, "contouring-mediato.png"));
        File.WriteAllText(Path.Combine(output, "media-vertici.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            Scope = "Primo avvio media: topologia, campo e ricostruzione scena sincrona; escluso completamento GPU e acquisizione", PrepareMilliseconds = prepareMilliseconds,
            Summary = state.ContourAveragingSummary, Corners = raw.Length,
            ChangedCorners = state.ContourValues.Zip(raw).Count(v => v.First.Value != v.Second.Value),
            RawMinimum = raw.Min(v => v.Value), RawMaximum = raw.Max(v => v.Value),
            DisplayMinimum = state.ContourValues.Min(v => v.Value), DisplayMaximum = state.ContourValues.Max(v => v.Value)
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        state.SelectedContourValues = 0; state.SelectedColorScale = 0; state.ShowMesh = true; state.ShowInspector = true;
    }
}
