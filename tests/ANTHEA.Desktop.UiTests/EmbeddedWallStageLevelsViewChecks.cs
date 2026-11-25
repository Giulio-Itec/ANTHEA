using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Anthea.Presentation.Modules.EmbeddedWalls;
using X.Desktop.Views.EmbeddedWalls;

namespace X.Desktop;

/// <summary>
/// Organizzazione del modulo paratie (progetto del 16/11), step 3, off screen: the boxes Scavo, Falda and Sovraccarico of the page Modello show
/// the phase of the tabs; a value confirmed with Invio reaches the row of the phase and the drawing. Capture caselle-fase; data restored.
/// </summary>
internal static partial class EmbeddedWallViewChecks
{
    private static async Task CheckStageLevels(FrameworkElement view, EmbeddedWallSheetViewModel sheet,
        Func<Task> settle, Action<bool, string> check, Action<string> capture)
    {
        var before = sheet.Data.DeepClone().AsObject();
        TextBox Box(string name) => Visual(view).OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == name);
        var sketch = Visual(view).OfType<EmbeddedWallModelSketch>().Single();
        var right = Box("Scavo a destra della fase"); var water = Box("Falda a sinistra della fase");
        check(right.Text == sheet.InputStage?["right_ground"] && right.Text == "-4" && water.Text == "" && !right.IsReadOnly,
            "Passo 3: le caselle della barra non mostrano la fase scelta.");
        right.Text = "-5";
        right.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(right)!, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
        water.Text = "-1"; water.GetBindingExpression(TextBox.TextProperty)!.UpdateSource(); await settle();
        check(sheet.InputStage?["right_ground"] == "-5" && sheet.InputStage["left_water"] == "-1" && sketch.Model?.RightGround == -5 && sketch.Model.LeftWater == -1,
            "Passo 3: Invio o la conferma della casella non scrivono la fase o non aggiornano il disegno.");
        capture("caselle-fase");
        sheet.InputStage = sheet.InputStages[0]; await settle();
        check(right.Text == "0" && water.Text == "", "Passo 3: le caselle non seguono la scheda della fase.");
        sheet.Reload(before); await settle();
        check(JsonNode.DeepEquals(sheet.Data, before), "Passo 3: dati non ripristinati.");
    }
}
