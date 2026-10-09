using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using System.Text.Json;

static partial class Program
{
    static void DisplayAndTableChecks()
    {
        var sample = Sample.Model();
        string oldJson = JsonSerializer.Serialize(sample);
        Check(!oldJson.Contains("ImportMessages") && !oldJson.Contains("LocalFrames") && !oldJson.Contains("Fields"), "optional additions do not change serialization of old snapshots");
        sample.SourceTables = new()
        {
            ["GRUP"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                ["8"] = new { NAME = "Intero", E_LIST = new[] { 10, 11, 12 } },
                ["3"] = new { NAME = "Piastra", E_LIST = new[] { 10 } }
            }),
            ["FRLS"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>())
        };
        using var vm = new ModelViewerViewModel(sample, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
        vm.SelectedMode = 0; vm.SelectedColorMode = 4;
        Check(vm.ElementColors[10].Key == "GRUP:3" && vm.ElementColors[11].Key == "GRUP:8", "overlapping group colors prefer the most specific imported group");
        string stableColor = vm.ElementColors[10].Color;
        vm.SelectedTreeItem = vm.FindTreeItem("ELEM:10"); vm.IsolateCommand.Execute(null);
        Check(vm.VisibleElementIds!.SetEquals(new[] { 10 }) && vm.ColorLegend.Sum(c => c.Count) == 1 && vm.ElementColors[10].Color == stableColor, "isolation filters legend without recoloring the model");
        Check(vm.ActiveValues.Count == 4 && vm.Minimum == "10" && vm.Maximum == "40", "visible result range excludes hidden extrema");
        vm.ShowHiddenWireframe = true;
        Check(vm.ActiveValues.Count == 4 && vm.VisibleElementIds.Count == 1, "inactive wireframe cannot reintroduce hidden result values");
        vm.InvertVisibilityCommand.Execute(null);
        Check(vm.VisibleElementIds!.SetEquals(new[] { 11, 12 }), "invert visibility uses the exact complement");
        vm.ShowSelectedCommand.Execute(null);
        Check(vm.VisibleElementIds == null || vm.VisibleElementIds.Count == 3, "show selected restores a hidden selection");
        vm.ShowAllCommand.Execute(null); vm.ShowTable = true;
        Check(vm.TableRows!.Count == 3 && vm.TableRows.Table!.Columns["ID"]!.DataType == typeof(int), "element table keeps numeric types for sorting");
        vm.TableRows.Sort = "ID DESC";
        Check((int)vm.TableRows[0]["ID"] == 12 && vm.TableCsv().Split('\n')[1].StartsWith("\"12\""), "CSV follows the displayed numeric ordering");
        vm.TableSearch = "PLATE";
        Check(vm.TableRows.Count == 2, "table search filters stored values");
        vm.TableSearch = ""; vm.SelectedTableRow = vm.TableRows[1];
        Check(vm.SelectedElementIds.SetEquals(new[] { 11 }), "table selection resolves to the model tree");
        vm.SelectedTableKind = 5;
        Check(vm.TableRows!.Count == 5 && vm.TableRows.Cast<System.Data.DataRowView>().Where(r => (int)r["Nodo"] == 2).Select(r => (double)r["Valore"]).Order().SequenceEqual(new[] { -200.0, 20.0 }), "table retains different values at a shared node, including incomplete elements");
        vm.TableOnlySelected = true;
        Check(vm.TableRows!.Count == 1, "result table filters by selected element");
        vm.TableOnlySelected = false; vm.ForceUnit = "N"; vm.LengthUnit = "mm";
        Check(vm.ActiveValues.Max(v => v.Value) == 40000 && vm.ResultUnit == "Myy [N·mm/mm]" && vm.TableRows!.Cast<System.Data.DataRowView>().Max(r => (double)r["Valore"]) == 40000, "plate moment units update scene and table consistently");
        vm.SelectedAxes = 1;
        Check(!vm.HasResults && vm.TableRows!.Count == 0 && vm.ResultAvailability.Contains("non importate"), "missing principal results stay unavailable");
        Check(vm.FindTreeItem("FRLS")!.Badge == "0" && vm.FindTreeItem("NSPR")!.Badge == "non importato", "empty source table differs from a missing source table");
        vm.SearchText = "Piastra";
        Check(vm.Tree.Count > 0, "model tree search keeps matching paths");
        var beam = sample.Elements.Single(e => e.Type == "BEAM");
        sample.Results[0] = sample.Results[0] with { Fields = [new("BEAM", "LOCAL", "My", [new(beam.Id, beam.Nodes[0], -7, 0), new(beam.Id, 0, 2, .5), new(beam.Id, beam.Nodes[1], 3, 1)])] };
        sample.Validate();
        using var beamVm = new ModelViewerViewModel(sample, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
        beamVm.SelectedFamily = 1; beamVm.SelectedComponent = "My"; beamVm.ForceUnit = "N"; beamVm.LengthUnit = "mm";
        Check(beamVm.ActiveValues.Min(v => v.Value) == -7000000 && beamVm.ResultUnit == "My [N·mm]", "beam moment scales force and length independently");
        beamVm.SelectedBeamLocation = 3;
        Check(beamVm.ActiveValues.Single().Value == 2000000, "beam station selects the imported midpoint without interpolation");
        sample.Sections[0] = sample.Sections[0] with { Name = "=SUM(1;2)" };
        using var csvVm = new ModelViewerViewModel(sample, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
        csvVm.ShowTable = true; csvVm.SelectedTableKind = 2;
        Check(csvVm.TableCsv().Contains("'\u003dSUM(1;2)"), "CSV preserves source text without spreadsheet formula execution");
        var axial = Sample.Model(); int index = Array.FindIndex(axial.Elements, e => e.Type == "BEAM");
        axial.Elements[index] = axial.Elements[index] with { Type = "TRUSS" }; var truss = axial.Elements[index];
        axial.Results[0] = axial.Results[0] with { Fields = [new("TRUSS", "LOCAL", "Fx", [new(truss.Id, truss.Nodes[0], 15, 0)])] };
        axial.Validate();
        using var trussVm = new ModelViewerViewModel(axial, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
        trussVm.SelectedFamily = 2;
        Check(trussVm.Components.SequenceEqual(new[] { "Fx" }) && trussVm.ActiveValues.Single().Value == 15, "truss keeps its own formulation and axial-only result contract");
        axial.Results[0] = axial.Results[0] with { Fields = [new("TRUSS", "LOCAL", "My", [new(truss.Id, truss.Nodes[0], 15, 0)])] };
        Reject(axial.Validate, "bending results cannot be assigned to a truss");
    }
}
