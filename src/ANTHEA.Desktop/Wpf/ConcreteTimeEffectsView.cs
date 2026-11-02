using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.Rheology;
using Microsoft.Win32;

namespace X.Desktop;

/// <summary>Saved sheet view; XAML owns the layout and bindings. Code below only projects the existing technical chart.</summary>
internal sealed partial class ConcreteTimeEffectsView : UserControl
{
    private ConcreteTimeEffectsViewModel? model;
    private readonly Plot plot = new() { InvertY = false, FollowAppearance = true, FitPadding = 0, FitIncludesMarkers = false };
    internal static Window CreateWindow(DependencyObject owner, double? fckMpa = null)
    {
        var view = new ConcreteTimeEffectsView(new ConcreteTimeEffectsViewModel(fckMpa));
        var window = Ui.Dialog(owner, "ANTHEA · Reologia del calcestruzzo", view, 1320, 880);
        window.MinWidth = 1000; window.MinHeight = 680;
        return window;
    }
    public ConcreteTimeEffectsView()
    {
        InitializeComponent(); PlotHost.Content = plot;
        DataContextChanged += (_, _) => AttachModel();
        Loaded += (_, _) => { AttachModel(); Appearance.Changed += RefreshPlot; RefreshPlot(); };
        Unloaded += (_, _) => { Appearance.Changed -= RefreshPlot; if (model is not null) model.PropertyChanged -= OnModelChanged; };
    }
    internal ConcreteTimeEffectsView(ConcreteTimeEffectsViewModel model) : this() => DataContext = model;
    private void AttachModel()
    {
        if (model is not null) model.PropertyChanged -= OnModelChanged;
        model = DataContext as ConcreteTimeEffectsViewModel;
        if (model is not null) model.PropertyChanged += OnModelChanged;
        RefreshPlot();
    }
    private void OnFit(object sender, RoutedEventArgs e) => plot.ResetView();
    private void OnExport(object sender, RoutedEventArgs e) => ExportPlot();
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(model.Points) or nameof(model.SelectedCurve) or nameof(model.LogX) or nameof(model.LogY) or nameof(model.QueryPoint)) RefreshPlot();
    }
    private void RefreshPlot()
    {
        if (model is null) return;
        var type = model.SelectedCurve.Value;
        double Value(ConcreteTimePointView p) => type switch { ConcreteTimeCurve.Modulus => p.EcmMpa, ConcreteTimeCurve.ModulusFactor => p.ModulusFactor, _ => p.CreepCoefficient };
        double X(double value) => model.LogX ? Math.Log10(value) : value;
        double Y(double value) => model.LogY ? Math.Log10(value) : value;
        bool Visible(double value) => !model.LogY || value > 0;
        plot.Title = model.SelectedCurve.Label; plot.XLabel = "Età t dal getto [giorni]" + (model.LogX ? " · scala logaritmica" : "");
        plot.YLabel = type switch { ConcreteTimeCurve.Modulus => "Ecm(t) [MPa]", ConcreteTimeCurve.ModulusFactor => "Ecm(t) / Ecm,28 [−]", _ => "φ(t, t₀) [−]" };
        if (model.LogY) plot.YLabel += " · scala logaritmica";
        plot.Note = model.GraphNote;
        // The decimal separator chosen in «Aspetto», as the values of the sheet; no thousands separator (review of 2/11/2026).
        var culture = CultureInfo.CurrentCulture;
        string Tick(double v) => Math.Pow(10, v).ToString(Math.Abs(v) > 5 ? "0.##E+0" : "0.###", culture);
        plot.XMinimum = model.LogX && model.Points.Count > 0 ? Math.Floor(Math.Log10(model.Points[0].AgeDays)) : null;
        plot.XMaximum = model.LogX && model.Points.Count > 0 ? Math.Ceiling(Math.Log10(model.Points[^1].AgeDays)) : null;
        plot.XTickStep = model.LogX ? 1 : null;
        plot.XTickFormat = model.LogX ? Tick : v => v.ToString("0.###", culture);
        plot.YTickFormat = v => (model.LogY ? Math.Pow(10, v) : v).ToString(type == ConcreteTimeCurve.Modulus ? "0" : "0.###", culture);
        plot.Series = [new(model.SelectedCurve.Label, model.Points.Where(p => Visible(Value(p))).Select(p => new[] { X(p.AgeDays), Y(Value(p)) }).ToList(), Appearance.Series(Ui.Blue), Highlighted: true)];
        if (type == ConcreteTimeCurve.Creep && model.CreepInfinity is double limit && model.Points.Count > 0 && Visible(limit))
            plot.Series.Add(new("φ∞ · limite analitico", [ [X(model.Points[0].AgeDays), Y(limit)], [X(model.Points[^1].AgeDays), Y(limit)] ], Appearance.Series(Ui.Brush("#B45309")), Dashed: true));
        plot.Markers = model.QueryPoint is { } point && Visible(Value(point))
            ? [new(X(point.AgeDays), Y(Value(point)), "Lettura", Appearance.Series(Ui.Brush("#C02672")))] : [];
        plot.EmptyMessage = "Aggiorna le curve con dati validi."; plot.ResetView();
    }
    private void ExportPlot()
    {
        if (model?.HasResults != true) return;
        var dialog = new SaveFileDialog { Title = "Esporta grafico", Filter = "Immagine PNG|*.png", FileName = "Reologia-calcestruzzo.png" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try { File.WriteAllBytes(dialog.FileName, plot.Png(1500, 950)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { MessageBox.Show(Window.GetWindow(this), ex.Message, "Esportazione non completata", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
