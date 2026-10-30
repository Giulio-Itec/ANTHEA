using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Materials;
using Anthea.Application.Modules;
using Anthea.Application.Scheduling;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>Cables, second layers, links and quick resistances of the inputs (ConcreteTendons.cs, ConcreteLayers.cs, ConcreteParameters.cs and ConcreteQuickResistance.cs of the historic view).</summary>
public sealed partial class ConcreteInputViewModel
{
    private readonly RecalculationScheduler quickResistanceScheduler = new();
    private List<JsonObject> cableMaterials = [];
    private string cableMessage = "", quickResistanceText = "Inserire N per calcolare le quattro resistenze.";
    private bool quickResistanceTextVisible = true;
    private int quickResistanceRuns;
    private IReadOnlyList<ConcreteQuickResistanceCard> quickResistanceCards = [];

    /// <summary>The cables of the section (<c>trefoli</c>): coordinates, equivalent diameter and area, material and its law, σp0.</summary>
    public ConcreteTableViewModel Tendons { get; }
    /// <summary>The material of the next cable («+ Cavo / trefolo»).</summary>
    public ConcreteChoiceViewModel CableMaterial { get; } = new();
    /// <summary>Why the last «+ Cavo / trefolo» was refused; empty otherwise.</summary>
    public string CableMessage { get => cableMessage; private set => SetProperty(ref cableMessage, value); }
    /// <summary>«+ Cavo / trefolo» and «−» (the selected cable).</summary>
    public IRelayCommand AddCableCommand { get; }
    public IRelayCommand RemoveCableCommand { get; }

    /// <summary>The text of the quick resistances (hidden when the cards are shown).</summary>
    public string QuickResistanceText { get => quickResistanceText; private set => SetProperty(ref quickResistanceText, value); }
    public bool QuickResistanceTextVisible { get => quickResistanceTextVisible; private set => SetProperty(ref quickResistanceTextVisible, value); }
    /// <summary>Mx± and My± at the N of the form, one card per direction.</summary>
    public IReadOnlyList<ConcreteQuickResistanceCard> QuickResistanceCards { get => quickResistanceCards; private set => SetProperty(ref quickResistanceCards, value); }
    /// <summary>True while the quick resistances are calculated.</summary>
    public bool IsQuickResistanceRunning => quickResistanceRuns > 0;

    private IEnumerable<JsonObject> TendonMaterials() => ConcreteInputPresentation.TendonMaterials(Settings);

    private static ConcreteColumnViewModel[] TendonColumns() =>
    [
        new("id", "ID"), new("x", "x [mm]"), new("y", "y [mm]"),
        new("materiale", "Materiale", ConcreteColumnKind.Choice, choices: MaterialInputCatalog.Steel(true).Select(m => m.S("nome")).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray()),
        new("sigma0", "Tensione iniziale σp0 [MPa]"), new("diametro", "Øeq cavo [mm]"), new("area", "Ap totale [mm²]"),
        new("diagramma", "Diagramma", ConcreteColumnKind.Choice, choices: ["Elastoplastico", "Incrudente"]),
        new("Ep", "Ep [MPa]", readOnly: true), new("fpyk", "fpyk [MPa]", readOnly: true), new("fpk", "fpk [MPa]", readOnly: true), new("eps_u", "εpu [‰]", readOnly: true)
    ];

    // A cable: the area and the equivalent diameter follow each other; a material chosen in the row copies its properties.
    private JsonRowViewModel TendonRow(JsonObject values)
    {
        if (!values.ContainsKey("diametro")) values["diametro"] = ConcreteInputPresentation.TendonDiameter(values);
        JsonRowViewModel? row = null;
        row = new JsonRowViewModel(values, field =>
        {
            if (field is "area" or "diametro") row!.Output(field == "area" ? "diametro" : "area", ConcreteInputPresentation.TendonCounterpart(field, row.Values.S(field)));
            if (field == "materiale")
            {
                var selected = TendonMaterials().FirstOrDefault(m => m.S("nome") == values.S("materiale"));
                if (selected is not null) ApplyTendonMaterial(row!, selected);
            }
            TendonsChanged();
        });
        return row;
    }

    private static void ApplyTendonMaterial(JsonRowViewModel row, JsonObject material)
    {
        foreach (string key in ConcreteInputPresentation.TendonMaterialKeys) row.Output(key, ConcreteInputPresentation.TendonMaterialValue(material, key));
        row.Output("materiale", material.S("nome")); row.Output("materiale_id", material.S("id"));
    }

    private void TendonsChanged()
    {
        Settings["trefoli"] = new JsonArray(Tendons.Rows.Select(r => (JsonNode?)r.Values.DeepClone()).ToArray());
        sheet.Invalidate();
    }

    private FormViewModel BuildTendonInput()
    {
        if (Settings["materiale_trefolo"] is not JsonObject) Settings["materiale_trefolo"] = TendonMaterials().First(m => m.S("nome") == "Y1860").DeepClone();
        ReloadTendonMaterials();
        return Form(ConcreteInputPresentation.NewTendonForm(), [new("numero", "Numero trefoli nel cavo"), new("diametro", "Ø equivalente singolo trefolo", "mm"),
            new("x", "x cavo", "mm"), new("y", "y cavo", "mm"), new("sigma0", "Tensione iniziale σp0", "MPa")], _ => { });
    }

    // The materials of the cables (the saved default added when it is not listed), the material of the next cable and the column of the table.
    private void ReloadTendonMaterials()
    {
        cableMaterials = TendonMaterials().ToList();
        if (Settings["materiale_trefolo"] is JsonObject current && !cableMaterials.Any(m => m.S("id") == current.S("id"))) cableMaterials.Add(current);
        CableMaterial.SetItems(cableMaterials.Select(m => m.S("nome", "Materiale trefolo")));
        int currentIndex = cableMaterials.FindIndex(m => m.S("id") == Settings["materiale_trefolo"].S("id"));
        CableMaterial.SelectedIndex = currentIndex >= 0 ? currentIndex : 0;
        Tendons.Columns.First(c => c.Key == "materiale").Choices = cableMaterials.Select(m => m.S("nome")).Concat(Tendons.Rows.Select(r => r.Values.S("materiale"))).Distinct().ToArray();
    }

    private void AddCable()
    {
        if (!sheet.CanEdit) return;
        try
        {
            Cable.Commit();
            var material = CableMaterial.SelectedIndex < 0 ? null : cableMaterials[CableMaterial.SelectedIndex];
            var row = TendonRow(ConcreteInputPresentation.NewTendon(Cable.Values, material, Tendons.Rows.Select(r => r.Values.S("id"))));
            ApplyTendonMaterial(row, material!);
            Tendons.Rows.Add(row); Tendons.Selected = row; TendonsChanged(); CableMessage = "";
        }
        catch (ArgumentException error) { CableMessage = error.Message; }
    }

    private void RemoveCable()
    {
        if (!sheet.CanEdit || Tendons.Selected is not { } row) return;
        Tendons.Rows.Remove(row); TendonsChanged();
    }

    // ---------------------------------------------------------------- second layers (ConcreteLayers.cs)

    private ConcreteLayerViewModel[] BuildLayers()
    {
        var layers = new List<ConcreteLayerViewModel>();
        foreach (var (key, title, source) in ConcreteInputPresentation.Layers)
        {
            string prefix = "second_" + key;
            ConcreteSheetPreparation.PrepareLayer(Input, prefix, source);
            var form = Form(Input, [new(prefix + "_count", "Numero barre"), new(prefix + "_diameter", "Diametro", "mm"), new(prefix + "_gap", "Distanza libera dalla prima fila", "mm")], _ => sheet.Invalidate());
            layers.Add(new ConcreteLayerViewModel(key, title, form, () =>
            {
                if (!sheet.CanEdit || ConcreteInputPresentation.LayersLocked(Input)) return;
                Input[prefix + "_enabled"] = !Input.B(prefix + "_enabled"); sheet.Invalidate();
            }, () => sheet.CanEdit));
        }
        return layers.ToArray();
    }

    // ---------------------------------------------------------------- links (ConcreteParameters.cs)

    private (FormViewModel Dimensions, FormViewModel Scheme) BuildStirrups()
    {
        if (!Input.ContainsKey("staffe_presenti")) Input["staffe_presenti"] = "Sì";
        var dimensions = Form(Input, [new("staffe_presenti", "Staffe presenti", Choices: ["Sì", "No"]), new("transverse_bar_diameter_mm", "Ø staffe", "mm"), new("transverse_spacing_mm", "Passo", "mm")], key =>
        {
            if (Input.S("staffe_presenti") == "No") sheet.StirrupsAbsent();
            sheet.SynchronizeStirrups(); sheet.RefreshDetailing();
            if (key is "transverse_bar_diameter_mm" or "staffe_presenti") sheet.Invalidate(); else sheet.InvalidateActions("Taglio");
        });
        var scheme = Form(sheet.ShearOptions, [new("tipo_staffa", "Circolare", Choices: ["Staffa chiusa", "Spirale"]), new("rami_x", "Braccia resistenti a Vx"), new("rami_y", "Braccia resistenti a Vy"),
            new("schema_interno", "Schema interno", Choices: ["Bracci paralleli", "Staffe chiuse sovrapposte"]), new("rami_interni", "Bracci aggiunti / staffe interne"), new("rotazione_staffa", "Rotazione schema", "°")], _ =>
        {
            sheet.SynchronizeStirrups(); sheet.RefreshDetailing(); sheet.InvalidateActions("Taglio");
        }, wideChoices: true);
        return (dimensions, scheme);
    }

    // ---------------------------------------------------------------- quick resistances (ConcreteQuickResistance.cs)

    private FormViewModel BuildQuickResistance()
    {
        ConcreteSheetPreparation.PrepareQuickResistance(Settings);
        return Form(Settings["resistenze_rapide"]!.AsObject(), [new("N", "N (− compressione)", "kN"), new("tipo", "Resistenza", Choices: ["Plastico", "Elastico"])],
            key => { _ = RefreshQuickResistanceAsync(); sheet.RaiseModified(); }, wideChoices: true);
    }

    /// <summary>
    /// The four resistances at the N of the form, on a background thread with a scheduler of their own (latest request wins), in a place of the
    /// limiter of the application (1.16.0, D-5).
    /// </summary>
    internal async Task RefreshQuickResistanceAsync()
    {
        if (sheet.Initializing || sheet.IsDisposed) return;
        quickResistanceScheduler.Invalidate();
        QuickResistanceCards = []; QuickResistanceTextVisible = true;
        QuickResistanceText = "Calcolo delle quattro direzioni…";
        var input = (JsonObject)Input.DeepClone(); var workspace = (JsonObject)Settings.DeepClone();
        quickResistanceRuns++; OnPropertyChanged(nameof(IsQuickResistanceRunning));
        try
        {
            var completion = await quickResistanceScheduler.ScheduleAsync(async token =>
            {
                var options = workspace["resistenze_rapide"]!;
                double n = ConcreteEditor.Number(options.S("N"), "N"); bool elastic = options.S("tipo") == "Elastico";
                return await sheet.Limiter.RunAsync(() => StructuralQueries.ConcreteQuickResistance(input, workspace, n, elastic, token), token);
            });
            completion.TryPublish(current =>
            {
                if (current.Error is not null) { QuickResistanceText = "Resistenze non disponibili: " + current.Error.Message; return; }
                var result = current.Value!;
                QuickResistanceText = string.Join("\n", result.Select(r => $"{r.Direction}   {(r.Moment is double m ? EngineeringFormat.Number(m) + " kNm" : "— · " + r.Status)}"));
                QuickResistanceTextVisible = false;
                QuickResistanceCards = result.Select(r => new ConcreteQuickResistanceCard(r.Direction, r.Moment is double m ? EngineeringFormat.Number(m) + " kNm" : "— · " + r.Status)).ToArray();
            });
        }
        finally { quickResistanceRuns--; OnPropertyChanged(nameof(IsQuickResistanceRunning)); }
    }

    internal void DisposeSchedulers() => quickResistanceScheduler.Dispose();
}
