using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.HorizontalPile;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>
/// View of <see cref="HorizontalPileSheetViewModel"/> (F5.7). The code-behind does only what decision Q3 allows and what is state of the view:
/// <list type="bullet">
/// <item>the technical drawings: the historic <see cref="StratigraphyDrawing"/> with the forces on the data of the sheet, the selected
/// stratigraphy, every stratigraphy with the profile extended and the result of the capacity (UpdateVerification and LayoutCards of the
/// historic view); the c.a. section (<see cref="SectionDrawing"/>, 400 × 400 in a Viewbox) from <see cref="HorizontalPileSheetViewModel.SectionDrawing"/>,
/// or the CHS (<see cref="ChsDrawing"/>) from <see cref="HorizontalPileSheetViewModel.ChsProperties"/>;</item>
/// <item>the scrolling to the top when a card is extended or reduced, as the historic «Estendi»;</item>
/// <item>the dialogs of the view: «Diagrammi e dettagli» (<see cref="HorizontalPileResultsView"/>, «Palo orizzontale · risultati», 1280 × 900) and
/// the details of the soil editor of a layer (<see cref="HorizontalSoilDetailsView"/>, «Parametro dello strato · origine e applicabilità»,
/// 1080 × 760), modal as the historic ones.</item>
/// </list>
/// No calculation and no write of the document.
/// </summary>
public partial class HorizontalPileSheetView : UserControl
{
    private HorizontalPileSheetViewModel? sheet;

    /// <summary>The profile with the forces, as the historic view creates it.</summary>
    internal StratigraphyDrawing ProfileDrawing { get; } = new() { ShowAll = false, HorizontalForces = true };

    /// <summary>The c.a. section of the pile, 400 × 400 as the historic view.</summary>
    internal SectionDrawing ConcreteDrawing { get; } = new() { Width = 400, Height = 400 };

    /// <summary>The CHS of the micropile, 400 × 400 as the historic view.</summary>
    internal ChsDrawing ChsDrawing { get; } = new() { Width = 400, Height = 400 };

    /// <summary>Shows a window of the view (the dialogs): modal, as the historic ones; the checks show it off screen without waiting.</summary>
    internal Func<Window, bool?> ShowWindow { get; set; } = window => window.ShowDialog();

    public HorizontalPileSheetView()
    {
        InitializeComponent();
        ProfileHost.Content = ProfileDrawing;
        DataContextChanged += (_, _) => Attach(DataContext as HorizontalPileSheetViewModel);
        Loaded += (_, _) => Attach(DataContext as HorizontalPileSheetViewModel);
        Unloaded += (_, _) => Attach(null);
    }

    private void Attach(HorizontalPileSheetViewModel? next)
    {
        if (ReferenceEquals(sheet, next)) return;
        if (sheet is not null) sheet.PropertyChanged -= OnSheetChanged;
        sheet = next;
        if (sheet is null) return;
        sheet.PropertyChanged += OnSheetChanged;
        ProfileDrawing.Data = sheet.Data;
        SectionPreview.Child = sheet.IsMicro ? ChsDrawing : ConcreteDrawing;
        DrawSection();
        DrawProfile();
    }

    private void OnSheetChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(HorizontalPileSheetViewModel.SectionDrawing) or nameof(HorizontalPileSheetViewModel.ChsProperties): DrawSection(); break;
            case nameof(HorizontalPileSheetViewModel.ExpandedCard): Scroll.ScrollToTop(); break;
        }
        // The profile follows the data, the selection, the result and the extended card, as Preview, UpdateVerification and LayoutCards.
        DrawProfile();
    }

    private void DrawProfile()
    {
        if (sheet is null) return;
        ProfileDrawing.SelectedIndex = sheet.SelectedSurveyIndex;
        ProfileDrawing.HorizontalResult = sheet.Result;
        ProfileDrawing.ShowAll = sheet.ExpandedCard == HorizontalPileSheetViewModel.ProfileCard;
        ProfileDrawing.InvalidateVisual();
    }

    private void DrawSection()
    {
        if (sheet is null) return;
        if (sheet.IsMicro) { ChsDrawing.Properties = sheet.ChsProperties; ChsDrawing.InvalidateVisual(); return; }
        var shown = sheet.SectionDrawing;
        ConcreteDrawing.Outline = [.. shown.Outline]; ConcreteDrawing.Bars = [.. shown.Bars];
        ConcreteDrawing.CircularLinkRadius = shown.LinkRadius; ConcreteDrawing.LinkDiameter = shown.LinkDiameter;
        ConcreteDrawing.ShowAxes = shown.ShowAxes; ConcreteDrawing.Caption = shown.Caption;
        ConcreteDrawing.InvalidateVisual();
    }

    /// <summary>«Diagrammi e dettagli»: the diagrams and the tables of every survey of the result, the references and the JSON.</summary>
    private void Details_Click(object sender, RoutedEventArgs e)
    {
        if (sheet is not { CanShowResults: true } shown) return;
        ShowWindow(Ui.Dialog(this, HorizontalPileResultsView.Title, new HorizontalPileResultsView(shown), 1280, 900));
    }

    /// <summary>«ⓘ Tabelle» of the soil editor of a layer: its details (source, reason of the override, mean) on the editor of the row.</summary>
    private void SoilDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: HorizontalSoilRowViewModel soil }) return;
        ShowWindow(Ui.Dialog(this, HorizontalSoilDetailsView.Title, new HorizontalSoilDetailsView { DataContext = soil }, 1080, 760));
    }
}
