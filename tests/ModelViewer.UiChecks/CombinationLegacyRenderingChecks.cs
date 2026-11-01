using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using ANTHEA.ModelWorkspace;

partial class Program
{
    /// <summary>
    /// Decision of the user of 1/11/2026 evening («Calcolate + avviso»; «solo chiedendo le principali la vista propone il ricalcolo»): a project
    /// written by ANTHEA 1.16.2 (fixture of COMBO-REV-01-bis) opens its combinations «Calcolata»; the command of a combination whose saved
    /// results have no principal components of the plates is «Ricalcola con le componenti principali», calculates it again with GPC and keeps
    /// every value already saved, adding the principal components.
    /// </summary>
    static void CombinationLegacyRenderingChecks(string output)
    {
        const string envelope = "LCOM-GEN/cENV";
        string root = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(root, "ANTHEA.sln"))) root = Path.GetDirectoryName(root)!;
        var container = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests", "fixtures", "model-viewer-combinations-1.16.2", "progetto-1.16.2.json")))!;
        string id = ProjectModelStore.Models(container).Single()["id"]!.GetValue<string>();
        var saved = ProjectModelStore.Read(container, id)!;
        // The saved values without what the new calculation adds or renews: the revision, the principal components and their governing states.
        static string Local(ModelResultSet r) => JsonSerializer.Serialize(r with
        {
            Combination = r.Combination! with { InputRevision = "", PrincipalGoverning = null, PrincipalAlternatives = null },
            Fields = r.Fields?.Where(f => f.Axes != "PRINCIPAL").ToArray()
        });
        var before = saved.Results.Where(r => r.Combination?.DefinitionId == envelope).ToDictionary(r => r.Name, Local);
        int writes = 0;
        using var control = new ModelViewerControl(saved, [], false, entry => { writes++; ProjectModelStore.Set(container, entry, id); }, (_, _, _, _) => { });
        var window = new Window { Title = "ANTHEA · Modelli 1.16.2", Content = control, Width = 1400, Height = 900, ShowInTaskbar = false, ShowActivated = false };
        window.Show(); Wait(800);
        try
        {
            var state = (ModelViewerViewModel)control.DataContext;
            state.ShowInspector = false; state.OpenCombinationManagerCommand.Execute(null); Wait(250);
            state.SelectedCombination = state.Combinations.Single(c => c.Definition.Id == envelope); Wait(150);
            var command = Children<Button>(control).Single(b => ReferenceEquals(b.Command, state.CalculateCombinationCommand));
            Check(state.SelectedCombination.State == "Calcolata" && (string)command.Content == ModelViewerViewModel.RecalculatePrincipalsLabel && command.IsEnabled,
                "combination of 1.16.2: «Calcolata» and the command offers «Ricalcola con le componenti principali»");
            CapturePage(control, Path.Combine(output, "combinazioni-1.16.2-ricalcolo.png"));
            Run(() => state.CalculateCombinationCommand.ExecuteAsync(null)); Wait(200);
            var after = state.Snapshot!.Results.Where(r => r.Combination?.DefinitionId == envelope).ToArray();
            Check(writes == 1 && after.Length == before.Count && after.All(r => before.TryGetValue(r.Name, out var local) && local == Local(r)),
                "combination of 1.16.2 calculated again: every value saved by 1.16.2 identical bit for bit (local fields, governing states, alternatives)");
            Check(after.All(r => r.Fields?.Any(f => f.Family == "PLATE" && f.Axes == "PRINCIPAL" && f.Values.Length > 0) == true && r.Combination!.PrincipalGoverning?.Length > 0) &&
                ProjectModelStore.Read(container, id)!.Results.Where(r => r.Combination?.DefinitionId == envelope).All(r => r.Fields!.Any(f => f.Axes == "PRINCIPAL")),
                "combination of 1.16.2 calculated again: the principal components of the plates are in the view and in the document");
            Check(state.SelectedCombination?.State == "Calcolata" && (string)command.Content == "Visualizza risultati" &&
                state.Combinations.Single(c => c.Definition.Id == "ANTHEA/utente-somma").State == "Calcolata",
                "after the new calculation the command shows the results again; the other combination of 1.16.2 stays «Calcolata»");
        }
        finally { window.Close(); }
    }
}
