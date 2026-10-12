using System.Text.Json.Nodes;
namespace Anthea.Application.Modules;

public static class ModuleServices
{
    // Identities come from the existing catalog; this is not a second module catalog.
    private static readonly IReadOnlyDictionary<string, IModuleService> Services = ModuleCatalog.All
        .ToDictionary(m => m.Id, m => (IModuleService)new CalculationModule(m.Id), StringComparer.Ordinal);
    public static IModuleService Get(string module) => Services.TryGetValue(module, out var service) ? service
        : throw new ArgumentException("Modulo di calcolo non disponibile: " + module, nameof(module));

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
        {
            cancellation.ThrowIfCancellationRequested();
            var snapshot = Prepare(source);
            object? native = null;
            JsonObject result;
            // Preserve typed outcomes needed for presentation without evaluating an engine twice.
            if (module == BridgeSection.Module) { var value = BridgeSection.Calculate(snapshot, cancellation); native = value; result = value.Json(); }
            else if (module == RetainingWall.Module) { var value = RetainingWall.Calculate(snapshot, cancellation); native = value; result = value.Json(); }
            else if (module == BridgeConcept.Module) { var value = BridgeConcept.Calculate(snapshot); native = value; result = value.Json(); }
            else if (module == RebarMaterial.Module)
            {
                var value = RebarMaterial.Evaluate(snapshot["input"]!.AsObject()); native = value;
                result = J.Obj(("errore", ""), ("materiale", value), ("diagramma", RebarMaterial.Curve(snapshot["input"]!.AsObject())));
            }
            else if (foundation is not null) result = foundation.Calculate(snapshot);
            else result = CalculationService.Calculate(module, snapshot, cancellation);
            cancellation.ThrowIfCancellationRequested();
            return new(module, snapshot, result, native);
        }
    }
}
