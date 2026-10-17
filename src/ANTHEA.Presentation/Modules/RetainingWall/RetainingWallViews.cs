using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Reporting;
using Anthea.Application.Walls;
using Anthea.Presentation.Forms;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Modules.RetainingWall;

/// <summary>What a block of a panel of the wall sheet is.</summary>
public enum WallBlockKind { Text, Table, Button, Group }

/// <summary>Size and emphasis of a text of a panel, as the historic view wrote it (Ui.Text size, bold, muted).</summary>
public enum WallTextStyle { Small, SmallMuted, Normal, NormalBold, Large }

/// <summary>What selecting a row of a table of a panel does.</summary>
public enum WallRowAction
{
    None,
    /// <summary>A check of a member at a position: the section that can be sent to the module c.a.</summary>
    CheckSection,
    /// <summary>The internal forces of a section: the section that can be sent to the module c.a.</summary>
    ForceSection,
    /// <summary>A layer of the pressures: highlighted in the drawing of the inputs.</summary>
    Pressure,
    /// <summary>A combination of the global stability: shown in the drawing.</summary>
    GlobalCombination,
    /// <summary>A slice of the critical surface: highlighted in the drawing of the global stability.</summary>
    Slice
}

/// <summary>
/// A block of a panel of results (F5.9): a text, a table with its columns and the rows already formatted, a button with its command,
/// or an expandable group of blocks. The panels of the sheet are lists of these blocks, the plain data of the historic WPF panels.
/// </summary>
public sealed record WallPanelBlock(WallBlockKind Kind, string Text, IReadOnlyList<string>? Columns = null, IReadOnlyList<string[]>? Rows = null,
    IReadOnlyList<WallPanelBlock>? Children = null, WallTextStyle Style = WallTextStyle.Normal, double Height = double.NaN, WallRowAction RowAction = WallRowAction.None,
    string? Command = null, bool Expanded = false)
{
    public static WallPanelBlock Of(string text, WallTextStyle style = WallTextStyle.Normal) => new(WallBlockKind.Text, text, Style: style);
    public static WallPanelBlock Table(IReadOnlyList<string> columns, IReadOnlyList<string[]> rows, double height = double.NaN, WallRowAction action = WallRowAction.None)
        => new(WallBlockKind.Table, "", columns, rows, Height: height, RowAction: action);
    public static WallPanelBlock Group(string title, IReadOnlyList<WallPanelBlock> children, bool expanded = false) => new(WallBlockKind.Group, title, Children: children, Expanded: expanded);
    public static WallPanelBlock Button(string caption, string command) => new(WallBlockKind.Button, caption, Command: command);
}

/// <summary>A card of the inputs (an expander of the historic view): visible and expanded.</summary>
public sealed class WallCardViewModel(string key, bool expanded) : ObservableObject
{
    private bool visible = true, isExpanded = expanded;
    public string Key { get; } = key;
    public bool IsVisible { get => visible; internal set => SetProperty(ref visible, value); }
    public bool IsExpanded { get => isExpanded; set => SetProperty(ref isExpanded, value); }
}

/// <summary>
/// A table of the inputs (a JsonGrid of the historic view): its columns, its rows on the objects they edit (the data, or a display copy)
/// and the selected row. The selection follows the rows as the WPF selection does: removing the selected row clears it (and is
/// notified), removing or moving other rows keeps the selected row without a notification.
/// </summary>
public sealed class WallTableViewModel : ObservableObject
{
    private int selected = -1;
    private IReadOnlyList<FieldSpec> columns;
    private readonly Action<int>? selectionChanged;

    internal WallTableViewModel(string key, IReadOnlyList<FieldSpec> columns, Action<int>? selectionChanged = null)
    {
        Key = key; this.columns = columns; this.selectionChanged = selectionChanged;
    }

    public string Key { get; }

    public IReadOnlyList<FieldSpec> Columns { get => columns; internal set => SetProperty(ref columns, value); }

    public JsonRowCollection Rows { get; } = [];

    /// <summary>The selected row, −1 without; setting it is the selection of the user.</summary>
    public int SelectedIndex
    {
        get => selected;
        set { if (value < -1 || value >= Rows.Count || value == selected) return; selected = value; OnPropertyChanged(); selectionChanged?.Invoke(value); }
    }

    /// <summary>Removes a row as the grid does: the selection is cleared (notified) when it is the selected row, otherwise it follows its row.</summary>
    internal void RemoveAt(int index)
    {
        Rows.RemoveAt(index);
        if (index == selected) { selected = -1; OnPropertyChanged(nameof(SelectedIndex)); selectionChanged?.Invoke(-1); }
        else if (index < selected) { selected--; OnPropertyChanged(nameof(SelectedIndex)); }
    }

    /// <summary>Moves a row; the selected row keeps its selection, without a notification.</summary>
    internal void Move(int from, int to)
    {
        Rows.Move(from, to);
        if (selected == from) selected = to;
        else if (from < selected && to >= selected) selected--;
        else if (from > selected && to <= selected) selected++;
        OnPropertyChanged(nameof(SelectedIndex));
    }

    /// <summary>Removes every row; a selection is cleared and notified.</summary>
    internal void Clear()
    {
        Rows.Clear();
        if (selected != -1) { selected = -1; OnPropertyChanged(nameof(SelectedIndex)); selectionChanged?.Invoke(-1); }
    }
}

/// <summary>The drawing of the inputs (RetainingWallDrawing): combination, result, selections and switches.</summary>
public sealed record WallDrawingState(string? CaseName, bool HasCalculation, string? SelectedAction, int SelectedLayer, bool SelectedValley, int SelectedPressure,
    bool ShowLoads, bool ShowLabels, bool ShowRebar, bool ShowPressures);

/// <summary>The drawing of the results (the diagrams): visibility, view, member, combination and switches.</summary>
public sealed record WallDiagramState(bool Visible, string Mode, bool Diagrams, string Member, string? CaseName, bool ShowRebar, bool ShowLoads, bool WideLimit);

/// <summary>The drawing of the global stability in the results.</summary>
public sealed record WallGlobalDrawingState(bool Visible, string? CaseName, bool HasResult, bool FocusCritical, int SelectedSlice);

/// <summary>The preview of the global stability in the inputs.</summary>
public sealed record WallGlobalPreviewState(string? CaseName, bool HasResult, int SelectedSoil, string SelectedColumn);

/// <summary>Draws the figures of the report of the wall (in the desktop: ModuleFigureRenderer.WallFigures).</summary>
public interface IRetainingWallFigureRenderer
{
    /// <summary>The figures of RetainingWallPresentation.Figures, in its order, with its captions, categories and aspect ratios.</summary>
    IReadOnlyList<ReportFigure> Render(WallModel.Result calculation, WallReportPresentation context);

    /// <summary>The figure of «Word globale» (RetainingWallPresentation.GlobalExportFigure): the global stability of <paramref name="input"/> with the combination shown.</summary>
    ReportFigure RenderGlobal(JsonObject input, SlopeResult result, SlopeCaseResult? shown);
}

/// <summary>
/// The calculation service of the sheet with the end of every engine run reported on the thread of the sheet (F5.9): the historic
/// view stays «busy» until the engine returns, also when its request was overtaken by an edit. The end is posted to the
/// synchronization context of the sheet, after the result and before its publication, as the finally of the historic work.
/// </summary>
internal sealed class WallEngineWatch(IModuleService inner) : IModuleService
{
    private readonly SynchronizationContext? context = SynchronizationContext.Current;

    /// <summary>Raised on the thread of the sheet when an engine run ends (completed, failed or cancelled).</summary>
    internal event Action? Ended;

    /// <summary>
    /// One engine at a time, as the one scheduler of the historic view: the calculation waits here for the work that took its place
    /// (the search of the reinforcement, the independent global calculation), which waits here for the calculation still running.
    /// </summary>
    internal SemaphoreSlim Serial { get; } = new(1, 1);

    public string ModuleId => inner.ModuleId;

    public JsonObject Prepare(JsonObject source) => inner.Prepare(source);

    public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
    {
        bool entered = false;
        try
        {
            Serial.Wait(cancellation); entered = true;
            return inner.Calculate(source, cancellation);
        }
        finally
        {
            if (context is null) Ended?.Invoke();
            else context.Post(_ => Ended?.Invoke(), null);
            if (entered) Serial.Release();
        }
    }
}
