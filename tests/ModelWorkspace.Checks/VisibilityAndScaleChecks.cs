using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

static partial class Program
{
    static void VisibilityAndScaleChecks()
    {
        var sample = Sample.Model(); string fingerprint = sample.Fingerprint();
        using var vm = new ModelViewerViewModel(sample, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
        void Criterion(string key) { vm.SelectedVisibilityChoice = vm.VisibilityChoices.Single(c => c.Key == key); vm.AddVisibilityCriterionCommand.Execute(null); }
        Criterion("PLATE"); Criterion("SECT:2");
        Check(vm.VisibleElementIds!.Count == 0 && !vm.HasResults, "contradictory visibility criteria produce an empty intersection");
        vm.VisibilityName = "Vuota"; vm.SaveVisibilityCommand.Execute(null);
        vm.VisibilityCombination = 1;
        Check(vm.VisibleElementIds!.SetEquals(new[] { 10, 11, 12 }), "union of plate family and beam section includes both sets");
        vm.RemoveVisibilityCriterionCommand.Execute(vm.VisibilityCriteria.Single(c => c.Key == "SECT:2"));
        Check(vm.VisibleElementIds!.SetEquals(new[] { 10, 11 }), "removing a criterion refreshes the visible set");
        vm.VisibilityName = "Piastre"; vm.SaveVisibilityCommand.Execute(null);
        vm.VisibilityName = "PIASTRE"; vm.SaveVisibilityCommand.Execute(null);
        Check(vm.SavedVisibilities.Count == 2 && vm.VisibilityMessage.Contains("già presente"), "duplicate visibility names cannot silently replace a saved set");
        vm.InvertVisibilityCommand.Execute(null);
        Check(vm.VisibleElementIds!.SetEquals(new[] { 12 }) && vm.VisibilityCriteria.Count == 0, "inverting filtered visibility operates on the entire model");
        vm.SelectedSavedVisibility = vm.SavedVisibilities.Single(s => s.Name == "Piastre"); vm.RecallVisibilityCommand.Execute(null);
        Check(vm.VisibleElementIds!.SetEquals(new[] { 10, 11 }), "saved visibility recalls its original membership");
        vm.SelectedSavedVisibility = vm.SavedVisibilities.Single(s => s.Name == "Vuota"); vm.RecallVisibilityCommand.Execute(null);
        Check(vm.VisibleElementIds!.Count == 0, "an empty saved visibility stays empty");
        vm.ShowAllCommand.Execute(null); vm.SelectedTreeItem = vm.FindTreeItem("ELEM:10"); vm.IsolateCommand.Execute(null);
        Check(vm.ColorMinimum == 10 && vm.ColorMaximum == 40, "automatic color scale uses only visible result values");
        vm.SelectedColorScale = 1;
        Check(vm.ColorMinimum == -200 && vm.ColorMaximum == 40 && vm.Minimum == "10", "global color scale includes hidden extrema without changing visible data extrema");
        vm.SelectedColorScale = 2;
        Check(vm.ColorMinimum == -40 && vm.ColorMaximum == 40, "symmetric color scale centers on zero");
        vm.SelectedColorScale = 3; vm.ColorScaleLower = "15,5"; vm.ColorScaleUpper = "30.5"; vm.ApplyColorScaleCommand.Execute(null);
        Check(vm.ColorMinimum == 15.5 && vm.ColorMaximum == 30.5 && vm.ColorMinimumLabel.StartsWith("≤") && vm.ColorMaximumLabel.StartsWith("≥"), "manual limits accept decimal conventions and label saturated extrema");
        vm.ShowTable = true; vm.SelectedTableKind = 5;
        Check(vm.ActiveValues.Max(v => v.Value) == 40 && vm.TableRows!.Cast<System.Data.DataRowView>().Max(row => (double)row["Valore"]) == 40, "manual color saturation never clips numeric results or tables");
        vm.ColorScaleLower = "NaN"; vm.ApplyColorScaleCommand.Execute(null);
        Check(vm.ColorMinimum == 15.5 && vm.ColorMaximum == 30.5 && vm.ColorScaleMessage.Contains("invariata"), "invalid manual bounds preserve the previously applied color range");
        vm.ColorScaleLower = "60"; vm.ColorScaleUpper = "40"; vm.ApplyColorScaleCommand.Execute(null);
        Check(vm.ColorMinimum == 15.5 && vm.ColorMaximum == 30.5, "reversed manual limits cannot change the applied color range");
        vm.ForceUnit = "N"; vm.LengthUnit = "mm";
        Check(vm.ColorMinimum == 15500 && vm.ColorMaximum == 30500 && vm.ColorScaleLower == "15500", "changing display units rescales manual limits without reinterpreting the inputs");
        vm.SelectedComponent = "Mxx";
        Check(vm.SelectedColorScale == 0, "changing field resets a manual scale belonging to another component");
        Check(sample.Fingerprint() == fingerprint, "visibility and color controls never alter the saved model or result values");

        var unused = Sample.Model();
        unused.Sections = [.. unused.Sections, new(3, "Non assegnata", "SB", .2, .3, true)];
        unused.Plates = [.. unused.Plates, new(4, "Non assegnato", .2, 0)];
        using var tables = new ModelViewerViewModel(unused, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
        tables.ShowTable = true; tables.TableOnlyVisible = false; tables.SelectedTableKind = 2;
        Check(tables.TableRows!.Count == 2, "unfiltered section table includes saved properties not assigned to elements");
        tables.SelectedTableKind = 3;
        Check(tables.TableRows!.Count == 2, "unfiltered thickness table includes unassigned saved properties");
        tables.TableOnlyVisible = true;
        Check(tables.TableRows!.Count == 1, "visible-only property table excludes unused definitions");
        tables.TableOnlyVisible = false; tables.TableOnlySelected = true;
        Check(tables.TableRows!.Count == 0, "selected-only table does not include unused properties without a selection");
    }
}
