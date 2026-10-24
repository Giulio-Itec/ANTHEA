using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.HorizontalPile.Elastic;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>
/// View of <see cref="ElasticPileViewModel"/> (F5.8). The code-behind does only what decision Q3 allows and what is state of the view:
/// <list type="bullet">
/// <item>the technical drawings: the historic <see cref="ElasticPileDrawing"/> (1750 × 650) on <see cref="ElasticPileViewModel.DrawingSource"/> with the
/// options of the data and the selected point of the ViewModel (a click gives the depth to <see cref="ElasticPileViewModel.SelectDepth"/>), the
/// <see cref="PileReinforcementDrawing"/> of <see cref="ElasticPileViewModel.SheetResult"/> and the <see cref="SectionDrawing"/> (280 × 280) of
/// <see cref="ElasticPileViewModel.Section"/>;</item>
/// <item>the dialogs of the view, modal as the historic ones: «Info» and «Mostra confronto completo» (a text, 900 × 650) and «Apri tavola armature…»
/// (<see cref="ElasticSheetView"/>, 1250 × 900; without a result the status in a text).</item>
/// </list>
/// No calculation and no write of the document.
/// </summary>
public partial class ElasticPileView : UserControl
{
    private ElasticPileViewModel? sheet;

    /// <summary>The drawing of the response, as the historic view creates it.</summary>
    internal ElasticPileDrawing Drawing { get; } = new() { Height = 650, MinWidth = 1750, SelectsOnClick = false };

    /// <summary>The sheet of the reinforcement under «Tavola armature e distinta ferri».</summary>
    internal PileReinforcementDrawing SheetDrawing { get; } = new() { HorizontalAlignment = HorizontalAlignment.Left };

    /// <summary>The section at the selected depth.</summary>
    internal SectionDrawing SectionDrawing { get; } = new() { Width = 280, Height = 280 };

    /// <summary>Shows a window of the view (the dialogs): modal, as the historic ones; the checks show it off screen without waiting.</summary>
    internal Func<Window, bool?> ShowWindow { get; set; } = window => window.ShowDialog();

    public ElasticPileView()
    {
        InitializeComponent();
        DrawingHost.Content = Drawing; SheetHost.Content = SheetDrawing; SectionHost.Content = SectionDrawing;
        Drawing.DepthClicked += depth => sheet?.SelectDepth(depth);
        DataContextChanged += (_, _) => Attach(DataContext as ElasticPileViewModel);
        // The drawings follow the ViewModel also while the tab of the capacity is shown (the tab is unloaded), as the historic tab: the view and its
        // ViewModel live and end with the sheet.
        Loaded += (_, _) => Attach(DataContext as ElasticPileViewModel);
    }

    private void Attach(ElasticPileViewModel? next)
    {
        if (ReferenceEquals(sheet, next)) return;
        if (sheet is not null) sheet.PropertyChanged -= OnSheetChanged;
        sheet = next;
        if (sheet is null) return;
        sheet.PropertyChanged += OnSheetChanged;
        Drawing.Options = sheet.DrawingOptions;
        Drawing.Set(sheet.DrawingSource);
        Drawing.Selection = sheet.SelectedPoint;
        SheetDrawing.Set(sheet.SheetResult, sheet.SheetUnavailableReason);
        DrawSection();
    }

    private void OnSheetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sheet is null) return;
        switch (e.PropertyName)
        {
            // ElasticPileDrawing.Set as the historic view calls it: the selection kept or cleared with the scale it retains.
            case nameof(ElasticPileViewModel.DrawingSource): Drawing.Set(sheet.DrawingSource, sheet.DrawingKeepsSelection); Drawing.Selection = sheet.SelectedPoint; break;
            case nameof(ElasticPileViewModel.SelectedPoint): Drawing.Selection = sheet.SelectedPoint; break;
            case nameof(ElasticPileViewModel.DrawingOptions): Drawing.Options = sheet.DrawingOptions; Drawing.InvalidateVisual(); break;
            case nameof(ElasticPileViewModel.SheetResult): SheetDrawing.Set(sheet.SheetResult, sheet.SheetUnavailableReason); break;
            case nameof(ElasticPileViewModel.Section): DrawSection(); break;
        }
    }

    private void DrawSection()
    {
        if (sheet is null) return;
        var shown = sheet.Section;
        SectionDrawing.Outline = [.. shown.Outline]; SectionDrawing.Bars = [.. shown.Bars]; SectionDrawing.CircularLinkRadius = shown.LinkRadius;
        SectionDrawing.LinkDiameter = shown.LinkDiameter; SectionDrawing.LinkCaption = shown.LinkCaption; SectionDrawing.ShowAxes = shown.ShowAxes; SectionDrawing.Caption = shown.Caption;
        SectionDrawing.InvalidateVisual();
    }

    /// <summary>A text in a dialog of the view, as Info of the historic view (read-only, wrapping, 14 points, 900 × 650).</summary>
    internal void ShowText(string title, string text) => ShowWindow(Ui.Dialog(this, title, new ElasticTextView { Text = text }, 900, 650));

    private void Info_Click(object sender, RoutedEventArgs e) => ShowText("Info · risposta elastica del palo", sheet?.Info ?? "");

    private void Comparison_Click(object sender, RoutedEventArgs e) => ShowText("Confronto completo", sheet?.MigrationComparison ?? "");

    /// <summary>«Apri tavola armature…»: the sheet of the result with zoom and «Esporta PNG…»; without a result the status, as the historic view.</summary>
    private void Sheet_Click(object sender, RoutedEventArgs e)
    {
        if (sheet is null) return;
        if (sheet.Result is not { } result) { ShowText("Tavola armature", sheet.Status); return; }
        ShowWindow(Ui.Dialog(this, ElasticSheetView.Title, new ElasticSheetView(sheet, result), 1250, 900));
    }
}
