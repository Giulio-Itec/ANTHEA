using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The section σ/ε of a domain panel (the tab «Sezione σ / ε» of the tabs 02 and 03, F5.10 sub-step S7: SectionInspection, BuildSectionInspection
/// and UpdateSectionInspection of the historic ConcreteInspection.cs), without WPF:
/// <list type="bullet">
/// <item>the solution shown (<see cref="State"/>: the action entered or the limit point, allowed in a read-only revision), the contour of the map
/// (<see cref="Contour"/>, allowed in a read-only revision, not saved), the message and the values of the solution;</item>
/// <item>the stress map (<see cref="Map"/>) and the strain plane (<see cref="Plane"/>) as plain data (decisions Q3 and C8): the drawings
/// ConcreteSectionViewport and ConcreteStrainPlaneView stay WPF; the neutral axis and the values on the bars follow the check boxes of the panel
/// (<see cref="ConcreteDomainPanelViewModel.NeutralAxis"/>, <see cref="ConcreteDomainPanelViewModel.BarValues"/>).</item>
/// </list>
/// Every update clears the solution, then, with the tab «Sezione σ / ε» chosen and a calculated row selected, reads the plane of the limit point
/// or analyses the action entered with the session of the panel (<see cref="ConcreteInspectionSession"/>, one per panel as the historic view): the
/// solution is published only when it is the last one asked and the data did not change meanwhile.
/// </summary>
public sealed class ConcreteSectionInspectionViewModel : ObservableObject
{
    private readonly ConcreteSheetViewModel sheet;
    private readonly ConcreteDomainPanelViewModel panel;
    private int state = ConcreteInspectionPresentation.DefaultState, running;
    private string contour = ConcreteSectionDrawingViewModel.Contours[0], message = ConcreteInspectionPresentation.NoSelection, values = "";
    private ConcreteStrainDiagram? plane;
    private bool closed;

    internal ConcreteSectionInspectionViewModel(ConcreteSheetViewModel sheet, ConcreteDomainPanelViewModel panel, bool neutralAxis)
    {
        this.sheet = sheet; this.panel = panel;
        Map.ShowNeutralAxis = neutralAxis;
        PropertiesCommand = new RelayCommand(sheet.OpenProperties, () => sheet.CanEdit);
    }

    /// <summary>The session of the panel: the plane of the limit point, or the analysis of the action entered, one at a time.</summary>
    internal ConcreteInspectionSession Session { get; } = new();

    /// <summary>The solutions of the choice: «Azione inserita», «Punto limite».</summary>
    public IReadOnlyList<string> States => ConcreteInspectionPresentation.States;

    /// <summary>The solution shown: 0 the action entered, 1 the limit point (at opening). Allowed in a read-only revision; no write.</summary>
    public int State
    {
        get => state;
        set
        {
            if (value < 0 || value >= States.Count || value == state) return;
            state = value; OnPropertyChanged(); OnPropertyChanged(nameof(StateText));
            _ = UpdateAsync();
        }
    }

    /// <summary>The text of the solution shown.</summary>
    public string StateText => States[state];

    /// <summary>The contours of the map.</summary>
    public IReadOnlyList<string> Contours => ConcreteSectionDrawingViewModel.Contours;

    /// <summary>The contour of the map (the first at opening): drawn again; allowed in a read-only revision; not saved, no Modified.</summary>
    public string Contour
    {
        get => contour;
        set
        {
            if (value is null || !Contours.Contains(value) || value == contour) return;
            contour = value; OnPropertyChanged();
            Map.Contour = value; Map.Invalidate();
        }
    }

    /// <summary>The section with the stresses and strains of the solution, as plain data.</summary>
    public ConcreteSectionDrawingViewModel Map { get; } = new();

    /// <summary>The strain plane of the solution (ConcreteStrainPlaneView draws it); null without a solution.</summary>
    public ConcreteStrainDiagram? Plane { get => plane; private set => SetProperty(ref plane, value); }

    /// <summary>The message under the section σ/ε.</summary>
    public string Message { get => message; private set => SetProperty(ref message, value); }

    /// <summary>The values of the solution (the tab «Valori»).</summary>
    public string Values { get => values; private set => SetProperty(ref values, value); }

    /// <summary>«Proprietà…» of the bar of the map: the window of the properties of the section (not in a read-only revision).</summary>
    public IRelayCommand PropertiesCommand { get; }

    /// <summary>A solution is being read or calculated.</summary>
    public bool IsRunning => running > 0;

    /// <summary>«Asse neutro» of the panel: the map follows.</summary>
    internal void ShowNeutralAxis(bool value) { Map.ShowNeutralAxis = value; Map.Invalidate(); }

    /// <summary>«Valori σ/ε sulle barre» of the panel: the map follows.</summary>
    internal void ShowBarValues(bool value) { Map.BarValues = value; Map.Invalidate(); }

    /// <summary>The read-only revision changed.</summary>
    internal void RefreshEditable() => PropertiesCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// The preview of the section changed (RefreshPreview): without a solution the map takes its section, cables and message. F5.15, C5·S7
    /// (F5-48): the historic map kept the section before the change until the panel solved it again.
    /// </summary>
    internal void ShowSection(ConcreteSectionDrawingViewModel preview)
    {
        if (Map.Stress is not null) return;
        Map.Section = preview.Section; Map.Tendons = preview.Tendons; Map.Message = preview.Message; Map.Invalidate();
    }

    /// <summary>The sheet was opened again or closed: a solution still running is never published.</summary>
    internal void Close()
    {
        closed = true;
        Session.Dispose();
    }

    /// <summary>
    /// UpdateSectionInspection of the historic view: the solution in progress is abandoned and cleared (the map takes the section of the control
    /// panel); with the tab «Sezione σ / ε» chosen and a calculated row selected, the solution is read or calculated and published when it is
    /// still the current one. F5.15, S7-c (F5-47): the map shows the message of the section of the control panel, and away from the tab «Sezione
    /// σ / ε» the message goes back to the first one (the historic view kept «Lettura del piano…» of a solution abandoned).
    /// </summary>
    internal async Task UpdateAsync()
    {
        Session.Invalidate();
        Map.Stress = null; ShowSection(sheet.Inputs.Drawing);
        Plane = null;
        Values = ConcreteInspectionPresentation.NoSolution;
        if (sheet.Initializing || sheet.IsDisposed || closed) return;
        if (panel.InspectionTab != 1) { Message = ConcreteInspectionPresentation.NoSelection; return; }
        var row = panel.Table.Table.Selected;
        var check = row is null ? null : sheet.DomainChecks(panel.Prefix + panel.Key)?.GetValueOrDefault(row.Values.S("id"));
        if (row is null || check?.LimitState is null) { Message = ConcreteInspectionPresentation.SelectCalculated; return; }
        bool limit = state == 1;
        var calculation = sheet.Calculation; long version = calculation.Revision;
        bool Current() => !sheet.IsDisposed && !closed && ReferenceEquals(calculation, sheet.Calculation) && version == calculation.Revision;
        Message = ConcreteInspectionPresentation.Pending(limit);
        running++;
        try
        {
            var action = ConcreteDomainChart.ReadNamedAction(row.Values);
            JsonObject options = panel.Options;
            var completion = await Session.CalculateAsync(check, limit, sheet.Input, sheet.Settings, options, panel.Key, action);
            completion.TryPublish(current =>
            {
                if (!Current()) return;
                if (current.Error is not null) { Message = ConcreteInspectionPresentation.Unavailable(current.Error.Message); Values = Message; return; }
                var solution = current.Value!;
                var section = sheet.Inputs.Drawing.Section!;
                Map.Stress = solution; Map.Invalidate();
                Plane = ConcreteStrainDiagram.Read(solution, section);
                Values = ConcreteInspectionPresentation.Values(solution, check, action, limit, options, section);
                Message = ConcreteInspectionPresentation.Completed(limit);
            });
        }
        // The historic view shows every failure of the solution in its message.
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            if (Current()) { Message = ConcreteInspectionPresentation.Unavailable(failure.Message); Values = Message; }
        }
        finally { running--; }
    }
}
