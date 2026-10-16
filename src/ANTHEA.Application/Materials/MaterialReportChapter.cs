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

    /// <summary>
    /// Report of one material sheet, as the sheet exports it: the report of a section with only this sheet, in the form of the
    /// material sheet, with the chapter of <see cref="Create"/>. Moved without changes from SheetEditor.ExportReport in F5.5, so that
    /// the historic view and the ViewModel compose the same document; the report code does not change (reports frozen on 8/10).
    /// </summary>
    public static Anthea.Reports.DocxDocument Document(string module, string title, JsonObject data)
    {
        var (plan, sheet) = Plan(module, title, data);
        return ReportProject.Compose(plan, new Dictionary<JsonObject, ReportProject.SheetContent> { [sheet] = Create(module, data) }, [], materialSheet: true);
    }

    /// <summary>Writes the report of <see cref="Document"/> as DOCX and PDF (<see cref="ReportProject.Write"/>).</summary>
    public static void Write(string path, string module, string title, JsonObject data)
    {
        var (plan, sheet) = Plan(module, title, data);
        ReportProject.Write(path, plan, new Dictionary<JsonObject, ReportProject.SheetContent> { [sheet] = Create(module, data) }, [], materialSheet: true);
    }

    private static (ProjectReportPlan Plan, JsonObject Sheet) Plan(string module, string title, JsonObject data)
    {
        var sheet = J.Obj(("nome", title), ("modulo_id", module), ("dati", data.DeepClone()));
        var section = J.Obj(("nome", title), ("fogli", new JsonArray()), ("strutture", new JsonArray()));
        section.Array("fogli").Add(sheet);
        return (new ProjectReportPlan(section), sheet);
    }
}
