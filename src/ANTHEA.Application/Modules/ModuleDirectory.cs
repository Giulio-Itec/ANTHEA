using System.Text.Json.Nodes;
using Native = Anthea.Calculations.ModuleCatalog;
namespace Anthea.Application.Modules;

public sealed record ModuleInfo(string Id, string Area, string Name, string Description, string Element, string ProjectTitle, string ProjectSubtitle);

/// <summary>Owned metadata for navigation; validation and defaults have one implementation.</summary>
public static class ModuleDirectory
{
    private static ModuleInfo Copy(Anthea.Calculations.ModuleDefinition m) => new(m.Id, m.Area, m.Name, m.Description, m.Element, m.ProjectTitle, m.ProjectSubtitle);
    public static IReadOnlyList<ModuleInfo> All { get; } = Array.AsReadOnly(Native.All.Select(Copy).Append(
        new ModuleInfo(Stratigraphy.StratigraphyDocument.Module, "Geotecnica", "Stratigrafia del terreno",
            "Strati, parametri e falda", "Stratigrafia", "Stratigrafia del terreno", "Dati geotecnici condivisibili"))
        .Append(new ModuleInfo(Actions.SpectraDocument.Module, "Azioni", "Spettri NTC e isolamento sismico", "Pericolosità, spettri orizzontali e verticali, isolamento", "Spettro", "Spettri NTC", "Azioni sismiche"))
        .Append(new ModuleInfo(EmbeddedWalls.EmbeddedWallDocument.Module, "Geotecnica", "Paratie", "Analisi per fasi di paratie e sostegni", "Paratia", "Paratie", "Terreno, fasi e sostegni")).Concat(Actions.ClimateDocument.Modules).ToArray());
    public static ModuleInfo Get(string id) => All.FirstOrDefault(m => m.Id == id)
        ?? throw new ArgumentException("Modulo del foglio non disponibile: " + id);
    public static JsonObject CreateData(string id)
    {
        if(Actions.ClimateDocument.Supports(id))return Actions.ClimateDocument.Create(id);
        if(id==Actions.SpectraDocument.Module)return Actions.SpectraDocument.Create();
        if (id == Stratigraphy.StratigraphyDocument.Module) return Stratigraphy.StratigraphyDocument.Create();
        if (id == EmbeddedWalls.EmbeddedWallDocument.Module) return EmbeddedWalls.EmbeddedWallDocument.Create();
        var data = Native.CreateData(id);
        return id == "str_palo" ? Anthea.Application.Concrete.SectionActionInputs.Initialize(data) : data;
    }
    public static void ValidateData(string id, JsonObject data)
    {
        if (Actions.ClimateDocument.Supports(id)) Actions.ClimateDocument.Validate(id,data);
        else if (id == Actions.SpectraDocument.Module) Actions.SpectraDocument.Validate(data);
        else if (id == Stratigraphy.StratigraphyDocument.Module) Stratigraphy.StratigraphyDocument.Validate(data);
        else if (id == EmbeddedWalls.EmbeddedWallDocument.Module) EmbeddedWalls.EmbeddedWallDocument.Validate(data);
        else Native.ValidateData(id, data);
    }
}
