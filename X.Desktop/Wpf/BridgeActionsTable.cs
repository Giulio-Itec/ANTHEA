using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    internal readonly JsonGrid ActionsTable = new([
        new("attiva", "Attiva", Bool: true), new("nome", "Fase"), new("tipo", "Sezione / azione", Choices: BridgeSection.PhaseKinds),
        new("N", "ΔN [kN]"), new("Mx", "ΔMx [kNm]"), new("V", "ΔV [kN]"),
        new("riferimento_N", "Punto N / riferimento Mx", Choices: BridgeSection.LoadReferences),
        new("phi", "φ"), new("psi", "ψL"), new("n", "n = Ea/Ec,eff"), new("epsilon_cs", "Δεcs [µε]" ), new("q_conn", "Δq pioli [kN/m]")]);
    private readonly TextBlock loadConvention = Ui.Text("", 11, color: Ui.Muted);
    private readonly TextBlock phaseCount = Ui.Text("", 11, true);
    private readonly ContentControl verificationTable = new();
    private bool refreshingActions;
    private UIElement BuildActionsTable()
    {
        double[] widths = [60, 180, 130, 90, 100, 90, 220, 70, 70, 110, 105, 120];
        for (int i = 0; i < widths.Length; i++) ActionsTable.Columns[i].Width = widths[i];
        ActionsTable.FrozenColumnCount = 2;
        ActionsTable.MinHeight = 110;
        ConfigureActionCells();
        ActionsTable.BeginningEdit += (_, e) =>
        {
            var row = (JsonRow)e.Row.Item; string key = e.Column.SortMemberPath;
            bool shrinkage = row.Values.S("tipo") == BridgeSection.ShrinkageKind;
            e.Cancel = shrinkage && key is "N" or "Mx" or "V" or "riferimento_N"
                || row.Values.S("tipo") == "Solo acciaio" && key == "q_conn"
                || !shrinkage && key == "epsilon_cs" || !PhaseHomogenizationNeeded(row.Values.S("tipo")) && key is "phi" or "psi" or "n";
        };
        var commands = Ui.Bar(Ui.Button("+ Carico", () => AddPhase(false)), Ui.Button("+ Ritiro", () => AddPhase(true)),
            Ui.Button("↑", () => MoveSelected(-1)), Ui.Button("↓", () => MoveSelected(1)), Ui.Button("Elimina", () =>
            { if (ActionsTable.SelectedItem is JsonRow row && Data.Array("fasi").Count > 1) { ActionsTable.Commit(); Data.Array("fasi").Remove(row.Values); BuildPhases(); Changed(); } }));
        return Ui.Dock(ActionsTable, Ui.Stack(phaseCount, loadConvention,
            Ui.Text("Ritiro: Δεcs < 0 accorciamento, in µε (−250 = −0,25‰). φ, ψL e n sono propri della fase. Il risultato somma gli incrementi ΔV. Δq aggiunge scorrimento ai pioli (effetti locali/estremità), nullo nelle fasi di solo acciaio.", 11, color: Ui.Muted)), commands);
    }
    private string actionStyleKey = "";
    private void ConfigureActionCells()
    {
        string styleKey = Data.S("normativa") + Data.B("pioli");
        if (actionStyleKey == styleKey) return;
        actionStyleKey = styleKey;
        foreach (var column in ActionsTable.Columns)
        {
            string key = column.SortMemberPath;
            var inactiveKinds = BridgeSection.PhaseKinds.Where(kind =>
                kind == BridgeSection.ShrinkageKind && key is "N" or "Mx" or "V" or "riferimento_N"
                || kind != BridgeSection.ShrinkageKind && key == "epsilon_cs"
                || kind == "Solo acciaio" && key == "q_conn"
                || !PhaseHomogenizationNeeded(kind) && key is "phi" or "psi" or "n");
            var style = new Style(typeof(DataGridCell), ActionsTable.TryFindResource(typeof(DataGridCell)) as Style);
            foreach (string kind in inactiveKinds)
            {
                var trigger = new DataTrigger { Binding = new Binding("[tipo]"), Value = kind };
                trigger.Setters.Add(new Setter(IsEnabledProperty, false));
                trigger.Setters.Add(new Setter(OpacityProperty, .3));
                trigger.Setters.Add(new Setter(ToolTipProperty, "Dato non applicabile a questo tipo di fase; non utilizzato nel calcolo."));
                style.Triggers.Add(trigger);
            }
            column.CellStyle = style;
        }
    }
    private void AddPhase(bool shrinkage)
    {
        if (!BridgeSection.IsHistory(Data) && Data.Array("fasi").Count >= 20)
        { status.Text = "Il metodo cumulativo ammette 20 fasi. Selezionare un metodo con storico per aggiungerne altre."; return; }
        ActionsTable.Commit();
        var phase = shrinkage ? BridgeSection.ShrinkagePhase() : BridgeSection.Phase(reference: BridgeSection.GrossLoadReference);
        Data.Array("fasi").Add(phase); selectedPhase = phase; followLatestStage = true;
        BuildPhases(); Changed(); ActionsTable.SelectedIndex = ActionsTable.Rows.Count - 1;
        ActionsTable.ScrollIntoView(ActionsTable.SelectedItem);
    }
    private void MoveSelected(int delta)
    {
        if (ActionsTable.SelectedItem is not JsonRow row) return;
        ActionsTable.Commit(); int index = Data.Array("fasi").IndexOf(row.Values); MovePhase(index, delta);
        ActionsTable.SelectedIndex = Math.Clamp(index + delta, 0, ActionsTable.Rows.Count - 1);
    }
    private void RefreshActionsTable()
    {
        if (refreshingActions) return;
        refreshingActions = true;
        try
        {
        ConfigureActionCells();
        var phases = Data.Array("fasi").OfType<JsonObject>().ToArray();
        if (!ActionsTable.Rows.Select(r => r.Values).SequenceEqual(phases))
        {
            var selected = (ActionsTable.SelectedItem as JsonRow)?.Values;
            // Reconcile rows in place. Reset/Clear during an edit can leave the WPF view
            // empty and loses row selection and scroll state even when the JSON is intact.
            for (int i = 0; i < phases.Length; i++)
            {
                var phase = phases[i];
                int old = ActionsTable.Rows.ToList().FindIndex(r => ReferenceEquals(r.Values, phase));
                if (old >= 0) { if (old != i) ActionsTable.Rows.Move(old, i); continue; }
                ActionsTable.Rows.Insert(i, new JsonRow(phase, key =>
                {
                    if (refreshingActions) return;
                    if (key is "phi" or "psi" or "n") phase["modo"] = key == "n" ? "Da n" : "Da φ";
                    if (key == "tipo" && phase.S("tipo") == BridgeSection.ShrinkageKind) { phase["psi"] = .55; phase["modo"] = "Da φ"; }
                    Changed();
                    // Rebuild the detail editors after the grid has committed the current cell.
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(BuildPhases));
                }));
            }
            while (ActionsTable.Rows.Count > phases.Length) ActionsTable.Rows.RemoveAt(ActionsTable.Rows.Count - 1);
            ActionsTable.SelectedItem = ActionsTable.Rows.FirstOrDefault(r => ReferenceEquals(r.Values, selected));
        }
        else foreach (var row in ActionsTable.Rows) row.Refresh();
        phaseCount.Text = $"{phases.Length} fasi inserite · {phases.Count(p => p.B("attiva"))} attive · la tabella include sempre tutti gli ingressi";
        loadConvention.Text = Data.S("stato") == "SLU"
            ? "SLU: inserire incrementi di progetto già combinati e coefficientati (γF e ψ). Il modulo non applica altri coefficienti ai carichi; γM0, γc e γs riducono le resistenze."
            : Data.S("stato") + ": inserire gli incrementi della corrispondente combinazione di esercizio, già con i relativi ψ. Il selettore cambia i limiti, non genera combinazioni di carico.";
        if (BridgeSection.IsNonlinear(Data)) loadConvention.Text = "Non lineare: carichi incrementali già combinati, legami caratteristici senza γM. SLU/SLE seleziona soltanto le linee di confronto tensionali: non assegna una verifica normativa. φ/n memorizzati: vedere l’opzione di viscosità sopra.";
        }
        finally { refreshingActions = false; }
    }
}
