using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Presentation;
using Anthea.Application.Scheduling;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>A chart of the model column as plain data (titles and the <see cref="PlotData"/> of the result); the view draws it.</summary>
public sealed class ConcreteModelColumnChartViewModel : ObservableObject
{
    private PlotData? data;

    internal ConcreteModelColumnChartViewModel(string title, string yLabel) { Title = title; YLabel = yLabel; }

    public string Title { get; }
    public string XLabel => ConcreteModelColumnPresentation.XLabel;
    public string YLabel { get; }
    public string EmptyMessage => ConcreteModelColumnPresentation.EmptyMessage;
    /// <summary>The series and the markers of the result; null without a result.</summary>
    public PlotData? Data { get => data; private set => SetProperty(ref data, value); }

    /// <summary>The view draws again.</summary>
    public event EventHandler? Changed;

    internal void Show(PlotData? value) { Data = value; Changed?.Invoke(this, EventArgs.Empty); }
}

/// <summary>
/// The model column of the RC section (tab 08 «Colonna modello», 1.15.0), without WPF: the form of <c>workspace_ca.colonna_modello</c>, «Calcola»
/// (not in a read-only revision), «Interrompi» and «Esporta CSV…», the cards and the text of the result and the two charts. The calculation is the
/// one of GPCChecker.Concrete 0.27 (<see cref="ConcreteModelColumn"/>), on a worker thread with its own scheduler from a copy of the data, as the
/// M–χ curve; a result with the same data is not calculated again and every change of the data or of the form clears it.
/// <para>Sheets saved before 1.15.0 have no <c>colonna_modello</c>: the form shows the defaults of <see cref="ConcreteModelColumn.Defaults"/> on a
/// detached object, attached to the settings at the first change made by the user, so that opening writes nothing and raises no Modified.</para>
/// </summary>
public sealed class ConcreteModelColumnViewModel : ObservableObject, IDisposable
{
    private readonly ConcreteSheetViewModel sheet;
    private readonly JsonObject values;
    private readonly RecalculationScheduler scheduler = new();
    private ConcreteModelColumnResult? result;
    private string? signature;
    private string text = "";
    private bool running, disposed;

    internal ConcreteModelColumnViewModel(ConcreteSheetViewModel sheet)
    {
        this.sheet = sheet;
        values = sheet.Settings[ConcreteModelColumn.OptionsKey] as JsonObject ?? new JsonObject();
        var d = ConcreteModelColumn.Defaults;
        FieldSpec Text(string key, string label, string unit, string tip) => new(key, label, unit, Default: d[key], ToolTip: tip);
        FieldSpec Choice(string key, string label, IEnumerable<string> choices, string tip) => new(key, label, Choices: [.. choices], Default: d[key], ToolTip: tip);
        Form = new FormViewModel(values, [
            Text("N", "N costante (− compressione)", "kN", "Sforzo normale di progetto, costante lungo il pilastro; negativo a compressione. N di trazione rifiutato."),
            Text("theta", "Direzione del momento θ", "°", "Direzione del momento negli assi locali: (Mx, My) = M · (cos θ, sin θ), come nella scheda Momento–curvatura."),
            Text("M0Ed", "M0Ed nella sezione critica (vuoto: solo capacità)", "kNm",
                "Momento del primo ordine nella sezione critica, ≥ 0 nella direzione θ, senza imperfezione. Per gli elementi controventati con momenti "
                + "d'estremità diversi il momento equivalente M0e di EN 1992-1-1 (5.32); M02 va verificato a parte all'estremità. Vuoto: solo la capacità M1Rd,max."),
            Text("l0", "Lunghezza libera di inflessione l0", "m", "l0 dallo schema di vincolo (NTC 2018 §4.1.2.3.9.2, EN 1992-1-1 5.8.3.2 e Fig. 5.7): "
                + "mensola 2 l, cerniera–cerniera l, incastro–cerniera 0,7 l, incastro–incastro 0,5 l."),
            Text("l", "Lunghezza dell'elemento l", "m", "Lunghezza reale del pilastro: αh = 2/√l di EN 1992-1-1 5.2(5) e difetto l/300 di NTC 2018 §4.1.2.3.9.3."),
            Text("c", "Fattore della curvatura c (8–10)", "−", "e2 = χ·l0²/c (EN 1992-1-1 5.8.8.2(4)): 10 ≈ π² per la deformata sinusoidale, 8 con momento del primo ordine costante."),
            Choice("imperfezione", "Imperfezione geometrica", ConcreteModelColumn.ImperfectionModes, "EN 1992-1-1 5.2(7)a con θ0 = 1/200 (anche per le NTC, che rinviano "
                + "ai documenti di comprovata validità); EN l0/400 per pilastri isolati di sistemi controventati; NTC l/300 per i pilastri di telai a nodi fissi; "
                + "assegnata; nessuna. Agisce nella direzione del momento."),
            Text("ei", "Eccentricità assegnata ei", "mm", "Eccentricità dell'imperfezione assegnata, ≥ 0."),
            Choice("eccentricita_minima", "Eccentricità minima", ConcreteModelColumn.MinimumEccentricityModes, "Minimo del momento totale del primo ordine, "
                + "M0Ed,tot = max(M0Ed + NEd·ei; NEd·e0), non sommato all'imperfezione. Della norma: NTC 2018 e CNR-DT 200 max(l0/200; 20 mm), EN e UNI max(h/30; 20 mm)."),
            Choice("viscosita", "Viscosità", ConcreteModelColumn.CreepModes, "Deformazioni del legame del calcestruzzo moltiplicate per (1 + φef) (EN 1992-1-1 5.8.6(4)). "
                + "Trascurata: controllo delle condizioni di 5.8.4(4). Dalla (5.19): φef = φ(∞, t0)·M0Eqp/M0Ed, con l'imperfezione in entrambi i momenti."),
            Text("phi_ef", "Coefficiente di viscosità efficace φef", "−", "φef ≥ 0 (EN 1992-1-1 5.8.4)."),
            Text("phi_inf", "Coefficiente di viscosità φ(∞, t0)", "−", "NTC 2018 §11.2.10.7 o EN 1992-1-1 3.1.4. Richiesto dalla (5.19); negli altri casi facoltativo, "
                + "per le condizioni di 5.8.4(4)."),
            Text("M0Eqp", "M0Eqp quasi permanente", "kNm", "Momento del primo ordine della combinazione quasi permanente nella direzione θ, senza imperfezione."),
            Text("NEqp", "NEqp quasi permanente (− compressione)", "kN", "Sforzo normale della combinazione quasi permanente, per il momento della sua imperfezione."),
            Text("rm", "rm = M01/M02 (vuoto: non noto)", "−", "Rapporto dei momenti d'estremità per il fattore C di λlim (EN 1992-1-1 (5.13N)), fra −1 e 1; vuoto: C = 0,7."),
            Text("passi", "Passi di curvatura (10–500)", "", "Campioni uniformi fra 0 e la curvatura ultima; il primo snervamento e il massimo sono inseriti."),
            Text("tolleranza_n", "Tolleranza su N di MRd(N)", "kN", "Residuo massimo su N della resistenza MRd(N) del confronto con la curvatura nominale; "
                + "il calcolo usa il maggiore fra questo valore e la regola della libreria."),
            Text("limite_trasversale", "Momento trasversale ammesso", "%", "Oltre questo rapporto fra il momento trasversale e quello nella direzione θ il calcolo è "
                + "rifiutato: la sezione non è simmetrica rispetto al piano di flessione.")],
            Changed, compact: true, wideChoices: true)
        {
            CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
        };
        Form.GroupFields("Imperfezione ed eccentricità minima", ["imperfezione", "ei", "eccentricita_minima"], expanded: true);
        Form.GroupFields("Viscosità", ["viscosita", "phi_ef", "phi_inf", "M0Eqp", "NEqp"], expanded: true);
        Form.GroupFields("Calcolo", ["rm", "passi", "tolleranza_n", "limite_trasversale"]);
        RefreshFields();
        CalculateCommand = new AsyncRelayCommand(CalculateAsync, () => sheet.CanEdit, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        StopCommand = new RelayCommand(scheduler.Invalidate);
        ExportCommand = new RelayCommand(Export);
    }

    /// <summary>The data of the column.</summary>
    public FormViewModel Form { get; }
    /// <summary>The cards of the result: slenderness, first-order moment, creep, capacity, check, nominal curvature, warnings.</summary>
    public ConcreteCardsViewModel Cards { get; } = new();
    /// <summary>The text of the result (status, steps of the library, points) or the message of the calculation.</summary>
    public string Text { get => text; private set => SetProperty(ref text, value); }
    /// <summary>The chart M(χ) with the external moment and the tangent at the maximum.</summary>
    public ConcreteModelColumnChartViewModel CapacityChart { get; } = new(ConcreteModelColumnPresentation.CapacityTitle, ConcreteModelColumnPresentation.CapacityYLabel);
    /// <summary>The chart M − M2 with the maximum.</summary>
    public ConcreteModelColumnChartViewModel FirstOrderChart { get; } = new(ConcreteModelColumnPresentation.FirstOrderTitle, ConcreteModelColumnPresentation.FirstOrderYLabel);
    /// <summary>The note under the form.</summary>
    public string Notice => ConcreteModelColumnPresentation.Notice;
    /// <summary>The result published for the current data; null otherwise.</summary>
    public ConcreteModelColumnResult? Result => result;
    /// <summary>A model column is being calculated.</summary>
    public bool IsRunning => running;

    /// <summary>«Calcola»: confirms the sheet and the form, then calculates unless the result is current; not in a read-only revision.</summary>
    public IAsyncRelayCommand CalculateCommand { get; }
    /// <summary>«Interrompi»: the calculation in progress is abandoned, nothing is published.</summary>
    public IRelayCommand StopCommand { get; }
    /// <summary>«Esporta CSV…»: the points of the result published, in a CSV chosen by the user.</summary>
    public IRelayCommand ExportCommand { get; }

    // The fields that depend on a choice: ei with «Assegnata», φef with «φef assegnato», M0Eqp and NEqp with the (5.19).
    private void RefreshFields()
    {
        Form.ShowField("ei", ConcreteModelColumn.Text(values, "imperfezione") == ConcreteModelColumn.ImperfectionModes[3]);
        string creep = ConcreteModelColumn.Text(values, "viscosita");
        Form.ShowField("phi_ef", creep == ConcreteModelColumn.CreepModes[1]);
        foreach (string key in new[] { "M0Eqp", "NEqp" }) Form.ShowField(key, creep == ConcreteModelColumn.CreepModes[2]);
    }

    // The callback of the form: the options attached to the settings at the first change, the result cleared, Modified.
    private void Changed(string key)
    {
        if (values.Parent is null && !sheet.IsDisposed) sheet.Settings[ConcreteModelColumn.OptionsKey] = values;
        RefreshFields(); Invalidate(); sheet.RaiseModified();
    }

    /// <summary>The data changed: the calculation in progress abandoned, the result cleared, the export to read again.</summary>
    internal void Invalidate()
    {
        scheduler.Invalidate();
        bool shown = result is not null;
        result = null; signature = null;
        if (shown) sheet.ResetExportCache();
        Cards.Start(); CapacityChart.Show(null); FirstOrderChart.Show(null);
        Text = ConcreteModelColumnPresentation.Stale;
    }

    /// <summary>The read-only revision changed.</summary>
    internal void RefreshEditable(bool readOnly) { Form.IsReadOnly = readOnly; CalculateCommand.NotifyCanExecuteChanged(); }

    /// <summary>The texts typed and not yet confirmed (the sheet commits, a calculation starts).</summary>
    internal void Commit() => Form.Commit();

    private async Task CalculateAsync()
    {
        if (running || disposed || sheet.IsDisposed || !sheet.CanEdit) return;
        sheet.CommitSheet();
        var input = (JsonObject)sheet.Input.DeepClone(); var workspace = (JsonObject)sheet.Settings.DeepClone();
        var options = (JsonObject)values.DeepClone();
        string requested = ConcreteModelColumnPresentation.Signature(input, workspace) + options.ToJsonString();
        if (signature == requested && result is not null) return;
        running = true; OnPropertyChanged(nameof(IsRunning));
        Text = ConcreteModelColumnPresentation.Running;
        try
        {
            var completion = await scheduler.ScheduleAsync(token => Task.Run(() => ConcreteModelColumn.Calculate(input, workspace, options, token), token));
            if (disposed || sheet.IsDisposed) return;
            if (completion.Cancelled) { if (Text != ConcreteModelColumnPresentation.Stale) Text = ConcreteModelColumnPresentation.Interrupted; return; }
            completion.TryPublish(current =>
            {
                if (current.Error is not null) { Cards.Start(); Text = ConcreteModelColumnPresentation.Unavailable(current.Error.Message); return; }
                result = current.Value!; signature = requested;
                Show(result);
                sheet.ResetExportCache();
            });
        }
        finally { running = false; OnPropertyChanged(nameof(IsRunning)); }
    }

    private void Show(ConcreteModelColumnResult value)
    {
        Cards.Start();
        foreach (var card in ConcreteModelColumnPresentation.Cards(value))
            if (card.IsCheck) Cards.AddDetail(card.Title, card.Passed, card.Value, card.Explanation, card.Reference);
            else Cards.AddValue(card.Title, card.Value, card.Explanation);
        CapacityChart.Show(ConcreteModelColumnPresentation.CapacityChart(value));
        FirstOrderChart.Show(ConcreteModelColumnPresentation.FirstOrderChart(value));
        Text = ConcreteModelColumnPresentation.Text(value);
    }

    private void Export()
    {
        if (result is null) return;
        string? path = sheet.Files.SaveFile(new SaveFileRequest([new FileFilter("CSV", "*.csv")], ConcreteModelColumnPresentation.CsvFileName));
        if (path is null) return;
        try { Archivio.ScriviAtomico(path, Encoding.UTF8.GetBytes(ConcreteModelColumnPresentation.Csv(result))); }
        catch (Exception failure) { sheet.Messages.Inform("Esportazione colonna modello", failure.Message); }
    }

    /// <summary>The sheet was opened again or closed: the calculation in progress is abandoned.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; scheduler.Dispose();
    }
}
