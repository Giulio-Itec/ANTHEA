using System.Text.Json.Nodes;
using GPC.Design.Sections;

namespace Anthea.Calculations;

public static partial class ConcreteReinforcementDesign
{
    internal static void AddRatio(List<ConcreteDesignCheck> checks, string key, string combination, double? value,
        ConcreteDesignOptions o, string detail)
    {
        var check=GPC.Design.Sections.SectionDesignCheck.FromRatio(key,Targets.Single(t=>t.Key==key).Name,combination,value,o.Target(key),detail);
        checks.Add(DisplayCheck(check with {Detail=detail+(check.Ratio.HasValue?"":" · Esito numerico mancante: proposta esclusa.")}));
    }
    internal static IReadOnlyList<ConcreteDesignCheck> LongitudinalChecks(JsonObject input, JsonObject workspace,
        Dictionary<string, JsonObject[]> rows, bool torsion, ConcreteDesignOptions options, CancellationToken token)
    {
        var checks = new List<ConcreteDesignCheck>();
        var full = CheckerSection.PrepareModel(input, workspace);
        var reduced = input;
        if (torsion)
        {
            reduced = (JsonObject)input.DeepClone();
            // Reserve the same area fraction of EVERY bar, rather than double-counting total As.
            double factor = GPC.Design.Sections.SectionDesignQuantities.ReservedDiameterFactor(options.TorsionReservePercent);
            reduced["barre_manuali"] = new JsonArray(full.Geometry.Bars.Select(b => (JsonNode)J.Obj(("x", b.X), ("y", b.Y), ("phi", b.Diametro * factor))).ToArray());
        }
        var resistanceModel = torsion ? CheckerSection.PrepareModel(reduced, workspace) : full;
        foreach (string set in new[] { "SLU", "SLV" })
        {
            if (rows[set].Length == 0) continue;
            var o = (JsonObject)workspace["dominio3d"]!.DeepClone(); o["strategia"] = "Iterativo";
            // One radial N–Mx–My check covers combined and pure axial actions, without a mesh.
            o["criterio"] = "Eccentricità costante";
            var engine = new CheckerSection(resistanceModel, reduced, workspace, o, set);
            var localOptions = (JsonObject)o.DeepClone(); localOptions["assi"] = "Locali";
            var localEngine = o.S("assi", "Locali") == "Locali" || !rows[set].Any(r => r.B("__locali")) ? engine
                : new CheckerSection(resistanceModel, reduced, workspace, localOptions, set);
            foreach (var row in rows[set])
            {
                token.ThrowIfCancellationRequested(); var action = SectionWorkspace.ReadAction(row);
                var solver = row.B("__locali") ? localEngine : engine;
                double? ratio = null;
                if (action.Length == 0) ratio = 0;
                else
                {
                    var force = solver.Force(action);
                    // Punto dall'adattatore SLU, motore della libreria (revisione di architettura R2, registro F2-17).
                    var point = ConcreteResistanceAdapter.Points(solver, [force], token)[0].Point;
                    if (point is not null)
                    {
                        double value = point.WorkingRatio(CheckerSection.Criterion("Eccentricità costante"));
                        var resistance = CheckerSection.Point(point);
                        if (double.IsFinite(value) && value >= 0 && double.IsFinite(resistance.N + resistance.Mx + resistance.My)) ratio = value;
                    }
                }
                AddRatio(checks, set.ToLowerInvariant(), row.S("nome"), ratio, options,
                    $"Iterativo 3D a eccentricità costante, assi {(row.B("__locali") ? "Locali" : o.S("assi", "Locali"))}. N={action.N:0.###} kN; Mx={action.Mx:0.###}, My={action.My:0.###} kNm." +
                    (torsion ? $" Area longitudinale resistente ridotta del {options.TorsionReservePercent:0.#}% per la torsione." : ""));
                if (checks[^1].Passed == false) return checks;
            }
        }
        if (checks.Any(c => c.Passed == false)) return checks;
        foreach (string set in SectionWorkspace.Sets.Skip(2))
        {
            if (rows[set].Length == 0) continue;
            var o = workspace["sle"]![set]!.AsObject();
            var engine = new CheckerSection(full, input, workspace, o, "SLU");
            foreach (var row in rows[set])
            {
                token.ThrowIfCancellationRequested(); var action = SectionWorkspace.ReadAction(row);
                var s = engine.Stress(action, set);
                if (set is "SLE" or "SLE_QP")
                    AddRatio(checks, set == "SLE" ? "sigma_c_rara" : "sigma_c_qp", row.S("nome"), SectionDesignDemand.Ratio(SectionDesignDemand.Compression(s.sigma_cls), s.ConcreteStressLimit), options,
                        $"|σc,compressione| = {SectionDesignDemand.Compression(s.sigma_cls):0.###} MPa; limite = {s.ConcreteStressLimit:0.###} MPa.");
                if (set == "SLE") AddRatio(checks, "sigma_s_rara", row.S("nome"), SectionDesignDemand.Ratio(s.sigma_acciaio, s.SteelStressLimit), options,
                    $"|σs|max = {s.sigma_acciaio:0.###} MPa; limite = {s.SteelStressLimit:0.###} MPa.");
                if (checks.Any(c => c.Passed == false)) return checks;
                if (set != "SLE")
                {
                    var r = ConcreteServiceabilityAdapter.Cracking(engine, s, action, input, workspace, o, set);
                    string key = set == "SLE_FREQ" ? "wk_freq" : "wk_qp";
                    if (r.Ratio is not null) AddRatio(checks, key, row.S("nome"), r.Ratio, options,
                        $"wk = {r.Width:0.####} mm; wlim = {r.Limit:0.###} mm. {r.Status}");
                    else if (r.Passed is not null) checks.Add(new(key, Targets.Single(t => t.Key == key).Name, row.S("nome"), null, options.Target(key), r.Passed,
                        r.Status + " · Criterio non esprimibile come wk/wlim: requisito assoluto, soglia η non applicabile."));
                    else AddRatio(checks, key, row.S("nome"), null, options, r.Status);
                }
                if (checks.Any(c => c.Passed == false)) return checks;
            }
        }
        return checks;
    }

    internal static void AnchorageChecks(JsonObject input, JsonObject workspace, ConcreteDesignOptions options, List<ConcreteDesignCheck> checks)
    {
        var anchor = workspace["ancoraggi"]!.AsObject();
        bool available = !string.IsNullOrWhiteSpace(anchor.S("lunghezza"));
        var copy = (JsonObject)workspace.DeepClone(); var a = copy["ancoraggi"]!.AsObject();
        // Design every actual bar at fyd. A single explicit diameter in the verification tab must not hide larger bars.
        a["sigma"] = "";
        if (!available) a["lunghezza"] = "0";
        var diameters = new SezioneCA(input).Bars.Select(b => b.Diametro);
        if (workspace["dettagli_costruttivi"].D("diametro_secondaria") > 0)
            diameters = diameters.Append(workspace["dettagli_costruttivi"].D("diametro_secondaria"));
        foreach (double phi in diameters.Distinct().Order())
        {
            a["diametro"] = phi; var r = ConcreteDetailingAnalysis.Anchorage(input, copy).Check;
            string detail = $"Ø{phi:0.#}; l richiesta = {r.RequiredLength:0.#} mm; lb,rqd = {r.BasicLength:0.#} mm; fbd = {r.Fbd:0.###} MPa. σsd = fyd. {r.Expression}";
            if (available) AddRatio(checks, "ancoraggio", "Ø" + phi, SectionDesignDemand.Ratio(r.RequiredLength, anchor.D("lunghezza")), options, detail);
            else checks.Add(new("ancoraggio", "Ancoraggio / giunzione Ø" + phi, "Dettaglio esecutivo", null, options.Target("ancoraggio"), null,
                detail + $" Lunghezza da predisporre per η obiettivo: {SectionDesignDemand.RequiredCapacity(r.RequiredLength, options.Target("ancoraggio")):0.#} mm. Inserire la lunghezza disponibile nei dettagli costruttivi."));
            if (a.S("tipo").StartsWith("Sovrapposizione")) checks.Add(new("giunzione", "Interferro giunzione", "Ø" + phi, null, 1, r.LapClearDistancePassed, $"Interferro massimo {r.MaximumLapClearDistance:0.#} mm."));
        }
        foreach (string key in new[] { "confinamento", "posizione", "cautele" })
        {
            if (key == "posizione" && !a.S("tipo").StartsWith("Sovrapposizione") || key == "cautele" && diameters.Max() <= 32) continue;
            // Changing the arrangement invalidates favourable manual confirmations on the old layout.
            bool? passed = anchor.S(key) == "Non conforme" ? false : null;
            checks.Add(new("esecutivo", "Ancoraggi · " + key, "Nuova disposizione", null, 1, passed,
                "Riscontro sul disegno della configurazione proposta; le conferme del dettaglio precedente non vengono trasferite."));
        }
    }

    internal static void CheckTorsionDistribution(SezioneCA section, TorsionResult torsion, ConcreteDesignOptions options,
        List<ConcreteDesignCheck> checks, string combination)
        => CheckTorsionDistributionNative(section, torsion, options, checks, combination);

    public static ConcreteDesignMetrics Metrics(SezioneCA section, JsonObject shear, JsonObject detailing, ConcreteDesignOptions options)
        => MetricsNative(section, shear, detailing, options);

    internal static void SelectSecondary(SezioneCA section, JsonObject shear, JsonObject detailing,
        IReadOnlyList<DetailingCheck> checks, ConcreteDesignOptions options)
        => SelectSecondaryNative(section, shear, detailing, checks, options);
}
