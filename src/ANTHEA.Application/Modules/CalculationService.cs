using System.Text.Json.Nodes;
using Anthea.Application.Modules;

namespace Anthea.Calculations;

/// <summary>Nonvisual calculation entry point. Every branch calls the same engines as the sheet UI.</summary>
public static class CalculationService
{
    public static JsonObject Calculate(string module, JsonObject data, CancellationToken cancellation = default)
        => CalculateModule(module, data, cancellation).Values;

    internal static ModuleCalculation CalculateModule(string module, JsonObject data, CancellationToken cancellation = default,
        Anthea.FoundationWorkspace.FoundationLibrary? foundation = null)
    {
        if (module == Anthea.Application.EmbeddedWalls.EmbeddedWallDocument.Module)
            return new Anthea.Application.EmbeddedWalls.EmbeddedWallModule().Calculate(data, cancellation);
        ModuleCatalog.ValidateData(module, data);
        CalculationValidation.RequireValidCoefficients(module, data);
        cancellation.ThrowIfCancellationRequested();
        // Calculation preparation and legacy migrations must never mutate the user's document.
        var snapshot = (JsonObject)data.DeepClone();
        object? native = null;
        JsonObject Typed<T>(T value, Func<T, JsonObject> project) { native = value; return project(value); }
        var result = module switch
        {
            Anthea.FoundationWorkspace.FoundationDocument.Module => foundation is null ? Anthea.FoundationWorkspace.FoundationDocument.Calculate(snapshot) : foundation.Calculate(snapshot),
            HorizontalPileGroup.Module => HorizontalPileGroup.Calculate(snapshot),
            "geo_palo_verticale" => Calcolo.Calcola(snapshot),
            "geo_micropalo_verticale" => Calcolo.Calcola(snapshot, true),
            PaloOrizzontale.Module or MicropaloOrizzontale.Module => PaloOrizzontale.Calculate(snapshot),
            RetainingWall.Module => Typed(RetainingWall.Calculate(snapshot, cancellation), value => value.Json()),
            BridgeSection.Module => Typed(BridgeSection.Calculate(snapshot, cancellation), value => value.Json()),
            BridgeConcept.Module => Typed(BridgeConcept.Calculate(snapshot), value => value.Json()),
            "str_palo" => ConcreteAnalysis.Calculate(snapshot, cancellation),
            RebarMaterial.Module => Typed(RebarMaterial.Evaluate(snapshot["input"]!.AsObject()), value => J.Obj(("errore", ""), ("materiale", value),
                ("diagramma", RebarMaterial.Curve(snapshot["input"]!.AsObject())))),
            "mat_calcestruzzo" => MaterialProperties(snapshot),
            _ => throw new ArgumentException("Modulo di calcolo non disponibile: " + module)
        };
        cancellation.ThrowIfCancellationRequested();
        return new(module, snapshot, result, native);
    }
    private static JsonObject MaterialProperties(JsonObject data)
    {
        var preset = ConcreteMaterialCatalog.MaterialSheetClasses().SingleOrDefault(m => m.Name == data.S("classe", "C30/37"));
        if (preset.Name is null) throw new ArgumentException("Classe del calcestruzzo non riconosciuta.");
        var material = ConcreteMaterialCatalog.Material(preset.Fck);
        return J.Obj(("errore", ""), ("classe", preset.Name), ("fck_mpa", Math.Abs(material.Fck)),
            ("copriferro_nominale_mm", Materiali.MaterialCover.Required(data, Math.Abs(material.Fck))),
            ("calcoli_inclusi", new[] { "Proprietà del calcestruzzo", "Copriferro nominale" }));
    }
}
