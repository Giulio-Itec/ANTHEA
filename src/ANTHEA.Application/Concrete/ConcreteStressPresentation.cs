using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
namespace Anthea.Application.Concrete;

/// <summary>
/// The scale of the contour of a stress map (Render of the historic ConcreteSectionViewport): the values coloured on the concrete and on
/// the bars, the bounds of the colour scale and the text of its legend. <see cref="Legend"/> is formatted with the current culture.
/// </summary>
public sealed record ConcreteContourScale(bool OnlyBars, bool Ratios, bool Bands, bool Strains, double[] ConcreteValues, double[] BarValues,
    double Lower, double Upper, string Legend);

/// <summary>
/// What the serviceability tab of the RC section shows (tab 04 «Tensioni e fessurazione», F5.10 sub-step S4): the outputs of the rows after
/// a calculation, the detail and the steps of the cracking of the selected combination, the tables of the bars and of the concrete
/// vertices, the options of the analysis that the form enables, and the scale of the stress map. Moved without changes from the historic
/// view (ConcreteStress.cs and the Render of ConcreteSectionViewport), which calls them; no rule of engineering: the values come from the
/// results of the libraries.
/// </summary>
public static class ConcreteStressPresentation
{
    /// <summary>The contour that draws only the geometry, without a scale.</summary>
    public const string GeometryOnly = "Solo geometria";

    /// <summary>The outputs of a row of the actions after the calculation of its family (PublishStress), in the order written.</summary>
    public static IReadOnlyList<(string Key, string Value)> Outputs(StressOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return [("sigma_c", EngineeringFormat.Number(outcome.State?.sigma_cls)), ("sigma_s", EngineeringFormat.Number(outcome.State?.sigma_acciaio)),
            ("eta_sigma", EngineeringFormat.Number(outcome.Ratio)), ("stress_status", outcome.Status),
            ("wk", outcome.CrackResult?.Width is double width
                ? $"{EngineeringFormat.Number(width)} / {EngineeringFormat.Number(outcome.CrackResult.Limit)} mm · η={EngineeringFormat.Number(outcome.CrackResult.Ratio)}"
                : outcome.Cracking)];
    }

    /// <summary>The fields of the analysis enabled by the options of a family: φ and n with the linear analysis, the origin and the rotation
    /// with the custom axes; every other field is enabled.</summary>
    public static bool FieldEnabled(JsonNode options, string field) => field switch
    {
        "phi" or "phi_trefoli" or "n_armature" or "n_trefoli" => options.S("modello") == "Lineare",
        "origine_x" or "origine_y" or "rotazione" => options.S("assi") == "Personalizzati",
        _ => true
    };

    /// <summary>The fields that <see cref="FieldEnabled"/> enables or disables, in the order of the historic view.</summary>
    public static IReadOnlyList<string> SwitchedFields { get; } = ["phi", "phi_trefoli", "n_armature", "n_trefoli", "origine_x", "origine_y", "rotazione"];

    /// <summary>The notice of the non-linear analysis is shown.</summary>
    public static bool NonLinear(JsonNode options) => options.S("modello") == "Non lineare";

    /// <summary>
    /// «Fessurazione · passaggi» of the selected combination: family and name, the summary of the cracking (or why it is missing), then the
    /// effective zones and the complete steps when the library gives them.
    /// </summary>
    public static string CrackSteps(string key, JsonObject? row, StressOutcome? outcome)
    {
        string text = (row is null ? "" : ConcreteEditor.Label(key) + " · " + row.S("nome") + "\n\n") + ConcreteEditor.CrackSummary(outcome?.CrackResult, outcome?.Cracking ?? "Selezionare una combinazione calcolata.");
        if (outcome?.CrackResult is { } crackTrace && crackTrace.Regions.Length > 0)
            text += "\n\nZONE EFFICACI\n" + string.Join("\n", crackTrace.Regions.Select(r => $"{r.Name}: Ac,eff={r.Area:0.###} mm²; As,eff={r.SteelArea:0.###} mm²; wk={r.Width:0.######} mm · "
                + string.Join(", ", r.BarIndices.Select(i => $"B{i + 1:00}")))) + "\n\nPASSAGGI COMPLETI\n" + string.Join("\n\n", crackTrace.Details.Select(d => d.Format()));
        return text;
    }

    /// <summary>
    /// «Riepilogo» of the selected combination: with a stress state the family, the analysis, the action, the stresses and their limits (only
    /// the support of the cracking when the family has no stress check), the state at the applied action, the cracking (when the family checks
    /// it) and the homogenization; otherwise the status of the outcome, or why there is none.
    /// </summary>
    /// <param name="key">SLE, SLE_FREQ or SLE_QP.</param>
    /// <param name="settings">The settings of the sheet (<c>workspace_ca</c>).</param>
    /// <param name="row">The values of the selected row; null without selection.</param>
    /// <param name="outcome">Its outcome, when calculated.</param>
    /// <param name="tendons">The section has cables or tendons.</param>
    public static string Detail(string key, JsonObject settings, JsonObject? row, StressOutcome? outcome, bool tendons)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (outcome?.State is not ConcreteResults.StressState state) return outcome?.Status ?? (row is null ? "Nessuna combinazione selezionata" : "Tensioni da calcolare");
        bool stressRequired = ConcreteEditor.HasStressCheck(key), crackRequired = ConcreteEditor.HasCrackCheck(key, settings);
        var options = settings["sle"]![key]!; var force = ConcreteDomainChart.ReadNamedAction(row!);
        string limits = state.ConcreteStressLimit is double limit ? $"|σc,comp| ≤ {limit:0.00} MPa" : "Nessun limite tensionale automatico";
        if (key == "SLE") limits += state.OrdinarySteelStressLimits.Distinct().Count() > 1
            ? "\nLimiti σs per barra [MPa]: " + string.Join("; ", state.OrdinarySteelStressLimits.Select((v, i) => $"B{i + 1:00}: {v:0.00}"))
            : $"\nσs ≤ {state.SteelStressLimit:0.00} MPa (armatura ordinaria)";
        string text = $"{ConcreteEditor.Label(key)} · {options.S("modello")}\n{row!.S("nome")} · assi {options.S("assi")}\nN = {force.N:0.00} kN\nMx / My = {force.Mx:0.00} / {force.My:0.00} kNm\n\nTENSIONI · NTC 2018\n{limits}\nησ = {outcome.Ratio?.ToString("0.00") ?? "—"}\n{outcome.Status}\n"
            + ConcreteResponsePresentation.ResponseSummary(state.Response, "STATO ALL’AZIONE APPLICATA")
            + $"\n\nFESSURAZIONE\n{options.S("esposizione")} · armatura {options.S("sensibilita").ToLowerInvariant()}\nCarico di durata {options.S("durata").ToLowerInvariant()}\n{outcome.Cracking}\nwk / limite = {outcome.CrackResult?.Width?.ToString("0.00") ?? "—"} / {outcome.CrackResult?.Limit?.ToString("0.00") ?? "—"} mm\nηw = {outcome.CrackResult?.Ratio?.ToString("0.00") ?? "—"}\nAc,eff = {outcome.CrackResult?.EffectiveArea?.ToString("0.00") ?? "—"} mm²\nAs,eff = {outcome.CrackResult?.EffectiveSteel?.ToString("0.00") ?? "—"} mm²";
        text += "\nCoefficienti e formule: scheda «Fessurazione · passaggi» (testo selezionabile e copiabile).";
        text += $"\nInterasse barre tese = {EngineeringFormat.Number(outcome.CrackResult?.BarSpacing)} mm · {outcome.CrackResult?.SpacingSource}";
        if (!crackRequired) text = text.Split("\n\nFESSURAZIONE")[0];
        if (!stressRequired)
        {
            int start = text.IndexOf("\n\nTENSIONI", StringComparison.Ordinal), end = text.IndexOf("\nSTATO ALL’AZIONE APPLICATA", StringComparison.Ordinal);
            if (start >= 0 && end > start) text = text[..start] + "\n\nTensioni calcolate a supporto della fessurazione; verifica tensionale non richiesta.\n" + text[end..];
        }
        text = text.Replace("TENSIONI · NTC 2018", "TENSIONI · " + settings.S("normativa"), StringComparison.Ordinal);
        text += $"\n\nOMOGENEIZZAZIONE\nn armature = {EngineeringFormat.Number(options.D("n_armature"))} · φ = {EngineeringFormat.Number(options.D("phi"))}";
        if (tendons) text += $"\nn trefoli = {EngineeringFormat.Number(options.D("n_trefoli"))} · φp = {EngineeringFormat.Number(options.D("phi_trefoli"))}\nCon Ep diversi, n trefoli è riferito al primo materiale; φp è comune.";
        return text + "\nn = Eacciaio (1 + φ) / Ec. Deformazioni incrementali da GPC Engine.";
    }

    /// <summary>
    /// «Barre e trefoli» of a stress state: the ordinary bars of the section drawn («B01»…), then the cables in the order of the settings
    /// (their identifier, or «T1»…), with stress, strain and state.
    /// </summary>
    /// <param name="ordinaryBars">The bars of the section drawn (the cables follow them in the stresses).</param>
    public static IReadOnlyList<JsonObject> BarRows(ConcreteResults.StressState state, int ordinaryBars, JsonObject settings)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(settings);
        var rows = new List<JsonObject>();
        for (int i = 0; i < state.tensioni_barre.Length; i++)
        {
            double stress = state.tensioni_barre[i];
            string id = i < ordinaryBars ? "B" + (i + 1).ToString("D2") : settings.Array("trefoli").ElementAtOrDefault(i - ordinaryBars).S("id", "T" + (i - ordinaryBars + 1));
            rows.Add(J.Obj(("id", id), ("stress", EngineeringFormat.Number(stress)), ("strain", EngineeringFormat.Number(state.BarStrains.ElementAtOrDefault(i))),
                ("type", stress > 0 ? "Trazione" : stress < 0 ? "Compressione" : "Nullo")));
        }
        return rows;
    }

    /// <summary>«Calcestruzzo» of a stress state: every vertex of the concrete with position, stress and strain.</summary>
    public static IReadOnlyList<JsonObject> ConcreteRows(ConcreteResults.StressState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.ConcreteVertices.Select(p => J.Obj(("id", p.Id), ("x", EngineeringFormat.Number(p.X)), ("y", EngineeringFormat.Number(p.Y)),
            ("stress", EngineeringFormat.Number(p.Stress)), ("strain", EngineeringFormat.Number(p.Strain)))).ToArray();
    }

    /// <summary>
    /// The scale of the contour <paramref name="contour"/> of a stress map; null without a stress state or with «Solo geometria» (no scale,
    /// no legend). The concrete is coloured by its stresses or strains (or by the ratio to its strength), the bars by their stresses (or by
    /// the ratio to theirs); the bounds include zero, or are the strengths of the concrete for «scala resistenza», or 0–1 at least for the
    /// ratios. The legend gives the bounds with their unit, or the colours of the ratios.
    /// </summary>
    public static ConcreteContourScale? ContourScale(ConcreteResults.StressState? stress, string contour)
    {
        ArgumentNullException.ThrowIfNull(contour);
        if (stress is null || contour == GeometryOnly) return null;
        bool onlyBars = contour.StartsWith("Barre", StringComparison.Ordinal), ratios = contour.Contains("tasso", StringComparison.Ordinal), bands = contour.Contains("bande", StringComparison.Ordinal);
        bool strains = contour.Contains("deformazioni", StringComparison.Ordinal), fixedScale = contour.Contains("scala resistenza", StringComparison.Ordinal);
        double[] concreteValues = [], barValues = [];
        if (!onlyBars)
        {
            // Include boundary extrema: internal samples miss the extreme strains at the edges.
            var samples = strains
                ? stress.FiberStrains.Concat(stress.ConcreteVertices.Select(v => v.Strain))
                : stress.FiberStresses.Concat(stress.ConcreteVertices.Select(v => v.Stress));
            concreteValues = (ratios ? samples.Select(v => Math.Abs(v) / Math.Max(1e-12, v < 0 ? stress.ConcreteCompressionStrength : stress.ConcreteTensionStrength)) : samples).ToArray();
        }
        if (onlyBars || ratios) barValues = ratios ? stress.tensioni_barre.Select((v, i) => Math.Abs(v) / Math.Max(1e-12, stress.BarStrengths.ElementAtOrDefault(i))).ToArray() : stress.tensioni_barre;
        var values = concreteValues.Concat(barValues).ToArray();
        double lower = fixedScale ? -stress.ConcreteCompressionStrength : ratios ? 0 : Math.Min(0, values.DefaultIfEmpty(0).Min());
        double upper = fixedScale ? stress.ConcreteTensionStrength : Math.Max(ratios ? 1 : 0, values.DefaultIfEmpty(0).Max());
        if (lower == upper) upper = lower + 1;
        string legend = ratios ? UtilizationColors.Legend : $"{EngineeringFormat.Number(lower)} … {EngineeringFormat.Number(upper)} {(strains ? "‰" : "MPa")}";
        return new(onlyBars, ratios, bands, strains, concreteValues, barValues, lower, upper, legend);
    }
}
