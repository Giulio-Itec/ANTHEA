using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class ConcreteReinforcementDesign
{
    private static void AddRatio(List<ConcreteDesignCheck> checks, string key, string combination, double? value,
        ConcreteDesignOptions o, string detail)
    {
        bool finite = value is double v && double.IsFinite(v) && v >= 0;
        checks.Add(new(key, Targets.Single(t => t.Key == key).Name, combination, finite ? value : null, o.Target(key),
            finite && value <= o.Target(key) + 1e-10, detail + (finite ? "" : " · Esito numerico mancante: proposta esclusa.")));
    }
    private static IReadOnlyList<ConcreteDesignCheck> LongitudinalChecks(JsonObject input, JsonObject workspace,
        Dictionary<string, JsonObject[]> rows, bool torsion, ConcreteDesignOptions options, CancellationToken token)
    {
        var checks = new List<ConcreteDesignCheck>();
        var full = CheckerSection.PrepareModel(input, workspace);
        var reduced = input;
        if (torsion)
        {
            reduced = (JsonObject)input.DeepClone();
            // Reserve the same area fraction of EVERY bar, rather than double-counting total As.
            double factor = Math.Sqrt(1 - options.TorsionReservePercent / 100);
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
                token.ThrowIfCancellationRequested(); var action = ConcreteAnalysisSession.ReadAction(row);
                var solver = row.B("__locali") ? localEngine : engine;
                double? ratio = null;
                if (action.Length == 0) ratio = 0;
                else
                {
                    var force = solver.Force(action);
                    var point = solver.Checker.SectionSolver.CalculateDomainPoint([force])[0];
                    if (point is not null)
                    {
                        double value = point.CalculateWorkingRatio(CheckerSection.Criterion("Eccentricità costante"), force, 1e6, 1000);
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
            var engine = new CheckerSection(full, input, workspace, o, "SLU", options.ServiceabilityEngine);
            foreach (var row in rows[set])
            {
                token.ThrowIfCancellationRequested(); var action = ConcreteAnalysisSession.ReadAction(row);
                var s = engine.Stress(action, set);
                if (set is "SLE" or "SLE_QP")
                    AddRatio(checks, set == "SLE" ? "sigma_c_rara" : "sigma_c_qp", row.S("nome"), s.ConcreteStressLimit is > 0 ? Math.Max(0, -s.sigma_cls) / s.ConcreteStressLimit : null, options,
                        $"|σc,compressione| = {Math.Max(0, -s.sigma_cls):0.###} MPa; limite = {s.ConcreteStressLimit:0.###} MPa.");
                if (set == "SLE") AddRatio(checks, "sigma_s_rara", row.S("nome"), s.SteelStressLimit > 0 ? s.sigma_acciaio / s.SteelStressLimit : null, options,
                    $"|σs|max = {s.sigma_acciaio:0.###} MPa; limite = {s.SteelStressLimit:0.###} MPa.");
                if (checks.Any(c => c.Passed == false)) return checks;
                if (set != "SLE")
                {
                    var r = ConcreteServiceabilityAdapter.Cracking(engine, s, action, input, workspace, o, set, options.ServiceabilityEngine);
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

    private static void AnchorageChecks(JsonObject input, JsonObject workspace, ConcreteDesignOptions options, List<ConcreteDesignCheck> checks)
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
            if (available) AddRatio(checks, "ancoraggio", "Ø" + phi, anchor.D("lunghezza") > 0 ? r.RequiredLength / anchor.D("lunghezza") : null, options, detail);
            else checks.Add(new("ancoraggio", "Ancoraggio / giunzione Ø" + phi, "Dettaglio esecutivo", null, options.Target("ancoraggio"), null,
                detail + $" Lunghezza da predisporre per η obiettivo: {r.RequiredLength / options.Target("ancoraggio"):0.#} mm. Inserire la lunghezza disponibile nei dettagli costruttivi."));
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

    private static void CheckTorsionDistribution(SezioneCA section, TorsionResult torsion, ConcreteDesignOptions options,
        List<ConcreteDesignCheck> checks, string combination)
    {
        // Tributary perimeter for single-ring/peripheral layouts; no internal bars are generated by this search.
        var bars = section.Bars.OrderBy(b => Math.Atan2(b.Y, b.X)).ToArray();
        var lengths = bars.Select((b, i) => double.Hypot(b.X - bars[(i + 1) % bars.Length].X, b.Y - bars[(i + 1) % bars.Length].Y)).ToArray();
        double perimeter = lengths.Sum(); double worst = 0;
        for (int i = 0; i < bars.Length; i++)
        {
            double required = torsion.RequiredLongitudinalArea * (lengths[(i + bars.Length - 1) % bars.Length] + lengths[i]) / (2 * perimeter);
            worst = Math.Max(worst, required / (bars[i].Area * options.TorsionReservePercent / 100));
        }
        AddRatio(checks, "torsione", combination + " · distribuzione longitudinale", worst, options,
            "As,T ripartita per lunghezza tributaria fra barre consecutive sul perimetro; verifica della quota riservata su ciascuna barra.");
    }

    public static ConcreteDesignMetrics Metrics(SezioneCA section, JsonObject shear, JsonObject detailing, ConcreteDesignOptions options)
    {
        var input = section.Input; var diameters = section.Bars.Select(b => b.Diametro).ToList();
        double weight = 0, pieces = section.Bars.Count;
        if (input.S("staffe_presenti", "Sì") == "Sì")
        {
            double phi = input.Required("transverse_bar_diameter_mm", strict: true), spacing = input.Required("transverse_spacing_mm", strict: true);
            double c = input.D("cover_mm") + phi / 2, width = section.Width - 2 * c, height = section.Height - 2 * c;
            double extra = section.Shape == "Circolare" ? shear.D("rami_interni") : Math.Max(0, shear.D("rami_x", 2) - 2) + Math.Max(0, shear.D("rami_y", 2) - 2);
            double length = section.Shape == "Circolare" ? Math.PI * width + extra * width : 2 * (width + height) + Math.Max(0, shear.D("rami_x", 2) - 2) * width + Math.Max(0, shear.D("rami_y", 2) - 2) * height;
            length += options.HookAllowance * phi * (1 + extra);
            if (length <= 0) throw new ArgumentException("Sviluppo staffe incompatibile con il copriferro.");
            weight = Math.PI * phi * phi / 4 * length / spacing * .00785;
            pieces += (1 + extra) * 1000 / spacing; diameters.Add(phi);
        }
        if (detailing.S("elemento") is "Soletta piena" or "Parete")
        {
            double span = detailing.S("elemento") == "Parete" ? Math.Max(section.Width, section.Height) : section.Width;
            weight += detailing.D("as_secondaria") * span / 1000 * .00785;
            if (detailing.D("passo_secondaria") > 0) pieces += 2000 / detailing.D("passo_secondaria");
            if (detailing.D("diametro_secondaria") > 0) diameters.Add(detailing.D("diametro_secondaria"));
        }
        return new(section.AreaSteel, section.AreaSteel * .00785, weight, section.Bars.Count, diameters.Distinct().Count(), pieces);
    }

    private static void SelectSecondary(SezioneCA section, JsonObject shear, JsonObject detailing,
        IReadOnlyList<DetailingCheck> checks, ConcreteDesignOptions options)
    {
        bool slab = detailing.S("elemento") == "Soletta piena";
        double minimum = checks.Single(c => c.Name == (slab ? "Armatura secondaria" : "Armatura orizzontale per metro")).Limit!.Value;
        if (slab)
        {
            double middle = (section.Outline.Min(p => p[1]) + section.Outline.Max(p => p[1])) / 2;
            double upper = section.Bars.Where(b => b.Y >= middle).Sum(b => b.Area), lower = section.Bars.Where(b => b.Y < middle).Sum(b => b.Area);
            // Equal orthogonal mats must each meet 20% of the principal steel on their own face (EC2 9.3.1.1).
            minimum = Math.Max(minimum, .4 * Math.Max(upper, lower) * 1000 / section.Width);
        }
        var spacingCheck = checks.Single(c => c.Name == (slab ? "Interasse armatura secondaria" : "Interasse orizzontale"));
        // Ask the shared adapter for a numeric spacing limit when the original sheet had no spacing yet.
        double maximum = spacingCheck.Limit ?? (slab ? Math.Min((detailing.S("zona_critica") == "Sì" ? 3 : 3.5) * section.Height, detailing.S("zona_critica") == "Sì" ? 400 : 450) : 400);
        (double Phi, double Spacing, double Area, double Score, double Weight)? best = null;
        foreach (double phi in options.SecondaryDiameters.Distinct().Order())
        foreach (double spacing in options.SecondarySpacings.Distinct().Order())
        {
            double area = 2 * Math.PI * phi * phi / 4 * 1000 / spacing;
            if (area < minimum || spacing > maximum || spacing - phi < Math.Max(20, Math.Max(phi, detailing.D("aggregato") + 5))) continue;
            // Two orthogonal mats must fit inside the section thickness, in addition to the longitudinal bars.
            double thickness = slab ? section.Height : Math.Min(section.Width, section.Height);
            double remaining = thickness - 2 * (section.Input.D("cover_mm") + section.Bars.Max(b => b.Diametro) + phi +
                (section.Input.S("staffe_presenti", "Sì") == "Sì" ? section.Input.D("transverse_bar_diameter_mm") : 0));
            if (remaining < Math.Max(20, detailing.D("aggregato") + 5)) continue;
            detailing["diametro_secondaria"] = phi; detailing["passo_secondaria"] = spacing; detailing["as_secondaria"] = area;
            var m = Metrics(section, shear, detailing, options);
            if (m.Diameters > options.MaxDiameterKinds) continue;
            double score = Score(m, options);
            if (best is null || score < best.Value.Score || score == best.Value.Score && m.KgPerM < best.Value.Weight) best = (phi, spacing, area, score, m.KgPerM);
        }
        if (best is not { } b) throw new ArgumentException("Nessuna rete ortogonale ammissibile: controllare diametri, passi, spazio e numero di diametri distinti.");
        detailing["diametro_secondaria"] = b.Phi; detailing["passo_secondaria"] = b.Spacing; detailing["as_secondaria"] = b.Area;
    }
}
