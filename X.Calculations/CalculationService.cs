using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Nonvisual calculation entry point. Every branch calls the same engines as the sheet UI.</summary>
public static class CalculationService
{
    public static JsonObject Calculate(string module, JsonObject data, CancellationToken cancellation = default)
    {
        ModuleCatalog.ValidateData(module, data);
        CalculationValidation.RequireValidCoefficients(module, data);
        cancellation.ThrowIfCancellationRequested();
        // Calculation preparation and legacy migrations must never mutate the user's document.
        var snapshot = (JsonObject)data.DeepClone();
        var result = module switch
        {
            "geo_palo_verticale" => Calcolo.Calcola(snapshot),
            "geo_micropalo_verticale" => Calcolo.Calcola(snapshot, true),
            PaloOrizzontale.Module or MicropaloOrizzontale.Module => PaloOrizzontale.Calculate(snapshot),
            BridgeSection.Module => BridgeSection.Calculate(snapshot, cancellation).Json(),
            "str_palo" => ConcreteAnalysis.Calculate(snapshot, cancellation),
            RebarMaterial.Module => J.Obj(("errore", ""), ("materiale", RebarMaterial.Evaluate(snapshot["input"]!.AsObject())),
                ("diagramma", RebarMaterial.Curve(snapshot["input"]!.AsObject()))),
            "mat_calcestruzzo" => MaterialProperties(snapshot),
            _ => throw new ArgumentException("Modulo di calcolo non disponibile: " + module)
        };
        cancellation.ThrowIfCancellationRequested();
        return result;
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
