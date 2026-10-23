using Anthea.Application.Walls;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.RetainingWall;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    private readonly List<Anthea.Application.Scheduling.RecalculationScheduler> parameterSessions = [];
    private Task parameterApplyTask = Task.CompletedTask;
    internal Window CalculationParametersWindow()
    {
        Commit(); int openedRevision = revision; bool closed = false;
        var scheduler = new Anthea.Application.Scheduling.RecalculationScheduler(); parameterSessions.Add(scheduler);
        var window = new Window { Owner = Window.GetWindow(this), Title = RetainingWallPresentation.ParametersTitle, Width = 1150, Height = 780, MinWidth = 780, MinHeight = 530, Background = Appearance.Surface, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Appearance.Watch(window);
        window.Closed += (_, _) => { closed = true; scheduler.Dispose(); parameterSessions.Remove(scheduler); };
        var rows = new JsonGrid(RetainingWallFields.ParameterColumns) { Height = 230 };
        rows.Columns[0].Width = 270; rows.Columns[1].Width = 340; rows.Columns[2].Width = 190;
        var parameters = new Anthea.Application.Presentation.WallParameterTable(Data);
        foreach (var row in parameters.Rows) rows.Rows.Add(new JsonRow(row));
        var combo = Ui.Choice(Calculation?.Cases.Select(c => c.Name) ?? [], Combination.SelectedItem as string ?? Calculation?.Cases.FirstOrDefault()?.Name ?? "");
        var derived = new ContentControl();
        void Display()
        {
            if (Calculation?.Cases.FirstOrDefault(c => c.Name == combo.SelectedItem as string) is not { } c) { derived.Content = Ui.Text(RetainingWallPresentation.DerivedMissing, 12); return; }
            var table = Table([.. RetainingWallPresentation.DerivedColumns], RetainingWallPresentation.DerivedRows(c)); table.Height = 230; derived.Content = table;
        }
        combo.SelectionChanged += (_, _) => Display(); Display();
        var message = Ui.Text(RetainingWallPresentation.ParametersHint, 11);
        async Task Apply()
        {
            if (closed || !window.IsEnabled) return;
            rows.Commit();
            try
            {
                if (disposed || revision != openedRevision) throw new ArgumentException(RetainingWallPresentation.ParametersChanged);
                window.IsEnabled = false; message.Text = RetainingWallPresentation.ParametersChecking;
                var completed = await scheduler.ScheduleAsync(token => parameters.ValidateAsync(calculationService, token));
                bool applied = false;
                completed.TryPublish(current =>
                {
                    if (disposed || closed) return;
                    if (revision != openedRevision) throw new ArgumentException(RetainingWallPresentation.ParametersChanged);
                    if (current.Error is not null) throw current.Error;
                    Data.Clear(); foreach (var p in current.Value!) Data[p.Key] = p.Value?.DeepClone();
                    BuildInputs(); BuildMatrix(); UpdateFields(); Changed(); applied = true;
                });
                if (applied) { window.Close(); await CalculateAsync(); }
            }
            catch (Exception ex) { if (!closed && !disposed) { message.Text = ex.Message; message.Foreground = Appearance.Foreground(Brushes.Firebrick); } }
            finally { if (!closed) window.IsEnabled = true; }
        }
        var apply = Ui.Button("Applica e ricalcola", () => { parameterApplyTask = Apply(); });
        var body = Ui.Stack(Ui.Text("Input modificabili e valori effettivamente utilizzati", 18, true), message, rows, Ui.Bar(Ui.Text("Combinazione", 12), combo), derived, Ui.Bar(apply, Ui.Button("Chiudi", window.Close)));
        body.Background = Appearance.Surface; body.Margin = new Thickness(15); window.Content = new Border { Background = Appearance.Surface, Child = Scroll(body) }; return window;
    }
    private async Task ShowCalculationParameters() { await CalculateAsync(); if (!disposed) CalculationParametersWindow().ShowDialog(); }
}
