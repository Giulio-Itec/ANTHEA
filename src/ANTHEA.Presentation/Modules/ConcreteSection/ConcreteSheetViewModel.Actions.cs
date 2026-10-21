using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Services;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The side of the sheet that the actions (sub-step S2, <see cref="ConcreteActionsViewModel"/>) use: the rows of every limit state and of
/// the shear, their creation and storing, the paste that writes without invalidating row by row, the status line, the outcome of a domain for
/// the filter of its table, the services and the window of the actions applied to the sheet. The rows themselves, their invalidations and
/// their outputs stay in ConcreteSheetViewModel.Checks.cs, where S1 put them.
/// </summary>
public sealed partial class ConcreteSheetViewModel
{
    // The editor of the actions opens the sheet again on its data; the calculation that the new historic workspace queues when it is loaded
    // waits one turn of the UI thread.
    private bool deferViewLoaded;

    /// <summary>The actions (S2): the tables of the panels, the file of the actions, the window «Sollecitazioni della sezione».</summary>
    public ConcreteActionsViewModel Actions { get; private set; } = null!;

    /// <summary>The rows of the actions of every limit state, in the order SLU, SLV, SLE, SLE_FREQ, SLE_QP.</summary>
    public IReadOnlyDictionary<string, JsonRowCollection> ActionRowSets => actions;

    /// <summary>The rows of the shear (historic actions; with the unified actions, those derived from them).</summary>
    public JsonRowCollection ShearRowCollection => shearRows;

    internal JsonRowCollection ActionRows(string key) => actions[key];

    internal JsonRowViewModel NewActionRow(string key, string name, string n, string mx, string my) => CreateAction(key, name, n, mx, my);

    internal JsonRowViewModel NewShearRow(string name) => ShearRow(ConcreteActionPresentation.NewShearRow(name));

    /// <summary>The rows of the shear into the options of the shear (Store of BuildShearPanel).</summary>
    internal void StoreShearRows() => ShearOptions["azioni"] = ConcreteSheetPreparation.StoreShearRows(shearRows.Select(r => r.Values));

    /// <summary>Writes into the rows without their invalidations (the flag «synchronizing» of the historic paste).</summary>
    internal void Synchronized(Action write)
    {
        synchronizing = true;
        try { write(); }
        finally { synchronizing = false; }
    }

    /// <summary>The status line, written by the commands of the tables.</summary>
    internal void ShowStatus(string text) => Status = text;

    /// <summary>The outcome of the row <paramref name="id"/> in the domain <paramref name="prefix"/> («3D:» or «2D:») of the limit state shown.</summary>
    internal ConcreteResults.Check? DomainCheck(string prefix, string id) =>
        domainResults.TryGetValue(prefix + DomainKey(prefix), out var checks) ? checks.GetValueOrDefault(id) : null;

    /// <summary>The limit state of a domain panel: chosen through the field «stato» of its form, whose callback attaches the rows again (S3).</summary>
    internal void ChooseDomainState(string prefix, string state) => DomainPanel(prefix).Form.Set("stato", state);

    internal IFileDialogService Files => Context.Services.Files;
    internal IClipboardService Clipboard => Context.Services.Clipboard;
    internal IMessageService Messages => Context.Services.Messages;

    /// <summary>
    /// «Applica» of the window of the actions (OpenActionEditor of the historic view): the calculation in progress is superseded, the actions and
    /// the settings of the window replace those of the sheet, Modified, and the sheet opens again on its data (<see cref="SheetViewModelBase.Reload"/>
    /// in place of ReloadRequested), with the calculation of the opening one turn of the UI thread later.
    /// </summary>
    internal void ApplyActionEditor(JsonObject result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Calculation.InvalidateAll(); ResetResult();
        Data["combinazioni"] = result["combinazioni"]!.DeepClone();
        Data["workspace_ca"] = result["workspace_ca"]!.DeepClone();
        RaiseModified();
        deferViewLoaded = true;
        try { Reload(Data); }
        finally { deferViewLoaded = false; }
    }

    // The new historic workspace shown, then loaded: the domain panel of the tab writes the outputs of its rows at once (IsVisibleChanged,
    // with the layout of the window), the calculation of the opening is queued by the Loaded handler: at once, or one turn of the UI thread
    // later after the editor of the actions.
    private void ViewLoaded()
    {
        ShowPanel(tab);
        if (HasResults) return;
        if (!deferViewLoaded || ui is null) { Calculation.Queue(); return; }
        var calculation = Calculation;
        ui.Post(_ => { if (!IsDisposed && ReferenceEquals(calculation, Calculation) && !HasResults) calculation.Queue(); }, null);
    }

    /// <summary>
    /// A tab shown: the table of a domain panel writes the outputs of the rows of its limit state when they are not written yet (the
    /// IsVisibleChanged of the historic grid, RefreshDomainPanel).
    /// </summary>
    private void ShowPanel(int shown)
    {
        foreach (var panel in DomainPanels) panel.Shown();
        // The family of the serviceability on screen fills its detail and its map again (S4); the shear its detail and its section (S5).
        if (shown == StressTab && stressFamily < stressPanels.Count) StressPanels[stressFamily].Shown();
        if (shown == ShearTab) Shear?.Shown();
    }
}
