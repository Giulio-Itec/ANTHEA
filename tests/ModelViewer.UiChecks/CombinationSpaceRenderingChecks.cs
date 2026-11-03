using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using ANTHEA.ModelWorkspace;

partial class Program
{
    /// <summary>
    /// Request of the user of 3/11/2026 («riesci a mettere un pannello di gestione limite?»): with the results archive of the project the window
    /// of the combinations shows the space of the copy, the limit to choose and «Libera selezionate» / «Libera tutte»; «Libera tutte» removes the
    /// saved results from the copy and the document, the definitions stay. Without the archive the panel is not there.
    /// </summary>
    static void CombinationSpaceRenderingChecks(string output)
    {
        string root = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(root, "ANTHEA.sln"))) root = Path.GetDirectoryName(root)!;
        var container = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests", "fixtures", "model-viewer-combinations-1.16.2", "progetto-1.16.2.json")))!;
        string id = ProjectModelStore.Models(container).Single()["id"]!.GetValue<string>();
        var saved = ProjectModelStore.Read(container, id)!;
        var archive = new ModelResultArchive(null);
        using var control = new ModelViewerControl(saved, [], false, entry => ProjectModelStore.Set(container, entry, id, archive), (_, _, _, _) => { }, resultsArchive: true);
        var window = new Window { Title = "ANTHEA · Spazio delle combinazioni", Content = control, Width = 1400, Height = 900, ShowInTaskbar = false, ShowActivated = false };
        window.Show(); Wait(800);
        try
        {
            var state = (ModelViewerViewModel)control.DataContext;
            state.ShowInspector = false; state.OpenCombinationManagerCommand.Execute(null);
            for (int wait = 0; wait < 100 && state.CombinationSpaceText.Length == 0; wait++) Wait(50);
            var panel = Children<StackPanel>(control).Single(p => AutomationProperties.GetName(p) == "Spazio delle combinazioni");
            var limit = Children<ComboBox>(control).Single(c => AutomationProperties.GetName(c) == "Limite dello spazio delle combinazioni");
            var freeAll = Children<Button>(control).Single(b => ReferenceEquals(b.Command, state.FreeAllCombinationResultsCommand));
            Check(panel.IsVisible && limit.Items.Count == state.CombinationLimits.Count && (string)limit.SelectedItem == ModelCombinations.CopyLimit(state.CombinationCopyBytes) &&
                state.CombinationSpaceText.Contains("combinazioni con risultati salvati", StringComparison.Ordinal) && freeAll.IsEnabled,
                $"space of the combinations: panel with the space ({state.CombinationSpaceText}), the limit and «Libera tutte» enabled");
            foreach (string theme in new[] { "Light", "Dark" }) { SetAppearance(theme); Wait(250); CapturePage(control, Path.Combine(output, "combinazioni-spazio-" + theme + ".png")); }
            int definitions = state.Combinations.Count;
            Run(() => state.FreeAllCombinationResultsCommand.ExecuteAsync(null)); Wait(200);
            Check(!state.Snapshot!.Results.Any(r => r.Combination != null) && !ProjectModelStore.Read(container, id, archive)!.Results.Any(r => r.Combination != null) &&
                state.Combinations.Count == definitions && state.Combinations.All(c => c.State != "Calcolata") && !freeAll.IsEnabled,
                "space of the combinations: «Libera tutte» removes the saved results from the copy and the document and keeps every definition");
        }
        finally { window.Close(); }
        using var plain = new ModelViewerControl(saved, [], false, _ => { }, (_, _, _, _) => { });
        var other = new Window { Content = plain, Width = 1400, Height = 900, ShowInTaskbar = false, ShowActivated = false };
        other.Show(); Wait(500);
        try
        {
            ((ModelViewerViewModel)plain.DataContext).OpenCombinationManagerCommand.Execute(null); Wait(200);
            Check(!Children<StackPanel>(plain).Single(p => AutomationProperties.GetName(p) == "Spazio delle combinazioni").IsVisible,
                "space of the combinations: without the results archive the panel is not shown (the copy stays within 256 MiB)");
        }
        finally { other.Close(); }
    }
}
