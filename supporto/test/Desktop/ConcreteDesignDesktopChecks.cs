using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

// --check-concrete-design <cartella> (AppHarness): reinforcement search of the c.a. section on the ui-fixture.json
// written by supporto/test/ConcreteDesign.Checks. Moved unchanged from supporto/test/ConcreteDesign.DesktopChecks/App.cs,
// which recompiled the sources of X.Desktop and no longer built (refactoring F1.6).
internal sealed partial class ConcreteWorkspace
{
    internal void PrepareDesignSmoke()
    {
        pendingCalculations.Clear(); tabs.SelectedIndex = 7;
        var o = DesignOptions;
        foreach (var (k, v) in new[] { ("diametri", "16;20"), ("n_superiori", "3;4"), ("n_inferiori", "3;4"), ("n_laterali", "2"), ("diametri_staffe", "8"), ("passi_staffe", "100;150"), ("max_tentativi", "100") })
            designForm!.Set(k, v);
    }
    internal async Task RunDesignSmoke(Window window, string folder)
    {
        void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
        await RunDesignAsync();
        File.WriteAllText(Path.Combine(folder, "ui-diagnostics.txt"), designStatus.Text + "\n\n" + designDiagnostics!.Text);
        Assert(DesignOptions.B("ricerca_completa") && designForm!.Editors["max_tentativi"].Visibility == Visibility.Collapsed, "Full search not default/limited budget still shown");
        Assert(tabs.Items.Count == 8 && tabs.SelectedIndex == 7, "Missing design tab");
        Assert(designResult is { Candidates.Count: > 1 }, "No UI alternatives: " + designStatus.Text + " / " + designDiagnostics?.Text);
        Assert(designForm!.Editors["parallelismo"] is ComboBox && designDiagnostics!.Text.Contains("Tempi di ricerca") && designDiagnostics.Text.Contains("riutilizzati"), "Parallel control or timing diagnostics missing");
        Assert(designApply.IsEnabled, "Apply not enabled: " + designStatus.Text);
        Assert(designRanking.Rows.Count == 10, "Top-N not applied");
        DesignOptions["mostra"] = "Tutte"; RefreshDesignRanking();
        Assert(designRanking.Rows.Count == designResult!.Candidates.Count, "Show all failed");
        designRanking.SelectedIndex = 1;
        Assert(designSelection!.Id == designResult.Candidates[1].Id && designPreview.Section is not null, "Alternative preview mismatch");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
        File.WriteAllBytes(Path.Combine(folder, "calcola-armature.png"), Ui.Snapshot(window));
        window.Width = 1140; window.Height = 800;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
        File.WriteAllBytes(Path.Combine(folder, "calcola-armature-compatto.png"), Ui.Snapshot(window));
        var chosen = designSelection; ApplyDesign();
        Assert(Input.S("bottom_bar_count") == chosen.Input.S("bottom_bar_count") && Input.S("top_bar_diameter_mm") == chosen.Input.S("top_bar_diameter_mm"), "Apply not transferred");
        Assert(settings["proposta_armature_applicata"] is JsonObject, "Applied audit missing");
        Assert(!designApply.IsEnabled, "Applied result still reusable");
        cancellation?.Cancel(); pendingCalculations.Clear(); calculationQueued = false;
        var roundtrip = JsonNode.Parse(Data.ToJsonString())!.AsObject(); SectionWorkspace.Prepare(roundtrip);
        Assert(roundtrip["workspace_ca"]!["calcola_armature"].S("diametri") == "16;20", "Search settings not persisted");
        designForm.Set("parallelismo", "2");
        await RunDesignAsync(); Assert(designApply.IsEnabled, "Second search did not recover");
        Assert(designResult!.Performance!.Workers == Math.Min(2, Environment.ProcessorCount), "UI parallel setting not applied");
        string before = Input.ToJsonString(); actions["SLU"][0]["Mx"] = "9000";
        Assert(!designApply.IsEnabled && designStale, "Stale proposal not disabled");
        ApplyDesign(); Assert(Input.ToJsonString() == before, "Stale candidate applied");
        cancellation?.Cancel(); pendingCalculations.Clear(); calculationQueued = false;
        await RunDesignAsync();
        Assert(designResult is { Completed: true, Candidates.Count: 0 }, "Expected fully searched infeasible case");
        Assert(designScope.Text.Contains("Primi motivi di scarto") && designDiagnostics!.Text.Contains("obiettivo ≤"), "Failed search hides its actual check and target");
        Assert(!designApply.IsEnabled && designPreview.Section is null, "Failed search retained old proposal");
        ((CheckBox)designForm!.Editors["ricerca_completa"]).IsChecked = false;
        designForm.Set("max_tentativi", "1");
        Assert(designForm.Editors["max_tentativi"].Visibility == Visibility.Visible, "Limited budget not exposed");
        await RunDesignAsync();
        Assert(designResult is { Completed: false, Candidates.Count: 0 } && designDescription.Text.Contains("ricerca incompleta"), "Limited empty result looks exhaustive");
        window.Width = 1500; window.Height = 1000;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); window.UpdateLayout();
        File.WriteAllBytes(Path.Combine(folder, "ricerca-incompleta.png"), Ui.Snapshot(window));
        File.WriteAllText(Path.Combine(folder, "ui-after-apply.json"), Data.ToJsonString());
    }
}
