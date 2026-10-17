using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Application.Stratigraphy;
using Anthea.Application.Walls;
using Anthea.Calculations;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Modules.Shared;

/// <summary>A destination of «Invia a un nuovo foglio»: the module and its label («elemento · descrizione»).</summary>
public sealed record SoilTransferDestination(string Module, string Label);

/// <summary>
/// The soil transfer dialog «Terreno · invio e riutilizzo» (decision T5 of 17/10, V9 of F5.6, S3 of F5.6b): the behaviour of the historic
/// window X.Desktop/Wpf/SoilTransferDialog.cs without WPF, shared by the soil modules and shown through <see cref="IDialogService"/>.
/// <list type="bullet">
/// <item>At opening: the profile of the sheet (the first profile, «Monte» for a wall) from a copy of its data, the destination (the wall
/// from the vertical pile, the vertical pile otherwise), «Medi», the first soil sheet of the project.</item>
/// <item>«Usa il profilo corrente», «Carica terreno…» (file *.anthea-terreno.json or *.json, validated), «Salva terreno…» (atomic
/// write) and «Importa dal foglio selezionato» choose the profile; the preview table and the description follow it.</item>
/// <item>«Invia a un nuovo foglio»: a copy in a new sheet of the destination, named «Terreno da …»; «Sostituisci il terreno del foglio
/// corrente»: the data of the sheet with the profile applied, after the question of the historic window. Both close the dialog with
/// their <see cref="SoilTransferChoice"/>; they need a profile.</item>
/// <item>Every error of a command stays in <see cref="Status"/> (<see cref="StatusIsError"/>), as the historic window does; nothing
/// reaches the error log.</item>
/// </list>
/// The equivalence with the historic window is checked by ui/check-palo-verticale-view (dialoghi condivisi) on the same data, and by
/// the stratigraphy traces of Presentation.Tests, which drive this ViewModel (F5.6b traces of the historic window).
/// </summary>
public sealed class SoilTransferDialogViewModel : DialogViewModel<SoilTransferChoice>
{
    /// <summary>Caption of the window.</summary>
    public const string DialogTitle = "Terreno · invio e riutilizzo";

    /// <summary>Status after «Usa il profilo corrente».</summary>
    public const string CurrentProfileStatus = "Profilo del foglio corrente. Puoi salvarlo oppure inviarlo a un altro modulo.";

    /// <summary>The question before «Sostituisci il terreno del foglio corrente».</summary>
    public const string ReplaceQuestion = "Sostituire il profilo selezionato e i dati di falda del foglio corrente con una copia indipendente?";

    /// <summary>Caption of <see cref="ReplaceQuestion"/>.</summary>
    public const string ReplaceTitle = "Importa terreno";

    private static readonly FileFilter ProfileFilter = new("Profilo terreno ANTHEA", "*.anthea-terreno.json");

    private readonly string module;
    private readonly JsonObject original;
    private readonly IReadOnlyList<SoilSource> sources;
    private readonly IFileDialogService files;
    private readonly IConfirmationService confirmations;
    private JsonObject? profile;
    private int selectedSurvey, selectedSource, selectedSourceSurvey;
    private string selectedDestination, scenario = "Medi", status = "", description = "", guidance = "";
    private bool statusIsError;
    private IReadOnlyList<string> sourceSurveys = [];
    private IReadOnlyList<string[]> previewRows = [];
    private bool previewIsStratigraphy, hasPreview;

    /// <param name="module">Module of the sheet.</param>
    /// <param name="data">Data of the sheet (committed); the dialog works on a copy.</param>
    /// <param name="sources">The other soil sheets of the project.</param>
    /// <param name="files">Open and save dialogs of «Carica terreno…» and «Salva terreno…».</param>
    /// <param name="confirmations">The question before the replacement.</param>
    public SoilTransferDialogViewModel(string module, JsonObject data, IReadOnlyList<SoilSource> sources, IFileDialogService files, IConfirmationService confirmations)
        : base(DialogTitle)
    {
        ArgumentException.ThrowIfNullOrEmpty(module);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(confirmations);
        this.module = module; this.sources = sources; this.files = files; this.confirmations = confirmations;
        original = (JsonObject)data.DeepClone();
        Surveys = module == WallModel.Module ? ["Monte", "Valle"] : Enumerable.Range(1, SoilProfileTransfer.SurveyCount(module, data)).Select(i => "Profilo " + i).ToArray();
        selectedSurvey = IndexOf(Surveys, module == WallModel.Module ? "Monte" : "Profilo 1");
        Destinations = SoilProfileTransfer.Modules.Select(target => { var info = ModuleDirectory.Get(target); return new SoilTransferDestination(target, info.Element + " · " + info.Description); }).ToArray();
        selectedDestination = module == "geo_palo_verticale" ? WallModel.Module : "geo_palo_verticale";
        SourceLabels = sources.Select((s, i) => $"{i + 1}. {s.Label}").ToArray();
        selectedSource = sources.Count > 0 ? 0 : -1;
        UseCurrentCommand = new RelayCommand(UseCurrent);
        LoadCommand = new RelayCommand(() => Try(Load));
        SaveCommand = new RelayCommand(() => Try(Save));
        ImportSourceCommand = new RelayCommand(() => Try(ImportSource), () => sources.Count > 0);
        SendCommand = new RelayCommand(() => Try(Send), () => profile is not null);
        ApplyCommand = new RelayCommand(() => Try(Apply), () => profile is not null);
        SourceChanged();
        UseCurrent();
        Target();
    }

    // ---------------------------------------------------------------- profile of the sheet

    /// <summary>The profiles of the sheet: «Monte» and «Valle» for a wall, otherwise «Profilo 1…n».</summary>
    public IReadOnlyList<string> Surveys { get; }

    /// <summary>The profile of the sheet; a new choice takes it as the current profile.</summary>
    public int SelectedSurveyIndex
    {
        get => selectedSurvey;
        set { if (SetProperty(ref selectedSurvey, value)) UseCurrent(); }
    }

    /// <summary>«Usa il profilo corrente»: the selected profile of the sheet (the error in the status when it cannot be extracted).</summary>
    public IRelayCommand UseCurrentCommand { get; }

    /// <summary>«Carica terreno…».</summary>
    public IRelayCommand LoadCommand { get; }

    /// <summary>«Salva terreno…».</summary>
    public IRelayCommand SaveCommand { get; }

    /// <summary>The chosen profile, or null.</summary>
    public JsonObject? Profile => profile;

    /// <summary>The preview shows a stratigraphy document (columns «Strato / insieme», «Famiglia», …), otherwise a profile («Strato», «Tipo», …).</summary>
    public bool PreviewIsStratigraphy { get => previewIsStratigraphy; private set => SetProperty(ref previewIsStratigraphy, value); }

    /// <summary>A preview is shown (a profile is chosen).</summary>
    public bool HasPreview { get => hasPreview; private set => SetProperty(ref hasPreview, value); }

    /// <summary>Rows of the preview table, nine cells each.</summary>
    public IReadOnlyList<string[]> PreviewRows { get => previewRows; private set => SetProperty(ref previewRows, value); }

    /// <summary>Description of the profile under the commands (kept when the profile is lost, as the historic window).</summary>
    public string Description { get => description; private set => SetProperty(ref description, value); }

    // ---------------------------------------------------------------- another sheet of the project

    /// <summary>The soil sheets of the project, «n. etichetta».</summary>
    public IReadOnlyList<string> SourceLabels { get; }

    /// <summary>The chosen sheet of the project (−1 without sheets).</summary>
    public int SelectedSourceIndex
    {
        get => selectedSource;
        set { if (SetProperty(ref selectedSource, value)) SourceChanged(); }
    }

    /// <summary>The profiles of the chosen sheet.</summary>
    public IReadOnlyList<string> SourceSurveys { get => sourceSurveys; private set => SetProperty(ref sourceSurveys, value); }

    /// <summary>The chosen profile of the chosen sheet.</summary>
    public int SelectedSourceSurveyIndex { get => selectedSourceSurvey; set => SetProperty(ref selectedSourceSurvey, value); }

    /// <summary>«Importa dal foglio selezionato» (disabled without other soil sheets in the project).</summary>
    public IRelayCommand ImportSourceCommand { get; }

    // ---------------------------------------------------------------- send and replace

    /// <summary>«Medi» and «Minimi».</summary>
    public IReadOnlyList<string> Scenarios { get; } = ["Medi", "Minimi"];

    /// <summary>The parameters for the checking module.</summary>
    public string Scenario { get => scenario; set => SetProperty(ref scenario, value); }

    /// <summary>The modules that receive a soil.</summary>
    public IReadOnlyList<SoilTransferDestination> Destinations { get; }

    /// <summary>The module of the new sheet.</summary>
    public string SelectedDestination
    {
        get => selectedDestination;
        set { if (SetProperty(ref selectedDestination, value)) Target(); }
    }

    /// <summary>Guidance for the destination.</summary>
    public string Guidance { get => guidance; private set => SetProperty(ref guidance, value); }

    /// <summary>«Invia a un nuovo foglio» (needs a profile).</summary>
    public IRelayCommand SendCommand { get; }

    /// <summary>«Sostituisci il terreno del foglio corrente» (needs a profile).</summary>
    public IRelayCommand ApplyCommand { get; }

    /// <summary>Last message: what was done, or the error of the last command.</summary>
    public string Status { get => status; private set => SetProperty(ref status, value); }

    /// <summary>The status is an error (shown in the failure colour).</summary>
    public bool StatusIsError { get => statusIsError; private set => SetProperty(ref statusIsError, value); }

    /// <summary>The name of the new sheet of «Invia a un nuovo foglio» for <paramref name="destination"/>.</summary>
    public string OutputName(string destination) => "Terreno da " + (profile?["source_sheet"]?.ToString() ?? profile?["stratigraphy"]?["name"]?.ToString() ?? ModuleDirectory.Get(module).Element)
        + (profile?["stratigraphy"] is not null && destination != StratigraphyDocument.Module ? " · " + Scenario : "");

    // ---------------------------------------------------------------- commands

    /// <summary>Try of the historic window: the status returns to the ink colour, an error goes into the status.</summary>
    private void Try(Action action)
    {
        StatusIsError = false;
        try { action(); }
        catch (Exception error) { Status = error.Message; StatusIsError = true; }
    }

    private void UseCurrent()
    {
        SetProfile(null);
        Try(() => { SetProfile(SoilProfileTransfer.Extract(module, original, SelectedSurveyIndex)); Status = CurrentProfileStatus; });
        Refresh();
    }

    private void Load()
    {
        string? file = files.OpenFile(new OpenFileRequest([ProfileFilter, new FileFilter("JSON", "*.json")]));
        if (file is null) return;
        var candidate = JsonNode.Parse(File.ReadAllText(file, Encoding.UTF8)) as JsonObject ?? throw new ArgumentException("File terreno non valido.");
        SoilProfileTransfer.Validate(candidate);
        SetProfile(candidate);
        Status = "Caricato: " + Path.GetFileName(file) + ". Controlla l’anteprima, poi scegli invio o sostituzione del profilo corrente.";
        Refresh();
    }

    private void Save()
    {
        if (profile is null) throw new ArgumentException("Completare o caricare prima un profilo terreno.");
        string? file = files.SaveFile(new SaveFileRequest([ProfileFilter], "Terreno.anthea-terreno.json"));
        if (file is null) return;
        Archivio.ScriviAtomico(file, Encoding.UTF8.GetBytes(profile.ToJsonString(J.Options)));
        Status = "Profilo salvato e riutilizzabile negli altri moduli compatibili.";
    }

    private void SourceChanged()
    {
        if (SelectedSourceIndex < 0) return;
        var source = sources[SelectedSourceIndex];
        SourceSurveys = source.Module == WallModel.Module ? ["Monte", "Valle"] : Enumerable.Range(1, SoilProfileTransfer.SurveyCount(source.Module, source.Data)).Select(i => "Profilo " + i).ToArray();
        SelectedSourceSurveyIndex = SourceSurveys.Count > 0 ? 0 : -1;
    }

    private void ImportSource()
    {
        if (SelectedSourceIndex < 0) throw new ArgumentException("Nel progetto non ci sono altri fogli terreno compatibili.");
        var source = sources[SelectedSourceIndex];
        var candidate = SoilProfileTransfer.Extract(source.Module, source.Data, SelectedSourceSurveyIndex);
        candidate["source_sheet"] = source.Label; candidate["source_id"] = source.Id;
        SetProfile(candidate);
        Status = "Sorgente: " + source.Label + ". Verificare l’anteprima prima di confermare la sostituzione o l’invio.";
        Refresh();
    }

    private void Send()
    {
        string destination = SelectedDestination;
        var output = SoilProfileTransfer.Create(destination, profile!, Scenario);
        Close(new SoilTransferChoice(output, destination, OutputName(destination)));
    }

    private void Apply()
    {
        var candidate = SoilProfileTransfer.Apply(module, original, profile!, SelectedSurveyIndex, Scenario);
        if (!confirmations.ConfirmReplace(ReplaceTitle, ReplaceQuestion)) return;
        Close(new SoilTransferChoice(candidate));
    }

    /// <summary>Cancel of the window (closing without a choice).</summary>
    public void Dismiss() => Cancel();

    // ---------------------------------------------------------------- preview

    private void SetProfile(JsonObject? value)
    {
        profile = value;
        OnPropertyChanged(nameof(Profile));
        SendCommand?.NotifyCanExecuteChanged();
        ApplyCommand?.NotifyCanExecuteChanged();
    }

    /// <summary>Refresh of the historic window: the preview table and the description of the profile (the description stays without a profile).</summary>
    private void Refresh()
    {
        HasPreview = profile is not null;
        if (profile is null) { PreviewRows = []; return; }
        if (profile["stratigraphy"] is JsonObject document)
        {
            var rows = new List<string[]>(); double top = 0;
            foreach (var layer in StratigraphyDocument.Layers(document).OfType<JsonObject>())
            {
                bool infinite = StratigraphyDocument.Infinite(layer);
                double bottom = top + (StratigraphyDocument.Number(layer["thickness"], "Spessore") ?? 0);
                string[] Values(JsonObject values, string set) => [layer.S("name") + " · " + set, layer.S("soil_type"), WallSheetPresentation.Number(top), infinite ? "Indefinito" : WallSheetPresentation.Number(bottom),
                    values.S("gamma"), values.S("gamma_sat"), values.S("phi"), values.S("c"), values.S("cu")];
                rows.Add(Values(layer, "Medi"));
                if (layer["minimum"] is JsonObject minimum) rows.Add(Values(minimum, "Minimi"));
                top = bottom;
            }
            PreviewIsStratigraphy = true;
            PreviewRows = rows;
            Description = "Stratigrafia completa: copia con medi, minimi, fonti e retini. Per i moduli di verifica scegliere un insieme; per entrambi inviare due fogli distinti. Falda e spessori devono essere definiti; nessuna sostituzione di dati mancanti."
                + "\nFalda: " + document.S("water_state") + (document.S("water_state") == "Assegnata" ? " a " + document.S("water_depth") + " m" : "") + ". Pesi in kN/m³; angoli in gradi; coesioni in kPa. Stesso piano di riferimento, senza traslazione di quota.";
            return;
        }
        double depth = 0;
        var table = profile.Array("layers").Select((r, i) =>
        {
            double from = depth; depth += r.D("spessore");
            return new[] { r.S("name", "Strato " + (i + 1)), r.S("tipologia"), WallSheetPresentation.Number(from), WallSheetPresentation.Number(depth), r.S("peso_specifico"), r.S("peso_specifico_saturo"), r.S("angolo_attrito"), r.S("coesione_efficace"), r.S("coesione_non_drenata") };
        }).ToArray();
        PreviewIsStratigraphy = false;
        PreviewRows = table;
        Description = profile.S("datum") + $". Profondità indagata: {depth:0.##} m. " + (profile["water"].B("enabled") ? $"Falda a z={profile["water"].D("depth"):0.##} m." : "Falda assente.")
            + "\nPesi in kN/m³; angoli in gradi; coesioni in kPa. Le profondità vengono copiate senza traslazione: usare lo stesso riferimento geometrico nel foglio di destinazione.";
    }

    /// <summary>Target of the historic window: the guidance for the chosen destination.</summary>
    private void Target() => Guidance = (SelectedDestination == StratigraphyDocument.Module
        ? "Copia indipendente completa fra stratigrafie. Dai moduli di verifica l’insieme unico viene riportato nei campi principali: verificarne la qualificazione come medi; minimi e litologia non vengono ricostruiti."
        : SoilProfileTransfer.Guidance(SelectedDestination ?? module)) + "\nMicropalo verticale Bustamante–Doix: schema specifico, escluso da questo trasferimento.";

    private static int IndexOf(IReadOnlyList<string> items, string item)
    {
        for (int i = 0; i < items.Count; i++) if (items[i] == item) return i;
        return -1;
    }
}
