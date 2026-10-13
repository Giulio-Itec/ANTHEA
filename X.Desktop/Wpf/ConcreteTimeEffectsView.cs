using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Anthea.DesignTools.Presentation;
using Microsoft.Win32;

namespace X.Desktop;

/// <summary>Reusable support window. Bindings own input and commands; code below only lays out and projects the chart.</summary>
internal sealed class ConcreteTimeEffectsView : UserControl
{
    private readonly ConcreteTimeEffectsViewModel model;
    private readonly Plot plot = new() { InvertY = false, FollowAppearance = true, FitPadding = 0, FitIncludesMarkers = false };
    internal static Window CreateWindow(DependencyObject owner, double? fckMpa = null)
    {
        var view = new ConcreteTimeEffectsView(new ConcreteTimeEffectsViewModel(fckMpa));
        var window = Ui.Dialog(owner, "ANTHEA · Reologia del calcestruzzo", view, 1320, 880);
        window.MinWidth = 1000; window.MinHeight = 680;
        return window;
    }
    internal ConcreteTimeEffectsView(ConcreteTimeEffectsViewModel model)
    {
        this.model = model; DataContext = model; Background = Appearance.Surface;
        var layout = new Grid { Margin = new Thickness(20) };
        layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new());
        layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = Ui.Stack(Ui.Text("Reologia del calcestruzzo", 23, true), Ui.Text("Viscosità e maturazione · strumento di supporto alla progettazione", 12, color: Ui.Muted));
        heading.Margin = new Thickness(0, 0, 0, 18); layout.Children.Add(heading);
        var body = new Grid(); body.ColumnDefinitions.Add(new() { Width = new GridLength(300) });
        body.ColumnDefinitions.Add(new() { Width = new GridLength(18) }); body.ColumnDefinitions.Add(new());
        Grid.SetRow(body, 1); layout.Children.Add(body);
        var inputs = Ui.Stack(Section("MATERIALE E AMBIENTE"), Field("Resistenza cilindrica fck [MPa]", nameof(model.Fck)),
            Choice("Classe di cemento", nameof(model.Cements), nameof(model.Cement)),
            Field("Umidità relativa RH [%]", nameof(model.Humidity)),
            Field("Dimensione nominale h₀ [mm]", nameof(model.NotionalSize)),
            Ui.Text("h₀ = 2 Ac / u · u è il perimetro esposto all'essiccamento.", 11, color: Ui.Muted),
            Field("Età all'applicazione del carico t₀ [giorni]", nameof(model.LoadingAge)),
            Section("INTERVALLO DI OSSERVAZIONE"), Field("Età iniziale [giorni]", nameof(model.StartAge)),
            Field("Età finale [giorni]", nameof(model.EndAge)), Field("Risoluzione del campionamento", nameof(model.Samples)));
        var calculate = new Button { Content = "Aggiorna le curve", Margin = new Thickness(0, 12, 0, 10), MinHeight = 38 };
        Ui.SetSelected(calculate, true); calculate.SetBinding(Button.CommandProperty, nameof(model.CalculateCommand)); inputs.Children.Add(calculate);
        inputs.Children.Add(Ui.Text(ConcreteTimeEffectsViewModel.Assumptions, 11, color: Ui.Muted));
        body.Children.Add(new ScrollViewer { Content = Ui.Paper(inputs, 16), VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var result = new Grid(); Grid.SetColumn(result, 2); body.Children.Add(result);
        result.RowDefinitions.Add(new() { Height = GridLength.Auto });
        result.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star), MinHeight = 260 });
        result.RowDefinitions.Add(new() { Height = new GridLength(8) });
        result.RowDefinitions.Add(new() { Height = new GridLength(.5, GridUnitType.Star), MinHeight = 150 });
        var cards = new Grid(); cards.ColumnDefinitions.Add(new()); cards.ColumnDefinitions.Add(new());
        var infinity = Card("φ(∞, t₀)", nameof(model.InfinityText), "Limite analitico della viscosità"); cards.Children.Add(infinity);
        var modulus = Card("Ecm,28", nameof(model.E28Text), "Modulo secante a 28 giorni"); Grid.SetColumn(modulus, 1); cards.Children.Add(modulus);
        result.Children.Add(cards);
        var graphCommands = Ui.Bar(Choice("Grandezza", nameof(model.Curves), nameof(model.SelectedCurve), 260),
            Toggle("Asse X log", nameof(model.LogX)), Toggle("Asse Y log", nameof(model.LogY)),
            Ui.Button("Adatta", plot.ResetView), Ui.Button("Esporta grafico…", ExportPlot));
        foreach (var button in graphCommands.Children.OfType<Button>()) button.VerticalAlignment = VerticalAlignment.Center;
        var graph = Ui.Paper(Ui.Dock(plot, graphCommands), 8); graph.Margin = new Thickness(0, 10, 0, 0); Grid.SetRow(graph, 1); result.Children.Add(graph);
        var splitter = new GridSplitter { Height = 6, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center, Background = Appearance.Outline("#D8E0EB"), ResizeDirection = GridResizeDirection.Rows };
        Grid.SetRow(splitter, 2); result.Children.Add(splitter);
        var tabs = new TabControl { Background = Appearance.Paper }; Grid.SetRow(tabs, 3); result.Children.Add(tabs);
        var query = new Grid { Margin = new Thickness(14) };
        query.ColumnDefinitions.Add(new() { Width = new GridLength(155) }); query.ColumnDefinitions.Add(new()); query.ColumnDefinitions.Add(new()); query.ColumnDefinitions.Add(new());
        query.RowDefinitions.Add(new() { Height = GridLength.Auto }); query.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var queryField = Field("Leggi all'età t [giorni]", nameof(model.QueryAge)); query.Children.Add(queryField);
        var phi = Card("φ(t, t₀)", nameof(model.QueryPhiText), "Coefficiente di viscosità"); Grid.SetColumn(phi, 1); query.Children.Add(phi);
        var e = Card("Ecm(t) [MPa]", nameof(model.QueryEText), "Modulo per maturazione"); Grid.SetColumn(e, 2); query.Children.Add(e);
        var factor = Card("Ecm(t) / Ecm,28", nameof(model.QueryFactorText), "Fattore adimensionale"); Grid.SetColumn(factor, 3); query.Children.Add(factor);
        var note = Ui.Stack(BoundText(nameof(model.QueryError), 12), BoundText(nameof(model.ReferenceText), 11));
        note.Margin = new Thickness(0, 12, 0, 0); Grid.SetRow(note, 1); Grid.SetColumnSpan(note, 4); query.Children.Add(note);
        Ui.Tab(tabs, "Lettura puntuale", new ScrollViewer { Content = query, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var table = new DataGrid { IsReadOnly = true, AutoGenerateColumns = false, CanUserAddRows = false, EnableRowVirtualization = true, Margin = new Thickness(4), SelectionMode = DataGridSelectionMode.Single };
        table.SetBinding(ItemsControl.ItemsSourceProperty, nameof(model.Points));
        table.SetBinding(DataGrid.SelectedItemProperty, new Binding(nameof(model.SelectedPoint)) { Mode = BindingMode.TwoWay });
        foreach (var (title, property, format) in new[] { ("Età t [giorni]", "AgeDays", "0.###"), ("φ(t, t₀)", "CreepCoefficient", "0.0000"), ("Ecm(t) [MPa]", "EcmMpa", "N1"), ("Ecm(t) / Ecm,28", "ModulusFactor", "0.0000") })
            table.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(property) { StringFormat = format, ConverterCulture = CultureInfo.GetCultureInfo("it-IT") }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        Ui.Tab(tabs, "Tabella · seleziona un'età", table);
        var status = BoundText(nameof(model.Status), 12); status.Margin = new Thickness(0, 14, 0, 0); Grid.SetRow(status, 2); layout.Children.Add(status);
        Content = layout;
        model.PropertyChanged += OnModelChanged;
        Loaded += (_, _) => { Appearance.Changed += RefreshPlot; RefreshPlot(); };
        Unloaded += (_, _) => { Appearance.Changed -= RefreshPlot; };
        RefreshPlot();
    }
    private static TextBlock Section(string text) { var label = Ui.Text(text, 11, true, Ui.Muted); label.Margin = new Thickness(0, 12, 0, 7); return label; }
    private static TextBlock BoundText(string property, double size, bool bold = false)
    { var text = Ui.Text("", size, bold); text.SetBinding(TextBlock.TextProperty, property); return text; }
    private static FrameworkElement Field(string label, string property)
    {
        var box = new TextBox { Margin = new Thickness(0, 5, 0, 8), MinHeight = 30 };
        box.SetBinding(TextBox.TextProperty, new Binding(property) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, ValidatesOnDataErrors = true });
        return Ui.Stack(Ui.Text(label, 12), box);
    }
    private static FrameworkElement Choice(string label, string items, string selected, double width = double.NaN)
    {
        var choice = new ComboBox { DisplayMemberPath = "Label", MinHeight = 30, Margin = new Thickness(0, 5, 8, 8), Width = width };
        choice.SetBinding(ItemsControl.ItemsSourceProperty, items); choice.SetBinding(ComboBox.SelectedItemProperty, new Binding(selected) { Mode = BindingMode.TwoWay });
        return Ui.Stack(Ui.Text(label, 12), choice);
    }
    private static CheckBox Toggle(string title, string property)
    {
        var box = new CheckBox { Content = title, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 12, 8, 0) };
        box.SetBinding(CheckBox.IsCheckedProperty, new Binding(property) { Mode = BindingMode.TwoWay }); return box;
    }
    private static FrameworkElement Card(string label, string property, string caption)
    {
        var body = Ui.Stack(Ui.Text(label, 12, true), BoundText(property, 23, true), Ui.Text(caption, 11, color: Ui.Muted));
        var card = Ui.Paper(body, 12); card.Margin = new Thickness(0, 0, 8, 0); return card;
    }
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(model.Points) or nameof(model.SelectedCurve) or nameof(model.LogX) or nameof(model.LogY) or nameof(model.QueryPoint)) RefreshPlot();
    }
    private void RefreshPlot()
    {
        var type = model.SelectedCurve.Value;
        double Value(ConcreteTimePointView p) => type switch { ConcreteTimeCurve.Modulus => p.EcmMpa, ConcreteTimeCurve.ModulusFactor => p.ModulusFactor, _ => p.CreepCoefficient };
        double X(double value) => model.LogX ? Math.Log10(value) : value;
        double Y(double value) => model.LogY ? Math.Log10(value) : value;
        bool Visible(double value) => !model.LogY || value > 0;
        plot.Title = model.SelectedCurve.Label; plot.XLabel = "Età t dal getto [giorni]" + (model.LogX ? " · scala logaritmica" : "");
        plot.YLabel = type switch { ConcreteTimeCurve.Modulus => "Ecm(t) [MPa]", ConcreteTimeCurve.ModulusFactor => "Ecm(t) / Ecm,28 [−]", _ => "φ(t, t₀) [−]" };
        if (model.LogY) plot.YLabel += " · scala logaritmica";
        plot.Note = model.GraphNote;
        string Tick(double v) => Math.Pow(10, v).ToString(Math.Abs(v) > 5 ? "0.##E+0" : "0.###", CultureInfo.GetCultureInfo("it-IT"));
        plot.XMinimum = model.LogX && model.Points.Count > 0 ? Math.Floor(Math.Log10(model.Points[0].AgeDays)) : null;
        plot.XMaximum = model.LogX && model.Points.Count > 0 ? Math.Ceiling(Math.Log10(model.Points[^1].AgeDays)) : null;
        plot.XTickStep = model.LogX ? 1 : null;
        var italian = CultureInfo.GetCultureInfo("it-IT");
        plot.XTickFormat = model.LogX ? Tick : v => v.ToString("0.###", italian);
        plot.YTickFormat = v => (model.LogY ? Math.Pow(10, v) : v).ToString(type == ConcreteTimeCurve.Modulus ? "N0" : "0.###", italian);
        plot.Series = [new(model.SelectedCurve.Label, model.Points.Where(p => Visible(Value(p))).Select(p => new[] { X(p.AgeDays), Y(Value(p)) }).ToList(), Appearance.Series(Ui.Blue), Highlighted: true)];
        if (type == ConcreteTimeCurve.Creep && model.CreepInfinity is double limit && model.Points.Count > 0 && Visible(limit))
            plot.Series.Add(new("φ∞ · limite analitico", [ [X(model.Points[0].AgeDays), Y(limit)], [X(model.Points[^1].AgeDays), Y(limit)] ], Appearance.Series(Ui.Brush("#B45309")), Dashed: true));
        plot.Markers = model.QueryPoint is { } point && Visible(Value(point))
            ? [new(X(point.AgeDays), Y(Value(point)), "Lettura", Appearance.Series(Ui.Brush("#C02672")))] : [];
        plot.EmptyMessage = "Aggiorna le curve con dati validi."; plot.ResetView();
    }
    private void ExportPlot()
    {
        if (!model.HasResults) return;
        var dialog = new SaveFileDialog { Title = "Esporta grafico", Filter = "Immagine PNG|*.png", FileName = "Reologia-calcestruzzo.png" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try { File.WriteAllBytes(dialog.FileName, plot.Png(1500, 950)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { MessageBox.Show(Window.GetWindow(this), ex.Message, "Esportazione non completata", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
