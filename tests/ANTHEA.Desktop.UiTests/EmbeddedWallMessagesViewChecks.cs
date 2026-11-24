using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.EmbeddedWalls;

namespace X.Desktop;

/// <summary>
/// Organizzazione del modulo paratie (progetto del 16/11), step 11, off screen: the panel Messaggi at the bottom of the documents shows the lines of
/// the sheet (notices of the data drawn, then the request and the results of a calculation) with time, level in words, origin and element; a line
/// opens its row; filter, «Pulisci» and the button that hides the list are state of the view. Captures messaggi-*; data restored afterwards.
/// </summary>
internal static partial class EmbeddedWallViewChecks
{
    private static async Task CheckMessagesPanel(FrameworkElement view, EmbeddedWallSheetViewModel sheet,
        Func<Task> settle, Action<bool, string> check, Action<string> capture)
    {
        var before = sheet.Data.DeepClone().AsObject();
        var panel = Visual(view).OfType<Border>().Single(b => b.Name == "MessagesPanel");
        var list = Visual(view).OfType<ItemsControl>().Single(i => i.Name == "MessageList");
        check(panel.IsVisible && list.Items.Count == sheet.Messages.Items.Count, "Passo 11: pannello dei messaggi assente o non legato al foglio.");
        // A notice of the data drawn: an invalid level of the water of the phase.
        var stage = sheet.InputStage!;
        stage["right_water"] = "abc"; await settle();
        var water = sheet.Messages.Items.Single(m => m.Text.Contains("Falda a destra"));
        var rows = Visual(list).OfType<Button>().Where(b => b.DataContext is EmbeddedWallMessage).ToArray();
        var row = rows.Single(b => ReferenceEquals(b.DataContext, water));
        check(row.IsVisible && Visual(row).OfType<TextBlock>().Any(t => t.Text == "Errore") && Visual(row).OfType<TextBlock>().Any(t => t.Text == water.Place),
            "Passo 11: la riga non mostra livello in parole ed elemento.");
        capture("messaggi");
        sheet.EditModelCommand.Execute("general"); await settle();
        row.Command!.Execute(row.CommandParameter); await settle();
        check(sheet.InspectorTable?.Key == "stages" && ReferenceEquals(sheet.InspectorTable.Selected, stage) && sheet.Documents.Active == EmbeddedWallDocuments.Section,
            "Passo 11: la riga del messaggio non apre la riga della fase.");
        var filter = Visual(view).OfType<ComboBox>().Single(c => c.Name == "MessageFilter");
        filter.SelectedItem = EmbeddedWallMessages.ErrorsOnly; await settle();
        check(sheet.Messages.Filter == EmbeddedWallMessages.ErrorsOnly && list.Items.Count == sheet.Messages.Items.Count && sheet.Messages.Items.All(m => m.IsError),
            "Passo 11: il filtro della vista non arriva al pannello.");
        capture("messaggi-filtro");
        filter.SelectedItem = EmbeddedWallMessages.AllLevels; await settle();
        Appearance.Set(AppAppearance.Dark, false); await settle(); capture("messaggi-scuro");
        Appearance.Set(AppAppearance.Light, false); await settle();
        sheet.Messages.ToggleCommand.Execute(null); await settle();
        check(!list.IsVisible && panel.IsVisible, "Passo 11: l'elenco dei messaggi non si richiude.");
        capture("messaggi-richiuso");
        sheet.Messages.ToggleCommand.Execute(null); await settle();
        // A calculation: the request and the results on screen enter the panel; the message of the data drawn stays.
        sheet.Reload(before); await settle();
        check(await sheet.CalculateAsync() && sheet.Current is not null, "Passo 11: calcolo non pubblicato.");
        await settle();
        check(sheet.Messages.Items.Any(m => m.Origin == EmbeddedWallMessages.Calculation && m.Text.StartsWith("Calcolo terminato"))
            && sheet.Messages.Items.Any(m => m.Text == sheet.Current!.Diagnostic) && list.Items.Count == sheet.Messages.Items.Count,
            "Passo 11: richiesta o risultati assenti dal pannello: " + string.Join(" | ", sheet.Messages.Items.Select(m => m.Text)));
        capture("messaggi-calcolo");
        sheet.Messages.ClearCommand.Execute(null); await settle();
        check(list.Items.Count == 0 && sheet.Messages.All.Count > 0, "Passo 11: «Pulisci» non svuota la vista.");
        sheet.Reload(before); await settle();
        check(JsonNode.DeepEquals(sheet.Data, before), "Passo 11: il pannello lascia i dati modificati.");
    }
}
