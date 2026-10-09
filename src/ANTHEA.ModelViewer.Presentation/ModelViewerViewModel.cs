using ANTHEA.ModelWorkspace;
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
    void FitView();
    void SaveImage();
}

public enum ModelViewMode { Geometry, Solids, Results }

/// <summary>Testable presentation state. No WPF, renderer, file dialogs or structural calculations.</summary>
public sealed partial class ModelViewerViewModel : ObservableObject, IDisposable
{
    private readonly IModelViewerServices services;
    private readonly Action<ModelSnapshot> imported;
    private readonly Action<string, int, string, string> linked;
    private readonly CancellationTokenSource lifetime = new();
    private bool updating;
    private bool disposed;

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
    public IReadOnlyList<string> Components => ModelSnapshot.Components;
    public IReadOnlyList<string> ModeLabels { get; } = ["Geometria", "Volumi e offset", "Risultati"];
    public IReadOnlyList<string> QualityLabels { get; } = ["Normale", "Alta", "Massima"];
    public ModelViewMode Mode => (ModelViewMode)SelectedMode;
    public int Subdivisions => new[] { 1, 4, 8 }[Math.Clamp(SelectedQuality, 0, 2)];
    public bool IsResultsView => Mode == ModelViewMode.Results;
    public bool CanShowMesh => Mode != ModelViewMode.Solids;
    public string ImportLabel => Snapshot == null ? "Importa modello…" : "Aggiorna modello…";

    [ObservableProperty] private string title = "Nessun modello importato";
    [ObservableProperty] private string summary = "Importa una copia del modello nella fase o sottofase selezionata.";
    [ObservableProperty] private string information = "La bozza legge pacchetti .antheamodel e la copia acquisita nel laboratorio. L’importazione tramite GPC Model è il passo successivo.";
    [ObservableProperty] private string status = "Pronto";
    [ObservableProperty] private string selectionInformation = "Indica il numero di un elemento per leggerne proprietà e valori.";
    [ObservableProperty] private string minimum = "—";
    [ObservableProperty] private string maximum = "—";
    [ObservableProperty] private string resultUnit = "";
    [ObservableProperty] private IReadOnlyList<string> cases = Array.Empty<string>();
    [ObservableProperty] private int selectedMode;
    [ObservableProperty] private int selectedQuality = 1;
    [ObservableProperty] private string? selectedCase;
    [ObservableProperty] private string selectedComponent = "Myy";
    [ObservableProperty] private bool showMesh = true;
    [ObservableProperty] private bool showIsolines = true;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string elementNumber = "";
    [ObservableProperty] private SheetTarget? selectedSheet;

    partial void OnSelectedModeChanged(int value)
    {
        bool wasUpdating = updating; updating = true; ShowMesh = value == 0; updating = wasUpdating;
        OnPropertyChanged(nameof(IsResultsView)); OnPropertyChanged(nameof(CanShowMesh)); RefreshScene();
    }
    partial void OnSelectedQualityChanged(int value) => RefreshScene();
    partial void OnSelectedCaseChanged(string? value) { RefreshScene(); LinkCommand.NotifyCanExecuteChanged(); }
    partial void OnSelectedComponentChanged(string value) => RefreshScene();
    partial void OnShowMeshChanged(bool value) => RefreshScene();
    partial void OnShowIsolinesChanged(bool value) => RefreshScene();
    partial void OnElementNumberChanged(string value) { QueryCommand.NotifyCanExecuteChanged(); LinkCommand.NotifyCanExecuteChanged(); }
    partial void OnSelectedSheetChanged(SheetTarget? value) => LinkCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) { ImportCommand.NotifyCanExecuteChanged(); LinkCommand.NotifyCanExecuteChanged(); }

    private bool CanImport() => !IsReadOnly && !IsBusy && !disposed;
    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync()
    {
        IsBusy = true; Status = "Lettura e controllo del modello…";
        try
        {
            var snapshot = await services.ImportAsync(lifetime.Token);
            if (snapshot == null || disposed) { if (!disposed) Status = "Importazione annullata"; return; }
            snapshot.Validate(); imported(snapshot); SetSnapshot(snapshot); services.FitView();
            Status = "Modello importato. Salva il progetto per conservare la copia e i riferimenti.";
        }
        catch (OperationCanceledException) { if (!disposed) Status = "Importazione annullata"; }
        catch (Exception ex) { if (!disposed) Status = "Importazione non completata: " + ex.Message; }
        finally { IsBusy = false; }
    }

    private void SetSnapshot(ModelSnapshot snapshot)
    {
        snapshot.Validate(); updating = true; Snapshot = snapshot;
        Title = snapshot.Name; Summary = $"{snapshot.Nodes.Length:N0} nodi · {snapshot.Elements.Length:N0} elementi";
        Cases = snapshot.Results.Select(r => r.Name).ToArray(); SelectedCase = Cases.FirstOrDefault();
        SelectedMode = snapshot.Results.Length > 0 ? 2 : 0;
        ElementNumber = snapshot.Elements.FirstOrDefault(e => e.Type == "PLATE")?.Id.ToString() ?? "";
        updating = false; OnPropertyChanged(nameof(Snapshot)); OnPropertyChanged(nameof(ImportLabel));
        QueryCommand.NotifyCanExecuteChanged(); LinkCommand.NotifyCanExecuteChanged(); RefreshScene();
    }

    private void RefreshScene()
    {
        if (updating || disposed || Snapshot == null) return;
        var result = Snapshot.Results.FirstOrDefault(r => r.Name == SelectedCase);
        int column = Array.IndexOf(ModelSnapshot.Components, SelectedComponent);
        var values = column >= 0 ? result?.Values.Select(v => v.Values[column]).ToArray() : null;
        Minimum = values?.Length > 0 ? values.Min().ToString("G6") : "—";
        Maximum = values?.Length > 0 ? values.Max().ToString("G6") : "—";
        ResultUnit = SelectedComponent + " [" + ModelSnapshot.Unit(SelectedComponent) + "]";
        Information = Mode switch
        {
            ModelViewMode.Geometry => "Superficie analitica e connettività originali.\n\nTasto destro: ruota · rotella: zoom · Maiusc + destro: sposta.",
            ModelViewMode.Solids => "Piastre: spessori e offset lungo la normale locale.\n\nAste: sezioni rettangolari centrate. Offset di estremità non importati.\n\nVolumi preliminari, da confrontare con il modello di origine.",
            _ => "Assi locali · valori non mediati. Piastre senza risultati in grigio.\n\nInterpolazione bilineare per elemento, senza raccordare discontinuità fra elementi." + (result?.IsEnvelope == true ? "\n\nInviluppo importato: estremi per componente, non simultanei." : "")
        };
        SceneChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool CanQuery() => Snapshot != null && int.TryParse(ElementNumber, out _) && !disposed;
    [RelayCommand(CanExecute = nameof(CanQuery))]
    private void Query()
    {
        var element = Snapshot!.Elements.FirstOrDefault(e => e.Id.ToString() == ElementNumber.Trim());
        if (element == null) { SelectionInformation = "Elemento non presente nel modello."; return; }
        int column = Array.IndexOf(ModelSnapshot.Components, SelectedComponent);
        var result = Snapshot.Results.FirstOrDefault(r => r.Name == SelectedCase);
        SelectionInformation = $"Elemento {element.Id} · {element.Type}\nNodi: {string.Join(", ", element.Nodes)}\nProprietà {element.Property}\n";
        if (element.Type == "PLATE")
        {
            var plate = Snapshot.Plates.Single(p => p.Id == element.Property);
            SelectionInformation += $"Spessore {plate.Thickness:G4} m · offset {plate.Offset:G4} m\n\n{SelectedCase}\n{ResultUnit}\n";
            SelectionInformation += string.Join("\n", result?.Values.Where(v => v.Element == element.Id).Select(v => $"Nodo {v.Node}: {v.Values[column]:G7}") ?? []);
        }
        else
        {
            var section = Snapshot.Sections.Single(s => s.Id == element.Property);
            SelectionInformation += $"{section.Name}\n{section.Width:G4} × {section.Height:G4} m\nRotazione {element.Angle:G4}°";
        }
    }

    private bool CanLink() => !IsReadOnly && !IsBusy && CanQuery() && SelectedSheet != null && SelectedCase != null;
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
    [RelayCommand] private void SaveImage() { try { services.SaveImage(); } catch (Exception ex) { Status = "Immagine non salvata: " + ex.Message; } }

    public void Dispose()
    {
        if (disposed) return; disposed = true; lifetime.Cancel(); lifetime.Dispose(); SceneChanged = null;
        ImportCommand.NotifyCanExecuteChanged(); QueryCommand.NotifyCanExecuteChanged(); LinkCommand.NotifyCanExecuteChanged();
    }
}
