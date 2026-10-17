using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.VerticalPile;

namespace X.Desktop.Views.VerticalPile;

/// <summary>
/// View of <see cref="VerticalPileSheetViewModel"/> (F5.6). The code-behind does only what decision Q3 allows, the technical drawings:
/// <list type="bullet">
/// <item>the capacity chart: the historic <see cref="Plot"/> (Capacity) receives <see cref="VerticalPileSheetViewModel.CapacityChart"/> at every
/// change (title, axes, depth, message, visible series) and fits the view when a result is shown, as ShowGeoResults; its menu «Adatta» and
/// «Salva PNG…» (the image of the chart, written by the ViewModel through the file dialog service);</item>
/// <item>the reference chart (Nq or the abacus): <see cref="PlotPresentation.Apply"/> of <see cref="VerticalPileSheetViewModel.ReferenceChart"/>;</item>
/// <item>the profile: the historic <see cref="StratigraphyDrawing"/> on the data of the sheet, the selected stratigraphy, every stratigraphy
/// with the profile extended (UpdateProfile).</item>
/// </list>
/// No calculation and no write of the document.
/// </summary>
public partial class VerticalPileSheetView : UserControl
{
    private VerticalPileSheetViewModel? sheet;

    /// <summary>The capacity chart, as the historic view creates it.</summary>
    internal Plot CapacityPlot { get; } = new() { Capacity = true };

    /// <summary>The reference chart, as the historic view creates it.</summary>
    internal Plot ReferencePlot { get; } = new() { InvertY = false };

    public VerticalPileSheetView()
    {
        InitializeComponent();
        CapacityHost.Content = CapacityPlot;
        ReferenceHost.Content = ReferencePlot;
        DataContextChanged += (_, _) => Attach(DataContext as VerticalPileSheetViewModel);
        Unloaded += (_, _) => Attach(null);
        Loaded += (_, _) => Attach(DataContext as VerticalPileSheetViewModel);
    }

    private void Attach(VerticalPileSheetViewModel? next)
    {
        if (ReferenceEquals(sheet, next)) return;
        if (sheet is not null) sheet.PropertyChanged -= OnSheetChanged;
        sheet = next;
        if (sheet is null) return;
        sheet.PropertyChanged += OnSheetChanged;
        ProfileDrawing.Data = sheet.Data;
        ProfileDrawing.Micro = sheet.IsMicro;
        DrawCapacity();
        DrawReference();
        DrawProfile();
    }

    private void OnSheetChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(VerticalPileSheetViewModel.CapacityChart): DrawCapacity(); break;
            case nameof(VerticalPileSheetViewModel.Curves): if (sheet?.Curves.Count > 0) CapacityPlot.ResetView(); break;
            case nameof(VerticalPileSheetViewModel.ReferenceChart): DrawReference(); break;
        }
        // The profile follows the data, the selection and the extended card, as UpdateProfile at every preview and layout.
        DrawProfile();
    }

    private void DrawCapacity()
    {
        if (sheet?.CapacityChart is not { } chart) return;
        CapacityPlot.Title = chart.Title; CapacityPlot.XLabel = chart.XLabel; CapacityPlot.YLabel = chart.YLabel;
        CapacityPlot.CapacityDepth = chart.CapacityDepth; CapacityPlot.EmptyMessage = chart.EmptyMessage;
        CapacityPlot.Series = chart.Series.Select(PlotPresentation.Series).ToList();
        CapacityPlot.InvalidateVisual();
    }

    private void DrawReference()
    {
        if (sheet?.ReferenceChart is { } chart) PlotPresentation.Apply(ReferencePlot, chart);
    }

    private void DrawProfile()
    {
        if (sheet is null) return;
        ProfileDrawing.SelectedIndex = sheet.SelectedSurveyIndex;
        ProfileDrawing.ShowAll = sheet.ExpandedCard == VerticalPileSheetViewModel.ProfileCard;
        ProfileDrawing.InvalidateVisual();
    }

    private void Fit_Click(object sender, RoutedEventArgs e) => CapacityPlot.ResetView();

    private void SavePng_Click(object sender, RoutedEventArgs e) => sheet?.SaveCapacityImage(() => CapacityPlot.Png());
}
