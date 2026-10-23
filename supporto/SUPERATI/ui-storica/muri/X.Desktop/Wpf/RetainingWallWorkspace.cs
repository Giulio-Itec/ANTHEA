using Anthea.Application.Walls;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Application.Scheduling;
using Anthea.Reports;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace : UserControl, IDisposable
{
    internal JsonObject Data { get; }
    internal WallModel.Result? Calculation { get; private set; }
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
    private readonly TextBlock sectionSelection = Ui.Text(RetainingWallPresentation.SectionSelection(null), 11, color: Ui.Muted);
    internal readonly TabControl Pages = new();
    internal readonly Dictionary<string, InputForm> Forms = new();
    internal readonly ComboBox Family = new(), Combination = new();
    internal readonly RetainingWallDrawing Drawing = new() { Height = 400, MinHeight = 300 };
    internal readonly RetainingWallDrawing Diagrams = new() { Height = 410, Diagrams = true };
    private readonly TextBlock status = Ui.Text(RetainingWallPresentation.StatusToVerify, 12), summary = Ui.Text("", 14, true);
    private readonly ContentControl checks = new(), pressures = new(), forces = new(), actionDetails = new(), matrixHost = new();
    private readonly List<JsonGrid> grids = [];
    private readonly RecalculationScheduler recalculation = new();
    private readonly IModuleService calculationService;
    private Task scheduledCalculation = Task.CompletedTask;
    private readonly Grid upper = new();
    private int revision;
    private bool building = true, disposed;
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    internal static string F(double d) => Anthea.Application.Presentation.WallSheetPresentation.Number(d);
    private void Preview() { Drawing.Data = Data; Drawing.Calculation = null; Drawing.Case = null; Drawing.InvalidateVisual(); }
    private void Changed()
    {
        if (building || disposed) return;
        revision++; recalculation.Invalidate(); Calculation = null; UpdateSeismicStatus(); checks.Content = null; pressures.Content = null; forces.Content = null;
        independentGlobal = null; independentGlobalInput = null; independentGlobalError = null; GlobalCombination.ItemsSource = null; RefreshGlobal();
        SelectSection(null);
        Combination.ItemsSource = null; summary.Text = RetainingWallPresentation.ResultsToUpdate; Diagrams.Data = Data; Diagrams.Calculation = null; Diagrams.Case = null; Diagrams.InvalidateVisual(); Preview();
        status.Text = RetainingWallPresentation.StatusPending; QueueCalculation(); Modified?.Invoke();
    }
    private void QueueCalculation() => scheduledCalculation = CalculateScheduledAsync(TimeSpan.FromMilliseconds(450), false);
    internal void Commit() { foreach (var form in Forms.Values) form.Commit(); foreach (var grid in grids) grid.Commit(); }
    internal Task CalculateAsync() => scheduledCalculation = CalculateScheduledAsync(TimeSpan.Zero, true);
    private async Task CalculateScheduledAsync(TimeSpan delay, bool commit)
    {
        if (disposed) return;
        if (commit) Commit();
        var snapshot = (JsonObject)Data.DeepClone();
        var completed = await recalculation.ScheduleAsync(async token =>
        {
            Busy = true; status.Text = RetainingWallPresentation.StatusRunning;
            try { return WallModel.Read(await Task.Run(() => calculationService.Calculate(snapshot, token), token)); }
            finally { Busy = false; }
        }, delay);
        completed.TryPublish(current =>
        {
            try
            {
                if (current.Error is not null) throw current.Error;
                var result = current.Value!; Calculation = result; ShowResults();
                var (text, severity) = RetainingWallPresentation.Status(result);
                status.Text = text;
                status.Foreground = Appearance.Foreground(severity == WallStatusSeverity.Failure ? Brushes.Firebrick : severity == WallStatusSeverity.Warning ? Brushes.DarkGoldenrod : Ui.Navy);
            }
            catch (Exception ex) { Calculation = null; status.Text = RetainingWallPresentation.StatusError(ex.Message); }
        });
    }
    internal void Example(string family)
    {
        building = true;
        try
        {
            var d = WallModel.Example(family); Data.Clear(); foreach (var p in d) Data[p.Key] = p.Value?.DeepClone();
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
    private static DataGrid CheckTable(IEnumerable<WallModel.Check> checks) => Table([.. RetainingWallPresentation.CheckColumns], RetainingWallPresentation.CheckRows(checks));
    internal static IEnumerable<WallModel.Check> Envelope(IEnumerable<WallModel.Check> checks) => WallExports.Envelope(checks);
    private void SelectSection((string Member, double Position, string Combination)? selected)
    {
        selectedSection = selected;
        SendSectionButton.IsEnabled = selected is not null && Calculation is not null && Data.S("family") == "cantilever";
        sectionSelection.Text = RetainingWallPresentation.SectionSelection(selected);
    }
    internal void SendSelectedSection()
    {
        if (selectedSection is not { } s || Calculation is null || Busy || Data.S("family") != "cantilever") return;
        var output = WallModel.ExportSection(Calculation, s.Member, s.Position, s.Combination);
        SectionRequested?.Invoke(output, RetainingWallPresentation.SectionName(s));
    }
    private void ExportCsv()
    {
        bool global = (string?)ViewMode.SelectedItem == "Stabilità globale" && GlobalResult is not null;
        if ((!global && Calculation is null) || Busy) { MessageBox.Show(Window.GetWindow(this), RetainingWallPresentation.WaitBeforeExport); return; }
        var dialog = new SaveFileDialog { Filter = "Tabelle CSV|*.csv", FileName = global ? "Muro_stabilita_globale_conci.csv" : "Muro_verifiche.csv" };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) Archivio.ScriviAtomico(dialog.FileName, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(global ? WallExports.GlobalCsv(GlobalResult!) : WallExports.Csv(Calculation!))).ToArray());
    }
    internal byte[] BuildReport(string title) => DocxWriter.Write(BuildReportDocument(title));
    internal DocxDocument BuildReportDocument(string title)
    {
        if (Calculation is null || Busy) throw new InvalidOperationException("Attendere il calcolo del muro.");
        var context = Anthea.Application.Reporting.WallReportPresentation.Prepare(Calculation, Drawing.Case ?? Calculation.Cases[0], GlobalCase);
        var figures = ModuleFigureRenderer.WallFigures(Calculation, context).Select(f => new ReportRetainingWall.Figure(f.Caption, f.Png, f.AspectRatio)).ToList();
        return WallExports.CreateDocument(title, Calculation, figures);
    }
    public void Dispose() { disposed = true; recalculation.Dispose(); foreach (var session in parameterSessions) session.Dispose(); parameterSessions.Clear(); }
}

