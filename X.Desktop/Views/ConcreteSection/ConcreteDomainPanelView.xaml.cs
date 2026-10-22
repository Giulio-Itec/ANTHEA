using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.Application.Concrete;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.ConcreteSection;
using X.Core;

namespace X.Desktop.Views.ConcreteSection;

/// <summary>
/// View of a <see cref="ConcreteDomainPanelViewModel"/> (tabs 02 and 03 of the RC section, F5.10 view after S9). The code-behind does only what decision
/// Q3 allows: the drawings of the historic view fed by the data of the panel (<see cref="DomainViewport3D"/> from
/// <see cref="ConcreteDomainPanelViewModel.View3D"/> or <see cref="Plot"/> from <see cref="ConcreteDomainPanelViewModel.Chart"/>; the map σ/ε and the strain
/// plane from <see cref="ConcreteDomainPanelViewModel.Inspection"/>), the camera and the zoom (FitRequested, ZoomRequested, the reset of the chart, the
/// point clicked), «PNG» and «Espandi», the texts that depend on 3D or 2D. No calculation and no write of the document.
/// </summary>
public partial class ConcreteDomainPanelView : UserControl
{
    /// <summary>The legend under the options (UtilizationPalette.Legend of the historic view and the grey of the missing outcomes).</summary>
    public const string LegendNote = UtilizationColors.Legend + "\nGrigio: esito incompleto o non disponibile";

    /// <summary>The notice of the options of the 3D panel.</summary>
    public const string Notice3D = "Domini plastico/elastico e punti resistenti calcolati da GPC Engine. L’interpolazione modifica la mesh visualizzata. Gli assi scelti si riferiscono alle azioni; il grafico è negli assi locali della sezione.";

    /// <summary>The notice of the options of the 2D panel.</summary>
    public const string Notice2D = "GPC Engine: N–M diretto nelle direzioni locali principali, altrimenti sezione del dominio tramite GPC Engine. Con proiezione attiva si verifica l’azione proiettata, non l’intera azione 3D.";

    private ConcreteDomainPanelViewModel? panel;
    private DomainViewport3D? view3D;
    private Plot? plot;
    private ConcreteResults.Mesh? shownMesh;

    /// <summary>The section σ/ε of the tab «Sezione σ / ε».</summary>
    internal ConcreteSectionViewport Map { get; } = new();

    /// <summary>The strain plane of the tab «Piano ε».</summary>
    internal ConcreteStrainPlaneView Plane { get; } = new();

    /// <summary>The drawing of the domain: the 3D viewport or the 2D chart.</summary>
    internal FrameworkElement? Drawing => (FrameworkElement?)view3D ?? plot;

    public ConcreteDomainPanelView()
    {
        InitializeComponent();
        MapHost.Content = Map; PlaneHost.Content = Plane;
        DataContextChanged += (_, _) => Attach(DataContext as ConcreteDomainPanelViewModel);
    }

    private void Attach(ConcreteDomainPanelViewModel? next)
    {
        if (ReferenceEquals(panel, next)) return;
        Detach();
        panel = next;
        if (panel is null) return;
        FrameTitle.Text = panel.ThreeD ? "Dominio N–Mx–My" : "Sezione del dominio";
        NoticeText.Text = panel.ThreeD ? Notice3D : Notice2D;
        TableSubtitle.Text = panel.Table.Unified ? "Sollecitazioni SLU / SLV condivise anche con taglio e torsione" : "Le azioni Plastico / Elastico sono condivise fra le schede 3D e 2D";
        if (panel.ThreeD)
        {
            view3D = new DomainViewport3D { ToolTip = UtilizationColors.Legend };
            view3D.ActionSelected += OnActionSelected;
            panel.View3D!.Changed += OnDrawingChanged;
        }
        else
        {
            plot = ConcreteSectionDrawings.DomainPlot();
            plot.ViewReset += OnChartReset;
            plot.MouseWheel += OnChartWheel;
            panel.Chart!.Changed += OnDrawingChanged;
        }
        DrawingHost.Content = Drawing;
        panel.FitRequested += OnFitRequested; panel.ZoomRequested += OnZoomRequested;
        panel.Inspection.Map.Changed += OnMapChanged; panel.Inspection.Map.PropertyChanged += OnMapChanged; panel.Inspection.PropertyChanged += OnInspectionChanged;
        Draw(); DrawMap(); DrawPlane();
    }

    private void Detach()
    {
        if (panel is null) return;
        if (panel.View3D is { } data) data.Changed -= OnDrawingChanged;
        if (panel.Chart is { } chart) chart.Changed -= OnDrawingChanged;
        panel.FitRequested -= OnFitRequested; panel.ZoomRequested -= OnZoomRequested;
        panel.Inspection.Map.Changed -= OnMapChanged; panel.Inspection.Map.PropertyChanged -= OnMapChanged; panel.Inspection.PropertyChanged -= OnInspectionChanged;
        if (view3D is not null) view3D.ActionSelected -= OnActionSelected;
        if (plot is not null) { plot.ViewReset -= OnChartReset; plot.MouseWheel -= OnChartWheel; }
        DrawingHost.Content = null; view3D = null; plot = null; shownMesh = null; panel = null;
    }

    private void OnDrawingChanged(object? sender, EventArgs e) => Draw();

    private void Draw()
    {
        if (panel is null) return;
        if (view3D is not null) ConcreteSectionDrawings.Apply(view3D, panel.View3D!, ref shownMesh);
        else if (plot is not null) ConcreteSectionDrawings.Apply(plot, panel.Chart!);
    }

    private void OnMapChanged(object? sender, EventArgs e) => DrawMap();

    private void DrawMap() { if (panel is not null) ConcreteSectionDrawings.Apply(Map, panel.Inspection.Map); }

    private void OnInspectionChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(ConcreteSectionInspectionViewModel.Plane)) DrawPlane(); }

    private void DrawPlane() { if (panel is null) return; Plane.Diagram = panel.Inspection.Plane; Plane.InvalidateVisual(); }

    // The point of a row clicked in the 3D domain: the panel selects the row.
    private void OnActionSelected(string id) => panel?.View3D?.Pick(id);

    // «Adatta» and «Scala…» of the panel: the camera or the chart fitted (the reset of the chart writes the factors x and y again, as the historic one).
    private void OnFitRequested(object? sender, EventArgs e)
    {
        if (view3D is not null) view3D.FitView();
        else plot?.ResetView();
    }

    private void OnZoomRequested(object? sender, EventArgs e)
    {
        if (panel is null) return;
        if (view3D is not null) { view3D.FitIncludesActions = panel.View3D!.FitIncludesActions; view3D.FitView(); view3D.Zoom = panel.Zoom; }
        else if (plot is not null) plot.Zoom = panel.Zoom;
    }

    private void OnChartReset() => panel?.ChartViewReset();

    // The zoom of the chart with the wheel stays with the panel (its figure of the report and «Scala…» read it).
    private void OnChartWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) { if (panel is not null && plot is not null) panel.Zoom = plot.Zoom; }

    private void ViewChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (view3D is not null && ViewChoice.SelectedItem is ComboBoxItem { Content: string axis }) view3D.StandardView(axis);
    }

    // ---------------------------------------------------------------- «PNG», «Espandi», «Adatta» of the frames (ViewportFrame of the historic view)

    private ConcreteSectionSheetView? Sheet
    {
        get
        {
            for (DependencyObject? node = this; node is not null; node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node))
                if (node is ConcreteSectionSheetView sheet) return sheet;
            return null;
        }
    }

    private void SavePng(FrameworkElement drawing, string title)
    {
        var choose = Sheet?.ChooseImagePath ?? ConcreteSectionSheetView.ChooseImage;
        if (choose("ANTHEA_" + title.Replace(' ', '_') + ".png") is not { } path) return;
        try { Archivio.ScriviAtomico(path, drawing is DrawingView view ? view.Png() : Ui.DocumentSnapshot(drawing)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { MessageBox.Show(Window.GetWindow(this), error.Message, "Esportazione immagine"); }
    }

    private void Expand(ContentControl host, FrameworkElement drawing, string title)
    {
        host.Content = null;
        var window = Ui.Dialog(this, title, drawing, 1200, 800);
        window.Closed += (_, _) => { window.Content = null; host.Content = drawing; };
        (Sheet?.ShowWindow ?? (w => w.ShowDialog()))(window);
    }

    private void Png_Click(object sender, RoutedEventArgs e) { if (Drawing is { } drawing) SavePng(drawing, FrameTitle.Text); }

    private void Expand_Click(object sender, RoutedEventArgs e) { if (Drawing is { } drawing) Expand(DrawingHost, drawing, FrameTitle.Text); }

    private void FitMap_Click(object sender, RoutedEventArgs e) => Map.ResetView();

    private void MapPng_Click(object sender, RoutedEventArgs e) => SavePng(Map, "Sezione · tensioni e deformazioni");

    private void MapExpand_Click(object sender, RoutedEventArgs e) => Expand(MapHost, Map, "Sezione · tensioni e deformazioni");

    private void FitPlane_Click(object sender, RoutedEventArgs e) => Plane.InvalidateVisual();

    private void PlanePng_Click(object sender, RoutedEventArgs e) => SavePng(Plane, "Piano delle deformazioni");

    private void PlaneExpand_Click(object sender, RoutedEventArgs e) => Expand(PlaneHost, Plane, "Piano delle deformazioni");
}
