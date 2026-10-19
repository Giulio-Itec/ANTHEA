using System.Text.Json.Nodes;
namespace Anthea.Application.Modules;

public static class ModuleServices
{
    // Identities come from the existing catalog; this is not a second module catalog.
    private static readonly IReadOnlyDictionary<string, IModuleService> Services = ModuleCatalog.All
        .ToDictionary(m => m.Id, m => (IModuleService)new CalculationModule(m.Id), StringComparer.Ordinal);
    public static IModuleService Get(string module) => module == Actions.SpectraDocument.Module ? new Actions.SpectraModuleService()
        : module == EmbeddedWalls.EmbeddedWallDocument.Module ? new EmbeddedWalls.EmbeddedWallModule()
        : module == Stratigraphy.StratigraphyDocument.Module ? new StratigraphyModule() : Services.TryGetValue(module, out var service) ? service
        : throw new ArgumentException("Modulo di calcolo non disponibile: " + module, nameof(module));

    private sealed class StratigraphyModule : IModuleService
    {
        public string ModuleId => Stratigraphy.StratigraphyDocument.Module;
        public JsonObject Prepare(JsonObject source) => Stratigraphy.StratigraphyDocument.Snapshot(source).Read();
        public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
            => throw new NotSupportedException("La stratigrafia gestisce dati; non esegue verifiche geotecniche.");
    }

    public static IModuleService ForFoundation(Anthea.FoundationWorkspace.FoundationLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        return new CalculationModule(Anthea.FoundationWorkspace.FoundationDocument.Module, library);
    }

    private sealed class CalculationModule(string module, Anthea.FoundationWorkspace.FoundationLibrary? foundation = null) : IModuleService
    {
        public string ModuleId => module;
        public JsonObject Prepare(JsonObject source)
        {
            ArgumentNullException.ThrowIfNull(source);
            ModuleCatalog.ValidateData(module, source);
            // Individual engines perform their established lazy migrations, on this owned copy only.
            return (JsonObject)source.DeepClone();
        }
        public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
            => CalculationService.CalculateModule(module, source, cancellation, foundation: foundation);
    }
}
