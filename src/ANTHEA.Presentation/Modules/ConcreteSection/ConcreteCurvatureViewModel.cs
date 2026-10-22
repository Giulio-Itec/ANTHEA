using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Modules;
using Anthea.Application.Scheduling;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The chart of the moment–curvature curve as plain data (the Plot of the historic tab, decisions Q3 and C8): its titles and the points (χ, M)
/// of the only series; the view draws them.
/// </summary>
public sealed class ConcreteCurvatureChartViewModel : ObservableObject
{
    private IReadOnlyList<double[]> points = [];

    public string Title => ConcreteCurvaturePresentation.Title;
    public string XLabel => ConcreteCurvaturePresentation.XLabel;
    public string YLabel => ConcreteCurvaturePresentation.YLabel;
    public string AxisNumberFormat => ConcreteCurvaturePresentation.AxisNumberFormat;
    public string EmptyMessage => ConcreteCurvaturePresentation.EmptyMessage;
    public string SeriesName => ConcreteCurvaturePresentation.SeriesName;
    /// <summary>The points (χ, M) of the curve; empty without a curve.</summary>
    public IReadOnlyList<double[]> Points { get => points; private set => SetProperty(ref points, value); }

    /// <summary>The view draws again.</summary>
    public event EventHandler? Changed;

    internal void Show(IReadOnlyList<double[]> curve) { Points = curve; Changed?.Invoke(this, EventArgs.Empty); }
}

/// <summary>
/// The moment–curvature curve of the RC section (tab 07, F5.10 sub-step S7: ConcreteCurvature.cs of the historic view), without WPF: the form of
/// the load path on <c>momento_curvatura</c>, «Calcola curva», «Interrompi» and «Esporta CSV…» (all three allowed in a read-only revision, as the
/// historic buttons), the text and the chart of the result. The curve is calculated on a worker thread with its own scheduler (the historic
/// curvatureScheduler) from a copy of the data; a curve with the same data is not calculated again. Every change of the data or of the form
/// clears it; the curve published enters the export of the results.
/// </summary>
public sealed class ConcreteCurvatureViewModel : ObservableObject, IDisposable
{
    private readonly ConcreteSheetViewModel sheet;
    private readonly RecalculationScheduler scheduler = new();
    private ConcreteResults.CurvatureResult? result;
    private string? signature;
    private string text = "";
    private bool running, disposed;

    internal ConcreteCurvatureViewModel(ConcreteSheetViewModel sheet)
    {
        this.sheet = sheet;
        // BuildCurvaturePanel of the historic view; nothing is written («CLS teso nelle risposte» shows its default without writing it).
        Form = new FormViewModel(sheet.Settings["momento_curvatura"]!.AsObject(), [new("N", "N costante (− compressione)", "kN"), new("theta", "Direzione del momento θ", "°"),
            new("passi", "Passi di carico"), new("frazione", "Frazione di MRd finale", "−"), new("campionamento", "Distribuzione dei passi", Choices: ["Quadratico", "Uniforme"]),
            new("trazione_cls", "CLS resistente a trazione", Choices: ["No", "Sì"]),
            new("cls_teso_risposte", "CLS teso nelle risposte", Choices: ConcreteEditor.ResponseConcreteTensionModes, Default: ConcreteEditor.ResponseConcreteTensionModes[0]),
            new("angoli", "Direzioni del dominio"), new("tolleranza_n", "Residuo N massimo al limite", "kN"), new("raffina_snervamento", "Bisezioni primo snervamento (0–30)")],
            Changed, compact: true, wideChoices: true)
        {
            CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
        };
        CalculateCommand = new AsyncRelayCommand(CalculateAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        StopCommand = new RelayCommand(scheduler.Invalidate);
        ExportCommand = new RelayCommand(Export);
    }

    /// <summary>«Percorso di carico».</summary>
    public FormViewModel Form { get; }
    /// <summary>The text of the result (status, MRd, χy, χu, ductility, points) or the message of the calculation.</summary>
    public string Text { get => text; private set => SetProperty(ref text, value); }
    /// <summary>The chart «M [kNm] · χ [1/m]».</summary>
    public ConcreteCurvatureChartViewModel Chart { get; } = new();
    /// <summary>The note under the form.</summary>
    public string Notice => ConcreteCurvaturePresentation.Notice;
    /// <summary>The curve published for the current data; null otherwise.</summary>
    public ConcreteResults.CurvatureResult? Result => result;
    /// <summary>A curve is being calculated.</summary>
    public bool IsRunning => running;

    /// <summary>«Calcola curva»: confirms the sheet, then calculates the curve unless it is current; allowed in a read-only revision.</summary>
    public IAsyncRelayCommand CalculateCommand { get; }
    /// <summary>«Interrompi»: the curve in progress is abandoned, nothing is published.</summary>
    public IRelayCommand StopCommand { get; }
    /// <summary>«Esporta CSV…»: the points of the curve published, in a CSV chosen by the user.</summary>
    public IRelayCommand ExportCommand { get; }

    // The callback of the form: the curve is cleared, Modified.
    private void Changed(string key)
    {
        Invalidate(); sheet.RaiseModified();
    }

    /// <summary>InvalidateCurvature of the historic view: the curve in progress abandoned, the curve cleared, the export to read again.</summary>
    internal void Invalidate()
    {
        scheduler.Invalidate(); result = null; signature = null; sheet.ResetExportCache();
        Chart.Show([]); Text = ConcreteCurvaturePresentation.Stale;
    }

    /// <summary>The read-only revision changed.</summary>
    internal void RefreshEditable(bool readOnly) => Form.IsReadOnly = readOnly;

    // CalculateCurvature of the historic view.
    private async Task CalculateAsync()
    {
        if (running || disposed || sheet.IsDisposed) return;
        sheet.CommitSheet();
        var input = (JsonObject)sheet.Input.DeepClone(); var workspace = (JsonObject)sheet.Settings.DeepClone(); var options = workspace["momento_curvatura"]!.AsObject();
        string requested = ConcreteCurvaturePresentation.Signature(input, workspace);
        if (signature == requested && result is not null) return;
        running = true; OnPropertyChanged(nameof(IsRunning));
        Text = ConcreteCurvaturePresentation.Running;
        try
        {
            var completion = await scheduler.ScheduleAsync(token => Task.Run(() => StructuralQueries.ConcreteCurvature(input, workspace, options, token), token));
            if (disposed || sheet.IsDisposed) return;
            if (completion.Cancelled) { Text = ConcreteCurvaturePresentation.Interrupted; return; }
            completion.TryPublish(current =>
            {
                if (current.Error is not null) { Text = ConcreteCurvaturePresentation.Unavailable(current.Error.Message); return; }
                result = current.Value!; signature = requested;
                Chart.Show(ConcreteCurvaturePresentation.Points(result));
                Text = ConcreteCurvaturePresentation.Text(result); sheet.ResetExportCache();
            });
        }
        finally { running = false; OnPropertyChanged(nameof(IsRunning)); }
    }

    // ExportCurvature of the historic view: the file chosen, written at once; an error in a message.
    private void Export()
    {
        if (result is null) return;
        string? path = sheet.Files.SaveFile(new SaveFileRequest([new FileFilter("CSV", "*.csv")], ConcreteCurvaturePresentation.CsvFileName));
        if (path is null) return;
        try { Archivio.ScriviAtomico(path, Encoding.UTF8.GetBytes(ConcreteCurvaturePresentation.Csv(result))); }
        // The historic view shows every failure of the writing in a message.
        catch (Exception failure) { sheet.Messages.Inform("Esportazione curva", failure.Message); }
    }

    /// <summary>The sheet was opened again or closed: the curve in progress is abandoned.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; scheduler.Dispose();
    }
}
