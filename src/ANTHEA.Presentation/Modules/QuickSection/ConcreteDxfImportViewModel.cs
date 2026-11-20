using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.QuickSection;

/// <summary>A cancellable import draft. No document changes before the caller accepts Prepared.</summary>
public sealed class ConcreteDxfImportViewModel : ObservableObject, IDialogViewModel<bool>
{
    private readonly JsonObject input;
    private readonly ConcreteDxfDrawing drawing;
    private readonly Dictionary<string, string> holeLayers, barLayers;
    private string error = "";
    public ConcreteDxfImportViewModel(JsonObject input, ConcreteDxfDrawing drawing)
    {
        this.input = (JsonObject)input.DeepClone(); this.drawing = drawing;
        ApplyCommand = new RelayCommand(() => Close(true), () => Prepared is not null);
        CancelCommand = new RelayCommand(() => Close(false));
        string? Preferred(IEnumerable<string> values, params string[] names) => values.FirstOrDefault(v => names.Contains(v, StringComparer.OrdinalIgnoreCase));
        string outline = drawing.Contours.Count == 1 ? drawing.Contours[0]
            : drawing.Entities.Where(e => e.IsContour && new[] { "CONTORNO", "CLS", "CALCESTRUZZO" }.Contains(e.Layer, StringComparer.OrdinalIgnoreCase)).Select(e => e.Label).FirstOrDefault() ?? ConcreteDxfImport.NoSelection;
        string? bars = Preferred(drawing.BarLayers, "ARMATURE", "ARMATURA", "BARRE", "REBARS");
        if (bars is null && drawing.BarLayers.Count == 1) bars = drawing.BarLayers[0];
        string? holes = Preferred(drawing.Layers, "FORI", "FORO", "HOLES");
        holeLayers = LayerChoices(drawing.Layers, ConcreteDxfImport.NoHoles);
        barLayers = LayerChoices(drawing.BarLayers, ConcreteDxfImport.NoSelection);
        string Choice(Dictionary<string, string> choices, string? layer, string placeholder) => layer is null ? placeholder : choices.Single(p => p.Value == layer).Key;
        var values = new JsonObject { ["unita"] = drawing.Unit, ["contorno"] = outline,
            ["fori"] = Choice(holeLayers, holes, ConcreteDxfImport.NoHoles), ["barre"] = Choice(barLayers, bars, ConcreteDxfImport.NoSelection) };
        Form = new(values,
        [
            new("unita", "Unità del disegno", Choices: [ConcreteDxfImport.NoSelection, .. ConcreteDxfImport.Units], Wide: true),
            new("contorno", "Contorno esterno", Choices: [ConcreteDxfImport.NoSelection, .. drawing.Contours], Wide: true),
            new("fori", "Layer dei fori", Choices: [ConcreteDxfImport.NoHoles, .. holeLayers.Keys], Wide: true),
            new("barre", "Layer delle armature", Choices: [ConcreteDxfImport.NoSelection, .. barLayers.Keys], Wide: true)
        ], _ => Refresh(), compact: true);
        Refresh();
    }
    public string Title => "Importa sezione da DXF";
    public string FileName => drawing.FileName;
    public string Instructions => "Contorno e fori: polilinee 2D chiuse o cerchi. Armature: cerchi con il diametro reale delle barre, su un layer distinto dai fori. Sezione nel piano XY a quota zero. Le staffe non vengono importate.";
    public string Notes => string.Join("\n", drawing.Notes);
    public FormViewModel Form { get; }
    public ConcreteDxfPrepared? Prepared { get; private set; }
    public ConcreteGeometry? Geometry => Prepared?.Geometry;
    public string Summary => Prepared?.Summary ?? "";
    public string Error { get => error; private set => SetProperty(ref error, value); }
    public bool Result { get; private set; }
    public IRelayCommand ApplyCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public event EventHandler? CloseRequested;
    public event EventHandler? PreviewChanged;
    private static Dictionary<string, string> LayerChoices(IReadOnlyList<string> layers, string placeholder)
    {
        var names = layers.ToHashSet(StringComparer.Ordinal);
        var choices = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var layer in layers)
        {
            string label = layer;
            if (string.Equals(label, placeholder, StringComparison.OrdinalIgnoreCase))
            {
                label = "Layer: " + layer;
                while (names.Contains(label) || choices.ContainsKey(label) || label == placeholder) label = "Layer: " + label;
            }
            choices.Add(label, layer);
        }
        return choices;
    }
    private void Refresh()
    {
        try
        {
            string? holes = Form["fori"].SelectedChoice == ConcreteDxfImport.NoHoles ? null
                : holeLayers.TryGetValue(Form["fori"].SelectedChoice ?? "", out var holeLayer) ? holeLayer
                : throw new ArgumentException("Scegliere un layer dei fori presente nel disegno.");
            string bars = barLayers.TryGetValue(Form["barre"].SelectedChoice ?? "", out var barLayer) ? barLayer : "";
            Prepared = ConcreteDxfImport.Prepare(input, drawing, new(Form["unita"].SelectedChoice ?? "", Form["contorno"].SelectedChoice ?? "",
                holes, bars));
            Error = "";
        }
        catch (ArgumentException failure) { Prepared = null; Error = failure.Message; }
        OnPropertyChanged(nameof(Prepared)); OnPropertyChanged(nameof(Geometry)); OnPropertyChanged(nameof(Summary));
        ApplyCommand.NotifyCanExecuteChanged(); PreviewChanged?.Invoke(this, EventArgs.Empty);
    }
    private void Close(bool apply)
    {
        if (apply) { Form.Commit(); Refresh(); if (Prepared is null) return; }
        Result = apply; CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
