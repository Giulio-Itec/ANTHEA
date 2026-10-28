using ANTHEA.ModelWorkspace;
using ANTHEA.ModelReports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed record SheetTarget(string Id, string Name, string Status)
{
    public override string ToString() => Name + " · " + Status;
}

public interface IModelViewerServices
{
    Task<ModelSnapshot?> ImportAsync(CancellationToken cancellationToken);
    Task<ModelSnapshot?> ImportAsync(ModelUpdateScope scope,CancellationToken cancellationToken)=>ImportAsync(cancellationToken);
    Task<MidasModelCatalog> InspectMidasAsync(MidasConnection connection,IProgress<ModelImportProgress> progress,CancellationToken token)=>throw new NotSupportedException("Collegamento MIDAS non disponibile in questa finestra.");
    Task<ModelSnapshot> ImportMidasAsync(MidasImportRequest request,IProgress<ModelImportProgress> progress,CancellationToken token)=>throw new NotSupportedException("Collegamento MIDAS non disponibile in questa finestra.");
    string? ChooseStrand7File(Strand7ImportFile kind)=>null;
    Task<ModelSnapshot?> OpenPreviousModelAsync(CancellationToken cancellationToken)=>ImportAsync(cancellationToken);
    ModelReportDocument? OpenReport(bool hasChanges) => null;
    bool SaveReport(ModelReportDocument document) => false;
    Task<ReportExportResult?> ExportReportAsync(ModelReportDocument document, ModelSnapshot snapshot, IProgress<ReportProgress> progress, CancellationToken cancellationToken) => Task.FromResult<ReportExportResult?>(null);
    void OpenReportOutput(string path) { }
    void FitView();
    void SaveImage();
    void ExportTable(string name, string csv);
    ModelCameraState? CaptureCamera() => null;
    void RestoreCamera(ModelCameraState camera) { }
    IReadOnlyList<ModelViewPreset> LoadViews(string revision) => [];
    void SaveViews(string revision, IReadOnlyList<ModelViewPreset> views) { }
}

/// <summary>Testable presentation state. No WPF, renderer, file dialogs or structural calculations.</summary>
public sealed partial class ModelViewerViewModel : ObservableObject, IDisposable
{
    private readonly IModelViewerServices services;
    private readonly Action<ModelSnapshot> imported;
    private readonly Action<string, int, string, string> linked;
    private readonly CancellationTokenSource lifetime = new();
    private bool updating;
    private bool disposed;
    private string? displayName;
    public void SetDisplayName(string name) { displayName = name; Title = name; }

    public ModelViewerViewModel(ModelSnapshot? snapshot, IReadOnlyList<SheetTarget> sheets, bool readOnly,
        IModelViewerServices services, Action<ModelSnapshot> imported, Action<string, int, string, string> linked)
    {
        this.services = services; this.imported = imported; this.linked = linked; IsReadOnly = readOnly;
        Sheets = sheets; selectedSheet = sheets.FirstOrDefault();
        if (snapshot != null) SetSnapshot(snapshot);
    }

    public event EventHandler? SceneChanged;
    public ModelSnapshot? Snapshot { get; private set; }
    public bool IsReadOnly { get; }
    public IReadOnlyList<SheetTarget> Sheets { get; }
    public IReadOnlyList<string> RepresentationLabels { get; } = ["Geometria", "Volumi e offset"];
    public IReadOnlyList<string> QualityLabels { get; } = ["Normale", "Alta", "Massima"];
    public int Subdivisions => new[] { 1, 4, 8 }[Math.Clamp(SelectedQuality, 0, 2)];
    public bool IsResultsView => ShowResults;
    public bool IsExtruded => SelectedRepresentation == 1;
    public string RepresentationLabel => RepresentationLabels[Math.Clamp(SelectedRepresentation, 0, 1)];
    // Compatibility for the original prototype callers. UI state uses the two independent properties below.
    public int SelectedMode
    {
        get => ShowResults ? 2 : SelectedRepresentation;
        set
        {
            if (value is < 0 or > 2) return;
            bool previous = updating; updating = true;
            SelectedRepresentation = value == 1 ? 1 : 0; ShowResults = value == 2;
            ShowMesh = value == 0; SelectedPanel = value == 2 ? 1 : 0;
            updating = previous; RefreshScene();
        }
    }
    public string ImportLabel => Snapshot == null ? "Importa modello…" : "Aggiorna modello…";

    [ObservableProperty] private string title = "Nessun modello importato";
    [ObservableProperty] private string summary = "Importa una copia del modello nella fase o sottofase selezionata.";
    [ObservableProperty] private string information = "Importa dal modello aperto in MIDAS o scegli un pacchetto .antheamodel o un file di testo MIDAS Civil/Gen (.mct, .mgt). La copia viene applicata solo dopo il controllo dei dati.";
    [ObservableProperty] private string status = "Pronto";
    [ObservableProperty] private string selectionInformation = "Indica il numero di un elemento per leggerne proprietà e valori.";
    [ObservableProperty] private string minimum = "—";
    [ObservableProperty] private string maximum = "—";
    [ObservableProperty] private string resultUnit = "";
    [ObservableProperty] private IReadOnlyList<string> cases = Array.Empty<string>();
    [ObservableProperty] private int selectedRepresentation;
    [ObservableProperty] private bool showResults;
    [ObservableProperty] private int selectedQuality = 1;
    [ObservableProperty] private string? selectedCase;
    [ObservableProperty] private string selectedComponent = "Myy";
    [ObservableProperty] private bool showMesh = true;
    [ObservableProperty] private bool showIsolines;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string elementNumber = "";
    [ObservableProperty] private SheetTarget? selectedSheet;

    partial void OnSelectedRepresentationChanged(int value)
    {
        OnPropertyChanged(nameof(IsExtruded)); OnPropertyChanged(nameof(RepresentationLabel));
        OnPropertyChanged(nameof(SelectedMode)); RefreshScene();
    }
    partial void OnShowResultsChanged(bool value)
    {
        OnPropertyChanged(nameof(IsResultsView)); OnPropertyChanged(nameof(SelectedMode)); RefreshScene();
    }
    partial void OnSelectedQualityChanged(int value) => RefreshScene();
    partial void OnSelectedCaseChanged(string? value) { NotifySelectedStage(); RefreshScene(); LinkCommand.NotifyCanExecuteChanged(); }
    partial void OnSelectedComponentChanged(string value) => RefreshScene();
    partial void OnShowMeshChanged(bool value) => RefreshScene();
    partial void OnShowIsolinesChanged(bool value) => RefreshScene();
    partial void OnElementNumberChanged(string value) { QueryCommand.NotifyCanExecuteChanged(); LinkCommand.NotifyCanExecuteChanged(); }
    partial void OnSelectedSheetChanged(SheetTarget? value) => LinkCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) { NotifyCombinationCommands(); CheckStrand7SetupCommand.NotifyCanExecuteChanged();CloseImportOptionsCommand.NotifyCanExecuteChanged();OnPropertyChanged(nameof(CanChooseUpdateScope));OnPropertyChanged(nameof(CanEditImport));InspectMidasCommand.NotifyCanExecuteChanged();ImportCommand.NotifyCanExecuteChanged(); LinkCommand.NotifyCanExecuteChanged(); ComparePreviousCommand.NotifyCanExecuteChanged();ComputeRevisionDeltaCommand.NotifyCanExecuteChanged();ClearRevisionCommand.NotifyCanExecuteChanged(); }

    private bool CanImport() => !IsReadOnly && !IsBusy && !IsCombinationDirty && !IsVerificationRunning && !IsReportRunning && !IsRevisionBusy && !disposed && (SelectedUpdateScope!=2 || Snapshot!=null) && HasLiveImportSelection;
    [RelayCommand(CanExecute = nameof(CanImport),IncludeCancelCommand=true)]
    private async Task ImportAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,cancellationToken);
        var token=linkedCancellation.Token;
        var scope=(ModelUpdateScope)SelectedUpdateScope;
        bool liveImport=IsMidasImport;
        IsBusy = true; ImportFailure = null; Status = "Lettura e controllo del modello…";
        if(!liveImport){++importOperation;ImportProgressMessage=Status;ImportPercent=0;ImportProgressIndeterminate=true;}
        try
        {
            var incoming = liveImport ? await ReadLiveModelAsync(scope,token) : await services.ImportAsync(scope,token);
            if (incoming == null || disposed) { if (!disposed) Status = "Importazione annullata"; return; }
            var snapshot=await Task.Run(()=>
            {
                if(liveImport)MidasNormalizationReuse.TryApply(Snapshot,incoming,token);
                return ModelSnapshotUpdate.Apply(Snapshot,incoming,scope,token);
            },token);
            var previousView=scope==ModelUpdateScope.Results?CaptureView("Vista corrente"):null;
            (ModelRevisionReview Review,ModelRevisionOverlay Overlay)? review=Snapshot is {} previous?await CreateRevisionAsync(previous,snapshot,token):null;
            token.ThrowIfCancellationRequested();if(disposed)return;
            imported(snapshot); SetSnapshot(snapshot);
            if(previousView!=null)ApplyView(previousView with {Case=Cases.Contains(previousView.Case)?previousView.Case:Cases.FirstOrDefault(),RevisionOverlay=null});
            if(review!=null)ApplyRevision(review.Value.Review,review.Value.Overlay);
            if(scope!=ModelUpdateScope.Results)services.FitView();
            ImportPercent=100;ImportProgressIndeterminate=false;ImportProgressMessage="Importazione completata";
            ShowImportOptions=false;
            Status = UpdateScopeLabels[(int)scope]+" completato. Salva il progetto per conservare la copia e i riferimenti.";
        }
        catch (TaskCanceledException) when (!token.IsCancellationRequested) { if (!disposed) { var failure = new ModelSourceException(ModelSourceFailureKind.Timeout); SetImportFailure(failure); Status = failure.Message; } }
        catch (OperationCanceledException) { if (!disposed) Status = "Importazione annullata"; }
        catch (Exception ex) { if (!disposed) { SetImportFailure(ex); Status = "Importazione non completata: " + ex.Message; } }
        finally { IsBusy = false; }
    }

    private void SetSnapshot(ModelSnapshot snapshot)
    {
        combinationService.ClearCache();
        snapshot.Validate(); updating = true; Snapshot = snapshot;
        categoryColors.Clear();
        ResetRevision();
        Title = displayName ?? snapshot.Name; Summary = $"{snapshot.Nodes.Length:N0} nodi · {snapshot.Elements.Length:N0} elementi";
        InitializeWorkspace();
        Cases = snapshot.Results.Select(r => r.Name).ToArray(); SelectedCase = Cases.FirstOrDefault();
        ShowResults = snapshot.Results.Length > 0; SelectedRepresentation = 0;
        ShowMesh = !ShowResults; SelectedPanel = ShowResults ? 1 : 0;
        ElementNumber = snapshot.Elements.FirstOrDefault(e => e.Type == "PLATE")?.Id.ToString() ?? "";
        updating = false; OnPropertyChanged(nameof(Snapshot)); OnPropertyChanged(nameof(ImportLabel));
        OnPropertyChanged(nameof(UpdateProvenance));
        OnPropertyChanged(nameof(UpdateScopeDescription));
        QueryCommand.NotifyCanExecuteChanged(); LinkCommand.NotifyCanExecuteChanged(); RefreshScene(); InitializeReport(); RefreshCombinations();
    }

    private void RefreshScene()
    {
        if (updating || disposed || Snapshot == null) return;
        PauseAnimation();
        var result = Snapshot.Results.FirstOrDefault(r => r.Name == SelectedCase);
        UpdateDeformations();
        UpdateMeasurements();
        UpdateResultField();
        UpdateRevisionState();
        UpdateMemberDiagram();
        UpdateLoadGraphics();
        UpdateComparison();
        UpdateExploration();
        UpdateDisplay();
        UpdateAnimationAvailability();
        OnPropertyChanged(nameof(LineRepresentationMessage));
        RefreshTable();
        Information = IsExtruded
            ? "Piastre: spessori e offset lungo la normale locale. Aste: contorni delle sezioni e punto di inserimento condivisi con il dettaglio 2D; fori conservati. Offset di estremità non normalizzati: vista ad asse."
            : "Superficie analitica e connettività originali.";
        if (ShowDeformation) Information += "\n\n" + DeformationHeading + ". " + DeformationMessage;
        if (ShowResults)
        {
            Information += "\n\n" + (HasResults ? (IsAveragedContour ? ContourAveragingSummary + ".\nMedia solo grafica: tabelle e riferimenti conservano i valori originali. Discontinuità mantenute tra proprietà, materiali e terne differenti." : "Interpolazione grafica interna all’elemento. Discontinuità conservate fra elementi.") + (result?.IsEnvelope == true ? "\n\nInviluppo: estremi per componente, non simultanei." : "") : ResultAvailability);
            if (IsDisplacementResult) Information += "\n\nSpostamenti nodali globali: gli stessi valori sono condivisi dagli elementi adiacenti. Il colore rappresenta il valore importato, indipendentemente dal fattore della deformata.";
            else if (IsExtruded) Information += "\n\nPlate: il campo è riportato su entrambe le facce e sui bordi; non rappresenta una distribuzione di tensione nello spessore.";
        }
        SceneChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool CanQuery() => Snapshot != null && int.TryParse(ElementNumber, out _) && !disposed;
    [RelayCommand(CanExecute = nameof(CanQuery))]
    private void Query()
    {
        var element = Snapshot!.Elements.FirstOrDefault(e => e.Id.ToString() == ElementNumber.Trim());
        if (element == null) { SelectionInformation = "Elemento non presente nel modello."; return; }
        SelectionInformation = $"Elemento {element.Id} · {element.Type}\nNodi: {string.Join(", ", element.Nodes)}\nProprietà {element.Property}\n";
        if (element.Type == "PLATE")
        {
            var plate = Snapshot.Plates.Single(p => p.Id == element.Property);
            SelectionInformation += $"Spessore {plate.Thickness:G4} m · offset {plate.Offset:G4} m\n\n{SelectedCase}\n{ResultUnit}\n";
        }
        else
        {
            var section = Snapshot.Sections.Single(s => s.Id == element.Property);
            SelectionInformation += section.Width > 0 ? $"{section.Name}\n{section.Width:G4} × {section.Height:G4} m\nRotazione {element.Angle:G4}°" : $"{section.Name} · {section.Shape}\nProfilo solido non importato.\nRotazione {element.Angle:G4}°";
        }
        SelectionInformation += "\n" + string.Join("\n", ActiveValues.Where(v => v.Element == element.Id).Select(v => $"{(v.Station.HasValue ? "x/L " + v.Station.Value.ToString("G3") : "Nodo " + v.Node)}: {v.Value:G7}"));
    }

    private bool CanLink() => !IsDisplacementResult && !IsReadOnly && !IsBusy && !IsVerificationRunning && CanQuery() && SelectedSheet != null && SelectedCase != null && SelectedFamily == 0 && SelectedAxes == 0 && ActiveValues.Any(v => v.Element.ToString() == ElementNumber.Trim());
    [RelayCommand(CanExecute = nameof(CanLink))]
    private void Link()
    {
        try
        {
            linked(SelectedSheet!.Id, int.Parse(ElementNumber), SelectedCase!, SelectedComponent);
            Status = "Riferimento collegato al foglio. Dati di verifica invariati.";
        }
        catch (Exception ex) { Status = "Collegamento non completato: " + ex.Message; }
    }

    [RelayCommand] private void Fit() => services.FitView();
    [RelayCommand] private void SaveImage() { try { PauseAnimation(); services.SaveImage(); } catch (Exception ex) { Status = "Immagine non salvata: " + ex.Message; } }

    public void Dispose()
    {
        if (disposed) return; disposed = true; lifetime.Cancel(); lifetime.Dispose(); combinationService.ClearCache(); MidasApiKey=""; SceneChanged = null;
        foreach(var chapter in ReportChapters) chapter.PropertyChanged -= ChapterChanged;
        Comparison?.Dispose(); Comparison=null; FocusSelectionRequested=null;PreviousCameraRequested=null;HoverChanged=null;
        ViewSettingsChanged = null; CameraPresetRequested = null;
        AnimationStateChanged = null; DeformationFrameChanged = null;
        FocusRemovedRevisionRequested=null;
        ImportCommand.NotifyCanExecuteChanged(); QueryCommand.NotifyCanExecuteChanged(); LinkCommand.NotifyCanExecuteChanged();
        IsolateCommand.NotifyCanExecuteChanged(); HideCommand.NotifyCanExecuteChanged(); ShowSelectedCommand.NotifyCanExecuteChanged();
        InspectSelectionCommand.NotifyCanExecuteChanged(); AddVisibilityCriterionCommand.NotifyCanExecuteChanged();
        CancelVerificationCommand.NotifyCanExecuteChanged(); DismissVerificationProgressCommand.NotifyCanExecuteChanged();
    }
}
