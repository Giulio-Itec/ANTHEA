using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private Func<string[], bool>? projectMoveChoiceForTest;
    // Build the destination first, without touching live sheets or their data.
    private JsonObject? PreviewProjectMove(JsonObject node, JsonObject destination)
    {
        var copy = (JsonObject)document.DeepClone();
        var proposed = ProjectRevisions.Find(copy, node.S("id"))!;
        var target = ProjectRevisions.Find(copy, destination.S("id"))!;
        ((JsonArray)proposed.Parent!).Remove(proposed);
        string list = node.ContainsKey("modulo_id") ? "fogli" : "strutture";
        if (target[list] is not JsonArray) target[list] = new JsonArray(); target.Array(list).Add(proposed);
        var sheets = proposed.ContainsKey("modulo_id") ? new[] { proposed } : ProjectSharedData.SubtreeSheets(proposed).ToArray();
        var changes = new List<string>();
        foreach (var sheet in sheets)
        {
            var before = ProjectSharedData.Fields(sheet);
            ProjectSharedData.InheritHierarchy(sheet, sheet.Parent!.Parent!.AsObject());
            var after = ProjectSharedData.Fields(sheet);
            foreach (var key in before.Keys.Intersect(after.Keys).Where(k => !ProjectSharedData.Equal(before[k].Value, after[k].Value)))
                changes.Add(sheet.S("nome") + " · " + ProjectReportPlan.Label(key) + ": " + ProjectSharedData.Text(before[key].Value) + " → " + ProjectSharedData.Text(after[key].Value));
        }
        if (changes.Count > 0)
        {
            bool accepted;
            if (projectMoveChoiceForTest is not null) accepted = projectMoveChoiceForTest(changes.ToArray());
            else if (testing) accepted = true;
            else
            {
                var panel = Ui.Stack(Ui.Text("Destinazione: " + destination.S("nome"), 18, true),
                    Ui.Text("Lo spostamento aggiorna questi dati comuni:", 14), Ui.Text(string.Join("\n\n", changes), 13)); panel.Margin = new Thickness(18);
                var dialog = Ui.Dialog(this, "Conferma spostamento", new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 730, 550);
                panel.Children.Add(Ui.Bar(Ui.Button("Sposta e aggiorna", () => dialog.DialogResult = true, true), Ui.Button("Annulla", () => dialog.DialogResult = false)));
                accepted = dialog.ShowDialog() == true;
            }
            if (!accepted) return null;
        }
        return proposed;
    }
    private void ReloadMovedEditor(JsonObject node)
    {
        if (currentSheet is null) return;
        bool contains = ReferenceEquals(currentSheet, node) || ProjectSharedData.SubtreeSheets(node).Contains(currentSheet);
        if (!contains) return;
        var active = currentSheet; bool open = ReferenceEquals(projectContent.Content, moduleView);
        editor?.Dispose(); editor = null; currentSheet = null;
        ShowSheet(active);
        if (!open) ShowProjectOverview(active.Parent?.Parent as JsonObject);
    }
    private bool CanNestSection(JsonObject source, JsonObject target) => !projectReadOnly && !source.ContainsKey("modulo_id") &&
        !target.ContainsKey("modulo_id") && ReferenceEquals(source.Root, document) && ReferenceEquals(target.Root, document) &&
        !ReferenceEquals(source.Parent, document["progetti"]) && !ProjectSharedData.Sections(source).Contains(target) && !ReferenceEquals(source.Parent?.Parent, target);
    private void NestProjectSection(JsonObject source, JsonObject target)
    {
        if (!CanNestSection(source, target)) return;
        Commit(); var preview = PreviewProjectMove(source, target); if (preview is null) return;
        if (target["strutture"] is not JsonArray) target["strutture"] = new JsonArray();
        ((JsonArray)source.Parent!).Remove(source); target.Array("strutture").Add(source);
        var data = ProjectSharedData.SubtreeSheets(preview).ToDictionary(s => s.S("id"));
        foreach (var sheet in ProjectSharedData.SubtreeSheets(source)) sheet["dati"] = data[sheet.S("id")]["dati"]!.DeepClone();
        MarkDirty(); ReloadMovedEditor(source); ShowProjectOverview(source);
    }
}
