using System.ComponentModel;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Modules;
using Anthea.Application.Stratigraphy;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using Anthea.Reports;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

public sealed class EmbeddedWallSheetModule : ISheetModule
{
    public ModuleInfo Info => ModuleDirectory.Get(EmbeddedWallDocument.Module);
    public ModuleHelp Help => ModuleHelp.For(Info.Id);
    public ModulePersistence Persistence { get; } = new([]);
    public ModuleReportCapabilities Reports { get; } = new(false);
    public ISheetViewModel CreateSheet(SheetContext context) => new EmbeddedWallSheetViewModel(context);
}

public sealed class EmbeddedWallSheetViewModel : SheetViewModelBase
{
    private EmbeddedWallReportData? current;
    private EmbeddedWallStageData? selectedStage, comparisonStage;
    private EmbeddedWallHydraulicData? hydraulicStage;
    private EmbeddedWallHydraulicCheckData? hydraulicCheck;
    private EmbeddedWallGlobalData? globalCheck;
    private string hydraulicDiagram = "Carico idraulico [m]";
    private string diagram = "Spostamento [mm]", importSide = "Sinistra", importScenario = "Medi";
    private SoilSource? soilSource;
    private bool storingResult;
    private string progress = "";
    public EmbeddedWallSheetViewModel(SheetContext context) : base(EmbeddedWallDocument.Module, context)
    {
        Rebuild(); Report = new SheetReport(this);
        RunCommand = GuardedCommands.CreateAsync(Guard, async c => { await CalculateAsync(c); }, Lifetime, () => !IsBusy);
        CancelCommand = GuardedCommands.Create(Guard, () => CancelCalculation(), () => IsBusy);
        CsvCommand = GuardedCommands.Create(Guard, ExportCsv, () => Current is not null);
        HydraulicCsvCommand = GuardedCommands.Create(Guard, ExportHydraulicCsv, () => HydraulicStages.Count > 0);
        HydraulicChecksCsvCommand = GuardedCommands.Create(Guard, ExportHydraulicChecksCsv, () => HydraulicChecks.Count > 0);
        GlobalCsvCommand = GuardedCommands.Create(Guard, ExportGlobalCsv, () => GlobalChecks.Count > 0);
        ImportSoilCommand = GuardedCommands.Create(Guard, ImportSoil, () => CanEdit && SelectedSoilSource is not null);
        RefreshSoilSourcesCommand = GuardedCommands.Create(Guard, RefreshSources);
        RefreshSources();
        RestoreResult();
    }
    public override IModuleReportSource Report { get; }
    public FormViewModel General { get; private set; } = null!;
    public FormViewModel Hydraulics { get; private set; } = null!;
    public IReadOnlyList<EmbeddedWallTable> Tables { get; private set; } = [];
    public IAsyncRelayCommand RunCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand CsvCommand { get; }
    public IRelayCommand HydraulicCsvCommand { get; }
    public IRelayCommand HydraulicChecksCsvCommand { get; }
    public IRelayCommand GlobalCsvCommand { get; }
    public IRelayCommand ImportSoilCommand { get; }
    public IRelayCommand RefreshSoilSourcesCommand { get; }
    public IReadOnlyList<SoilSource> SoilSources { get; private set; } = [];
    public SoilSource? SelectedSoilSource { get => soilSource; set { if (SetProperty(ref soilSource, value)) ImportSoilCommand?.NotifyCanExecuteChanged(); } }
    public string ImportSide { get => importSide; set => SetProperty(ref importSide, value); }
    public string ImportScenario { get => importScenario; set => SetProperty(ref importScenario, value); }
    public IReadOnlyList<string> ImportSides { get; } = ["Sinistra", "Destra"];
    public IReadOnlyList<string> ImportScenarios { get; } = ["Medi", "Minimi"];
    public IReadOnlyList<string> Diagrams { get; } = ["Spostamento [mm]", "Momento [kNm]", "Taglio [kN]", "Normale [kN]", "Pressione sinistra [kPa]", "Pressione destra [kPa]"];
    public string Diagram { get => diagram; set => SetProperty(ref diagram, value); }
    public IReadOnlyList<string> HydraulicDiagrams { get; } = ["Carico idraulico [m]", "Pressione interstiziale [kPa]"];
    public string HydraulicDiagram { get => hydraulicDiagram; set => SetProperty(ref hydraulicDiagram, value); }
    public IReadOnlyList<EmbeddedWallHydraulicData> HydraulicStages => current?.Hydraulics ?? [];
    public EmbeddedWallHydraulicData? SelectedHydraulicStage
    {
        get => hydraulicStage;
        set { if (SetProperty(ref hydraulicStage, value)) { OnPropertyChanged(nameof(HydraulicNodes)); OnPropertyChanged(nameof(HydraulicElements)); } }
    }
    public IReadOnlyList<EmbeddedWallHydraulicNode> HydraulicNodes => hydraulicStage?.Nodes ?? [];
    public IReadOnlyList<EmbeddedWallHydraulicElement> HydraulicElements => hydraulicStage?.Elements ?? [];
    public IReadOnlyList<EmbeddedWallHydraulicCheckData> HydraulicChecks => current?.HydraulicChecks ?? [];
    public EmbeddedWallHydraulicCheckData? SelectedHydraulicCheck
    { get => hydraulicCheck; set { if (SetProperty(ref hydraulicCheck, value)) OnPropertyChanged(nameof(HydraulicCoefficients)); } }
    public IReadOnlyList<KeyValuePair<string, double>> HydraulicCoefficients => hydraulicCheck?.Coefficients
        .Select(p => new KeyValuePair<string, double>(EmbeddedWallReport.DisplayLabel(p.Key), p.Value)).ToArray() ?? [];
    public EmbeddedWallReportData? Current => current;
    public IReadOnlyList<EmbeddedWallGlobalData> GlobalChecks => current?.GlobalChecks ?? [];
    public EmbeddedWallGlobalData? SelectedGlobalCheck { get => globalCheck; set => SetProperty(ref globalCheck, value); }
    public override JsonObject? Result => current is null ? null : EmbeddedWallDocument.ResultValues(current);
    public string Progress { get => progress; private set => SetProperty(ref progress, value); }
    public override bool HasResults => current is not null && current.Complete;
    public IReadOnlyList<EmbeddedWallStageData> Stages => current?.Stages ?? [];
    public EmbeddedWallStageData? SelectedStage
    {
        get => selectedStage;
        set { if (SetProperty(ref selectedStage, value)) { OnPropertyChanged(nameof(Points)); OnPropertyChanged(nameof(Checks)); OnPropertyChanged(nameof(Supports)); } }
    }
    public EmbeddedWallStageData? ComparisonStage { get => comparisonStage; set => SetProperty(ref comparisonStage, value); }
    public IReadOnlyList<EmbeddedWallPointData> Points => selectedStage?.Points ?? [];
    public IReadOnlyList<EmbeddedWallSupportData> Supports => selectedStage?.Supports ?? [];
    public IReadOnlyList<EmbeddedWallCheckData> Checks => current?.Checks.Where(c => c.Stage == selectedStage?.Id).ToArray() ?? [];
    public string ResultStatus => current is null ? "Risultati assenti o superati: eseguire il calcolo sui dati correnti."
        : current.Complete ? "Analisi convergente. Verifiche dell'opera: consultare ambito ed esiti." : "Analisi incompleta: sono visibili soltanto le fasi concluse.";
    public string Scope => current is null ? "Incremento in sviluppo: trave su molle, strati orizzontali, fasi, sostegni, filtrazione stazionaria 2D, UPL/HYD e stabilità globale per pareti continue senza sostegni esterni. Instabilità geotecnica del fondo scavo, acciaio e sisma sono ancora da integrare."
        : string.Join(Environment.NewLine, current.Scope);
    private void Rebuild()
    {
        EmbeddedWallDocument.Upgrade(Data);
        General = new(Data, EmbeddedWallFields.General(), _ => Changed()) { IsReadOnly = !CanEdit, WikiNavigator = Context.Services.Wiki };
        Hydraulics = new(Data, EmbeddedWallFields.Hydraulics(), _ => Changed()) { IsReadOnly = !CanEdit, WikiNavigator = Context.Services.Wiki };
        Tables = EmbeddedWallDocument.Tables.Select(key => new EmbeddedWallTable(key, Data[key]!.AsArray(), Changed, () => CanEdit, Guard)).ToArray();
        OnPropertyChanged(nameof(General)); OnPropertyChanged(nameof(Hydraulics)); OnPropertyChanged(nameof(Tables));
    }
    public override async Task<bool> CalculateAsync(CancellationToken cancellation = default)
    {
        Commit(); long started = Revision; var ui = SynchronizationContext.Current;
        void Show(EmbeddedWallProgress p)
        {
            void Apply() { if (!IsDisposed && Revision == started && IsBusy) Progress = p.Fraction.HasValue
                ? $"Fase {p.Stage} · {p.Fraction:P0} · {p.Iterations} iterazioni" : $"{p.Stage} · {p.Iterations} iterazioni"; }
            if (ui is null) return; ui.Post(_ => Apply(), null);
        }
        var service = Context.Calculation as EmbeddedWallModule;
        if (service is not null) service.ProgressChanged += Show;
        Progress = "Preparazione del modello";
        try { return await base.CalculateAsync(cancellation); }
        finally { if (service is not null) service.ProgressChanged -= Show; Progress = ""; }
    }
    protected override void OnReloaded() => Rebuild();
    protected override void OnCommit() { General.Commit(); Hydraulics.Commit(); foreach (var table in Tables) table.Editor?.Commit(); EmbeddedWallDocument.Validate(Data); }
    protected override void OnCalculationPublished()
    {
        current = PublishedCalculation?.Native as EmbeddedWallReportData;
        if (current is not null && CanEdit)
        {
            EmbeddedWallDocument.StoreResult(Data, current);
            storingResult = true;
            try { ChangedKeepingResults(); } finally { storingResult = false; }
        }
        SelectedStage = Stages.LastOrDefault(); ComparisonStage = null; SelectedHydraulicStage = HydraulicStages.LastOrDefault(); NotifyResult();
    }
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Revision) && !storingResult) RestoreResult();
        if (e.PropertyName == nameof(CanEdit))
        {
            if (General is not null) General.IsReadOnly = !CanEdit;
            if (Hydraulics is not null) Hydraulics.IsReadOnly = !CanEdit;
            foreach (var table in Tables) table.RefreshAccess(); ImportSoilCommand?.NotifyCanExecuteChanged();
        }
        if (e.PropertyName == nameof(IsBusy)) { RunCommand?.NotifyCanExecuteChanged(); CancelCommand?.NotifyCanExecuteChanged(); }
    }
    private void NotifyResult()
    { OnPropertyChanged(nameof(Current)); OnPropertyChanged(nameof(Result)); OnPropertyChanged(nameof(HasResults)); OnPropertyChanged(nameof(Stages)); OnPropertyChanged(nameof(HydraulicStages)); OnPropertyChanged(nameof(HydraulicChecks)); SelectedHydraulicCheck = HydraulicChecks.FirstOrDefault(); OnPropertyChanged(nameof(GlobalChecks)); SelectedGlobalCheck = GlobalChecks.FirstOrDefault(); GlobalCsvCommand?.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(ResultStatus)); OnPropertyChanged(nameof(Scope)); CsvCommand?.NotifyCanExecuteChanged(); HydraulicCsvCommand?.NotifyCanExecuteChanged(); HydraulicChecksCsvCommand?.NotifyCanExecuteChanged(); }
    private void RestoreResult()
    { current = EmbeddedWallDocument.ReadCurrentResult(Data); SelectedStage = Stages.LastOrDefault(); ComparisonStage = null; SelectedHydraulicStage = HydraulicStages.LastOrDefault(); NotifyResult(); }
    private EmbeddedWallReportData RequireCurrent()
    {
        Commit();
        if (current is null || current.Fingerprint != EmbeddedWallDocument.Fingerprint(Data)) throw new InvalidOperationException("Risultati assenti o superati: ricalcolare prima dell'esportazione.");
        return current;
    }
    private void ExportCsv()
    {
        var result = RequireCurrent(); var path = Context.Services.Files.SaveFile(new([new FileFilter("Tabella dei risultati", "*.csv")], "paratia.csv"));
        if (path is not null) File.WriteAllText(path, EmbeddedWallReport.Csv(result), new UTF8Encoding(true));
    }
    private void ImportSoil()
    {
        if (!CanEdit || SelectedSoilSource is null) return;
        var data = EmbeddedWallDocument.WithStratigraphy(Data, SelectedSoilSource.Data, ImportSide == "Sinistra" ? "left_soils" : "right_soils", ImportScenario);
        Reload(data); Changed();
        Context.Services.Messages.Inform("Importazione del terreno", "Terreno importato. Assegnare K0 e kh con la relativa fonte; verificare i livelli di falda in ogni fase.");
    }
    private void ExportHydraulicCsv()
    {
        var result = RequireCurrent(); var path = Context.Services.Files.SaveFile(new([new FileFilter("Nodi e flussi idraulici", "*.csv")], "filtrazione.csv"));
        if (path is not null) File.WriteAllText(path, EmbeddedWallReport.HydraulicCsv(result), new UTF8Encoding(true));
    }
    private void RefreshSources()
    {
        SoilSources = Context.Host.SoilSources().Where(s => s.Module == StratigraphyDocument.Module).ToArray();
        OnPropertyChanged(nameof(SoilSources)); SelectedSoilSource = SoilSources.FirstOrDefault(s => s.Id == SelectedSoilSource?.Id) ?? SoilSources.FirstOrDefault();
    }
    private void ExportHydraulicChecksCsv()
    {
        var result = RequireCurrent(); var path = Context.Services.Files.SaveFile(new([new FileFilter("Verifiche UPL e HYD", "*.csv")], "verifiche-idrauliche.csv"));
        if (path is not null) File.WriteAllText(path, EmbeddedWallReport.HydraulicChecksCsv(result), new UTF8Encoding(true));
    }
    private void ExportGlobalCsv()
    {
        var result = RequireCurrent(); var path = Context.Services.Files.SaveFile(new([new FileFilter("Stabilità globale", "*.csv")], "stabilita-globale.csv"));
        if (path is not null) File.WriteAllText(path, EmbeddedWallReport.GlobalCsv(result), new UTF8Encoding(true));
    }
    private sealed class SheetReport(EmbeddedWallSheetViewModel sheet) : IModuleReportSource
    {
        public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false)
            => DocxWriter.Write(EmbeddedWallReport.Create(title, sheet.RequireCurrent(), options.Contains("paratie_dettaglio")));
    }
}
