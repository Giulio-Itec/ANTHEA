using System.Text.Json.Nodes;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Modules;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.BridgeDesign;

/// <summary>Draws the bridge of the sheet (in the desktop: BridgeDesignDrawing.Png, 1200 × 480) for the PNG export and for the report.</summary>
public interface IBridgeDesignFigureRenderer
{
    /// <summary>The PNG of the elevation (<paramref name="section"/> false) or of the cross section of <paramref name="data"/> with <paramref name="result"/>.</summary>
    byte[] Drawing(JsonObject data, BridgeDesignModel.Result result, bool section);
}

/// <summary>How a text of a panel reads (the size, weight and colour of the historic view): body, title, highlighted, note, warning, error.</summary>
public enum BridgeDesignTextRole { Body, Title, Highlight, Note, Warning, Error }

/// <summary>An element of a tab of the inputs or of a panel of the results, in reading order (the view picks its template by type).</summary>
public abstract class BridgeDesignItem : ObservableObject;

/// <summary>A text of a panel.</summary>
public sealed class BridgeDesignText(string text, BridgeDesignTextRole role = BridgeDesignTextRole.Body) : BridgeDesignItem
{
    public string Text { get; } = text;
    public BridgeDesignTextRole Role { get; } = role;
}

/// <summary>A table of a panel: columns and rows already formatted; the selection of the ranking of the search goes to its handler.</summary>
public sealed class BridgeDesignTable : BridgeDesignItem
{
    private readonly Action<int>? selected;
    private int selectedIndex = -1;

    internal BridgeDesignTable(IReadOnlyList<string> columns, string[][] rows, Action<int>? selected = null)
    {
        Columns = columns; Rows = rows; this.selected = selected;
    }

    public IReadOnlyList<string> Columns { get; }
    public string[][] Rows { get; }

    /// <summary>The row selected (−1 none). The user selects only the rows of the ranking.</summary>
    public int SelectedIndex
    {
        get => selectedIndex;
        set { if (SetProperty(ref selectedIndex, value)) selected?.Invoke(value); }
    }

    /// <summary>The selection written by the sheet, without its handler.</summary>
    internal void Select(int index) => SetProperty(ref selectedIndex, index, nameof(SelectedIndex));
}

/// <summary>A button of a panel: caption and command.</summary>
public sealed class BridgeDesignButton(string caption, IRelayCommand command) : BridgeDesignItem
{
    public string Caption { get; } = caption;
    public IRelayCommand Command { get; } = command;
}

/// <summary>A group that the user opens (an expander): header and items.</summary>
public sealed class BridgeDesignGroup(string header, IReadOnlyList<BridgeDesignItem> items) : BridgeDesignItem
{
    public string Header { get; } = header;
    public IReadOnlyList<BridgeDesignItem> Items { get; } = items;
}

/// <summary>The moment diagram of the details: the result it draws (the BridgeConceptMomentPlot of the historic view stays WPF, decision Q3).</summary>
public sealed class BridgeDesignMomentChart(BridgeDesignModel.Result result) : BridgeDesignItem
{
    public BridgeDesignModel.Result Result { get; } = result;
}

/// <summary>
/// A parameter of the model (the row of the historic view: label, text, unit, and the slider of the inputs): the raw text of the document,
/// the red border of a value typed that is not a number or is outside the range, the value of the slider. The text and the slider go to
/// the sheet, which refuses them when read-only.
/// </summary>
public sealed class BridgeDesignParameter : BridgeDesignItem
{
    private readonly BridgeDesignSheetViewModel sheet;
    private string text;
    private bool invalid;
    private double slider;

    internal BridgeDesignParameter(BridgeDesignSheetViewModel sheet, string group, BridgeDesignModel.Parameter parameter, bool hasSlider, string text, double value)
    {
        this.sheet = sheet; Group = group; Parameter = parameter; HasSlider = hasSlider; this.text = text;
        slider = Math.Clamp(value, parameter.Min, parameter.Max);
    }

    /// <summary>«input», «rates» or «assumptions».</summary>
    public string Group { get; }
    public BridgeDesignModel.Parameter Parameter { get; }
    public string Key => Parameter.Key;
    public string Label => Parameter.Label;
    public string Unit => Parameter.Unit;
    public bool HasSlider { get; }

    /// <summary>The text of the field; the user types it (refused when read-only).</summary>
    public string Text { get => text; set => sheet.EditParameter(this, value); }

    /// <summary>True after a value typed that is not a number or is outside the range; kept when the sheet rewrites the text (undo).</summary>
    public bool Invalid { get => invalid; internal set => SetProperty(ref invalid, value); }

    /// <summary>The value of the slider, within the range; the user moves it (refused when read-only).</summary>
    public double SliderValue { get => slider; set => sheet.MoveSlider(this, value); }

    internal void SetText(string value) => SetProperty(ref text, value, nameof(Text));
    internal void SetSlider(double value) => SetProperty(ref slider, Math.Clamp(value, Parameter.Min, Parameter.Max), nameof(SliderValue));
}

/// <summary>A choice of the inputs: key in «input», label, items and the item chosen (refused when read-only).</summary>
public sealed class BridgeDesignChoice : BridgeDesignItem
{
    private readonly BridgeDesignSheetViewModel sheet;
    private string? selected;

    internal BridgeDesignChoice(BridgeDesignSheetViewModel sheet, string key, string label, IReadOnlyList<string> choices, string value)
    {
        this.sheet = sheet; Key = key; Label = label;
        Choices = choices.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray();
        selected = Choices.Contains(value) ? value : null;
    }

    public string Key { get; }
    public string Label { get; }
    public IReadOnlyList<string> Choices { get; }
    public string? Selected { get => selected; set => sheet.Choose(this, value); }
    internal void SetSelected(string value) => SetProperty(ref selected, value, nameof(Selected));
}

/// <summary>An option of the spans: key in «input», label, state (refused when read-only).</summary>
public sealed class BridgeDesignOption : BridgeDesignItem
{
    private readonly BridgeDesignSheetViewModel sheet;
    private bool value;

    internal BridgeDesignOption(BridgeDesignSheetViewModel sheet, string key, string label, bool value) { this.sheet = sheet; Key = key; Label = label; this.value = value; }

    public string Key { get; }
    public string Label { get; }
    public bool IsChecked { get => value; set => sheet.SetOption(this, value); }
    internal void Set(bool state) => SetProperty(ref value, state, nameof(IsChecked));
}

/// <summary>A typology on its button: identifier, name, usual spans, chosen.</summary>
public sealed record BridgeDesignFamilyOption(string Id, string Name, string Range, bool IsSelected);

/// <summary>The buttons of the typologies; <see cref="SelectCommand"/> takes the identifier (refused when read-only).</summary>
public sealed class BridgeDesignFamilies(IReadOnlyList<BridgeDesignFamilyOption> families, IRelayCommand<string> select) : BridgeDesignItem
{
    public IReadOnlyList<BridgeDesignFamilyOption> Families { get; } = families;
    public IRelayCommand<string> SelectCommand { get; } = select;
}

/// <summary>A metric under the drawing: title, value and caption.</summary>
public sealed record BridgeDesignMetric(string Label, string Value, string Caption);

/// <summary>What a chart of the search draws: the trials, the solutions and the reference design (the BridgeOptimizationPlot of the historic view stays WPF).</summary>
public sealed record BridgeDesignSearchData(IReadOnlyList<BridgeDesignModel.OptimizationTrial> Trials, IReadOnlyList<BridgeDesignModel.OptimizationSolution> Solutions,
    BridgeDesignModel.Result? Baseline)
{
    internal static BridgeDesignSearchData Empty(BridgeDesignModel.Result? baseline) => new([], [], baseline);
}

/// <summary>
/// The calculation of the sheet as the historic view runs it (F5.12): the analysis of Bridge Design (the bridge through the calculation
/// service of the context, then the alternative A and the alternatives through the service of the module), returned as the calculation of
/// the module with the analysis as its typed part; the end of every run is posted to the synchronization context of the sheet, after the
/// result and before its publication, as the finally of the historic work («occupato» until the engine returns, as F5.9 and F5.11).
/// </summary>
internal sealed class BridgeDesignEngine(IModuleService inner) : IModuleService
{
    private readonly SynchronizationContext? context = SynchronizationContext.Current;

    /// <summary>Raised on the thread of the sheet when an engine run ends (completed, failed or cancelled).</summary>
    internal event Action? Ended;

    public string ModuleId => inner.ModuleId;

    public JsonObject Prepare(JsonObject source) => inner.Prepare(source);

    public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
    {
        try
        {
            var analysis = BridgeDesignAnalysis.Calculate(source, cancellation, inner);
            return new ModuleCalculation(inner.ModuleId, source, analysis.Current.Json(), analysis);
        }
        finally
        {
            if (context is null) Ended?.Invoke();
            else context.Post(_ => Ended?.Invoke(), null);
        }
    }
}
