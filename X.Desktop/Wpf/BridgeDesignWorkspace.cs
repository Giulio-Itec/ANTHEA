using Anthea.Application.BridgeDesign;
using Anthea.Application.Presentation;
using Anthea.Reports;
using Anthea.Application.Modules;
using Anthea.Application.Scheduling;
using Anthea.Presentation.Modules.BridgeDesign;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeDesignWorkspace : UserControl, IDisposable
{
    internal JsonObject Data { get; }
    internal BridgeDesignModel.Result? Calculation { get; private set; }
    internal JsonObject? Result => Calculation?.Json();
    internal event Action? Modified;
    internal readonly TabControl Inputs = new(), Outputs = new(), WorkspaceTabs = new();
    internal ScrollViewer DesignScroll { get; private set; } = new();
    internal readonly Dictionary<string, TextBox> Editors = new();
    internal readonly BridgeDesignDrawing Drawing = new() { Height = 400, MinWidth = 300 };
    private readonly Grid upper = new();
    private readonly Border inputCard, drawingCard;
    private readonly TextBlock status = Ui.Text("", 12), configuration = Ui.Text("", 12, color: Ui.Muted);
    private readonly TextBlock[] values = Enumerable.Range(0, 4).Select(_ => Ui.Text("—", 26, true)).ToArray();
    private readonly TextBlock[] captions = Enumerable.Range(0, 4).Select(_ => Ui.Text("", 11, color: Ui.Muted)).ToArray();
    private readonly ContentControl quantities = new(), detail = new(), comparison = new(), advice = new(), prices = new();
    private StackPanel sectionPanel = new();
    private readonly BridgeDesignHistory history;
    private bool refreshing, disposed;
    internal bool Busy { get; private set; }
    private readonly RecalculationScheduler recalculation = new();
    private readonly IModuleService calculationService;
    private Task scheduledCalculation = Task.CompletedTask;
    private BridgeDesignAnalysis? analysis;
    internal static string F(double n, string format = "N1") => BridgeDesignPresentation.F(n, format);

    internal BridgeDesignWorkspace(JsonObject data) : this(data, null) { }
    internal BridgeDesignWorkspace(JsonObject data, IModuleService? service)
    {
        calculationService = service ?? ModuleServices.Get(BridgeDesignModel.Module);
        ModuleSheetPreparation.PrepareBridgeDesign(data);
        Data = data; history = new BridgeDesignHistory(Data); Background = Appearance.Surface;
        var title = Ui.Stack(Ui.Text(BridgeDesignPresentation.Title, 24, true), Ui.Text(BridgeDesignPresentation.Subtitle, 13, color: Ui.Muted));
        title.Margin = new Thickness(2, 0, 0, 10);
        var scope = Ui.Text(BridgeDesignPresentation.ScopeLine, 11, true, Ui.Blue);
        scope.Margin = new Thickness(2, 0, 0, 12);
        BuildInputs();
        Inputs.MinHeight = 470;
        inputCard = Ui.Paper(Inputs, 10);
        var controls = Ui.Bar(
            Ui.Button("Prospetto", () => { Drawing.Section = false; Drawing.InvalidateVisual(); }, inspection: true),
            Ui.Button("Sezione", () => { Drawing.Section = true; Drawing.InvalidateVisual(); }, inspection: true),
            Ui.Button("Nuovo paesaggio", () => Mutate(() => BridgeDesignPresentation.NewScene(Data))),
            Ui.Button("Ponte casuale", Randomize),
            Ui.Button("Ottimizza", () => WorkspaceTabs.SelectedIndex = 1, inspection: true),
            Ui.Button("Fissa A", Pin), Ui.Button("Annulla", Undo),
            Ui.Button("Sezioni e quote", () => { Outputs.SelectedIndex = 5; Outputs.BringIntoView(); }, inspection: true));
        var exports = Ui.Bar(Ui.Button("Esporta quantità CSV", ExportCsv, inspection: true), Ui.Button("Immagine PNG", ExportPng, inspection: true),
            Ui.Button("Stampa scheda", Print, inspection: true));
        drawingCard = Ui.Paper(Ui.Stack(controls, Drawing, configuration, exports), 12);
        upper.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(375) }); upper.ColumnDefinitions.Add(new ColumnDefinition());
        upper.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); upper.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        upper.Children.Add(inputCard); upper.Children.Add(drawingCard); Grid.SetColumn(drawingCard, 1);
        inputCard.Margin = new Thickness(0, 0, 12, 0);
        var metrics = new UniformGrid { Columns = 4, Margin = new Thickness(0, 12, 0, 12) };
        var labels = BridgeDesignPresentation.MetricLabels;
        for (int k = 0; k < 4; k++)
        {
            var p = Ui.Paper(Ui.Stack(Ui.Text(labels[k], 10, true, Ui.Muted), values[k], captions[k]), 14);
            p.Margin = new Thickness(k == 0 ? 0 : 6, 0, k == 3 ? 0 : 6, 0); metrics.Children.Add(p);
        }
        var tabs = BridgeDesignPresentation.OutputTabs;
        Ui.Tab(Outputs, tabs[0], quantities);
        Ui.Tab(Outputs, tabs[1], detail);
        Ui.Tab(Outputs, tabs[2], comparison);
        prices.Content = BuildPricesPanel(); Ui.Tab(Outputs, tabs[3], prices);
        Ui.Tab(Outputs, tabs[4], Scroll(ParameterPanel("assumptions", BridgeDesignPresentation.AssumptionParameters()), 365));
        Ui.Tab(Outputs, tabs[5], technical);
        Outputs.MinHeight = 330;
        var bottom = Ui.Stack(Ui.Paper(Outputs, 12), advice);
        status.Margin = new Thickness(4, 10, 4, 6);
        var stack = Ui.Stack(title, scope, upper, metrics, bottom, status); stack.Margin = new Thickness(20, 16, 20, 12);
        DesignScroll = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Ui.Tab(WorkspaceTabs, "Progetto", DesignScroll);
        Ui.Tab(WorkspaceTabs, "Ottimizzazione", BuildOptimizationPanel());
        Content = WorkspaceTabs;
        SizeChanged += (_, _) =>
        {
            bool compact = ActualWidth < 1100;
            upper.ColumnDefinitions[0].Width = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(375);
            upper.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(drawingCard, compact ? 0 : 1); Grid.SetRow(drawingCard, compact ? 1 : 0);
            inputCard.Margin = compact ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 12, 0);
            Inputs.MinHeight = compact ? 330 : 470; metrics.Columns = ActualWidth < 850 ? 2 : 4;
        };
        QueueCalculation(TimeSpan.Zero);
    }
    private static ScrollViewer Scroll(UIElement content, double max = double.PositiveInfinity) => new ChainedScrollViewer
    { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = max };
    private static JsonObject Clone(JsonObject d) => (JsonObject)d.DeepClone();
    private JsonObject Input => Data["input"]!.AsObject();
    private void BuildInputs()
    {
        Inputs.Items.Clear();
        foreach (string key in Editors.Keys.Where(k => k.StartsWith("input/")).ToArray()) Editors.Remove(key);
        sectionPanel = new StackPanel();
        var tabs = BridgeDesignPresentation.InputTabs;
        var site = new StackPanel();
        Choice(site, "obstacle", BridgeDesignPresentation.ObstacleLabel, BridgeDesignModel.Obstacles);
        Choice(site, "soil", BridgeDesignPresentation.SoilLabel, BridgeDesignModel.Soils);
        foreach (var p in BridgeDesignModel.Site) site.Children.Add(Number("input", p, true));
        Ui.Tab(Inputs, tabs[0], Scroll(site, 490));
        var layout = ParameterPanel("input", BridgeDesignModel.Layout, true);
        Toggle(layout, "continuous", BridgeDesignPresentation.ContinuousLabel); Toggle(layout, "start_pier", BridgeDesignPresentation.StartPierLabel); Toggle(layout, "end_pier", BridgeDesignPresentation.EndPierLabel);
        layout.Children.Add(Ui.Text(BridgeDesignPresentation.SpansNote, 12, color: Ui.Muted));
        layout.Children.Add(Ui.Button(BridgeDesignPresentation.AutoSizeButton, AutoSize));
        Ui.Tab(Inputs, tabs[1], Scroll(layout, 490));
        BuildSection(); Ui.Tab(Inputs, tabs[2], Scroll(sectionPanel, 490));
        var sub = new StackPanel();
        if (BridgeDesignModel.HasTowers(Input.S("family"))) sub.Children.Add(Ui.Text(BridgeDesignPresentation.TowersNote, 12));
        else Choice(sub, "pier", BridgeDesignPresentation.PierLabel, BridgeDesignModel.Piers);
        Choice(sub, "foundation", BridgeDesignPresentation.FoundationLabel, BridgeDesignModel.Foundations);
        foreach (var p in BridgeDesignModel.Substructure) sub.Children.Add(Number("input", p, true));
        sub.Children.Add(Ui.Text(BridgeDesignPresentation.SubstructureNote, 11, color: Ui.Muted));
        Ui.Tab(Inputs, tabs[3], Scroll(sub, 490));
        var traffic = new StackPanel();
        Choice(traffic, "traffic_model", BridgeDesignPresentation.TrafficLabel, BridgeDesignModel.TrafficModels);
        traffic.Children.Add(Ui.Text(BridgeDesignPresentation.TrafficNote(Input.S("traffic_model")), 12, color: Ui.Muted));
        traffic.Children.Add(Number("assumptions", BridgeDesignModel.Assumptions.Single(p => p.Key == "gamma_q"), false));
        if (Input.S("traffic_model") == BridgeDesignModel.UniformLegacy)
            traffic.Children.Add(Number("assumptions", BridgeDesignModel.Assumptions.Single(p => p.Key == "traffic_load"), false));
        else traffic.Children.Add(Ui.Button(BridgeDesignPresentation.RecommendedTrafficButton, () => Mutate(() => BridgeDesignModel.ApplyRecommendedTrafficFactor(Data), true)));
        traffic.Children.Add(Ui.Text(BridgeDesignPresentation.TrafficFootnote, 11, color: Ui.Muted));
        Ui.Tab(Inputs, tabs[4], Scroll(traffic, 490));
    }
    private void BuildSection()
    {
        sectionPanel.Children.Clear();
        var families = new UniformGrid { Columns = 2 };
        foreach (var f in BridgeDesignModel.Families)
        {
            var button = Ui.Button("", () => Mutate(() => BridgeDesignPresentation.SelectFamily(Input, f.Id), true));
            bool selected = Input.S("family") == f.Id;
            Ui.SetSelected(button, selected);
            button.Content = Ui.Stack(Ui.Text(f.Name, 11, true, button.Foreground), new BridgeFamilyIcon { Family = f.Id, Ink = button.Foreground, Height = 32, Width = 112 }, Ui.Text(BridgeDesignPresentation.FamilyRange(f), 10, color: selected ? Ui.Brush("#BCCFE2") : Ui.Muted));
            button.SetValue(AutomationProperties.NameProperty, f.Name); button.MinHeight = 82; button.Padding = new Thickness(6); families.Children.Add(button);
        }
        sectionPanel.Children.Add(families);
        var (deck, fields, advanced, note) = BridgeDesignPresentation.SectionFields(Input);
        if (deck) Choice(sectionPanel, "deck_type", BridgeDesignPresentation.DeckLabel, BridgeDesignModel.DeckTypes);
        foreach (var p in fields) sectionPanel.Children.Add(Number("input", p, true));
        if (note is not null)
        {
            foreach (var p in advanced) sectionPanel.Children.Add(Number("input", p, true));
            sectionPanel.Children.Add(Ui.Text(note, 11, color: Ui.Muted));
        }
        sectionPanel.Children.Add(Ui.Button(BridgeDesignPresentation.AutoSectionButton, AutoSize));
    }
    private UIElement BuildPricesPanel()
    {
        void Open()
        {
            try
            {
                var vm = new PriceCatalogueViewModel(Data); BridgePricesView.Create(this, vm).ShowDialog();
                if (vm.Result is not null) Mutate(() => vm.CommitTo(Data), true);
            }
            catch (ArgumentException e) { status.Text = e.Message; }
        }
        var controls = Ui.Stack(Ui.Button(BridgeDesignPresentation.ManagePricesButton, Open, inspection: true));
        if (Data["pricing"] is JsonObject pricing)
        {
            controls.Children.Add(Ui.Text(BridgeDesignPresentation.PriceDocumentTitle(pricing), 15, true));
            controls.Children.Add(Ui.Text(BridgeDesignPresentation.PriceDocumentNote, 12));
        }
        else controls.Children.Add(Scroll(ParameterPanel("rates", BridgeDesignModel.Rates), 330));
        return controls;
    }
    private StackPanel ParameterPanel(string group, IEnumerable<BridgeDesignModel.Parameter> parameters, bool sliders = false)
    {
        var panel = new StackPanel { Margin = new Thickness(4) };
        if (group == "rates")
        {
            panel.Children.Add(Ui.Text(BridgeDesignModel.PriceBasis, 12, color: Ui.Muted));
            panel.Children.Add(Ui.Button(BridgeDesignPresentation.IndicativeRatesButton, () => Mutate(() => BridgeDesignPresentation.ApplyIndicativeRates(Data), true)));
        }
        if (group == "assumptions") panel.Children.Add(Ui.Text(BridgeDesignPresentation.AssumptionsNote, 12, color: Ui.Muted));
        foreach (var p in parameters)
        {
            panel.Children.Add(Number(group, p, sliders));
            if (group == "rates" && BridgeDesignModel.RateNotes.TryGetValue(p.Key, out var note)) panel.Children.Add(Ui.Text(note, 11, color: Ui.Muted));
        }
        return panel;
    }
    private FrameworkElement Number(string group, BridgeDesignModel.Parameter p, bool slider)
    {
        var row = new Grid { Margin = new Thickness(3, 6, 3, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        row.Children.Add(Ui.Text(p.Label, 12));
        var edit = new TextBox { Text = BridgeDesignPresentation.ParameterText(Data, group, p.Key), TextAlignment = TextAlignment.Center, MinHeight = 26, Tag = group + "/" + p.Key };
        edit.SetValue(AutomationProperties.NameProperty, p.Label); Editors[group + "/" + p.Key] = edit;
        Grid.SetColumn(edit, 1); row.Children.Add(edit); var unit = Ui.Text(p.Unit, 11, color: Ui.Muted); unit.Margin = new Thickness(5, 0, 0, 0); Grid.SetColumn(unit, 2); row.Children.Add(unit);
        var panel = Ui.Stack(row); bool sync = false;
        Slider? range = slider ? new Slider { Minimum = p.Min, Maximum = p.Max, Value = BridgeDesignPresentation.ParameterValue(Data, group, p.Key), TickFrequency = p.Step, IsSnapToTickEnabled = true, Margin = new Thickness(6, 0, 6, 2), Focusable = false } : null;
        if (range is not null)
        {
            range.SetValue(AutomationProperties.NameProperty, p.Label + " · cursore"); panel.Children.Add(range);
            range.ValueChanged += (_, _) => { if (sync || refreshing) return; edit.Text = BridgeDesignPresentation.SliderText(range.Value); };
            range.PreviewMouseWheel += (_, e) => { ChainedScrollViewer.ForwardWheel(range, e); e.Handled = true; };
        }
        edit.TextChanged += (_, _) =>
        {
            if (refreshing) return;
            Data[group]![p.Key] = edit.Text;
            double? v = BridgeDesignPresentation.ParameterNumber(Data, group, p.Key);
            edit.BorderBrush = Appearance.Outline(BridgeDesignPresentation.ParameterInvalid(v, p) ? "#CD5C5C" : "#DCE2E9");
            if (range is not null && v.HasValue) { sync = true; range.Value = v.Value; sync = false; }
            Changed();
        };
        return panel;
    }
    private void Choice(Panel panel, string key, string label, string[] choices)
    {
        var combo = Ui.Choice(choices, Input.S(key)); combo.SetValue(AutomationProperties.NameProperty, label);
        var box = Ui.Stack(Ui.Text(label, 12), combo); box.Margin = new Thickness(4, 8, 4, 3); panel.Children.Add(box);
        combo.SelectionChanged += (_, _) => { if (!refreshing && combo.SelectedItem is string s) Mutate(() => Input[key] = s, BridgeDesignPresentation.ChoiceRebuilds(key)); };
    }
    private void Toggle(Panel panel, string key, string label)
    {
        var check = new CheckBox { Content = label, IsChecked = Input.B(key), Margin = new Thickness(6, 12, 6, 8) }; panel.Children.Add(check);
        check.Checked += (_, _) => { if (!refreshing) Mutate(() => Input[key] = true); };
        check.Unchecked += (_, _) => { if (!refreshing) Mutate(() => Input[key] = false); };
    }
    private void Changed()
    {
        if (refreshing || disposed) return;
        history.Record(Data);
        QueueCalculation(TimeSpan.FromMilliseconds(450)); Modified?.Invoke();
    }
    private void Mutate(Action change, bool rebuild = false)
    {
        change(); Changed();
        if (rebuild) RefreshInputs();
    }
    private void RefreshInputs()
    {
        int selected = Inputs.SelectedIndex; refreshing = true;
        try
        {
            BuildInputs(); Inputs.SelectedIndex = Math.Max(0, selected);
            foreach (string key in Editors.Keys.Where(k => k.StartsWith("rates/", StringComparison.Ordinal)).ToArray()) Editors.Remove(key);
            prices.Content = BuildPricesPanel();
            foreach (var (key, editor) in Editors) { var parts = key.Split('/'); editor.Text = Data[parts[0]]!.S(parts[1]); }
        }
        finally { refreshing = false; }
    }
    private void QueueCalculation(TimeSpan delay)
    {
        if(disposed)return;
        recalculation.Invalidate(); ClearCalculation(); InvalidateOptimization();
        status.Text=BridgeDesignPresentation.StatusPending;
        status.Foreground=Appearance.Foreground(Ui.Muted);
        scheduledCalculation=CalculateScheduledAsync(delay);
    }
    internal Task CalculateAsync()=>scheduledCalculation=CalculateScheduledAsync(TimeSpan.Zero);
    internal async Task WaitForCalculationAsync()
    {
        Task pending;
        do { pending=scheduledCalculation; await pending; }
        while(!ReferenceEquals(pending,scheduledCalculation));
    }
    private async Task CalculateScheduledAsync(TimeSpan delay)
    {
        if(disposed)return;
        try { Anthea.Serialization.BridgeEnvironmentData.Synchronize(Data); } catch (ArgumentException) { /* The calculation validates unfinished inputs. */ }
        var snapshot=Clone(Data);
        var completed=await recalculation.ScheduleAsync(async token=>
        {
            Busy=true;status.Text=BridgeDesignPresentation.StatusRunning;
            try{return await Task.Run(()=>BridgeDesignAnalysis.Calculate(snapshot,token,calculationService),token);}
            finally{Busy=false;}
        },delay);
        completed.TryPublish(current=>
        {
            try
            {
                if(current.Error is not null){ClearCalculation();status.Text=current.Error.Message;status.Foreground=Appearance.Foreground(Brushes.Firebrick);return;}
                analysis=current.Value!;Calculation=analysis.Current;Drawing.Data=Data;Drawing.Result=Calculation;Drawing.InvalidateVisual();
                var r=Calculation;
                var metrics = BridgeDesignPresentation.Metrics(r);
                for (int k = 0; k < values.Length; k++) { values[k].Text = metrics[k].Value; captions[k].Text = metrics[k].Caption; }
                configuration.Text = BridgeDesignPresentation.Configuration(r);
                ShowQuantities(r); ShowDetails(r); ShowComparison(); ShowAdvice(); ShowTechnical(r); InvalidateOptimization();
                status.Text = BridgeDesignModel.Scope; status.Foreground = Appearance.Foreground(Ui.Muted);
            }
            catch (ArgumentException error)
            {
                ClearCalculation(); status.Text = error.Message;
                status.Foreground = Appearance.Foreground(Brushes.Firebrick);
            }
        });
    }
    private void ClearCalculation()
    {
        analysis=null;Calculation=null;Drawing.Result=null;Drawing.InvalidateVisual();
        foreach(var v in values)v.Text=BridgeDesignPresentation.MetricEmpty;foreach(var c in captions)c.Text=BridgeDesignPresentation.MetricMissing;
        quantities.Content=detail.Content=comparison.Content=advice.Content=technical.Content=null;configuration.Text="";
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;recalculation.Dispose();optimizationCancellation?.Cancel();
    }
    private static DataGrid Table(string[] headers, IEnumerable<string[]> rows, double height = 300)
    {
        var grid = Ui.Table(headers, rows); grid.MaxHeight = height; grid.MinHeight = 90;
        foreach (var column in grid.Columns) column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
        grid.Columns[0].Width = new DataGridLength(2, DataGridLengthUnitType.Star);
        foreach (var column in grid.Columns.OfType<DataGridTextColumn>())
        {
            var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap)); style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(5, 3, 5, 3))); column.ElementStyle = style;
        }
        grid.RowHeight = double.NaN; return grid;
    }
    private void ShowQuantities(BridgeDesignModel.Result r)
    {
        var table = Table([.. BridgeDesignPresentation.QuantityColumns], BridgeDesignPresentation.QuantityRows(r));
        quantities.Content = Ui.Stack(table, Ui.Text(BridgeDesignPresentation.CostSummary(Data, r), 13, true));
    }
    private void ShowDetails(BridgeDesignModel.Result r)
    {
        var panel = Ui.Stack(Ui.Text(BridgeDesignModel.Scope, 12, true), Ui.Text(BridgeDesignPresentation.DetailScope(r), 12, color: Ui.Muted), new BridgeConceptMomentPlot { Result = r, Height = 180 });
        panel.Children.Add(Table([.. BridgeDesignPresentation.DetailColumns], BridgeDesignPresentation.DetailRows(r), 600));
        panel.Children.Add(Table([.. BridgeDesignPresentation.SupportColumns], BridgeDesignPresentation.SupportRows(r), 500));
        foreach (string warning in r.Warnings) panel.Children.Add(Ui.Text(BridgeDesignPresentation.Warning(warning), 12, color: Ui.Brush("#895C17")));
        panel.Children.Add(Ui.Text(BridgeDesignPresentation.Exclusions, 12, color: Ui.Muted));
        detail.Content = Scroll(panel, 365);
    }
    internal void Pin()
    {
        if (Calculation is null) return;
        var a = BridgeDesignPresentation.Pinned(Data); Mutate(() => Data["alternative_a"] = a);
        Outputs.SelectedIndex = 2;
    }
    private void ShowComparison()
    {
        if (Data["alternative_a"] is not JsonObject a || Calculation is null) { comparison.Content = Ui.Text(BridgeDesignPresentation.ComparisonPlaceholder, 14); return; }
        try
        {
            if (analysis?.ComparisonError is { } error) throw new ArgumentException(error);
            var old = analysis?.Comparison ?? throw new ArgumentException(BridgeDesignPresentation.ComparisonUnavailable); var r = Calculation;
            comparison.Content = Ui.Stack(Ui.Text(BridgeDesignPresentation.ComparisonTitle(old, r), 14, true),
                Table([.. BridgeDesignPresentation.ComparisonColumns], BridgeDesignPresentation.ComparisonRows(old, r)),
                Ui.Bar(Ui.Button(BridgeDesignPresentation.RestoreAButton, () => Restore(a)), Ui.Button(BridgeDesignPresentation.RemoveComparisonButton, () => Mutate(() => Data.Remove("alternative_a")))));
        }
        catch (ArgumentException ex) { comparison.Content = Ui.Text(BridgeDesignPresentation.ComparisonError(ex.Message), color: Brushes.Firebrick); }
    }
    private void ShowAdvice()
    {
        var panel = Ui.Stack(Ui.Text(BridgeDesignPresentation.AdviceTitle, 15, true)); panel.Margin = new Thickness(0, 14, 0, 0);
        foreach(var alternative in analysis!.Alternatives)
        {
            var button=Ui.Button(BridgeDesignPresentation.AlternativeCaption(alternative),()=>Mutate(()=>BridgeDesignPresentation.ApplyAlternative(Input,alternative),true));
            button.HorizontalContentAlignment=HorizontalAlignment.Left;panel.Children.Add(button);
        }
        panel.Children.Add(Ui.Text(BridgeDesignPresentation.AdviceNote, 11, color: Ui.Muted));
        if (BridgeDesignPresentation.EnvironmentRestorable(Input)) panel.Children.Add(Ui.Button(BridgeDesignPresentation.RestoreEnvironmentButton, () => Mutate(() => BridgeDesignPresentation.RestoreEnvironment(Input))));
        advice.Content = panel;
    }
    internal void AutoSize() => Mutate(() => BridgeDesignPresentation.AutoSize(Input), true);
    internal void Undo() { refreshing = true; try { if (!history.Undo(Data)) return; } finally { refreshing = false; } RefreshInputs(); QueueCalculation(TimeSpan.FromMilliseconds(450)); Modified?.Invoke(); }
    private void Restore(JsonObject source)
    {
        var copy = Clone(source); var pinned = Data["alternative_a"]?.DeepClone();
        Mutate(() => BridgeDesignPresentation.Restore(Data, copy, pinned), true);
    }
    internal void Randomize() => Randomize(Random.Shared);
    internal void Randomize(Random random) => Mutate(() => BridgeDesignPresentation.Randomize(Data, random), true);
    private void Save(string filter, string name, Action<string> write)
    {
        if (Calculation is null) return;
        var dialog = new SaveFileDialog { Filter = filter, FileName = name };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try { write(dialog.FileName); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(Window.GetWindow(this), ex.Message, "Esportazione non riuscita"); }
    }
    private void ExportCsv() => Save("Quantità CSV|*.csv", "BridgeDesign_quantita.csv", filename => Archivio.ScriviAtomico(filename, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(BridgeDesignModel.Csv(Calculation!))).ToArray()));
    private void ExportPng() => Save("Immagine PNG|*.png", "BridgeDesign.png", filename => Archivio.ScriviAtomico(filename, Drawing.Png()));
    internal byte[] BuildReport(string title) => DocxWriter.Write(BuildReportDocument(title));
    internal DocxDocument BuildReportDocument(string title) => BridgeDesignModel.ReportDocument(title, Data, Calculation ?? throw new InvalidOperationException("Completare i dati del ponte."), Drawing.Png(false), Drawing.Png(true));
    private void Print()
    {
        if (Calculation is null) return;
        var dialog = new PrintDialog(); if (dialog.ShowDialog() != true) return;
        var page = PrintPage(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
        using (Appearance.Document(page)) dialog.PrintVisual(page, "ANTHEA Bridge Design");
    }
    /// <summary>The printed summary of the current result, laid out on the printable area.</summary>
    internal FrameworkElement PrintPage(double width, double height)
    {
        var r = Calculation ?? throw new InvalidOperationException("Completare i dati del ponte.");
        var drawing = new BridgeDesignDrawing { Data = Data, Result = r, Width = 1000, Height = 420 };
        var lines = BridgeDesignPresentation.PrintLines(r);
        var panel = Ui.Stack(Ui.Text(lines[0], 26, true), Ui.Text(lines[1], 14), drawing, Ui.Text(lines[2], 16), Ui.Text(lines[3], 18, true));
        for (int k = 4; k < lines.Count - 1; k++) panel.Children.Add(Ui.Text(lines[k], 14));
        panel.Children.Add(Ui.Text(lines[^1], 12));
        var box = new Viewbox { Child = panel, Width = width, Height = height, Stretch = Stretch.Uniform };
        box.Measure(new Size(box.Width, box.Height)); box.Arrange(new Rect(0, 0, box.Width, box.Height)); return box;
    }
}
