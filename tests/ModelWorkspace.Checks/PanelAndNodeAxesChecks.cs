using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using System.Text.Json;

static partial class Program
{
    static void PanelAndNodeAxesChecks()
    {
        var sample = Sample.Model(); string original = sample.Fingerprint();
        sample.NodeFrames = [new(1, new(0, 1, 0), new(-1, 0, 0), new(0, 0, 1))]; sample.Validate();
        var copy = ModelSnapshot.FromJson(JsonSerializer.Serialize(sample));
        Check(copy.NodeFrames![0].X.Y == 1 && copy.Fingerprint() == sample.Fingerprint() && copy.Fingerprint() != original, "nodal axes persist explicitly and affect the imported revision fingerprint");
        copy.NodeFrames = [copy.NodeFrames[0], copy.NodeFrames[0]];
        Reject(copy.Validate, "duplicate nodal orientations are rejected");
        copy.NodeFrames = [sample.NodeFrames[0] with { Node = 999 }];
        Reject(copy.Validate, "nodal axes cannot refer to absent nodes");
        copy.NodeFrames = [sample.NodeFrames[0] with { Z = new(0, 0, -1) }];
        Reject(copy.Validate, "left-handed nodal axes cannot enter the renderer");
        using var vm = new ModelViewerViewModel(sample, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
        vm.SelectedTreeItem = vm.FindTreeItem("NODE:1");
        Check(vm.SelectedNodeIds.SetEquals(new[] { 1 }) && vm.SelectedElementIds.SetEquals(new[] { 10, 12 }) && vm.Attributes.Any(a => a.Name == "Asse locale X" && a.Value.Contains("0, 1, 0")), "node inspection exposes its own axes and adjacent elements");
        vm.SelectedTreeItem = vm.FindTreeItem("SECT:2"); vm.InspectSelectionCommand.Execute(null);
        Check(vm.ShowInspector && vm.ShowTable && vm.TableRows!.Count == 1 && (int)vm.TableRows[0]["ID"] == 12, "inspect property opens the table containing its associated elements");
        vm.IsolateCommand.Execute(null); vm.ToggleNavigationCommand.Execute(null); vm.ToggleInspectorCommand.Execute(null);
        Check(vm.IsNavigationCollapsed && !vm.ShowInspector && vm.VisibleElementIds!.SetEquals(new[] { 12 }), "collapsing sidebars keeps the isolated model unchanged");
        vm.OpenNavigationCommand.Execute("1"); vm.ToggleInspectorCommand.Execute(null);
        Check(!vm.IsNavigationCollapsed && vm.ShowInspector && vm.SelectedPanel == 1 && vm.SelectedElementIds.SetEquals(new[] { 12 }), "sidebar navigation restores the requested tab and retains selection");
        vm.ShowNodeAxes = vm.ShowLocalAxes = vm.ShowRestraints = vm.ShowConstraints = vm.ShowLinks = vm.ShowReleases = true;
        int updates = 0; vm.SceneChanged += (_, _) => updates++; vm.DisableOverlaysCommand.Execute(null);
        Check(!vm.ShowNodeAxes && !vm.ShowLocalAxes && !vm.ShowRestraints && !vm.ShowConstraints && !vm.ShowLinks && !vm.ShowReleases && updates == 1, "all axis and boundary layers can be disabled with one scene update");
    }
}
