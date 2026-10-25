using System.Text.Json.Nodes;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.BridgeDesign;

public sealed partial class BridgeDesignSheetViewModel
{
    // The parameters by «gruppo/chiave», as the Editors of the historic view: those of the inputs are replaced when the tabs are rebuilt,
    // those of the rates when the panel of the prices is rebuilt, those of the assumptions stay; the undo rewrites the text of all of them.
    private readonly Dictionary<string, BridgeDesignParameter> editors = new(StringComparer.Ordinal);
    private IReadOnlyList<IReadOnlyList<BridgeDesignItem>> inputs = [];
    private IReadOnlyList<BridgeDesignItem> prices = [], assumptions = [];

    /// <summary>The tabs of the inputs (<see cref="BridgeDesignPresentation.InputTabs"/>), each with its items in reading order.</summary>
    public IReadOnlyList<IReadOnlyList<BridgeDesignItem>> Inputs { get => inputs; private set => SetProperty(ref inputs, value); }

    /// <summary>The panel «Prezzario»: the price editor, then the catalogue of the document or the rates of the archive.</summary>
    public IReadOnlyList<BridgeDesignItem> Prices { get => prices; private set => SetProperty(ref prices, value); }

    /// <summary>The panel «Ipotesi e coefficienti».</summary>
    public IReadOnlyList<BridgeDesignItem> Assumptions { get => assumptions; private set => SetProperty(ref assumptions, value); }

    /// <summary>The parameters shown, by «gruppo/chiave» (stale entries, as the historic view, are never shown).</summary>
    public IReadOnlyDictionary<string, BridgeDesignParameter> Parameters => editors;

    /// <summary>«Ripristina dimensioni automatiche» and «Sezione automatica».</summary>
    public IRelayCommand AutoSizeCommand { get; private set; } = null!;
    /// <summary>«Applica γQ NTC consigliato».</summary>
    public IRelayCommand RecommendedTrafficCommand { get; private set; } = null!;
    /// <summary>The buttons of the typologies (the identifier as parameter).</summary>
    public IRelayCommand<string> FamilyCommand { get; private set; } = null!;
    /// <summary>«Gestisci prezzario…».</summary>
    public IAsyncRelayCommand ManagePricesCommand { get; private set; } = null!;
    /// <summary>«Applica valori orientativi 2026».</summary>
    public IRelayCommand IndicativeRatesCommand { get; private set; } = null!;

    private JsonObject Input => Data["input"]!.AsObject();

    private BridgeDesignParameter Parameter(string group, BridgeDesignModel.Parameter p, bool slider)
    {
        var parameter = new BridgeDesignParameter(this, group, p, slider, BridgeDesignPresentation.ParameterText(Data, group, p.Key), BridgeDesignPresentation.ParameterValue(Data, group, p.Key));
        editors[group + "/" + p.Key] = parameter;
        return parameter;
    }

    private BridgeDesignChoice Choice(string key, string label, IReadOnlyList<string> choices) => new(this, key, label, choices, Input.S(key));

    // The tabs of the inputs, as BuildInputs of the historic view.
    private void BuildInputs()
    {
        foreach (string key in editors.Keys.Where(k => k.StartsWith("input/")).ToArray()) editors.Remove(key);
        var site = new List<BridgeDesignItem>
        {
            Choice("obstacle", BridgeDesignPresentation.ObstacleLabel, BridgeDesignModel.Obstacles),
            Choice("soil", BridgeDesignPresentation.SoilLabel, BridgeDesignModel.Soils)
        };
        foreach (var p in BridgeDesignModel.Site) site.Add(Parameter("input", p, true));
        var layout = new List<BridgeDesignItem>();
        foreach (var p in BridgeDesignModel.Layout) layout.Add(Parameter("input", p, true));
        layout.Add(new BridgeDesignOption(this, "continuous", BridgeDesignPresentation.ContinuousLabel, Input.B("continuous")));
        layout.Add(new BridgeDesignOption(this, "start_pier", BridgeDesignPresentation.StartPierLabel, Input.B("start_pier")));
        layout.Add(new BridgeDesignOption(this, "end_pier", BridgeDesignPresentation.EndPierLabel, Input.B("end_pier")));
        layout.Add(new BridgeDesignText(BridgeDesignPresentation.SpansNote, BridgeDesignTextRole.Note));
        layout.Add(new BridgeDesignButton(BridgeDesignPresentation.AutoSizeButton, AutoSizeCommand));
        var section = BuildSection();
        var sub = new List<BridgeDesignItem>();
        if (BridgeDesignModel.HasTowers(Input.S("family"))) sub.Add(new BridgeDesignText(BridgeDesignPresentation.TowersNote));
        else sub.Add(Choice("pier", BridgeDesignPresentation.PierLabel, BridgeDesignModel.Piers));
        sub.Add(Choice("foundation", BridgeDesignPresentation.FoundationLabel, BridgeDesignModel.Foundations));
        foreach (var p in BridgeDesignModel.Substructure) sub.Add(Parameter("input", p, true));
        sub.Add(new BridgeDesignText(BridgeDesignPresentation.SubstructureNote, BridgeDesignTextRole.Note));
        var traffic = new List<BridgeDesignItem>
        {
            Choice("traffic_model", BridgeDesignPresentation.TrafficLabel, BridgeDesignModel.TrafficModels),
            new BridgeDesignText(BridgeDesignPresentation.TrafficNote(Input.S("traffic_model")), BridgeDesignTextRole.Note),
            Parameter("assumptions", BridgeDesignModel.Assumptions.Single(p => p.Key == "gamma_q"), false)
        };
        if (Input.S("traffic_model") == BridgeDesignModel.UniformLegacy) traffic.Add(Parameter("assumptions", BridgeDesignModel.Assumptions.Single(p => p.Key == "traffic_load"), false));
        else traffic.Add(new BridgeDesignButton(BridgeDesignPresentation.RecommendedTrafficButton, RecommendedTrafficCommand));
        traffic.Add(new BridgeDesignText(BridgeDesignPresentation.TrafficFootnote, BridgeDesignTextRole.Note));
        Inputs = [site, layout, section, sub, traffic];
    }

    private List<BridgeDesignItem> BuildSection()
    {
        string id = Input.S("family");
        var section = new List<BridgeDesignItem>
        {
            new BridgeDesignFamilies(BridgeDesignModel.Families.Select(f => new BridgeDesignFamilyOption(f.Id, f.Name, BridgeDesignPresentation.FamilyRange(f), id == f.Id)).ToArray(), FamilyCommand)
        };
        var (deck, fields, advanced, note) = BridgeDesignPresentation.SectionFields(Input);
        if (deck) section.Add(Choice("deck_type", BridgeDesignPresentation.DeckLabel, BridgeDesignModel.DeckTypes));
        foreach (var p in fields) section.Add(Parameter("input", p, true));
        if (note is not null)
        {
            foreach (var p in advanced) section.Add(Parameter("input", p, true));
            section.Add(new BridgeDesignText(note, BridgeDesignTextRole.Note));
        }
        section.Add(new BridgeDesignButton(BridgeDesignPresentation.AutoSectionButton, AutoSizeCommand));
        return section;
    }

    // The panel of the prices, as BuildPricesPanel of the historic view.
    private void BuildPrices()
    {
        var items = new List<BridgeDesignItem> { new BridgeDesignButton(BridgeDesignPresentation.ManagePricesButton, ManagePricesCommand) };
        if (Data["pricing"] is JsonObject pricing)
        {
            items.Add(new BridgeDesignText(BridgeDesignPresentation.PriceDocumentTitle(pricing), BridgeDesignTextRole.Title));
            items.Add(new BridgeDesignText(BridgeDesignPresentation.PriceDocumentNote));
        }
        else
        {
            items.Add(new BridgeDesignText(BridgeDesignModel.PriceBasis, BridgeDesignTextRole.Note));
            items.Add(new BridgeDesignButton(BridgeDesignPresentation.IndicativeRatesButton, IndicativeRatesCommand));
            foreach (var p in BridgeDesignModel.Rates)
            {
                items.Add(Parameter("rates", p, false));
                if (BridgeDesignModel.RateNotes.TryGetValue(p.Key, out var rateNote)) items.Add(new BridgeDesignText(rateNote, BridgeDesignTextRole.Note));
            }
        }
        Prices = items;
    }

    // The panel of the assumptions, built once as in the historic view.
    private void BuildAssumptions()
    {
        var items = new List<BridgeDesignItem> { new BridgeDesignText(BridgeDesignPresentation.AssumptionsNote, BridgeDesignTextRole.Note) };
        foreach (var p in BridgeDesignPresentation.AssumptionParameters()) items.Add(Parameter("assumptions", p, false));
        Assumptions = items;
    }

    // RefreshInputs of the historic view: the tabs and the prices rebuilt, every parameter shows the text of the data and its border (its
    // slider stays as it was). F5.15, BD10 point 3: the historic view kept the border after «Annulla», and the rebuilt fields lost it also with
    // a value still not valid.
    private void RefreshInputs()
    {
        BuildInputs();
        foreach (string key in editors.Keys.Where(k => k.StartsWith("rates/", StringComparison.Ordinal)).ToArray()) editors.Remove(key);
        BuildPrices();
        foreach (var (key, editor) in editors)
        {
            var parts = key.Split('/');
            editor.SetText(Data[parts[0]]!.S(parts[1]));
            editor.Invalid = BridgeDesignPresentation.ParameterInvalid(BridgeDesignPresentation.ParameterNumber(Data, parts[0], parts[1]), editor.Parameter);
        }
    }

    // ---------------------------------------------------------------- edits

    /// <summary>The user types <paramref name="text"/> in a parameter: the raw text into the data, its border, its slider, the edit.</summary>
    internal void EditParameter(BridgeDesignParameter parameter, string text)
    {
        if (!CanEdit || text == parameter.Text) return;
        parameter.SetText(text);
        Data[parameter.Group]![parameter.Key] = text;
        double? value = BridgeDesignPresentation.ParameterNumber(Data, parameter.Group, parameter.Key);
        parameter.Invalid = BridgeDesignPresentation.ParameterInvalid(value, parameter.Parameter);
        if (parameter.HasSlider && value.HasValue) parameter.SetSlider(value.Value);
        UserChanged();
    }

    /// <summary>The user moves the slider of a parameter: the text it writes, as typed.</summary>
    internal void MoveSlider(BridgeDesignParameter parameter, double value)
    {
        if (!CanEdit || !parameter.HasSlider) return;
        double clamped = Math.Clamp(value, parameter.Parameter.Min, parameter.Parameter.Max);
        if (clamped == parameter.SliderValue) return;
        parameter.SetSlider(clamped);
        EditParameter(parameter, BridgeDesignPresentation.SliderText(clamped));
    }

    /// <summary>The user chooses an item: the input written; the deck and the traffic model rebuild the tabs.</summary>
    internal void Choose(BridgeDesignChoice choice, string? value)
    {
        if (!CanEdit || value is null || value == choice.Selected || !choice.Choices.Contains(value)) return;
        choice.SetSelected(value);
        Mutate(() => Input[choice.Key] = value, BridgeDesignPresentation.ChoiceRebuilds(choice.Key));
    }

    /// <summary>The user clicks an option of the spans.</summary>
    internal void SetOption(BridgeDesignOption option, bool value)
    {
        if (!CanEdit || value == option.IsChecked) return;
        option.Set(value);
        Mutate(() => Input[option.Key] = value);
    }

    /// <summary>The button of a typology (also the one chosen): its rules, the tabs rebuilt.</summary>
    public void SelectFamily(string id)
    {
        if (!CanEdit || !BridgeDesignModel.Families.Any(f => f.Id == id)) return;
        Mutate(() => BridgeDesignPresentation.SelectFamily(Input, id), true);
    }

    /// <summary>«Ripristina dimensioni automatiche» and «Sezione automatica».</summary>
    public void AutoSize() { if (CanEdit) Mutate(() => BridgeDesignPresentation.AutoSize(Input), true); }

    /// <summary>«Applica γQ NTC consigliato».</summary>
    public void ApplyRecommendedTraffic() { if (CanEdit) Mutate(() => BridgeDesignModel.ApplyRecommendedTrafficFactor(Data), true); }

    /// <summary>«Applica valori orientativi 2026».</summary>
    public void ApplyIndicativeRates() { if (CanEdit) Mutate(() => BridgeDesignPresentation.ApplyIndicativeRates(Data), true); }

    /// <summary>
    /// «Gestisci prezzario…»: the price editor on the data, shown by the dialog service; the document applied goes into the data (an edit,
    /// the tabs rebuilt). A bridge that cannot be priced writes the reason in the status line. F5.15, BD4 (F5-59): in a read-only revision the
    /// editor opens without «Applica prezzario» (the historic view applied it to the revision).
    /// </summary>
    public async Task ManagePricesAsync()
    {
        PriceCatalogueViewModel editor;
        try { editor = new PriceCatalogueViewModel(Data, Context.Services.Files, !CanEdit); }
        catch (ArgumentException e) { Status = e.Message; return; }
        await Context.Services.Dialogs.ShowDialogAsync(editor);
        if (editor.Result is not null && !IsDisposed && CanEdit) Mutate(() => editor.CommitTo(Data), true);
    }
}
