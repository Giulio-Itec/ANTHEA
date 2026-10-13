using System.Text.Json.Nodes;
using Native = Anthea.Calculations.ModuleCatalog;
namespace Anthea.Application.Modules;

public sealed record ModuleInfo(string Id, string Area, string Name, string Description, string Element, string ProjectTitle, string ProjectSubtitle);

/// <summary>Owned metadata for navigation; validation and defaults have one implementation.</summary>
public static class ModuleDirectory
{
    private static ModuleInfo Copy(Anthea.Calculations.ModuleDefinition m) => new(m.Id, m.Area, m.Name, m.Description, m.Element, m.ProjectTitle, m.ProjectSubtitle);
    public static IReadOnlyList<ModuleInfo> All { get; } = Array.AsReadOnly(Native.All.Select(Copy).ToArray());
    public static ModuleInfo Get(string id) { Native.Get(id); return All.Single(m => m.Id == id); }
    public static JsonObject CreateData(string id)
    {
        var data = Native.CreateData(id);
        return id == "str_palo" ? Anthea.Application.Concrete.SectionActionInputs.Initialize(data) : data;
    }
    public static void ValidateData(string id, JsonObject data) => Native.ValidateData(id, data);
}
