using System.Windows;
using System.Windows.Controls;
using GPC.Checkers.CompositeBridge.History;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    private readonly TextBlock methodNotice = Ui.Text("", 12, color: Ui.Muted);
    private InputForm? methodForm;
    private UIElement? meshControls;
    private UIElement BuildCalculationOptions()
    {
        Data["metodo_analisi"] ??= BridgeSection.CalculationMethods[0];
        Data["viscosita_nl"] ??= BridgeSection.NonlinearCreepModes[0];
        Data["fibre_anima"] ??= 160; Data["fibre_flange"] ??= 8; Data["fibre_cls"] ??= 64; Data["sottopassi"] ??= 8;
        methodForm = Form(Data, [new("metodo_analisi", "Metodo di calcolo", Choices: BridgeSection.CalculationMethods, Wide: true),
            new("viscosita_nl", "Viscosità nel non lineare", Choices: BridgeSection.NonlinearCreepModes, Wide: true)], _ => { RefreshCalculationOptions(); Dispatcher.BeginInvoke(BuildPhases); });
        meshControls = Group("Discretizzazione e sottopassi", Ui.Stack(Form(Data, [new("fibre_anima", "Strisce anima"), new("fibre_flange", "Strisce per piattabanda"),
            new("fibre_cls", "Strisce soletta"), new("sottopassi", "Sottopassi per fase")]), Ui.Text("Due fibre per striscia. Aumentare i valori per verificare la sensibilità dei risultati.", 11, color: Ui.Muted)));
        RefreshCalculationOptions();
        return Ui.Stack(methodForm, methodNotice, meshControls);
    }
    private void RefreshCalculationOptions()
    {
        bool history = BridgeSection.IsHistory(Data), nonlinear = BridgeSection.IsNonlinear(Data);
        methodForm?.ShowField("viscosita_nl", nonlinear);
        if (meshControls is not null) meshControls.Visibility = history ? Visibility.Visible : Visibility.Collapsed;
        foreach (var form in inputForms.Where(f => f.Editors.ContainsKey("classe4")))
            foreach (string key in new[] { "classe4", "instabilita_sup", "instabilita_inf", "instabilita_anima" }) form.Enable(key, !nonlinear, dim: true);
        methodNotice.Text = nonlinear
            ? "Non lineare N–Mx sulla sezione lorda, con memoria plastica dell’acciaio e inviluppo CLS Model. Legami caratteristici: verifica SLU di classe 4 e controlli accessori da completare. Dettagli in Info modello."
            : history ? "Storico lineare: deformazioni al getto, carichi e ritiri incrementali senza limite numerico di fasi. φ/n modifica i nuovi incrementi. Le fasi già concluse conservano la propria storia. Taglio e connessione da verificare separatamente."
            : "Metodo cumulativo precedente: ricalcola i contributi sulla stessa sezione efficace della situazione. Comprende le verifiche di taglio e connessione. Fino a 20 fasi.";
    }
    private void ShowHistoryResults(BridgeResult result, BridgeStage stage)
    {
        var h = stage.GetHistory()!; var s = h.State;
        var history = result.Stages.Take(StageChoice.SelectedIndex + 1).Select(t => t.GetHistory()!).ToArray();
        propertiesTable.Content = Ui.Stack(Ui.Text("Proprietà riferite ai moduli elastici iniziali della fase. Nel non lineare non sono una rigidezza tangente né una legge σ=My/I.", 11, color: Ui.Muted),
            ResultTable(["Fase", "A* [mm²]", "yG elastico [mm]", "Ix* [mm⁴]", "ε₀ totale [µε]", "κ totale [1/m]", "Δε₀ [µε]", "Δκ [1/m]"],
                history.Select(t => new[] { t.State.Name, F(t.TransformedArea), F(t.ElasticCentroid), E(t.TransformedInertia), F(t.State.TotalPlane.AxialStrain * 1e6), E(t.State.TotalPlane.Curvature * 1000), F(t.State.IncrementPlane.AxialStrain * 1e6), E(t.State.IncrementPlane.Curvature * 1000) })));
        phaseTable.Content = ResultTable(["Fase", "ΔN [kN]", "ΔMx [kNm]", "yN [mm]", "ΣN [kN]", "ΣM₀ [kNm]", "ΣV [kN]", "Δεcs [µε]", "n applicato", "Residuo N [N]", "Residuo M [Nmm]"],
            history.Select((t, i) => new[] { t.State.Name, F(t.State.AppliedPhase.DeltaN / 1000), F(t.State.AppliedPhase.DeltaM / 1e6), F(t.State.IncrementApplicationY), F(t.State.N / 1000), F(t.State.MomentAtOrigin / 1e6), F(t.State.V / 1000),
                t.State.AppliedPhase.ImposedStrainIncrements.TryGetValue(HBridgeHistoryAnalysis.Concrete, out var eps) ? F(eps.AxialStrain * 1e6) : "—", Dash(stage.Contributions[i].HomogenizationN), E(t.State.ForceResidual), E(t.State.MomentResidual) }));
        stressTable.Content = Scroll(Ui.Stack(
            Ui.Text("Δσ = differenza tra due stati consecutivi, incluse le redistribuzioni. Non sono risposte indipendenti sovrapponibili. εmecc = εtot − εgetto − εimposta. Le fibre inattive conservano memoria, ma non portano risultanti.", 11, color: Ui.Muted),
            ResultTable(["Punto", "y [mm]", "σ [MPa]", "Ultimo Δσ [MPa]"], stage.Points.Select(p => new[] { p.Name, F(p.Y), p.Active ? F(p.Stress) : "—", F(p.Contributions.Last()) })),
            Group("Storico delle fibre · deformazioni in µε", ResultTable(["Componente / fibra", "Attiva", "y [mm]", "Aeff [mm²]", "εtot", "εgetto", "εimposta", "εmecc", "εpl", "σ [MPa]", "Δσ [MPa]"],
                s.Fibers.Select(f => new[] { f.Fiber.Id, f.Active ? "Sì" : "No", F(f.Fiber.Y), F(f.EffectiveArea), F(f.TotalStrain * 1e6), F(f.ActivationStrain * 1e6), F(f.ImposedStrain * 1e6), F(f.MechanicalStrain * 1e6), f.MaterialState is HistoryPlasticState p ? F(p.PlasticStrain * 1e6) : "—", F(f.Stress), F(f.StressIncrement) })))));
        if (h.Nonlinear)
        {
            summaryCards.Start(); summaryCards.Text = "Analisi non lineare N–Mx convergente · verifica normativa da completare";
            verificationTable.Content = Ui.Stack(Ui.Text("Nessun esito SLU/SLE assegnato. Le linee e il contouring σ/limite sono riferimenti visivi: la resistenza plastica richiede anche controlli di deformazione, stabilità e interazione.", 12, color: Ui.Muted),
                ResultTable(["Componente", "εmecc min [µε]", "εmecc max [µε]", "|εpl|max [µε]", "σmin [MPa]", "σmax [MPa]"], s.Fibers.Where(f => f.Active).GroupBy(f => f.ComponentId).Select(g => new[] {
                    g.Key, F(g.Min(f => f.MechanicalStrain) * 1e6), F(g.Max(f => f.MechanicalStrain) * 1e6), F(g.Max(f => f.MaterialState is HistoryPlasticState p ? Math.Abs(p.PlasticStrain) : 0) * 1e6), F(g.Min(f => f.Stress)), F(g.Max(f => f.Stress)) })));
            classTable.Content = Ui.Text("Non lineare sulla sezione lorda. L’instabilità locale di classe 4 non è modellata; nessuna larghezza efficace elastica viene presentata come verifica plastica.", 12);
        }
        details.Text = $"{result.Method}\nε(y) = ε₀ − κ·y; y=0 all’interfaccia.\nNewton: {s.NewtonIterations} · iterazioni aree: {s.EffectiveIterations}\nResiduo N: {s.ForceResidual:E3} N\nResiduo M: {s.MomentResidual:E3} Nmm\nVariazione aree: {s.EffectiveResidual:E3}\nN integrato: {s.IntegratedN / 1000:0.###} kN\nM integrato a y=0: {s.IntegratedMomentAtOrigin / 1e6:0.###} kNm";
    }
}
