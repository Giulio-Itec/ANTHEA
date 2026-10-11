using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using System.Text.Json;

static partial class Program
{
    static ModelSnapshot AveragingSample()
    {
        var model = Sample.Model();
        // Equal weights despite the second plate having twice the area. Independent hand expectations:
        // node 2: (20 - 200) / 2 = -90; node 3: (30 - 30) / 2 = 0.
        model.Nodes[4] = model.Nodes[4] with { X = 6 }; model.Nodes[5] = model.Nodes[5] with { X = 6 };
        model.Results[0] = model.Results[0] with { Values = [.. model.Results[0].Values,
            new(11, 3, [1, -30, 3, 4, 5, 6, 7, 8]), new(11, 5, [1, 60, 3, 4, 5, 6, 7, 8]), new(11, 6, [1, 80, 3, 4, 5, 6, 7, 8])] };
        model.LocalFrames = [new(10, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)), new(11, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1))];
        model.SourceTables = new() { ["ELEM"] = JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["10"] = new { MATL = 1 }, ["11"] = new { MATL = 1 } }) };
        model.Validate(); return model;
    }

    static async Task ContourAveragingChecks()
    {
        var model = AveragingSample(); string fingerprint = model.Fingerprint();
        var services = new FakeServices();
        using var vm = new ModelViewerViewModel(model, [], false, services, _ => { }, (_, _, _, _) => { });
        Check(!vm.IsAveragedContour && vm.ContourValues.SequenceEqual(vm.ActiveValues), "contouring defaults to the unchanged per-element field");
        int changes = 0; vm.SceneChanged += (_, _) => changes++;
        vm.SelectedContourValues = 1;
        Check(changes == 1 && vm.ContourValues.Where(v => v.Node == 2).All(v => v.Value == -90) && vm.ContourValues.Where(v => v.Node == 3).All(v => v.Value == 0), "one scene update applies the independent equal-weight shared-vertex means");
        Check(vm.ContourValues.Single(v => v.Node == 1).Value == 10 && vm.ContourValues.Single(v => v.Node == 6).Value == 80 && vm.ContourValues.Count == vm.ActiveValues.Count, "boundary corners and result membership stay unchanged");
        Check(vm.ColorMinimum == -90 && vm.ColorMaximum == 80 && vm.Minimum == "-200", "automatic legend uses the smoothed field while imported extrema remain available");
        vm.ShowTable = true; vm.SelectedTableKind = 5;
        Check(vm.TableRows!.Cast<System.Data.DataRowView>().Min(row => (double)row["Valore"]) == -200 && vm.ActiveValues.Where(v => v.Node == 2).Select(v => v.Value).SequenceEqual([20.0, -200.0]), "smoothing never alters tables or original per-element samples");
        vm.SelectElement(10); vm.IsolateCommand.Execute(null);
        Check(vm.ContourValues.Single(v => v.Node == 2).Value == -90 && vm.ColorMaximum == 40, "isolation keeps whole-model averaging but rescales the visible legend");
        vm.SelectedColorScale = 1;
        Check(vm.ColorMinimum == -90 && vm.ColorMaximum == 80, "global scale uses averaged hidden values too");
        vm.SelectedColorScale = 2;
        Check(vm.ColorMinimum == -90 && vm.ColorMaximum == 90, "symmetric scale is based on the averaged field");
        vm.SelectedColorScale = 3; vm.ColorScaleLower = "-50"; vm.ColorScaleUpper = "30"; vm.ApplyColorScaleCommand.Execute(null);
        Check(vm.ColorMinimumLabel.StartsWith("≤") && vm.ColorMaximumLabel.StartsWith("≥"), "manual scale saturation labels follow the displayed averaged samples");
        vm.ForceUnit = "N";
        Check(vm.ContourValues.Single(v => v.Node == 2).Value == -90000 && vm.ActiveValues.Single(v => v.Node == 2).Value == 20000 && vm.ColorMinimum == -50000, "display units scale averaged and raw values independently and consistently");
        vm.ForceUnit = "kN"; vm.ShowAllCommand.Execute(null); vm.SelectedComponent = "Mxx";
        Check(vm.ContourValues.All(v => v.Value == 1), "changing component invalidates the last averaged field");
        vm.SelectedComponent = "Myy"; vm.SelectedContourValues = 0;
        Check(vm.ContourValues.Where(v => v.Node == 2).Select(v => v.Value).SequenceEqual([20.0, -200.0]), "switching smoothing off restores the exact discontinuity");
        vm.SelectedContourValues = 1; vm.SelectedFamily = 1;
        Check(!vm.CanAverageContours && !vm.IsAveragedContour && vm.ContourValues.SequenceEqual(vm.ActiveValues), "beam diagrams cannot use plate nodal smoothing");
        vm.SelectedFamily = 0; vm.SelectedComponent = "Myy";
        Check(model.Fingerprint() == fingerprint, "smoothing, units, selection and display changes preserve the source fingerprint");
        var replacement = AveragingSample(); replacement.Results[0].Values.Single(v => v.Element == 11 && v.Node == 2).Values[1] = 100;
        services.Next = replacement; await vm.ImportCommand.ExecuteAsync(null);
        Check(vm.ContourValues.Where(v => v.Node == 2).All(v => v.Value == 60), "model replacement invalidates both smoothing topology and cached values");

        void Boundary(Action<ModelSnapshot> modify, string message)
        {
            var sample = AveragingSample(); modify(sample); sample.Validate();
            using var state = new ModelViewerViewModel(sample, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
            state.SelectedContourValues = 1;
            Check(state.ContourValues.Where(v => v.Node == 2).Select(v => v.Value).SequenceEqual([20.0, -200.0]), message);
        }
        Boundary(s => { s.Plates = [.. s.Plates, new(2, "Altro spessore/offset", .5, .2)]; s.Elements[1] = s.Elements[1] with { Property = 2 }; }, "smoothing preserves discontinuities at property and offset boundaries");
        Boundary(s => s.SourceTables!["ELEM"] = JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["10"] = new { MATL = 1 }, ["11"] = new { MATL = 2 } }), "different imported materials never share a mean");
        Boundary(s => s.LocalFrames![1] = new(11, new(0, 1, 0), new(-1, 0, 0), new(0, 0, 1)), "rotated in-plane local components are not mixed");
        Boundary(s => s.LocalFrames![1] = new(11, new(1, 0, 0), new(0, 0, 1), new(0, -1, 0)), "plates meeting at a fold remain discontinuous");
        Boundary(s => s.LocalFrames![1] = new(11, new(1, 0, 0), new(0, -1, 0), new(0, 0, -1)), "opposite normals are not treated as the same orientation");
        Boundary(s => s.LocalFrames = null, "missing imported frames keep original values rather than guessing directions");
        Boundary(s => s.Results[0] = s.Results[0] with { Values = s.Results[0].Values.Where(v => v.Node != 6).ToArray() }, "incomplete result plates neither contribute nor acquire invented samples");
        var triangular = AveragingSample(); triangular.Elements[1] = triangular.Elements[1] with { Nodes = [2, 5, 3] };
        triangular.Results[0] = triangular.Results[0] with { Values = triangular.Results[0].Values.Where(v => v.Node != 6).ToArray() }; triangular.Validate();
        using var tri = new ModelViewerViewModel(triangular, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }); tri.SelectedContourValues = 1;
        Check(tri.ContourValues.Where(v => v.Node == 2).All(v => v.Value == -90), "triangles and quadrilaterals share compatible vertex means");
        var extreme = AveragingSample(); extreme.Results[0].Values.Single(v => v.Element == 10 && v.Node == 2).Values[1] = double.MaxValue;
        extreme.Results[0].Values.Single(v => v.Element == 11 && v.Node == 2).Values[1] = -double.MaxValue;
        using var limit = new ModelViewerViewModel(extreme, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }); limit.SelectedContourValues = 1;
        Check(limit.ContourValues.Where(v => v.Node == 2).All(v => v.Value == 0), "finite opposite near-limit scalars average without overflow");
    }
}
