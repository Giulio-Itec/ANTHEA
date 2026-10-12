using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>Creation rules shared by the UI and nonvisual project workflows.</summary>
public static class ProjectDocuments
{
    public static JsonObject CreateArchive() => J.Obj(("formato", "X"), ("versione", 1),
        ("tipo", "progetti"), ("progetti", new JsonArray()));

    public static string NextName(JsonArray siblings, string prefix)
    {
        var names = siblings.OfType<JsonObject>().Select(s => s.S("nome")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (int index = 1; ; index++) if (!names.Contains(prefix + " " + index)) return prefix + " " + index;
    }
    private static string Name(string name) => !string.IsNullOrWhiteSpace(name) ? name.Trim()
        : throw new ArgumentException("Inserire un nome per l’elemento del progetto.");
    public static void Rename(JsonObject node, string name) => node["nome"] = Name(name);
    private static JsonObject Container(string name) => J.Obj(("id", Guid.NewGuid().ToString("N")),
        ("nome", Name(name)), ("strutture", new JsonArray()), ("fogli", new JsonArray()));
    private static void ValidateContainer(JsonObject parent)
    {
        if (parent.ContainsKey("modulo_id") || parent["strutture"] is not JsonArray ||
            parent.ContainsKey("fogli") && parent["fogli"] is not JsonArray)
            throw new ArgumentException("Selezionare un progetto o una sezione valida.");
    }
    public static JsonObject AddProject(JsonObject document, string? name = null)
    {
        if (document.S("tipo") != "progetti" || document["progetti"] is not JsonArray projects)
            throw new ArgumentException("Creare o aprire un archivio progetti.");
        var project = Container(name ?? NextName(projects, "Progetto"));
        projects.Add(project); return project;
    }
    public static JsonObject AddSection(JsonObject parent, string? name = null)
    {
        ValidateContainer(parent);
        var sections = parent["strutture"]!.AsArray();
        var section = Container(name ?? NextName(sections, "Sezione"));
        sections.Add(section); return section;
    }
    public static JsonObject AddSheet(JsonObject parent, string module, string? name = null)
    {
        ValidateContainer(parent);
        var definition = ModuleCatalog.Get(module);
        bool hadSheets = parent.ContainsKey("fogli");
        var sheets = parent["fogli"] as JsonArray ?? new JsonArray();
        var sheet = J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", Name(name ?? NextName(sheets, definition.Name))),
            ("modulo_id", module), ("dati", ModuleCatalog.CreateData(module)));
        // Inheritance needs the real ancestry; roll back the insertion if preparation fails.
        if (!hadSheets) parent["fogli"] = sheets;
        sheets.Add(sheet);
        try
        {
            ProjectSharedData.InheritHierarchy(sheet, parent);
            ModuleCatalog.ValidateData(module, sheet["dati"]!.AsObject());
            return sheet;
        }
        catch
        {
            sheets.Remove(sheet);
            if (!hadSheets) parent.Remove("fogli");
            throw;
        }
    }
}
