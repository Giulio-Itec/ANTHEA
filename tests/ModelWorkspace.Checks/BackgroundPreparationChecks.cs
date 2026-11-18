using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

/// <summary>
/// Rilievo R7 della prova dell'utente del 16/11/2026 (registro MOD-RIL-R7): the base cases of GPC prepared in the background when the scene
/// is ready, only with combinations to calculate and enough free memory, cancelled by a new copy. Review of 17/11/2026: P1 (the preparation
/// after «Importa/Aggiorna modello…», with the scene wired as ModelViewerControl does), P2 (the service of the tab: a viewer created again on
/// the same copy finds the base cases ready), the closing of the viewer that keeps nothing prepared.
/// </summary>
static partial class Program
{
    static async Task BackgroundPreparationChecks()
    {
        var linear = new ModelCombinationDefinition("ANTHEA/sum", "Somma", ModelCombinationKind.Linear, "ANTHEA", "", [new("G", false, 2), new("Q", false, -.5)], true);
        var model = ModelCombinations.Save(CombinationModel(), linear);
        static async Task Prepared(ModelViewerViewModel vm) { for (int i = 0; i < 600 && vm.IsPreparingCombinations; i++) await Task.Delay(50); }
        using (var vm = new ModelViewerViewModel(model, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }) { AvailableMemory = () => 32L << 30 })
        {
            Check(!vm.IsPreparingCombinations && vm.CombinationPreparationText == "", "R7: nothing is prepared before the scene is on screen");
            vm.SceneReady();
            Check(vm.IsPreparingCombinations, "R7: with the scene ready and a combination to calculate the base cases are prepared in the background");
            await Prepared(vm);
            Check(!vm.IsPreparingCombinations && vm.CombinationPreparationText == ModelViewerViewModel.PreparedText, "R7: the preparation in the background ends and says so");
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

        // The closing of the viewer during the preparation (until 17/11 a check that was always true): the preparation stops, nothing stays prepared.
        var closing = new ModelViewerViewModel(model, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }) { AvailableMemory = () => 32L << 30 };
        closing.SceneReady();
        bool started = closing.IsPreparingCombinations;
        var own = (GpcModelCombinationService)typeof(ModelViewerViewModel).GetField("combinationService", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(closing)!;
        closing.Dispose();
        var gate = (SemaphoreSlim)typeof(GpcModelCombinationService).GetField("calculationGate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(own)!;
        for (int i = 0; i < 600 && gate.CurrentCount == 0; i++) await Task.Delay(50);
        Check(started && gate.CurrentCount == 1 && typeof(GpcModelCombinationService).GetField("prepared", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(own) is null,
            "R7: closing the view while preparing stops the preparation and keeps nothing prepared");

        var services = new FakeServices { Next = CombinationModel() };
        using (var replaced = new ModelViewerViewModel(model, [], false, services, _ => { }, (_, _, _, _) => { }) { AvailableMemory = () => 32L << 30 })
        {
            replaced.SceneReady(); await replaced.ImportCommand.ExecuteAsync(null);
            Check(!replaced.IsPreparingCombinations && replaced.CombinationPreparationText == "", "R7: a new import stops the preparation of the previous copy");
        }

        // P1: «Importa modello…» on a new model with the scene wired as ModelViewerControl does (SceneReady at every scene, also the one drawn
        // during the import, while the viewer is busy and the catalogue is still the one of the previous copy). Until 17/11 the copy stopped
        // waiting at that scene and the preparation never started.
        var imports = new FakeServices { Next = model };
        using (var imported = new ModelViewerViewModel(null, [], false, imports, _ => { }, (_, _, _, _) => { }) { AvailableMemory = () => 32L << 30 })
        {
            imported.SceneChanged += (_, _) => imported.SceneReady();
            await imported.ImportCommand.ExecuteAsync(null);
            Check(imported.Snapshot is not null && (imported.IsPreparingCombinations || imported.CombinationPreparationText == ModelViewerViewModel.PreparedText),
                "P1 (review): after «Importa modello…» the base cases of the copy imported are prepared in the background");
            await Prepared(imported);
            imported.SelectedCombination = imported.Combinations.Single(c => c.Definition.Id == linear.Id);
            await imported.CalculateCombinationCommand.ExecuteAsync(null);
            Check(imported.CombinationMessage.StartsWith("Casi base riutilizzati", StringComparison.Ordinal), "P1 (review): the first combination after the import reuses them");
        }

        // P2: the service of the tab of the model. A viewer created again on the same copy (a change of tab or page) finds the base cases
        // ready; a viewer closed during the preparation leaves it running for the next one; a different copy drops them; closing the tab too.
        var shared = new GpcModelCombinationService();
        using (var first = new ModelViewerViewModel(model, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }, shared) { AvailableMemory = () => 32L << 30 })
        {
            first.SceneReady(); await Prepared(first);
            Check(first.CombinationPreparationText == ModelViewerViewModel.PreparedText && shared.IsPreparedFor(model), "P2 (review): the base cases prepared in the service of the tab");
        }
        Check(shared.IsPreparedFor(model), "P2 (review): closing the viewer keeps the base cases of the tab");
        using (var again = new ModelViewerViewModel(model, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }, shared) { AvailableMemory = () => 32L << 30 })
        {
            again.SceneReady();
            Check(!again.IsPreparingCombinations && again.CombinationPreparationText == ModelViewerViewModel.PreparedText,
                "P2 (review): a viewer created again on the same copy does not prepare again");
            again.SelectedCombination = again.Combinations.Single(c => c.Definition.Id == linear.Id);
            await again.CalculateCombinationCommand.ExecuteAsync(null);
            Check(again.CombinationMessage.StartsWith("Casi base riutilizzati", StringComparison.Ordinal), "P2 (review): its first combination reuses them");
        }
        var second = new GpcModelCombinationService();
        var leaving = new ModelViewerViewModel(model, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }, second) { AvailableMemory = () => 32L << 30 };
        leaving.SceneReady(); leaving.Dispose();
        using (var back = new ModelViewerViewModel(model, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }, second) { AvailableMemory = () => 32L << 30 })
        {
            back.SceneReady(); await Prepared(back);
            back.SelectedCombination = back.Combinations.Single(c => c.Definition.Id == linear.Id);
            await back.CalculateCombinationCommand.ExecuteAsync(null);
            Check(back.CombinationMessage.StartsWith("Casi base riutilizzati", StringComparison.Ordinal),
                "P2 (review): a viewer closed during the preparation leaves it to the next viewer of the tab, which reuses it");
        }
        var other = ModelCombinations.Save(CombinationModel(), linear with { Id = "ANTHEA/other", Name = "Altra" });
        using (var changed = new ModelViewerViewModel(other, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }, shared) { AvailableMemory = () => 32L << 30 })
            Check(!shared.IsPreparedFor(model), "P2 (review): a different copy on screen drops the base cases of the previous one (never two kept)");
        shared.Close(); second.Close();
        Check(!second.IsPreparedFor(model), "P2 (review): closing the tab keeps nothing prepared");
    }
}
