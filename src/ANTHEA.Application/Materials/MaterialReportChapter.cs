using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using X.Core;
namespace Materiali;

public static class MaterialReportChapter
{
    public static ReportProject.SheetContent Create(string module, JsonObject data)
    {
        var derived = new List<ProjectReportPlan.InputValue>();
        if (module == "mat_calcestruzzo")
        {
            foreach (var row in MaterialSheetPresentation.Evaluate(data).ReportProperties())
                derived.Add(new("Calcestruzzo", row.Nome + (row.Simbolo.Length > 0 ? " · " + row.Simbolo : "") + (row.Unita.Length > 0 ? " [" + row.Unita + "]" : ""), JsonValue.Create(row.Valore)));
            return new(Error: derived.Any(v => v.Value?.ToString() == "Da completare") ? "Completare i dati per aderenza o copriferro; vedere i dettagli della scheda." : null, Derived: derived);
        }
        if (module != RebarMaterial.Module) throw new InvalidOperationException("La scheda non è un materiale.");
        try
        {
            var values = ModuleServices.Get(RebarMaterial.Module).Calculate(data).NativeAs<RebarMaterial.Values>();
            derived.Add(new("Acciaio", "Resistenza di progetto fyd [MPa]", JsonValue.Create(values.Fyd)));
            derived.Add(new("Acciaio", "Deformazione εyd [‰]", JsonValue.Create(values.EpsilonYd)));
            derived.Add(new("Acciaio", "Rapporto fu / fyk", JsonValue.Create(values.Ratio)));
            return new(Derived: derived);
        }
        catch (ArgumentException ex) { return new(Error: ex.Message); }
    }
}
