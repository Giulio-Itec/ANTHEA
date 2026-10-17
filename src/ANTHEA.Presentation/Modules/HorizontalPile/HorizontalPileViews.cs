using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Reports;
using X.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.HorizontalPile;

/// <summary>
/// The c.a. section drawn next to the materials, as the historic SectionDrawing keeps it: outline, bars, link radius and diameter,
/// axes and caption. When the section cannot be drawn, outline, bars and link radius are cleared and the rest stays as it was.
/// </summary>
public sealed record HorizontalPileSectionDrawing(IReadOnlyList<double[]> Outline, IReadOnlyList<ReinforcementBar> Bars, double LinkRadius, double LinkDiameter,
    bool ShowAxes, string? Caption)
{
    /// <summary>Nothing drawn yet.</summary>
    public static HorizontalPileSectionDrawing Empty { get; } = new([], [], 0, 0, false, null);
}

/// <summary>
/// A stratigraphy of the sheet: its layers (the JSON array of the document) and the rows of its table, on display copies of the layers
/// (the calculated columns «__strato» and «__kp» only there); the soil editor of a row exists once the row is shown (the selected
/// stratigraphy, as the cells of the historic table). The view finds the soil editor of a row through <see cref="SoilOf"/>, again at every
/// <see cref="SoilsVersion"/>.
/// </summary>
public sealed class HorizontalPileSurveyViewModel : ObservableObject
{
    private int soilsVersion;

    internal HorizontalPileSurveyViewModel(int index, JsonArray layers)
    {
        Index = index; Layers = layers;
    }

    /// <summary>Position, from 0 (the tab shows <see cref="Index"/> + 1).</summary>
    public int Index { get; }

    /// <summary>The header of the tab: the number of the stratigraphy.</summary>
    public string Header => (Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The layers in the document.</summary>
    public JsonArray Layers { get; }

    /// <summary>The columns of the table (the soil editor comes after the second).</summary>
    public IReadOnlyList<FieldSpec> Columns { get; } = HorizontalPileFields.Layers();

    /// <summary>The rows of the table, on display copies of the layers.</summary>
    public JsonRowCollection Rows { get; } = [];

    /// <summary>The soil editors of the rows, by row; a row without one is not shown yet.</summary>
    public Dictionary<JsonRowViewModel, HorizontalSoilRowViewModel> Soils { get; } = [];

    /// <summary>The soil editor of <paramref name="row"/>, null while the row has none.</summary>
    public HorizontalSoilRowViewModel? SoilOf(JsonRowViewModel? row) => row is not null && Soils.TryGetValue(row, out var soil) ? soil : null;

    /// <summary>Changes when a soil editor is added to <see cref="Soils"/> or removed from it.</summary>
    public int SoilsVersion => soilsVersion;

    /// <summary>«Aggiungi strato» (disabled when read-only).</summary>
    public IRelayCommand? AddLayerCommand { get; internal set; }

    /// <summary>The [−] of a layer, with its row as parameter (disabled when read-only).</summary>
    public IRelayCommand<JsonRowViewModel>? RemoveLayerCommand { get; internal set; }

    internal void SoilsChanged()
    {
        soilsVersion++;
        OnPropertyChanged(nameof(SoilsVersion));
    }
}

/// <summary>
/// The soil editor of a layer (HorizontalSoilEditor of the historic view): the horizontal soil reaction of the layer, edited in its
/// document object. Category (or density), law, parameter, minimum, maximum, the adopted value (or the tabulated one), the «nh*»
/// override with its value, the description; in the details, the source, the reason of the override, «Adotta nuova media» and
/// «Mantieni valore con nuova fonte». Every change calls the change of the sheet; a change of the type of the layer (seen through the
/// notifications of its row) selects the soil of the new type, as the historic editor does. Refused when the sheet cannot be edited.
/// </summary>
public sealed class HorizontalSoilRowViewModel : ObservableObject
{
    private readonly JsonRowViewModel row;
    private readonly JsonObject layer;
    private readonly Action changed;
    private readonly Func<bool> canEdit;
    private HorizontalSoilEditorState state = null!;
    private JsonObject soil = null!;
    private string type = "", value = "", nh = "", description = "", details = "";
    private bool building, active;

    internal HorizontalSoilRowViewModel(JsonRowViewModel row, JsonObject layer, Action changed, Func<bool> canEdit)
    {
        this.row = row; this.layer = layer; this.changed = changed; this.canEdit = canEdit;
    }

    /// <summary>Categories of the cohesive soils or densities of the granular ones.</summary>
    public IReadOnlyList<string> Categories => state.Categories;

    /// <summary>The chosen category or density; empty when the soil has one that is not an item.</summary>
    public string Category
    {
        get => state.Categories.Contains(soil.S(CategoryKey)) ? soil.S(CategoryKey) : "";
        set
        {
            if (building || !canEdit() || !state.Categories.Contains(value) || value == Category) return;
            HorizontalSoilPresentation.Select(layer, CategoryKey, value);
            Edited(CategoryKey);
            Build();
        }
    }

    /// <summary>The laws (and the authors of table 14.6) of the category.</summary>
    public IReadOnlyList<string> Laws => state.Choices.Select(c => c.Label).ToArray();

    /// <summary>The chosen law, −1 when none matches.</summary>
    public int LawIndex
    {
        get => state.SelectedChoice;
        set
        {
            if (building || !canEdit() || value < 0 || value >= state.Choices.Length || value == LawIndex) return;
            HorizontalSoilPresentation.SelectLaw(layer, state.Choices[value]);
            changed();
            Build();
        }
    }

    public string Parameter => state.Parameter;
    public string Minimum => state.Minimum;
    public string Maximum => state.Maximum;

    /// <summary>False for the table 14.5 of the sands, whose value is tabulated (<see cref="TabulatedValue"/>).</summary>
    public bool ValueEditable => state.Mode != 3;

    public string TabulatedValue => state.TabulatedValue;

    /// <summary>The adopted value, as typed: written into the soil at every change, with the record of the manual choice.</summary>
    public string Value
    {
        get => value;
        set
        {
            if (building || !canEdit() || !ValueEditable || value == this.value) return;
            this.value = value; OnPropertyChanged();
            soil[state.ValueField] = value;
            Edited(state.ValueField);
        }
    }

    /// <summary>The «nh*» check box, shown for the tables (modes 3 and 4).</summary>
    public bool OverrideShown => state.Mode >= 3;

    public bool Override
    {
        get => soil.B("override");
        set
        {
            if (building || !canEdit() || !OverrideShown) return;
            soil["override"] = value;
            changed();
            Build();
        }
    }

    /// <summary>The value of the override, shown when it is active.</summary>
    public bool NhShown => OverrideShown && soil.B("override");

    public string Nh
    {
        get => nh;
        set
        {
            if (building || !canEdit() || !NhShown || value == nh) return;
            nh = value; OnPropertyChanged();
            soil["nh_override"] = value;
            changed();
        }
    }

    /// <summary>The description of the law and of its source (tool tip of the editor).</summary>
    public string Description { get => description; private set => SetProperty(ref description, value); }

    /// <summary>«Parametro dello strato · origine e applicabilità»: the source of the value.</summary>
    public string Source
    {
        get => soil.S("fonte");
        set { if (!canEdit()) return; soil["fonte"] = value; OnPropertyChanged(); changed(); }
    }

    /// <summary>The reason of the override, in the details when it is active.</summary>
    public string OverrideReason
    {
        get => soil.S("motivo_override");
        set { if (!canEdit() || !soil.B("override")) return; soil["motivo_override"] = value; OnPropertyChanged(); changed(); }
    }

    /// <summary>The text at the top of the details: the description, or the error of «Adotta nuova media».</summary>
    public string DetailsText { get => details; private set => SetProperty(ref details, value); }

    /// <summary>«Adotta nuova media» and «Mantieni valore con nuova fonte» are offered for the correlations of the tables.</summary>
    public bool CanAdoptMean => HorizontalSoilPresentation.CanAdoptMean(soil);

    /// <summary>«Adotta nuova media».</summary>
    public void AdoptMean()
    {
        if (!canEdit()) return;
        try
        {
            HorizontalSoilPresentation.AdoptMean(soil);
            changed();
            Build();
            DetailsText = Description;
        }
        catch (ArgumentException error) { DetailsText = error.Message; }
    }

    /// <summary>«Mantieni valore con nuova fonte».</summary>
    public void KeepValue()
    {
        if (!canEdit()) return;
        HorizontalSoilPresentation.KeepValue(soil);
        changed();
        Build();
        DetailsText = Description;
    }

    private string CategoryKey => state.Sand ? "densita" : "categoria";

    /// <summary>
    /// The row is shown again (Loaded of the historic cell): the editor follows its notifications and is built from the layer. A row that
    /// stays shown keeps its editor as it is.
    /// </summary>
    internal void Activate()
    {
        if (active) return;
        row.PropertyChanged += RowChanged; active = true;
        Build();
    }

    /// <summary>The row is no longer shown (Unloaded of the historic cell).</summary>
    internal void Deactivate()
    {
        if (active) { row.PropertyChanged -= RowChanged; active = false; }
    }

    private void RowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (type == layer.S("tipologia")) return;
        HorizontalSoilPresentation.Select(layer, "tipologia", layer.S("tipologia"));
        Build();
    }

    private void Edited(string key)
    {
        HorizontalSoilPresentation.RecordManualChoice(soil, key);
        changed();
    }

    /// <summary>Build of the historic editor: the state from the layer (with its lazy defaults), the texts shown.</summary>
    private void Build()
    {
        if (building) return;
        building = true;
        try
        {
            type = layer.S("tipologia");
            state = HorizontalSoilPresentation.Prepare(layer);
            soil = state.Soil;
            value = state.Mode == 3 ? "" : soil.S(state.ValueField);
            nh = soil.S("nh_override");
            Description = HorizontalSoilPresentation.Description(soil);
            DetailsText = Description;
        }
        finally { building = false; }
        OnPropertyChanged(string.Empty);
    }
}

/// <summary>Draws the figures of the report (desktop: the diagrams of the historic view; tests: images of the same size).</summary>
public interface IHorizontalPileFigureRenderer
{
    /// <summary>The figures of <see cref="HorizontalPilePresentation.Figures"/> for <paramref name="result"/>, in its order.</summary>
    IReadOnlyList<ReportOrizzontale.Figure> Render(JsonObject result);
}
