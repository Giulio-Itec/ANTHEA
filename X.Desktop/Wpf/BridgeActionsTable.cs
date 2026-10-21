using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.CompositeSection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    internal readonly JsonGrid ActionsTable = new(CompositeSectionFields.Actions);
    private readonly TextBlock loadConvention = Ui.Text("", 11, color: Ui.Muted);
    private readonly TextBlock phaseCount = Ui.Text("", 11, true);
    private readonly ContentControl verificationTable = new();
    private bool refreshingActions;
    private UIElement BuildActionsTable()
    {
        for (int i = 0; i < CompositeSectionFields.ActionWidths.Count; i++) ActionsTable.Columns[i].Width = CompositeSectionFields.ActionWidths[i];
        ActionsTable.FrozenColumnCount = 2;
        ActionsTable.MinHeight = 110;
        ConfigureActionCells();
        ActionsTable.BeginningEdit += (_, e) =>
        {
            var row = (JsonRow)e.Row.Item; string key = e.Column.SortMemberPath;
            e.Cancel = BridgeInputPresentation.CellInactive(Data, row.Values.S("tipo"), key);
        };
        var commands = Ui.Bar(Ui.Button("+ Carico", () => AddPhase(false)), Ui.Button("+ Ritiro", () => AddPhase(true)),
            Ui.Button("↑", () => MoveSelected(-1)), Ui.Button("↓", () => MoveSelected(1)), Ui.Button("Elimina", () =>
            { if (ActionsTable.SelectedItem is JsonRow row && Data.Array("fasi").Count > 1) { ActionsTable.Commit(); Data.Array("fasi").Remove(row.Values); BuildPhases(); Changed(); } }));
        return Ui.Dock(ActionsTable, Ui.Stack(phaseCount, loadConvention,
            Ui.Text("Ritiro: Δεcs < 0 accorciamento, in µε (−250 = −0,25‰). φ, ψL e n sono propri della fase. Il risultato somma gli incrementi ΔV. Δq aggiunge scorrimento ai pioli (effetti locali/estremità), nullo nelle fasi di solo acciaio. " +
                "ΔT: momento torcente del cassoncino, attivo con le verifiche a torsione.", 11, color: Ui.Muted)), commands);
    }
    private string actionStyleKey = "";
    private void ConfigureActionCells()
    {
        // ΔT only for the box girder, editable with the torsion checks
        string styleKey = BridgeInputPresentation.CellStyleKey(Data);
        if (actionStyleKey == styleKey) return;
        actionStyleKey = styleKey;
        foreach (var column in ActionsTable.Columns)
        {
            string key = column.SortMemberPath;
            if (key == "T") column.Visibility = BridgeInputPresentation.TorqueColumnShown(Data) ? Visibility.Visible : Visibility.Collapsed;
            var inactiveKinds = BridgeEditor.PhaseKinds.Where(kind => BridgeInputPresentation.CellInactive(Data, kind, key));
            var style = new Style(typeof(DataGridCell), ActionsTable.TryFindResource(typeof(DataGridCell)) as Style);
            foreach (string kind in inactiveKinds)
            {
                var trigger = new DataTrigger { Binding = new Binding("[tipo]"), Value = kind };
                trigger.Setters.Add(new Setter(IsEnabledProperty, false));
                trigger.Setters.Add(new Setter(OpacityProperty, .3));
                trigger.Setters.Add(new Setter(ToolTipProperty, BridgeInputPresentation.InactiveCellToolTip));
                style.Triggers.Add(trigger);
            }
            column.CellStyle = style;
        }
    }
    private void AddPhase(bool shrinkage)
    {
        if (!BridgeInputPresentation.CanAddPhase(Data)) { status.Text = BridgeInputPresentation.PhaseLimitReached; return; }
        ActionsTable.Commit();
        var phase = BridgeInputPresentation.NewPhase(shrinkage);
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
                    BridgeSheetPresentation.CellEdited(phase, key);
                    Changed();
                    // Rebuild the detail editors after the grid has committed the current cell.
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(BuildPhases));
                }));
            }
            while (ActionsTable.Rows.Count > phases.Length) ActionsTable.Rows.RemoveAt(ActionsTable.Rows.Count - 1);
            ActionsTable.SelectedItem = ActionsTable.Rows.FirstOrDefault(r => ReferenceEquals(r.Values, selected));
        }
        else foreach (var row in ActionsTable.Rows) row.Refresh();
        phaseCount.Text = BridgeInputPresentation.PhaseCount(Data);
        loadConvention.Text = BridgeInputPresentation.LoadConvention(Data);
        }
        finally { refreshingActions = false; }
    }
}
