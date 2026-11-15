using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.EmbeddedWalls;
using X.Desktop.Views.EmbeddedWalls;

namespace X.Desktop;

/// <summary>
/// Organizzazione del modulo paratie (progetto del 16/11), step 2, off screen: the tabs of the phases under the drawing follow the phases of the
/// case and choose the one drawn; «+» and the menu Fase are bound to the commands of the sheet. Captures fasi-schede and fasi-inserita;
/// the data are restored afterwards.
/// </summary>
internal static partial class EmbeddedWallViewChecks
{
    private static async Task CheckStageTabs(FrameworkElement view, EmbeddedWallSheetViewModel sheet,
        Func<Task> settle, Action<bool, string> check, Action<string> capture)
    {
        var before = sheet.Data.DeepClone().AsObject();
        var tabs = Visual(view).OfType<ListBox>().Single(l => l.Name == "StageTabs");
        var sketch = Visual(view).OfType<EmbeddedWallModelSketch>().Single();
        check(tabs.Items.Count == sheet.InputStages.Count && ReferenceEquals(tabs.SelectedItem, sheet.InputStage) && tabs.IsVisible,
            "Passo 2: le schede delle fasi non seguono le fasi del caso.");
        var add = Visual(view).OfType<Button>().Single(b => ReferenceEquals(b.Command, sheet.AddStageCommand) && b.Content as string == "+");
        var menu = Visual(view).OfType<Button>().Single(b => b.Content as string == "Fase ▾");
        check(add.IsEnabled && menu.ContextMenu?.Items.OfType<MenuItem>().Count() == 5 && tabs.ContextMenu?.Items.OfType<MenuItem>().Count() == 5,
            "Passo 2: «+» o il menu Fase assenti o incompleti.");
        tabs.SelectedItem = sheet.InputStages[0]; await settle();
        check(ReferenceEquals(sheet.InputStage, sheet.InputStages[0]) && sketch.Model?.RightGround == 0, "Passo 2: la scheda scelta non cambia la fase disegnata.");
        tabs.SelectedItem = sheet.InputStages[^1]; await settle();
        check(sketch.Model?.RightGround == -4, "Passo 2: la scheda dell'ultima fase non mostra lo scavo.");
        capture("fasi-schede");
        add.Command!.Execute(null); await settle();
        sheet.InsertStageCommand.Execute(null); await settle();
        check(tabs.Items.Count == 4 && ReferenceEquals(tabs.SelectedItem, sheet.InputStage) && sheet.InputStage?["id"] == "F4"
            && string.Join(",", tabs.Items.OfType<EmbeddedWallRow>().Select(r => r["id"])) == "F1,F2,F4,F3" && sketch.Model?.RightGround == -4,
            "Passo 2: «+» e «Inserisci fase» non aggiornano le schede o il disegno.");
        capture("fasi-inserita");
        sheet.Reload(before); await settle();
        check(JsonNode.DeepEquals(sheet.Data, before) && tabs.Items.Count == 2 && ReferenceEquals(tabs.SelectedItem, sheet.InputStage),
            "Passo 2: le schede non tornano alle fasi dopo Reload.");
    }
}
