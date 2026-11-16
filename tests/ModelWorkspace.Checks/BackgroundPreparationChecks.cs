using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

/// <summary>
/// Rilievo R7 della prova dell'utente del 16/11/2026 (registro MOD-RIL-R7): the base cases of GPC prepared in the background when the scene
/// is ready, only with combinations to calculate and enough free memory, cancelled by a new copy.
/// </summary>
static partial class Program
{
    static async Task BackgroundPreparationChecks()
    {
        var linear = new ModelCombinationDefinition("ANTHEA/sum", "Somma", ModelCombinationKind.Linear, "ANTHEA", "", [new("G", false, 2), new("Q", false, -.5)], true);
        var model = ModelCombinations.Save(CombinationModel(), linear);
        using (var vm = new ModelViewerViewModel(model, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }) { AvailableMemory = () => 32L << 30 })
        {
            Check(!vm.IsPreparingCombinations && vm.CombinationPreparationText == "", "R7: nothing is prepared before the scene is on screen");
            vm.SceneReady();
            Check(vm.IsPreparingCombinations, "R7: with the scene ready and a combination to calculate the base cases are prepared in the background");
            for (int i = 0; i < 600 && vm.IsPreparingCombinations; i++) await Task.Delay(50);
            Check(!vm.IsPreparingCombinations && vm.CombinationPreparationText.Contains("pronti", StringComparison.Ordinal), "R7: the preparation in the background ends and says so");
            vm.SceneReady();
            Check(!vm.IsPreparingCombinations, "R7: once for each copy");
            vm.SelectedCombination = vm.Combinations.Single(c => c.Definition.Id == linear.Id);
            await vm.CalculateCombinationCommand.ExecuteAsync(null);
            Check(vm.CombinationMessage.StartsWith("Casi base riutilizzati", StringComparison.Ordinal), "R7: the first combination reuses the preparation made in the background");
        }
        using (var low = new ModelViewerViewModel(model, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }) { AvailableMemory = () => 2L << 30 })
        {
            low.SceneReady();
            Check(!low.IsPreparingCombinations && low.CombinationPreparationText.Contains("non preparati in anticipo", StringComparison.Ordinal) &&
                  low.CombinationPreparationText.Contains("8 GiB", StringComparison.Ordinal), "R7: with less than 8 GiB free nothing starts by itself, and the catalogue says why");
            low.SelectedCombination = low.Combinations.Single(c => c.Definition.Id == linear.Id);
            await low.CalculateCombinationCommand.ExecuteAsync(null);
            Check(low.CombinationMessage.StartsWith("Preparazione iniziale", StringComparison.Ordinal), "R7: the first combination prepares, as before");
        }
        using (var none = new ModelViewerViewModel(CombinationModel(), [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }) { AvailableMemory = () => 32L << 30 })
        {
            none.SceneReady();
            Check(!none.IsPreparingCombinations && none.CombinationPreparationText == "", "R7: without combinations to calculate nothing is prepared");
        }
        using (var readOnly = new ModelViewerViewModel(model, [], true, new FakeServices(), _ => { }, (_, _, _, _) => { }) { AvailableMemory = () => 32L << 30 })
        {
            readOnly.SceneReady();
            Check(!readOnly.IsPreparingCombinations, "R7: a revision on screen (read-only) prepares nothing");
        }
        var closing = new ModelViewerViewModel(model, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }) { AvailableMemory = () => 32L << 30 };
        closing.SceneReady(); closing.Dispose();
        Check(!closing.IsPreparingCombinations || closing.IsPreparingCombinations, "R7: closing the view while preparing does not throw");
        var services = new FakeServices { Next = CombinationModel() };
        using var replaced = new ModelViewerViewModel(model, [], false, services, _ => { }, (_, _, _, _) => { }) { AvailableMemory = () => 32L << 30 };
        replaced.SceneReady(); await replaced.ImportCommand.ExecuteAsync(null);
        Check(!replaced.IsPreparingCombinations && replaced.CombinationPreparationText == "", "R7: a new import stops the preparation of the previous copy");
    }
}
