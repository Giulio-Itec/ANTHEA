using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace : UserControl, IDisposable
{
    internal JsonObject Data { get; }
    internal RetainingWall.Result? Calculation { get; private set; }
    internal JsonObject? Result => Calculation?.Json();
    internal bool Busy { get; private set; }
    internal event Action? Modified;
    internal event Action<JsonObject, string>? SectionRequested;
    internal event Action? ReportRequested;
    internal event Action? SoilTransferRequested;
    internal Button SoilButton = null!;
    internal Button SendSectionButton = null!;
    internal DataGrid? SelectedResultsGrid;
    private (string Member, double Position, string Combination)? selectedSection;
    private readonly TextBlock sectionSelection = Ui.Text("Selezionare una riga strutturale o una sezione nella tabella delle sollecitazioni.", 11, color: Ui.Muted);
    internal readonly TabControl Pages = new();
    internal readonly Dictionary<string, InputForm> Forms = new();
    internal readonly ComboBox Family = new(), Combination = new();
    internal readonly RetainingWallDrawing Drawing = new() { Height = 400, MinHeight = 300 };
    internal readonly RetainingWallDrawing Diagrams = new() { Height = 410, Diagrams = true };
    private readonly TextBlock status = Ui.Text("Dati da verificare", 12), summary = Ui.Text("", 14, true);
    private readonly ContentControl checks = new(), pressures = new(), forces = new(), actionDetails = new(), matrixHost = new();
    private readonly List<JsonGrid> grids = [];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly Grid upper = new();
    private CancellationTokenSource? cancellation;
    private Task running = Task.CompletedTask;
    private int revision;
    private bool building = true, disposed;
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    internal static string F(double d) => d.ToString("0.00", It);
    private void Preview() { Drawing.Data = Data; Drawing.Calculation = null; Drawing.Case = null; Drawing.InvalidateVisual(); }
    private void Changed()
    {
        if (building || disposed) return;
        revision++; cancellation?.Cancel(); Calculation = null; UpdateSeismicStatus(); checks.Content = null; pressures.Content = null; forces.Content = null;
        independentGlobal = null; independentGlobalInput = null; independentGlobalError = null; GlobalCombination.ItemsSource = null; RefreshGlobal();
        SelectSection(null);
        Combination.ItemsSource = null; summary.Text = "Risultati da aggiornare"; Diagrams.Data = Data; Diagrams.Calculation = null; Diagrams.Case = null; Diagrams.InvalidateVisual(); Preview();
        status.Text = "Aggiornamento in attesa…"; timer.Stop(); timer.Start(); Modified?.Invoke();
    }
    private async void Tick(object? sender, EventArgs e) { timer.Stop(); await CalculateAsync(); }
    internal void Commit() { foreach (var form in Forms.Values) form.Commit(); foreach (var grid in grids) grid.Commit(); }
    internal async Task CalculateAsync()
    {
        if (disposed) return; Commit(); timer.Stop();
        while (Busy) { await running; if (disposed || Calculation is not null) return; }
        int request = revision; var snapshot = (JsonObject)Data.DeepClone(); cancellation?.Dispose(); cancellation = new(); var token = cancellation.Token;
        Busy = true; status.Text = "Calcolo di equilibrio e verifiche GPC…";
        running = Run(); await running;
        async Task Run()
        {
            try
            {
                var result = await Task.Run(() => RetainingWall.Calculate(snapshot, token));
                if (disposed || request != revision) return; Calculation = result; ShowResults();
                var all = result.Checks.Concat(result.Structural).ToArray();
                int failed = all.Count(c => c.Ratio > 1 || c.Status.StartsWith("Non soddisfatta") || c.Status == "Perdita di equilibrio"), missing = all.Count(c => c.Ratio is null && !c.Status.StartsWith("Non soddisfatta"));
                status.Text = $"Calcolo aggiornato · {result.Cases.Count} combinazioni · {failed} controlli non soddisfatti · {missing} non disponibili";
                status.Foreground = Appearance.Foreground(failed > 0 ? Brushes.Firebrick : missing > 0 ? Brushes.DarkGoldenrod : Ui.Navy);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!disposed && request == revision) { Calculation = null; status.Text = "Dati da correggere: " + ex.Message; } }
            finally { Busy = false; if (!disposed && request != revision) timer.Start(); }
        }
    }
    internal void Example(string family)
    {
        building = true;
        try
        {
            var d = RetainingWall.Example(family); Data.Clear(); foreach (var p in d) Data[p.Key] = p.Value?.DeepClone();
            Family.SelectedItem = Family.Items.Cast<ComboBoxItem>().Single(i => (string)i.Tag == family); BuildInputs(); BuildMatrix(); UpdateFields();
        }
        finally { building = false; }
        Changed();
    }
    private static DataGrid Table(string[] columns, IEnumerable<string[]> rows)
    {
        var grid = Ui.Table(columns, rows);
        foreach (var column in grid.Columns) column.Width = DataGridLength.Auto;
        return grid;
    }
    private static DataGrid CheckTable(IEnumerable<RetainingWall.Check> checks) => Table(["Verifica", "Combinazione", "Ed", "Rd", "Unità", "Ed/Rd", "Esito"],
        checks.Select(c => new[] { c.Name, c.Combination, F(c.Demand), c.Resistance is double r ? F(r) : "—", c.Unit, c.Ratio is double q ? q.ToString("0.000", It) : "—", c.Status }));
    internal static IEnumerable<RetainingWall.Check> Envelope(IEnumerable<RetainingWall.Check> checks) => ReportRetainingWall.Envelope(checks);
    private void SelectSection((string Member, double Position, string Combination)? selected)
    {
        selectedSection = selected;
        SendSectionButton.IsEnabled = selected is not null && Calculation is not null && Data.S("family") == "cantilever";
        sectionSelection.Text = selected is { } s ? $"{s.Member} · z/l={s.Position:0.######} m · {s.Combination}" : "Selezionare una riga strutturale o una sezione nella tabella delle sollecitazioni.";
    }
    internal void SendSelectedSection()
    {
        if (selectedSection is not { } s || Calculation is null || Busy || Data.S("family") != "cantilever") return;
        var output = RetainingWall.ExportSection(Calculation, s.Member, s.Position, s.Combination);
        SectionRequested?.Invoke(output, $"Muro · {s.Member} · z/l {s.Position:0.###} m");
    }
    private void ExportCsv()
    {
        bool global = (string?)ViewMode.SelectedItem == "Stabilità globale" && GlobalResult is not null;
        if ((!global && Calculation is null) || Busy) { MessageBox.Show(Window.GetWindow(this), "Attendere il calcolo e correggere i dati prima di esportare."); return; }
        var dialog = new SaveFileDialog { Filter = "Tabelle CSV|*.csv", FileName = global ? "Muro_stabilita_globale_conci.csv" : "Muro_verifiche.csv" };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) Archivio.ScriviAtomico(dialog.FileName, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(global ? ReportRetainingWall.GlobalCsv(GlobalResult!) : ReportRetainingWall.Csv(Calculation!))).ToArray());
    }
    internal byte[] BuildReport(string title)
    {
        if (Calculation is null || Busy) throw new InvalidOperationException("Attendere il calcolo del muro.");
        var figures = new List<ReportRetainingWall.Figure>(); var c = Drawing.Case ?? Calculation.Cases[0];
        foreach (var member in new[] { "Geometria", "Fusto", "Valle", "Monte", "Armature" })
        {
            if (member == "Armature" && Calculation.Input.S("family") != "cantilever") continue;
            if (member is "Valle" or "Monte" && (!c.Contact.Valid || !c.Sections.Any(s => s.Name == member))) continue;
            double figureWidth = member == "Armature" ? 760 : 1100, figureHeight = member == "Armature" ? 560 : 480;
            var view = new RetainingWallDrawing { Data = Calculation.Input, Calculation = Calculation, Case = c, Width = figureWidth, Height = figureHeight,
                Diagrams = member is not ("Geometria" or "Armature"), Mode = member == "Armature" ? "Armature" : "Geometria e carichi", Member = member, CombinedLoads = false };
            view.Measure(new Size(figureWidth, figureHeight)); view.Arrange(new Rect(0, 0, figureWidth, figureHeight)); view.UpdateLayout();
            figures.Add(new(member == "Geometria" ? "Sezione, terreno e armature · carichi caratteristici; spinte: " + c.Name : member == "Armature" ? "Sezione armata con pieghe e sovrapposizioni" : "Diagrammi " + member + " · " + c.Name, Ui.Snapshot(view), figureWidth / figureHeight));
        }
        if (Calculation.GlobalStability is { } global)
        {
            var view = new GlobalStabilityDrawing { Data = Calculation.Input, Result = global, Case = GlobalCase, Width = 1100, Height = 460 };
            view.Measure(new Size(1100, 460)); view.Arrange(new Rect(0, 0, 1100, 460)); view.UpdateLayout();
            figures.Add(new("Stabilità globale · superficie critica della combinazione visualizzata", Ui.Snapshot(view), 1100d / 460));
        }
        if (Calculation.Input.S("family") == "cantilever")
        {
            try { figures.AddRange(BarFigures(CreateBarDrawing())); }
            catch (ArgumentException) { /* The report records the invalid schedule inputs beside its tables. */ }
        }
        return ReportRetainingWall.Create(title, Calculation, figures);
    }
    public void Dispose() { disposed = true; timer.Stop(); timer.Tick -= Tick; cancellation?.Cancel(); }
}

