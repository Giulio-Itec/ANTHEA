using System.Globalization;
using Anthea.Application.Bridge;

namespace Anthea.Application.Presentation;

/// <summary>Kind of a block of a panel of results of the composite bridge section.</summary>
public enum BridgeBlockKind { Text, Table, Group }

/// <summary>
/// The look of a text of the panels, as the historic view writes it (Ui.Text): <see cref="Body"/> 12 points, <see cref="Muted"/> 12 points
/// in the muted colour, <see cref="Note"/> 11 points muted, <see cref="Plain"/> 13 points (the empty placeholders of the studs).
/// </summary>
public enum BridgeTextStyle { Body, Muted, Note, Plain }

/// <summary>
/// One block of a panel of results of the composite bridge section (F5.11): a text with its look, a table (columns and rows already
/// formatted) or a group (an expander with its blocks, closed at first). The historic view draws it with its controls; the ViewModel
/// exposes it to the XAML view.
/// </summary>
public sealed record BridgeBlock(BridgeBlockKind Kind, string Text = "", BridgeTextStyle Style = BridgeTextStyle.Body,
    IReadOnlyList<string>? Columns = null, IReadOnlyList<string[]>? Rows = null, IReadOnlyList<BridgeBlock>? Children = null)
{
    public static BridgeBlock Paragraph(string text, BridgeTextStyle style = BridgeTextStyle.Body) => new(BridgeBlockKind.Text, text, style);

    public static BridgeBlock Table(IReadOnlyList<string> columns, IEnumerable<string[]> rows) => new(BridgeBlockKind.Table, Columns: columns, Rows: rows.ToArray());

    public static BridgeBlock Group(string title, params BridgeBlock[] children) => new(BridgeBlockKind.Group, title, Children: children);
}

/// <summary>A point of application of N drawn on the section: the phase (from 1), its name, its level and its force.</summary>
public sealed record BridgeLoadPoint(int Index, string Name, double Y, double Force);

/// <summary>A panel of results: its blocks, in a scroll viewer of its own when <see cref="Scroll"/>.</summary>
public sealed record BridgePanel(IReadOnlyList<BridgeBlock> Blocks, bool Scroll = false)
{
    public static BridgePanel Of(params BridgeBlock[] blocks) => new(blocks);
}

/// <summary>
/// What the results of the selected situation show (F5.11): the overview, the warnings, the cards of the summary, the panels of the
/// tabs «Fasi e proprietà», «Sezione efficace», «Tensioni», «Verifiche» and the details of the convergence. Built by
/// <see cref="BridgeResultPresentation.Show"/> from the result, in place of the historic view, which drew them itself.
/// </summary>
public sealed record BridgeResultsView(string Overview, string Warnings, IReadOnlyList<VerificationSummary> Summaries, string? SummaryText,
    BridgePanel Stress, BridgePanel Verification, BridgePanel Properties, BridgePanel Phases, BridgePanel Class4, BridgePanel Geometry,
    BridgePanel Shear, BridgePanel Studs, BridgePanel Torsion, bool TorsionShown, string Details);

/// <summary>
/// The results of the composite bridge section as the sheet shows them (F5.11): texts, tables and cards of the selected situation,
/// moved without changes from the historic view (BridgeWorkspace.ShowResults, ShowAccessoryResults, ShowTorsionResults,
/// ShowHistoryResults), which calls them. Units: N in kN, moments in kNm, stresses in MPa, lengths in mm.
/// </summary>
public static class BridgeResultPresentation
{
    /// <summary>The text of every panel without a result (ClearResults of the historic view).</summary>
    public const string NoResult = "Nessun risultato aggiornato";

    /// <summary>The panel without a result.</summary>
    public static BridgePanel Empty { get; } = BridgePanel.Of(BridgeBlock.Paragraph(NoResult, BridgeTextStyle.Muted));

    /// <summary>The overview after a change without a result shown.</summary>
    public const string OverviewToUpdate = "Risultati da aggiornare";

    /// <summary>The tooltip of «Asse neutro» without a situation.</summary>
    public const string NeutralAxisToolTip = "Asse neutro del risultato selezionato.";

    /// <summary>The proposed name of the CSV of the stresses.</summary>
    public const string CsvFileName = "sezione_mista_tensioni.csv";

    /// <summary>The points of application of N of a situation: one for each contribution that is not a shrinkage.</summary>
    public static BridgeLoadPoint[] LoadPoints(BridgeStage stage) =>
        stage.Contributions.Select((c, i) => (c, i)).Where(p => !p.c.IsShrinkage).Select(p => new BridgeLoadPoint(p.i + 1, p.c.Name, p.c.LoadY, p.c.N)).ToArray();

    /// <summary>
    /// «Esporta CSV…»: the stresses of every point of every situation; with the history the fibres of every situation instead. Rows
    /// separated by the line separator of the system (the bytes are written with the UTF-8 preamble).
    /// </summary>
    public static string Csv(BridgeResultView result)
    {
        var lines = new List<string> { "Situazione;Punto;Materiale;y [mm];Sigma [MPa];Limite [MPa];Eta;Attivo" };
        string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        foreach (var s in result.Stages) foreach (var p in s.Points) lines.Add(string.Join(";", Q(s.Name), Q(p.Name), Q(p.Material), p.Y.ToString("R", Italian), p.Stress.ToString("R", Italian), F(p.Limit), p.Utilization is { } u ? u.ToString("R", Italian) : "", p.Active));
        if (result.Stages.Any(s => s.GetHistory() is not null))
        {
            lines = ["Situazione;Fibra;Componente;Attiva;y [mm];Aeff [mm2];Eps totale;Eps getto;Eps imposta;Eps meccanica;Eps plastica;Sigma [MPa];Delta sigma [MPa]"];
            string R(double x) => x.ToString("R", Italian);
            foreach (var s in result.Stages) foreach (var f in s.GetHistory()!.State.Fibers)
                lines.Add(string.Join(";", Q(s.Name), Q(f.Fiber.Id), Q(f.ComponentId), f.Active, R(f.Fiber.Y), R(f.EffectiveArea), R(f.TotalStrain), R(f.ActivationStrain), R(f.ImposedStrain), R(f.MechanicalStrain),
                    f.MaterialState is HistoryPlasticState p ? R(p.PlasticStrain) : "", R(f.Stress), R(f.StressIncrement)));
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
    private const double Kilo = 1000, Mega = 1e6, Giga = 1e12;

    public static string F(double x) => BridgePropertyPresentation.Number(x);
    public static string E(double? x) => x?.ToString("0.#####E+0", Italian) ?? "—";
    private static string Dash(double x) => x > 0 ? F(x) : "—";

    /// <summary>The situations of the result («01 · Dopo G1 …»).</summary>
    public static IReadOnlyList<string> Situations(BridgeResultView result) => result.Stages.Select((s, i) => $"{i + 1:00} · Dopo {s.Name}").ToArray();

    /// <summary>The results of the situation <paramref name="index"/> of <paramref name="result"/>.</summary>
    public static BridgeResultsView Show(BridgeResultView result, int index)
    {
        var stage = result.Stages[index]; var g = result.Geometry;
        double removed = Math.Max(0, g.WebHeight - stage.Effective.WebTop - stage.Effective.WebBottom);
        string overview = $"|σa|max = {F(stage.Points.Where(p => p.Material == "Acciaio").Max(p => Math.Abs(p.Stress)))} MPa\nσc,min = {F(stage.Points.Where(p => p.Material == "CLS").Min(p => p.Stress))} MPa\n\nAnima inefficace: {F(removed)} mm\nAeff / Alorda = {F(stage.EffectiveSteel.Area / g.SteelArea)}\nConvergenza: {stage.Iterations} iterazioni";
        var summaries = new List<VerificationSummary>();
        foreach (string material in new[] { "Acciaio", "CLS", "Armatura" })
        {
            var points = stage.Points.Where(p => p.Material == material && p.Active).ToArray();
            if (points.Length > 0) summaries.Add(VerificationSummary.Create(material + " · tensioni", points.Length, points.Select(p => (p.Name, p.Utilization, p.Utilization is { } u ? (bool?)(u <= 1) : null))));
        }
        var shear = Shear(stage, summaries);
        var studs = Studs(stage, summaries);
        bool box = g.SectionType == BridgeSteelSectionType.Box;
        var torsion = Torsion(stage, box, summaries);
        string warnings = string.Join("  ", stage.Warnings);
        // PON-1: for the tensile bars of the cracked slab in SLE Σσ = σs,0 + Δσs, while the contributions sum σs,0 (tension stiffening column).
        bool stiffening = stage.Points.Any(p => p.GetTensionStiffening() is not null);
        var stress = BridgePanel.Of(BridgeBlock.Table(["Punto", "y [mm]", .. stage.Contributions.Select((_, i) => $"Δσ {i + 1} [MPa]"), .. (stiffening ? new[] { "Δσs tension stiff. [MPa]" } : []), "Σσ [MPa]"],
            stage.Points.Select(p => new[] { p.Name, F(p.Y) }.Concat(p.Contributions.Select(F)).Concat(stiffening ? [p.GetTensionStiffening() is { } t ? F(t.Increment) : "—"] : []).Append(p.Active ? F(p.Stress) : "—").ToArray())));
        var verification = BridgePanel.Of(BridgeBlock.Table(["Controllo", "Stato limite", "Domanda", "Limite", "η", "Esito", "Note"],
            stage.Points.Select(p => new[] { p.Name, result.Input.S("stato"), p.Active ? F(p.Stress) : "—", p.Active ? F(p.Limit) : "—", p.Utilization is { } u ? F(u) : "—",
                !p.Active ? "Non attivo" : p.Utilization is null ? "CLS teso" : p.Utilization <= 1 ? "Entro limite locale" : "Limite superato", "σ e limite in MPa" }).Concat((stage.Shear?.Checks ?? []).Concat(stage.Studs?.Checks ?? []).Concat(stage.Torsion?.Checks ?? [])
                .Select(c => new[] { c.Name, result.Input.S("stato"), F(c.Demand) + " " + c.Unit, F(c.Resistance) + " " + c.Unit, c.Ratio is { } r ? F(r) : "—", c.Status, c.Note }))));
        var properties = BridgePanel.Of(BridgeBlock.Table(["Fase", "n₀", "n = Ea/Ec,eff", "φ", "ψLφ", "Ec,eff [MPa]", "A* [mm²]", "yG [mm]", "Ix* [mm⁴]", "Wsup* [mm³]", "Winf* [mm³]", "yσ=0 [mm]", "κ [1/m]"],
            stage.Contributions.Select(c => new[] { c.Name, Dash(c.N0), Dash(c.HomogenizationN), c.HasConcrete ? F(c.Phi) : "—", c.HasConcrete ? F(c.EffectivePhi) : "—",
                c.HasConcrete ? F(BridgeEditor.EffectiveConcreteModulus(result.Materials.Ea, c.HomogenizationN)) : "—", F(c.Area), F(c.Centroid), E(c.Inertia), E(c.WTop), E(c.WBottom), c.NeutralAxis is { } z ? F(z) : "—", E(c.Curvature(result.Materials.Ea) * Kilo) })));
        var class4 = BridgePanel.Of(BridgeBlock.Table(["Pannello", "b [mm]", "t [mm]", "σ₁ [MPa]", "σ₂ [MPa]", "ψ", "kσ", "λp", "ρ", "bc [mm]", "b₁ eff [mm]", "b₂ eff [mm]"],
            new[] { ("Anima · sup → inf", stage.Effective.Web), ("Sbalzo superiore", stage.Effective.Top), (g.SectionType == BridgeSteelSectionType.Box ? "Fondo · lamiera interna" : stage.Effective.SecondBottom is null ? "Sbalzo inferiore" : "Sbalzo inferiore · piastra 1", stage.Effective.Bottom) }
                .Concat(g.SectionType == BridgeSteelSectionType.Box && stage.Effective.BottomOutstand is { } outstand ? new[] { ("Fondo · sbalzi", outstand) } : []).Concat(stage.Effective.SecondBottom is { } second ? new[] { ("Sbalzo inferiore · piastra 2", second) } : []).Select(x =>
                new[] { x.Item1, F(x.Item2.Width), F(x.Item2.Thickness), F(x.Item2.StartStress), F(x.Item2.EndStress), F(x.Item2.Psi), F(x.Item2.KSigma), F(x.Item2.Lambda), F(x.Item2.Rho), F(x.Item2.CompressedWidth), F(x.Item2.EffectiveAtStart), F(x.Item2.EffectiveAtEnd) })));
        var phases = BridgePanel.Of(BridgeBlock.Table(["Fase", "Sezione", "Riferimento N", "yN [mm]", "ΔN [kN]", "ΔMx al punto N [kNm]", "ΔV [kN]", "ΣN [kN]", "ΣMx a y=0 [kNm]", "ΣV [kN]", "Δεcs [µε]", "N eq. [kN]", "M eq. a y=0 [kNm]", "σc impedita [MPa]", "As [mm²]", "Ix* integrazione [mm⁴]", "Residuo equilibrio"],
            stage.Contributions.Select((c, i) => new[] { c.Name, c.Kind, c.LoadReference, F(c.LoadY), F(c.N), F(c.Mx), F(c.V), F(stage.Contributions.Take(i + 1).Sum(x => x.N)), F(stage.Contributions.Take(i + 1).Sum(x => x.MomentAtInterface)), F(stage.Contributions.Take(i + 1).Sum(x => x.V)),
                c.IsShrinkage ? F(c.ShrinkageStrain * Mega) : "—", c.IsShrinkage ? F(c.EquivalentN) : "—", c.IsShrinkage ? F(c.EquivalentMomentAtInterface) : "—", c.IsShrinkage ? F(c.ConcreteStressOffset) : "—", F(c.RebarArea), E(c.SolverInertia), E(c.EquilibriumResidual) })));
        var geometry = BridgePanel.Of(BridgeBlock.Table(["Proprietà", "Valore", "Unità"], new[] {
            new[] { "Area carpenteria lorda", F(g.SteelArea), "mm²" }, new[] { "Baricentro carpenteria lorda", F(g.SteelCentroid), "mm" }, new[] { "Ix carpenteria lorda", E(g.SteelInertia), "mm⁴" },
            new[] { "Area carpenteria efficace", F(stage.EffectiveSteel.Area), "mm²" }, new[] { "Aeff / Alorda", F(stage.EffectiveSteel.Area / g.SteelArea), "—" },
            new[] { "Baricentro carpenteria efficace", F(stage.EffectiveSteel.Centroid), "mm" }, new[] { "Spostamento baricentro efficace − lordo", F(stage.EffectiveSteel.Centroid - g.SteelCentroid), "mm" },
            new[] { "Ix carpenteria efficace", E(stage.EffectiveSteel.Inertia), "mm⁴" }, new[] { "Ieff / Ilorda", F(stage.EffectiveSteel.Inertia / g.SteelInertia), "—" },
            new[] { "Altezza totale trave H", F(g.Height), "mm" }, new[] { "Altezza libera anima ricavata", F(g.WebHeight), "mm" },
            new[] { "Anima inefficace · limite inferiore y", F(-g.TopThickness - g.WebHeight + stage.Effective.WebBottom), "mm" },
            new[] { "Anima inefficace · limite superiore y", F(-g.TopThickness - stage.Effective.WebTop), "mm" },
            new[] { "Piattabanda inferiore · b equivalente (solo confronto)", F(g.BottomEquivalentWidth), "mm" }, new[] { "Piattabanda inferiore · t equivalente (solo confronto)", F(g.BottomEquivalentThickness), "mm" },
            new[] { "Piastre inferiori · area reale = equivalente", F(g.BottomArea), "mm²" }, new[] { "Piastre inferiori · yG reale", F(g.BottomRealCentroid), "mm" },
            new[] { "Piastre inferiori · yG equivalente (solo confronto)", F(-g.Height + g.BottomEquivalentThickness / 2), "mm" }, new[] { "Piastre inferiori · Ix reale al proprio G", E(g.BottomRealInertia), "mm⁴" },
            new[] { "Piattabanda equivalente · Ix al proprio G (solo confronto)", E(BridgeEditor.EquivalentBottomInertia(g)), "mm⁴" },
            new[] { "Area soletta lorda", F(g.Width * g.SlabHeight), "mm²" }, new[] { "Barre complessive", g.Bars.Length.ToString(), "n." }, new[] { "Area armature", F(g.Bars.Sum(b => b.Area)), "mm²" },
            new[] { "Altezza complessiva", F(g.Height + g.SlabHeight), "mm" }, new[] { "Asse neutro delle tensioni totali nell’acciaio", stage.SteelNeutralAxis is { } z ? F(z) : "—", "mm" }
        }.Concat(g.Bars.GroupBy(b => b.Y).Select(r => new[] { $"Fila y={F(r.Key)} mm · {r.Count()} barre Ø{F(r.First().Diameter)}", F(r.Sum(b => b.Area)), "mm²" }))));
        string details = $"Convergenza: {stage.Iterations} iterazioni.\nVariazione relativa delle larghezze: {stage.Residual:E3} · tolleranza 1E−7.\n\n" +
            $"Residuo massimo dell’equilibrio N–Mx: {stage.Contributions.Max(c => Math.Abs(c.EquilibriumResidual)):E3} · limite 1E−5.";
        string? summaryText = null;
        if (stage.GetHistory() is { } h)
        {
            var s = h.State;
            var history = result.Stages.Take(index + 1).Select(t => t.GetHistory()!).ToArray();
            properties = BridgePanel.Of(BridgeBlock.Paragraph("Proprietà riferite ai moduli elastici iniziali della fase. Nel non lineare non sono una rigidezza tangente né una legge σ=My/I.", BridgeTextStyle.Note),
                BridgeBlock.Table(["Fase", "A* [mm²]", "yG elastico [mm]", "Ix* [mm⁴]", "ε₀ totale [µε]", "κ totale [1/m]", "Δε₀ [µε]", "Δκ [1/m]"],
                    history.Select(t => new[] { t.State.Name, F(t.TransformedArea), F(t.ElasticCentroid), E(t.TransformedInertia), F(t.State.TotalPlane.AxialStrain * Mega), E(t.State.TotalPlane.Curvature * Kilo), F(t.State.IncrementPlane.AxialStrain * Mega), E(t.State.IncrementPlane.Curvature * Kilo) })));
            phases = BridgePanel.Of(BridgeBlock.Table(["Fase", "ΔN [kN]", "ΔMx [kNm]", "yN [mm]", "ΣN [kN]", "ΣM₀ [kNm]", "ΣV [kN]", "Δεcs [µε]", "n applicato", "Residuo N [N]", "Residuo M [Nmm]"],
                history.Select((t, i) => new[] { t.State.Name, F(t.State.AppliedPhase.DeltaN / Kilo), F(t.State.AppliedPhase.DeltaM / Mega), F(t.State.IncrementApplicationY), F(t.State.N / Kilo), F(t.State.MomentAtOrigin / Mega), F(t.State.V / Kilo),
                    t.State.AppliedPhase.ImposedStrainIncrements.TryGetValue(BridgeEditor.ConcreteComponent, out var eps) ? F(eps.AxialStrain * Mega) : "—", Dash(stage.Contributions[i].HomogenizationN), E(t.State.ForceResidual), E(t.State.MomentResidual) })));
            stress = new BridgePanel([
                BridgeBlock.Paragraph("Δσ = differenza tra due stati consecutivi, incluse le redistribuzioni. Non sono risposte indipendenti sovrapponibili. εmecc = εtot − εgetto − εimposta. Le fibre inattive conservano memoria, ma non portano risultanti.", BridgeTextStyle.Note),
                BridgeBlock.Table(["Punto", "y [mm]", "σ [MPa]", "Ultimo Δσ [MPa]"], stage.Points.Select(p => new[] { p.Name, F(p.Y), p.Active ? F(p.Stress) : "—", F(p.Contributions.Last()) })),
                BridgeBlock.Group("Storico delle fibre · deformazioni in µε", BridgeBlock.Table(["Componente / fibra", "Attiva", "y [mm]", "Aeff [mm²]", "εtot", "εgetto", "εimposta", "εmecc", "εpl", "σ [MPa]", "Δσ [MPa]"],
                    s.Fibers.Select(f => new[] { f.Fiber.Id, f.Active ? "Sì" : "No", F(f.Fiber.Y), F(f.EffectiveArea), F(f.TotalStrain * Mega), F(f.ActivationStrain * Mega), F(f.ImposedStrain * Mega), F(f.MechanicalStrain * Mega), f.MaterialState is HistoryPlasticState p ? F(p.PlasticStrain * Mega) : "—", F(f.Stress), F(f.StressIncrement) })))], Scroll: true);
            if (h.Nonlinear)
            {
                summaries.Clear(); summaryText = NonlinearSummary;
                verification = BridgePanel.Of(BridgeBlock.Paragraph("Nessun esito SLU/SLE assegnato. Le linee e il contouring σ/limite sono riferimenti visivi: la resistenza plastica richiede anche controlli di deformazione, stabilità e interazione.", BridgeTextStyle.Muted),
                    BridgeBlock.Table(["Componente", "εmecc min [µε]", "εmecc max [µε]", "|εpl|max [µε]", "σmin [MPa]", "σmax [MPa]"], s.Fibers.Where(f => f.Active).GroupBy(f => f.ComponentId).Select(c => new[] {
                        c.Key, F(c.Min(f => f.MechanicalStrain) * Mega), F(c.Max(f => f.MechanicalStrain) * Mega), F(c.Max(f => f.MaterialState is HistoryPlasticState p ? Math.Abs(p.PlasticStrain) : 0) * Mega), F(c.Min(f => f.Stress)), F(c.Max(f => f.Stress)) })));
                class4 = BridgePanel.Of(BridgeBlock.Paragraph("Non lineare sulla sezione lorda. L’instabilità locale di classe 4 non è modellata; nessuna larghezza efficace elastica viene presentata come verifica plastica."));
            }
            details = $"{result.Method}\nε(y) = ε₀ − κ·y; y=0 all’interfaccia.\nNewton: {s.NewtonIterations} · iterazioni aree: {s.EffectiveIterations}\nResiduo N: {s.ForceResidual:E3} N\nResiduo M: {s.MomentResidual:E3} Nmm\nVariazione aree: {s.EffectiveResidual:E3}\nN integrato: {s.IntegratedN / Kilo:0.###} kN\nM integrato a y=0: {s.IntegratedMomentAtOrigin / Mega:0.###} kNm";
        }
        return new(overview, warnings, summaries, summaryText, stress, verification, properties, phases, class4, geometry, shear, studs, torsion, box, details);
    }

    /// <summary>The card of the non-linear history in place of the summaries.</summary>
    public const string NonlinearSummary = "Analisi non lineare N–Mx convergente · verifica normativa da completare";

    private static BridgePanel Shear(BridgeStage stage, List<VerificationSummary> summaries)
    {
        if (stage.Shear is not { } s) return BridgePanel.Of(BridgeBlock.Paragraph("Taglio e irrigidimenti non verificati nel metodo selezionato.", BridgeTextStyle.Muted));
        var w = s.Web;
        var panel = BridgePanel.Of(BridgeBlock.Table(["Parametro", "Valore", "Unità"], new[] {
            new[] { "V cumulato", F(s.V), "kN" }, new[] { "kτ", F(w.KTau), "—" }, new[] { "τcr", F(w.TauCritical), "MPa" },
            new[] { "λw", F(w.Slenderness), "—" }, new[] { "χw", F(w.Chi), "—" }, new[] { "Vpl,Rd", F(w.PlasticResistance / Kilo), "kN" },
            new[] { "Vbw,Rd", F(w.BucklingResistance / Kilo), "kN" }, new[] { "VRd adottato", F(w.Resistance / Kilo), "kN" },
            new[] { "τ nominale V/Av / τ max elastica lorda", F(s.TauAverage) + " / " + F(s.TauMaximum), "MPa" },
            new[] { "Beneficio irrigidimenti", s.UsesStiffeners ? "Sì" : "No", "—" }, new[] { "Montante terminale adottato", s.RigidEndPost ? "Rigido verificato" : "Non rigido", "—" } }.Concat(s.Stiffener is { } st ? new[] {
                new[] { "Irrigidimento · area con anima", F(st.Area), "mm²" }, new[] { "Irrigidimento · inerzia fuori piano", E(st.Inertia), "mm⁴" },
                new[] { "Irrigidimento · Nst", F(st.AxialForce / Kilo), "kN" }, new[] { "Irrigidimento · λ / χ", F(st.Lambda) + " / " + F(st.Chi), "—" },
                new[] { "Irrigidimento · imperfezione iniziale", F(st.InitialDeflection), "mm" } } : Array.Empty<string[]>()).Concat((s.Details ?? []).Select(x => new[] { x.Name, F(x.Value), x.Unit }))));
        summaries.Add(VerificationSummary.Create("Taglio e irrigidimenti", s.Checks.Count, s.Checks.Select(c => (c.Name, c.Ratio, c.Ratio is { } r ? (bool?)(r <= 1) : null))));
        return panel;
    }

    private static BridgePanel Studs(BridgeStage stage, List<VerificationSummary> summaries)
    {
        if (stage.Studs is not { } p) return BridgePanel.Of(BridgeBlock.Paragraph("Connessione a pioli non verificata nel metodo selezionato.", BridgeTextStyle.Muted));
        var panel = BridgePanel.Of(BridgeBlock.Paragraph(p.Enabled
                ? $"q = {F(p.Flow)} kN/m · PEd = {F(p.ForcePerStud)} kN/piolo · PRd adottato = {F(p.ResistancePerStud)} kN/piolo · qRd = {F(p.ResistancePerLength)} kN/m"
                : p.Contributions.All(c => c.Kind == "Solo acciaio") ? "Connessione non attiva nella situazione di solo acciaio." : "Verifica pioli disattivata; flussi riportati per consultazione."),
            p.Resistance is { } r ? BridgeBlock.Paragraph($"α = {F(r.Alpha)} · fu adottato = {F(r.UsedFu)} MPa · PRd acciaio = {F(r.SteelResistance / Kilo)} kN · PRd CLS = {F(r.ConcreteResistance / Kilo)} kN", BridgeTextStyle.Note) : BridgeBlock.Paragraph("", BridgeTextStyle.Plain),
            BridgeBlock.Table(["Fase", "V [kN]", "S* [mm³]", "I* [mm⁴]", "q(V) [kN/m]", "Δq [kN/m]", "Proprietà adottate"],
                p.Contributions.Select(c => new[] { c.Phase, F(c.V), E(c.StaticMoment), E(c.Inertia), F(c.Flow), F(c.AdditionalFlow), c.Basis })),
            p.Details is { Count: > 0 } details ? BridgeBlock.Table(["Parametro", "Valore", "Unità"], details.Select(x => new[] { x.Name, F(x.Value), x.Unit })) : BridgeBlock.Paragraph("", BridgeTextStyle.Plain));
        if (p.Enabled) summaries.Add(VerificationSummary.Create("Pioli · controlli locali", p.Checks.Count, p.Checks.Select(c => (c.Name, c.Ratio, c.Ratio is { } q ? (bool?)(q <= 1) : null))));
        return panel;
    }

    private static BridgePanel Torsion(BridgeStage stage, bool box, List<VerificationSummary> summaries)
    {
        if (stage.Torsion is not { } t)
            return BridgePanel.Of(BridgeBlock.Paragraph(box ? "Verifiche a torsione del cassoncino non attive: attivarle negli ingressi del cassoncino." : "Solo per il cassoncino.", BridgeTextStyle.Muted));
        var blocks = new List<BridgeBlock> { BridgeBlock.Paragraph($"q anime e fondo = {F(t.WebFlow)} kN/m · q soletta e connessione = {F(t.SlabFlow)} kN/m · q controvento = {F(t.BracingFlow)} kN/m"),
            BridgeBlock.Table(["Fase", "Sezione", "ΔT [kNm]", "A0 [m²]", "J acciaio [m⁴]", "q [kN/m]", "Cella"], t.Flows.Select(x => new[] {
                x.Phase, x.Kind, F(x.TorqueKNm), F(x.CellArea / Mega), E(x.TorsionConstant / Giga), F(x.Flow), x.Closed ? "Chiusa" : "Aperta · non verificata" })) };
        if (t.Distortion is { } dw)
            blocks.Add(BridgeBlock.Paragraph($"Distorsione: σdw fondo = {F(dw.WarpingStressBottom)} MPa ({F(dw.BendingRatio * 100)}% della flessione, " +
                (dw.Included ? "sommata al fondo" : "trascurata nel fondo") + $") · ψ max = {E(dw.Amplitude)} rad · diaframmi intermedi {dw.Diaphragms}"));
        blocks.Add(BridgeBlock.Table(["Parametro", "Valore", "Unità"], t.Details.Select(x => new[] { x.Name, F(x.Value), x.Unit })));
        summaries.Add(VerificationSummary.Create("Cassoncino · torsione e distorsione", t.Checks.Count, t.Checks.Select(c => (c.Name, c.Ratio, c.Ratio is { } r ? (bool?)(r <= 1) : null))));
        return new(blocks);
    }
}
