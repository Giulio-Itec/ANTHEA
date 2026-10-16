using System.Text.Json.Nodes;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    // Build the destination first, without touching live sheets or their data (decisions in SharedDataCoordinator, F5.2).
    private JsonObject? PreviewProjectMove(JsonObject node, JsonObject destination) => SharedData.PreviewMove(document, node, destination);
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
