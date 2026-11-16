using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

/// <summary>
/// Rilievo R4 della prova dell'utente del 16/11/2026 (registro MOD-RIL-R4): «↶ Annulla Libera» where «Libera» is, and the message that says
/// where the undo of the project is.
/// </summary>
static partial class Program
{
    static async Task UndoFreeChecks()
    {
        var envelope = new ModelCombinationDefinition("ANTHEA/env", "Inviluppo", ModelCombinationKind.Envelope, "ANTHEA", "", [new("G", false, 1), new("Q", false, 1)], true);
        var model = ModelCombinations.Save(CombinationModel(), envelope);
        model = ModelCombinations.Attach(model, envelope.Id, await new GpcModelCombinationService().CalculateAsync(model, envelope.Id));
        var undone = new List<string>();
        using (var vm = new ModelViewerViewModel(model, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { })
               { CombinationArchive = true, UndoProjectChange = label => { undone.Add(label); return true; } })
        {
            Check(!vm.CanUndoFree, "R4: «↶ Annulla Libera» is not offered before «Libera»");
            await vm.FreeAllCombinationResultsCommand.ExecuteAsync(null);
            Check(vm.CanUndoFree && vm.UndoFreeCommand.CanExecute(null) && vm.CombinationMessage.Contains("«↶ Annulla Libera» qui sotto", StringComparison.Ordinal) &&
                  vm.CombinationMessage.Contains("Ctrl+Z", StringComparison.Ordinal), "R4: right after «Libera» the message says where the undo is and the viewer offers it");
            vm.UndoFreeCommand.Execute(null);
            Check(undone.SequenceEqual([ModelViewerViewModel.FreeLabel(1)]) && !vm.CanUndoFree, "R4: «↶ Annulla Libera» asks the project to undo the change named «Libera…»");
        }
        using (var later = new ModelViewerViewModel(model, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { })
               { CombinationArchive = true, UndoProjectChange = label => { undone.Add(label); return false; } })
        {
            await later.FreeAllCombinationResultsCommand.ExecuteAsync(null);
            later.UndoFreeCommand.Execute(null);
            Check(later.CombinationMessage.StartsWith("Non annullato", StringComparison.Ordinal), "R4: when the project changed after «Libera» nothing is undone and the message says why");
        }
        using var alone = new ModelViewerViewModel(model, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }) { CombinationArchive = true };
        await alone.FreeAllCombinationResultsCommand.ExecuteAsync(null);
        Check(!alone.CanUndoFree && alone.CombinationMessage.Contains("finestra principale", StringComparison.Ordinal), "R4: outside a project the message points to «↶ Annulla» of the main window");
        using var calculated = new ModelViewerViewModel(model, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { })
            { CombinationArchive = true, UndoProjectChange = _ => true };
        await calculated.FreeAllCombinationResultsCommand.ExecuteAsync(null);
        calculated.SelectedCombination = calculated.Combinations.Single(c => c.Definition.Id == envelope.Id);
        await calculated.CalculateCombinationCommand.ExecuteAsync(null);
        Check(!calculated.CanUndoFree, "R4: after another change of the copy «↶ Annulla Libera» is not offered any more");
    }
}
