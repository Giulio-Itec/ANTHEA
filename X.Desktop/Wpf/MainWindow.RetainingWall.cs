using System.Text.Json.Nodes;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal void OpenWallConcreteSection(JsonObject data, string name)
        => OpenModuleCopy("str_palo", data, name);
    internal void OpenModuleCopy(string module, JsonObject data, string name)
    {
        ModuleCatalog.ValidateData(module, data);
        Commit();
        if (!projectReadOnly && document.S("tipo") == "progetti" && currentSheet?.Parent?.Parent is JsonObject section)
        {
            var sheet = ProjectDocuments.AddSheet(section, module, ProjectDocuments.NextName(section.Array("fogli"), name));
            sheet["dati"] = data.DeepClone(); MarkDirty(); RefreshTree(sheet); ShowSheet(sheet); return;
        }
        // A standalone wall stays open; its unsaved data are not discarded by a module switch.
        var copy = new MainWindow(serviceFactory) { document = Archivio.Documento(module), dirty = true };
        copy.document["dati"] = data.DeepClone(); copy.document["nome"] = name;
        copy.ShowSheet(copy.document); copy.UpdateTitle(); copy.Show();
    }
    internal void ReplaceCurrentSoil(JsonObject data)
    {
        if (projectReadOnly || currentSheet is null || editor is null) return;
        ModuleCatalog.ValidateData(editor.Module, data); currentSheet["dati"] = data.DeepClone();
        var sheet = currentSheet; currentSheet = null; ShowSheet(sheet); MarkDirty();
    }
}
