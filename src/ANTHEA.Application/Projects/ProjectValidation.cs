using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>Project consistency checks shared by the overview, live status and report.</summary>
public static class ProjectValidation
{
    // Refactoring F2.9-D2: il motore facoltativo della durabilità è uscito dal prodotto con la decisione D3 del 15/10/2026; le prove
    // agganciano il legacy da tests/ANTHEA.Legacy per tutta la chiamata (ConcreteCoverAnalysis, MaterialCover).
    public static string[] Warnings(JsonObject section) => ProjectSharedData.Limitations(section)
        .Concat(CoverChecks(section, null).Where(c => c.Passed != true).Select(c => c.Text)).Distinct().ToArray();

    /// <summary>A check of the cover of a sheet against one Materiali (Passed: true «RISPETTATO», false «NON RISPETTATO», null «DA
    /// VERIFICARE» or not verifiable), or the notice that follows it when a nearer level has Materiali that the check ignores (Passed null,
    /// decision P10 of the note of step 6, 16/11/2026: the outcome of the check does not change, the notice says what the nearer one gives).</summary>
    public sealed record CoverStatus(string Text, bool? Passed);

    /// <summary>A level from the project down to the section of a sheet (the project or a section) with its Materiali sheets.</summary>
    internal sealed record CoverLevel(JsonObject Level, JsonObject[] Materials);

    /// <summary>The outcome of one check: the status, the minimum of Materiali (null when not computable) and the short text of the notice.</summary>
    private sealed record CoverOutcome(CoverStatus Status, double? MaterialMinimum, string Summary);

    public static List<CoverStatus> CoverChecks(JsonObject section) => CoverChecks(section, null);
    public static List<CoverStatus> CoverChecks(JsonObject section, JsonObject? only)
    {
        var result = new List<CoverStatus>();
        foreach (var sheet in CoverSheets(section).Where(s => only is null || ReferenceEquals(s, only)))
            CoverChecks(sheet, CoverLevels(sheet, section), result);
        return result;
    }

    /// <summary>The sheets of <paramref name="section"/> and its subsections whose cover is checked against Materiali.</summary>
    internal static IEnumerable<JsonObject> CoverSheets(JsonObject section) => ProjectSharedData.SubtreeSheets(section).Where(s =>
        s.S("modulo_id") is "str_palo" or PaloOrizzontale.Module || s.S("modulo_id") == RetainingWall.Module && s["dati"].S("family") == "cantilever");

    /// <summary>The Materiali sheets that the check of <paramref name="sheet"/> uses: those of the highest level that has any, going from the
    /// project down to the section of the sheet (not the nearest: the guide says «prevale il livello più alto che la definisce»). The
    /// Materiali of the nearer levels are ignored by the check; <see cref="CoverLevels"/> gives them for the notice.</summary>
    internal static JsonObject[] CoverMaterials(JsonObject sheet, JsonObject section) => CoverLevels(sheet, section).FirstOrDefault()?.Materials ?? [];

    /// <summary>The levels from the project down to the section of <paramref name="sheet"/> that have Materiali sheets, the highest first:
    /// the check uses the first one, the others only give the notice of the Materiali ignored.</summary>
    internal static CoverLevel[] CoverLevels(JsonObject sheet, JsonObject section)
    {
        var owner = sheet.Parent?.Parent as JsonObject ?? section;
        return ProjectSharedData.Ancestors(owner).Reverse().Append(owner)
            .Select(level => new CoverLevel(level, level.Array("fogli").OfType<JsonObject>().Where(f => f.S("modulo_id") == "mat_calcestruzzo").ToArray()))
            .Where(level => level.Materials.Length > 0).ToArray();
    }

    /// <summary>«del progetto «nome»» or «della sezione «nome»»: the level of a Materiali in the notice.</summary>
    internal static string LevelLabel(JsonObject level) => ProjectSharedData.Ancestors(level).Any()
        ? "della sezione «" + level.S("nome", "Sezione") + "»" : "del progetto «" + level.S("nome", "Progetto") + "»";

    /// <summary>The checks of the cover of <paramref name="sheet"/> against the Materiali of the first of <paramref name="levels"/>, each
    /// followed by a notice for every Materiali of a nearer level whose outcome or minimum differs: they depend on these sheets and on the
    /// names of their levels only (step 3 of docs/sviluppo/progetto-sistema.md keeps them by the content of the sheets, ProjectCoherenceCache).</summary>
    internal static void CoverChecks(JsonObject sheet, CoverLevel[] levels, List<CoverStatus> result)
    {
        string name = sheet.S("nome"); var fields = ProjectSharedData.Fields(sheet);
        if (levels.Length == 0) { result.Add(new(name + ": minimo del copriferro non verificabile; aggiungere una scheda Materiali alla sezione o a un livello superiore.", null)); return; }
        var nearer = levels.Skip(1).SelectMany(level => level.Materials.Select(material => (level.Level, Material: material, Outcome: CoverChecks(sheet, name, material)))).ToArray();
        foreach (var material in levels[0].Materials)
        {
            var used = CoverChecks(sheet, name, material);
            result.Add(used.Status);
            foreach (var ignored in nearer.Where(n => !Same(n.Outcome, used)))
                result.Add(new($"{name}: il controllo del copriferro usa Materiali «{material.S("nome")}» {LevelLabel(levels[0].Level)}, il livello più alto ({used.Summary}), " +
                    $"e non «{ignored.Material.S("nome")}» {LevelLabel(ignored.Level)}, più vicino al foglio. Con «{ignored.Material.S("nome")}»: {ignored.Outcome.Summary}.", null));
        }
    }

    /// <summary>Same outcome and same minimum of Materiali, to the hundredth of a millimetre shown in the texts.</summary>
    private static bool Same(CoverOutcome a, CoverOutcome b) => a.Status.Passed == b.Status.Passed &&
        (a.MaterialMinimum is double x && b.MaterialMinimum is double y ? Math.Round(x, 2) == Math.Round(y, 2) : a.MaterialMinimum is null && b.MaterialMinimum is null);

    /// <summary>The check of the cover of <paramref name="sheet"/>, named <paramref name="name"/>, against <paramref name="material"/>: the
    /// calculation of today, moved unchanged out of the loop (same method name, so the F4 baseline of the architecture keeps its owner).</summary>
    private static CoverOutcome CoverChecks(JsonObject sheet, string name, JsonObject material)
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
            var state = material["dati"] as JsonObject ?? throw new ArgumentException("dati della scheda Materiali «" + material.S("nome") + "» mancanti o non validi");
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
            return new(new($"{name}: copriferro adottato {adopted:0.##} mm · minimo da Materiali {materialRequired:0.##} mm — {outcome} ({material.S("nome")}{barDetail})." +
                (issues.Count > 0 ? " " + string.Join(". ", issues) + "." : ""), status), materialRequired, $"minimo da Materiali {materialRequired:0.##} mm — {outcome}");
        }
        catch (ArgumentException ex) { return new(new(name + ": copriferro non verificabile — " + ex.Message + ".", null), null, "copriferro non verificabile — " + ex.Message); }
    }
}
