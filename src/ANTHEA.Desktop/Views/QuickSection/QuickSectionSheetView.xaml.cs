using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Anthea.Application.Concrete;
using Anthea.Presentation.Modules.QuickSection;
using X.Desktop.Themes;

namespace X.Desktop.Views.QuickSection;

/// <summary>
/// View of <see cref="QuickSectionSheetViewModel"/>. The code-behind only draws, as the views of the RC section: the section of the RC section
/// (<see cref="ConcreteSectionViewport"/>) with the stresses of the result, and the chart of the domain or of the moment–curvature (<see cref="Plot"/>),
/// fed by the data of the ViewModel. No calculation and no write of the document.
/// </summary>
public partial class QuickSectionSheetView : UserControl
{
    private QuickSectionSheetViewModel? sheet;

    /// <summary>The section drawn.</summary>
    internal ConcreteSectionViewport Drawing { get; } = new() { Labels = true, Axes = true };

    /// <summary>The chart of the domain or of the moment–curvature.</summary>
    internal Plot Chart { get; } = ConcreteSectionDrawings.DomainPlot();

    public QuickSectionSheetView()
    {
        InitializeComponent();
        DrawingHost.Content = Drawing;
        ChartHost.Content = Chart;
        DataContextChanged += (_, _) => Attach(DataContext as QuickSectionSheetViewModel);
    }

    /// <summary>The focus of an editor, reported to its field as FieldForm does: leaving the field confirms the text typed.</summary>
    private void Editor_FocusChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement { DataContext: Anthea.Presentation.Forms.FieldViewModel field }) field.IsFocused = (bool)e.NewValue;
    }

    /// <summary>«Centra sezione» (19/11/2026): the section in the middle of the drawing at its full size, as the double click on it.</summary>
    private void CenterSection_Click(object sender, RoutedEventArgs e) => Drawing.ResetView();

    /// <summary>The most elements that Enter crosses to find the next box (menus, buttons and boxes not to be written are skipped).</summary>
    private const int EnterSteps = 200;

    /// <summary>
    /// Enter in a box (19/11/2026): confirms the value and goes to the next box to be written, in the order of the screen; Shift+Enter to the
    /// previous one. Menus, buttons, selections, the title and the boxes not to be written (read-only, disabled) are skipped; the focus stays in the sheet.
    /// </summary>
    private void Sheet_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.OriginalSource is not TextBox { AcceptsReturn: false } box) return;
        e.Handled = true;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        var request = new TraversalRequest(e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift) ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next);
        UIElement current = box;
        for (int step = 0; step < EnterSteps; step++)
        {
            if (!current.MoveFocus(request) || Keyboard.FocusedElement is not UIElement next || ReferenceEquals(next, box)) break;
            if (next is TextBox { IsReadOnly: false, IsEnabled: true, IsVisible: true } target && !ReferenceEquals(target, TitleBox) && IsAncestorOf(target))
            {
                target.SelectAll();
                return;
            }
            current = next;
        }
        box.Focus();
    }

    private void Attach(QuickSectionSheetViewModel? next)
    {
        if (ReferenceEquals(sheet, next)) return;
        if (sheet is not null) sheet.PropertyChanged -= OnSheetChanged;
        sheet = next;
        if (sheet is null) return;
        sheet.PropertyChanged += OnSheetChanged;
        DrawSection(); DrawChart();
    }

    private void OnSheetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(QuickSectionSheetViewModel.Section) or nameof(QuickSectionSheetViewModel.SectionStress) or nameof(QuickSectionSheetViewModel.StressCallouts) or nameof(QuickSectionSheetViewModel.SecondRingLinkDiameter)
            or nameof(QuickSectionSheetViewModel.DrawingMessage))
            DrawSection();
        if (e.PropertyName is nameof(QuickSectionSheetViewModel.DomainGeometry) or nameof(QuickSectionSheetViewModel.DomainActions)
            or nameof(QuickSectionSheetViewModel.CurvaturePoints) or nameof(QuickSectionSheetViewModel.ShowsDomain))
            DrawChart();
        // The area shown again is drawn with its new size.
        if (e.PropertyName == nameof(QuickSectionSheetViewModel.ShowsSection))
            _ = Dispatcher.InvokeAsync(() => { DrawSection(); DrawChart(); }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void DrawSection()
    {
        if (sheet is null) return;
        Drawing.Section = sheet.Section;
        Drawing.Stress = sheet.SectionStress;
        Drawing.Callouts = sheet.StressCallouts;
        Drawing.InnerLinkDiameter = sheet.SecondRingLinkDiameter;
        Drawing.Stirrups = sheet.Stirrups;
        Drawing.Message = sheet.Section is null ? sheet.DrawingMessage : "";
        Drawing.InvalidateVisual();
    }

    private void DrawChart()
    {
        if (sheet is null) return;
        Chart.Series = []; Chart.Markers = []; Chart.Segments = []; Chart.VerificationSegments = [];
        if (sheet.ShowsDomain)
        {
            Chart.CenteredAxes = true;
            if (sheet.DomainGeometry is { } geometry) PlotPresentation.ApplyDomainGeometry(Chart, geometry);
            else { Chart.Title = "Sezione del dominio"; Chart.EmptyMessage = "Dominio in attesa di aggiornamento automatico"; }
            if (sheet.DomainActions is { } actions) PlotPresentation.ApplyDomainActions(Chart, actions);
        }
        else
        {
            Chart.CenteredAxes = false;
            Chart.Title = ConcreteCurvaturePresentation.Title; Chart.XLabel = ConcreteCurvaturePresentation.XLabel; Chart.YLabel = ConcreteCurvaturePresentation.YLabel;
            Chart.Note = ""; Chart.EmptyMessage = ConcreteCurvaturePresentation.EmptyMessage;
            if (sheet.CurvaturePoints.Count > 0)
                Chart.Series = [new(ConcreteCurvaturePresentation.SeriesName, [.. sheet.CurvaturePoints], ThemeTokens.Brush("Series.Blue"))];
        }
        Chart.ResetView();
    }
}
