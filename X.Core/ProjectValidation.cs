using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>Project consistency checks shared by the overview, live status and report.</summary>
public static class ProjectValidation
{
    public static string[] Warnings(JsonObject section) => ProjectSharedData.Limitations(section)
        .Concat(CoverChecks(section).Where(c => c.Passed != true).Select(c => c.Text)).Distinct().ToArray();
    public sealed record CoverStatus(string Text, bool? Passed);

    public static List<CoverStatus> CoverChecks(JsonObject section, JsonObject? only = null)
    {
        var sheets = ProjectSharedData.SubtreeSheets(section).ToArray();
        var result = new List<CoverStatus>();
        foreach (var sheet in sheets.Where(s => (only is null || ReferenceEquals(s, only)) && s.S("modulo_id") is "str_palo" or PaloOrizzontale.Module))
        {
            string name = sheet.S("nome"); var fields = ProjectSharedData.Fields(sheet);
            var owner = sheet.Parent?.Parent as JsonObject ?? section;
            var materials = ProjectSharedData.Ancestors(owner).Reverse().Append(owner)
                .Select(s => s.Array("fogli").OfType<JsonObject>().Where(f => f.S("modulo_id") == "mat_calcestruzzo").ToArray())
                .FirstOrDefault(m => m.Length > 0) ?? [];
            if (materials.Length == 0) result.Add(new(name + ": minimo del copriferro non verificabile; aggiungere una scheda Materiali alla sezione o a un livello superiore.", null));
            foreach (var material in materials)
            {
                try
                {
                    var mf = ProjectSharedData.Fields(material);
                    var issues = new List<string>();
                    var conflicts = ProjectSharedData.ComparableFields(sheet, material).Where(p =>
                        (p.Source.Key is "esposizione" or "CLS · fck [MPa]" || p.Source.Key.StartsWith("Durabilità · ")) &&
                        !ProjectSharedData.Equal(p.Source.Value, p.Target.Value)).ToArray();
                    if (conflicts.Length > 0) issues.Add("Dati diversi da " + material.S("nome") + ": " +
                        string.Join(", ", conflicts.Select(p => ProjectReportPlan.Label(p.Source.Key))) + "; uniformare per completare la verifica");
                    var state = material["dati"]!.AsObject();
                    double fck = J.Number(mf["CLS · fck [MPa]"].Value) ?? throw new ArgumentException("classe CLS di Materiali non valida");
                    var input = (JsonObject)sheet["dati"]![sheet.S("modulo_id") == "str_palo" ? "input" : "sezione"]!.DeepClone();
                    if (sheet.S("modulo_id") == PaloOrizzontale.Module)
                    { input["shape"] = "Circolare"; input["diameter_mm"] = sheet["dati"]!["generali"].D("diametro") * 1000; }
                    var check = ConcreteCoverAnalysis.Calculate(input, state, fck);
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
        return result;
    }

}
