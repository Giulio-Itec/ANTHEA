using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>Project consistency checks shared by the overview, live status and report.</summary>
public static class ProjectValidation
{
    // Refactoring F2.9-D2: il motore facoltativo della durabilità è uscito dal prodotto con la decisione D3 del 15/10/2026; le prove
    // agganciano il legacy da tests/ANTHEA.Legacy per tutta la chiamata (ConcreteCoverAnalysis, MaterialCover).
    public static string[] Warnings(JsonObject section) => ProjectSharedData.Limitations(section)
        .Concat(CoverChecks(section, null).Where(c => c.Passed != true).Select(c => c.Text)).Distinct().ToArray();
    public sealed record CoverStatus(string Text, bool? Passed);

    public static List<CoverStatus> CoverChecks(JsonObject section) => CoverChecks(section, null);
    public static List<CoverStatus> CoverChecks(JsonObject section, JsonObject? only)
    {
        var result = new List<CoverStatus>();
        foreach (var sheet in CoverSheets(section).Where(s => only is null || ReferenceEquals(s, only)))
            CoverChecks(sheet, CoverMaterials(sheet, section), result);
        return result;
    }

    /// <summary>The sheets of <paramref name="section"/> and its subsections whose cover is checked against Materiali.</summary>
    internal static IEnumerable<JsonObject> CoverSheets(JsonObject section) => ProjectSharedData.SubtreeSheets(section).Where(s =>
        s.S("modulo_id") is "str_palo" or PaloOrizzontale.Module || s.S("modulo_id") == RetainingWall.Module && s["dati"].S("family") == "cantilever");

    /// <summary>The Materiali sheets of <paramref name="sheet"/>: those of the nearest level, from the project down to its section, that has any.</summary>
    internal static JsonObject[] CoverMaterials(JsonObject sheet, JsonObject section)
    {
        var owner = sheet.Parent?.Parent as JsonObject ?? section;
        return ProjectSharedData.Ancestors(owner).Reverse().Append(owner)
            .Select(s => s.Array("fogli").OfType<JsonObject>().Where(f => f.S("modulo_id") == "mat_calcestruzzo").ToArray())
            .FirstOrDefault(m => m.Length > 0) ?? [];
    }

    /// <summary>The checks of the cover of <paramref name="sheet"/> against <paramref name="materials"/>: they depend on these sheets only
    /// (step 3 of docs/sviluppo/progetto-sistema.md keeps them by the content of the sheets, ProjectCoherenceCache).</summary>
    internal static void CoverChecks(JsonObject sheet, JsonObject[] materials, List<CoverStatus> result)
    {
        {
            string name = sheet.S("nome"); var fields = ProjectSharedData.Fields(sheet);
            if (materials.Length == 0) result.Add(new(name + ": minimo del copriferro non verificabile; aggiungere una scheda Materiali alla sezione o a un livello superiore.", null));
            foreach (var material in materials)
            {
                try
                {
                    var mf = ProjectSharedData.Fields(material);
                    var issues = new List<string>();
                    var conflicts = ProjectSharedData.ComparableFields(sheet, material).Where(p =>
                        (p.Source.Key is "esposizione" or "CLS · fck [MPa]" || p.Source.Key.StartsWith("Durabilità · ") || sheet.S("modulo_id") == RetainingWall.Module && p.Source.Key.StartsWith("Scheda CLS · ")) &&
                        !ProjectSharedData.Equal(p.Source.Value, p.Target.Value)).ToArray();
                    if (conflicts.Length > 0) issues.Add("Dati diversi da " + material.S("nome") + ": " +
                        string.Join(", ", conflicts.Select(p => ProjectReportPlan.Label(p.Source.Key))) + "; uniformare per completare la verifica");
                    var state = material["dati"]!.AsObject();
                    double fck = J.Number(mf["CLS · fck [MPa]"].Value) ?? throw new ArgumentException("classe CLS di Materiali non valida");
                    bool wall = sheet.S("modulo_id") == RetainingWall.Module;
                    var input = wall ? RetainingWall.MaterialSectionInput(sheet["dati"]!.AsObject()) : (JsonObject)sheet["dati"]![sheet.S("modulo_id") == "str_palo" ? "input" : "sezione"]!.DeepClone();
                    if (sheet.S("modulo_id") == PaloOrizzontale.Module)
                    { input["shape"] = "Circolare"; input["diameter_mm"] = sheet["dati"]!["generali"].D("diametro") * 1000; }
                    ConcreteCoverResult check;
                    if (wall)
                    {
                        var wallData = (JsonObject)sheet["dati"]!.DeepClone(); RetainingWall.Upgrade(wallData);
                        check = ConcreteCoverAnalysis.CalculateForBars(input.Required("cover_mm"), state, fck,
                            RetainingWall.MaximumBarDiameter(wallData));
                    }
                    else check = ConcreteCoverAnalysis.Calculate(input, state, fck);
                    double materialRequired = check.MaterialMinimum, adopted = check.Adopted;
                    string barDetail = check.MaximumBarDiameter is double diameter ? $", Ø massimo effettivo delle barre {diameter:0.##} mm" : "";
                    if (check.Required > check.MaterialMinimum) barDetail += $", minimo per le barre effettive {check.Required:0.##} mm";
                    if (check.ReinforcementError is not null) issues.Add("Controllo sulle barre effettive non completato: " + check.ReinforcementError);
                    bool? status = check.Passed == false ? false : issues.Count > 0 ? null : true;
                    string outcome = status == false ? "NON RISPETTATO" : status == true ? "RISPETTATO" : "DA VERIFICARE";
                    result.Add(new($"{name}: copriferro adottato {adopted:0.##} mm · minimo da Materiali {materialRequired:0.##} mm — {outcome} ({material.S("nome")}{barDetail})." +
                        (issues.Count > 0 ? " " + string.Join(". ", issues) + "." : ""), status));
                }
                catch (ArgumentException ex) { result.Add(new(name + ": copriferro non verificabile — " + ex.Message + ".", null)); }
            }
        }
    }

}
