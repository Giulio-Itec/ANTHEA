using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>A cable drawn in the section: identifier, position and total area (TendonPoint of the historic viewport).</summary>
public sealed record ConcreteTendonPoint(string Id, double X, double Y, double Area);

/// <summary>
/// The plain data of a drawing of the section (decision Q3 and C8: the drawing stays WPF in the code-behind, ConcreteSectionViewport): the
/// properties that the historic view assigns to its viewport. The view copies them into the viewport and draws again at
/// <see cref="Changed"/>. Not a drawing: no geometry is computed here.
/// </summary>
public sealed class ConcreteSectionDrawingViewModel : ObservableObject
{
    /// <summary>The contours of the stress maps, in the order of the viewport (ConcreteSheetPreparation.Contours).</summary>
    public static ImmutableArray<string> Contours => ConcreteSheetPreparation.Contours;

    private ConcreteGeometry? section;
    private IReadOnlyList<ConcreteTendonPoint> tendons = [];
    private string message = "Inserire la geometria della sezione", contour = Contours[0], selectedBar = "";
    private bool labels = true, axes = true, neutralAxis = true, barValues, tendonValues, concreteValues, dimensions, coverDimensions, spacingDimensions;
    private JsonObject? stirrups;

    /// <summary>The section of the engine; null when the data do not give a section (see <see cref="Message"/>).</summary>
    public ConcreteGeometry? Section { get => section; set => SetProperty(ref section, value); }
    public IReadOnlyList<ConcreteTendonPoint> Tendons { get => tendons; set => SetProperty(ref tendons, value ?? []); }
    /// <summary>Why there is no section; empty with a section.</summary>
    public string Message { get => message; set => SetProperty(ref message, value ?? ""); }
    /// <summary>The contour of a stress map (the preview of the section never changes it).</summary>
    public string Contour { get => contour; set => SetProperty(ref contour, value); }
    public bool Labels { get => labels; set => SetProperty(ref labels, value); }
    public bool Axes { get => axes; set => SetProperty(ref axes, value); }
    public bool ShowNeutralAxis { get => neutralAxis; set => SetProperty(ref neutralAxis, value); }
    public bool BarValues { get => barValues; set => SetProperty(ref barValues, value); }
    public bool TendonValues { get => tendonValues; set => SetProperty(ref tendonValues, value); }
    public bool ConcreteValues { get => concreteValues; set => SetProperty(ref concreteValues, value); }
    public bool Dimensions { get => dimensions; set => SetProperty(ref dimensions, value); }
    public bool CoverDimensions { get => coverDimensions; set => SetProperty(ref coverDimensions, value); }
    public bool SpacingDimensions { get => spacingDimensions; set => SetProperty(ref spacingDimensions, value); }
    /// <summary>The options of the links drawn (the shear options of the sheet); null draws none.</summary>
    public JsonObject? Stirrups { get => stirrups; set => SetProperty(ref stirrups, value); }
    /// <summary>Identifier of the bar highlighted («B03»); empty for none.</summary>
    public string SelectedBar { get => selectedBar; set => SetProperty(ref selectedBar, value ?? ""); }

    private ConcreteResults.StressState? stress;
    private ConcreteResults.EffectiveRegion? effectiveRegion;
    private string legend = "";

    /// <summary>The stress state drawn by a stress map (F5.10 S4); null draws the geometry only.</summary>
    public ConcreteResults.StressState? Stress { get => stress; set => SetProperty(ref stress, value); }
    /// <summary>The effective zone of the cracking highlighted on a stress map (F5.10 S4); null for none.</summary>
    public ConcreteResults.EffectiveRegion? EffectiveRegion { get => effectiveRegion; set => SetProperty(ref effectiveRegion, value); }
    /// <summary>
    /// The legend of the contour of a stress map when it was last drawn on screen (F5.10 S4: the ContourLegend of the historic viewport, from
    /// <see cref="ConcreteStressPresentation.ContourScale"/>); empty without a contour. The module writes it.
    /// </summary>
    public string Legend { get => legend; set => SetProperty(ref legend, value ?? ""); }

    /// <summary>Asks the view to draw again (the InvalidateVisual of the historic view, after a group of changes).</summary>
    public void Invalidate() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>The view draws again.</summary>
    public event EventHandler? Changed;

    /// <summary>A bar clicked in the drawing (index in the bars of the section): the sheet selects its row.</summary>
    public event EventHandler<int>? BarClicked;

    /// <summary>The view reports a click on the bar <paramref name="index"/>.</summary>
    public void ClickBar(int index)
    {
        SelectedBar = "B" + (index + 1).ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
        BarClicked?.Invoke(this, index);
        Invalidate();
    }
}
