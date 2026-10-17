using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.VerticalPile;

/// <summary>
/// A stratigraphy of the sheet (a tab of the historic view): its layers as rows of the table and the columns, with the values of older
/// sheets added to the choices. Every row is the display copy of a layer of the document, as the historic JsonGrid: the calculated
/// columns (layer, coefficients, Nq, τ) are written only there; an edit of the user copies the editable columns into the layer.
/// </summary>
public sealed class VerticalPileSurveyViewModel
{
    internal VerticalPileSurveyViewModel(int index, JsonArray layers, IReadOnlyList<FieldSpec> columns, IRelayCommand addLayer, IRelayCommand<JsonRowViewModel> removeLayer)
    {
        Index = index; Layers = layers; Columns = columns; AddLayerCommand = addLayer; RemoveLayerCommand = removeLayer;
        // The items of the choice cells, as StratigraphyTable showed them: the choices of the column without empty or repeated values.
        Choices = columns.Where(c => c.Choices is not null).ToDictionary(c => c.Key,
            c => (IReadOnlyList<string>)c.Choices!.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray(), StringComparer.Ordinal);
        Help = columns.ToDictionary(c => c.Key, c => c.Key == "__tau" ? TauHelp : Anthea.Calculations.CalculationHelp.Field(c.Key), StringComparer.Ordinal);
    }

    /// <summary>The tooltip of the bond column of the micropile, as StratigraphyTable.</summary>
    public const string TauHelp = "Aderenza dell’abaco (s), applicata al solo tratto aderente nel calcolo ΔRs = π ds Δl τ. Zero se la laterale è esclusa; — se i dati sono incompleti o fuori abaco.";

    /// <summary>The tooltips of the cells, by column key (the help of the field, as StratigraphyTable; null when there is none).</summary>
    public IReadOnlyDictionary<string, string?> Help { get; }

    /// <summary>The items of the choice cells, by column key.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Choices { get; }

    /// <summary>«Aggiungi strato» (disabled when read-only).</summary>
    public IRelayCommand AddLayerCommand { get; }

    /// <summary>The [−] of a layer, with the row as parameter (disabled when read-only).</summary>
    public IRelayCommand<JsonRowViewModel> RemoveLayerCommand { get; }

    /// <summary>Position of the stratigraphy, from 0.</summary>
    public int Index { get; }

    /// <summary>Header of the tab: the number of the stratigraphy.</summary>
    public string Header => (Index + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>Columns of the table, in their order (the calculated ones are read-only).</summary>
    public IReadOnlyList<FieldSpec> Columns { get; }

    /// <summary>Layers, as the table shows them.</summary>
    public JsonRowCollection Rows { get; } = [];

    /// <summary>The layers of the document.</summary>
    internal JsonArray Layers { get; }
}

/// <summary>
/// The capacity chart as the sheet shows it: texts, depth reached by the stratigraphies and the visible series of the chosen view.
/// Plain data: the drawing stays in WPF (Plot), the report draws the same data.
/// </summary>
/// <param name="Title">Title: «Premere Calcola» before the first result, then the title of the module (kept after an edit).</param>
/// <param name="XLabel">Horizontal axis.</param>
/// <param name="YLabel">Vertical axis: depth or length along the axis (micropile, after the first result).</param>
/// <param name="CapacityDepth">Depth of the chart, the available one of the last result (0 before the first result).</param>
/// <param name="EmptyMessage">Message without curves.</param>
/// <param name="Series">Visible series of the chosen view.</param>
public sealed record VerticalPileCapacityChart(string Title, string XLabel, string YLabel, double CapacityDepth, string EmptyMessage, IReadOnlyList<PlotSeriesData> Series)
{
    /// <summary>Title of the chart before the first result.</summary>
    public const string InitialTitle = "Premere Calcola";

    /// <summary>Horizontal axis of the chart.</summary>
    public const string Axis = "Forza assiale [kN]";

    /// <summary>Vertical axis of the chart before the first result.</summary>
    public const string InitialDepthAxis = "Profondità z [m]";
}

/// <summary>A check box of the curves: the name and the value at L, its colour and whether the curve is shown.</summary>
public sealed class VerticalPileCurveChoiceViewModel : ObservableObject
{
    private readonly Action<VerticalPileCurveChoiceViewModel> changed;
    private bool isChecked;

    internal VerticalPileCurveChoiceViewModel(string key, string label, string color, bool isChecked, Action<VerticalPileCurveChoiceViewModel> changed)
    {
        Key = key; Label = label; Color = color; this.isChecked = isChecked; this.changed = changed;
    }

    /// <summary>Key of the curve (branch and condition, or action).</summary>
    public string Key { get; }

    /// <summary>Name of the curve and value at L.</summary>
    public string Label { get; }

    /// <summary>Colour of the curve (#RRGGBB or #AARRGGBB), as the series.</summary>
    public string Color { get; }

    /// <summary>The curve is shown; changing it saves the visibility of every curve in the sheet (allowed also when read-only, as today).</summary>
    public bool IsChecked
    {
        get => isChecked;
        set { if (SetProperty(ref isChecked, value)) changed(this); }
    }
}

/// <summary>A table of «Tabelle e dettagli»: the title (none for the details, whose title is the chosen item), the columns and the cells.</summary>
public sealed record VerticalPileTableBlock(string? Title, IReadOnlyList<string> Columns, IReadOnlyList<string[]> Rows);

/// <summary>What «Tabelle e dettagli» shows: the capacity at the available depth (note, tables, footnote) or one table of details.</summary>
public sealed record VerticalPileTableView(string? Note, IReadOnlyList<VerticalPileTableBlock> Tables, string? Footnote);

/// <summary>
/// Draws the figures of the report of a vertical pile sheet (proposal F5, section 5.1: the report is built by the existing generators):
/// in the desktop the WPF charts and drawings of the historic report (ModuleFigureRenderer), in the tests images of the same size.
/// </summary>
public interface IVerticalPileFigureRenderer
{
    /// <summary>The figures of <see cref="VerticalPilePresentation.Figures"/>, in its order, with its captions, categories and sizes.</summary>
    IReadOnlyList<ReportFigure> Render(VerticalPileFigureRequest request);
}

/// <summary>What the figures of the report draw: the data and the result of the sheet and its two charts as the sheet shows them.</summary>
public sealed record VerticalPileFigureRequest(JsonObject Data, JsonObject Result, bool Micro, VerticalPileCapacityChart Capacity, PlotData Reference)
{
    /// <summary>The figures to draw, in the order of the report.</summary>
    public IReadOnlyList<VerticalPileFigurePlan> Figures => VerticalPilePresentation.Figures(Data, Micro, Capacity.Title, Reference.Title);
}

/// <summary>A choice of «Copia in…» and «Copia da…»: a stratigraphy (from 0) or a new one (−1).</summary>
public sealed record SurveyCopyChoice(int Survey, string Label);

/// <summary>
/// The dialog of «Copia in…» and «Copia da…» of the stratigraphies (ChooseSurveyCopy of the historic view): the question and the
/// stratigraphies to choose from; the result is the chosen one, null when cancelled. The view is a DataTemplate of the desktop.
/// </summary>
public sealed class SurveyCopyDialogViewModel : DialogViewModel<SurveyCopyChoice>
{
    private SurveyCopyChoice? selected;

    internal SurveyCopyDialogViewModel(string title, string question, IReadOnlyList<SurveyCopyChoice> choices) : base(title)
    {
        Question = question; Choices = choices; selected = choices[0];
        ConfirmCommand = new RelayCommand(Confirm);
        DismissCommand = new RelayCommand(Dismiss);
    }

    /// <summary>«Copia» (the default button of the dialog).</summary>
    public IRelayCommand ConfirmCommand { get; }

    /// <summary>«Annulla» (the cancel button of the dialog).</summary>
    public IRelayCommand DismissCommand { get; }

    /// <summary>The question above the choice.</summary>
    public string Question { get; }

    /// <summary>The stratigraphies to choose from (and «Nuova stratigrafia» for «Copia in…»).</summary>
    public IReadOnlyList<SurveyCopyChoice> Choices { get; }

    /// <summary>The chosen stratigraphy, the first at opening.</summary>
    public SurveyCopyChoice? Selected { get => selected; set => SetProperty(ref selected, value); }

    /// <summary>«Copia».</summary>
    public void Confirm() { if (Selected is not null) Close(Selected); }

    /// <summary>«Annulla».</summary>
    public void Dismiss() => Cancel();
}
