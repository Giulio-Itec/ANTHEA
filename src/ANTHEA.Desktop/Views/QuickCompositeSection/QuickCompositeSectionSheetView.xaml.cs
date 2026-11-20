using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.QuickCompositeSection;

namespace X.Desktop.Views.QuickCompositeSection;

public partial class QuickCompositeSectionSheetView : UserControl
{
    private QuickCompositeSectionSheetViewModel? sheet;
    internal BridgeDrawing Drawing { get; } = new() { MinHeight = 280 };
    public QuickCompositeSectionSheetView()
    {
        InitializeComponent(); SetValue(NumericPresentation.EnabledProperty, true); DrawingHost.Content = Drawing;
        DataContextChanged += (_, _) => Attach(DataContext as QuickCompositeSectionSheetViewModel);
        Loaded += (_, _) => Attach(DataContext as QuickCompositeSectionSheetViewModel);
        Unloaded += (_, _) => Attach(null);
    }
    private void Attach(QuickCompositeSectionSheetViewModel? next)
    {
        if (ReferenceEquals(sheet, next)) return;
        if (sheet is not null) sheet.PropertyChanged -= Changed;
        sheet = next;
        if (sheet is not null) { sheet.PropertyChanged += Changed; Draw(); }
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(QuickCompositeSectionSheetViewModel.Geometry) or nameof(QuickCompositeSectionSheetViewModel.Stage)
            or nameof(QuickCompositeSectionSheetViewModel.DrawingInput) or nameof(QuickCompositeSectionSheetViewModel.DrawingMode)) Draw();
    }
    private void Draw()
    {
        if (sheet is null) return;
        Drawing.Geometry = sheet.Geometry; Drawing.Stage = sheet.Stage; Drawing.Input = sheet.DrawingInput; Drawing.Mode = sheet.DrawingMode;
        Drawing.ShowGeometryLabels = true; Drawing.ShowRebarLabels = sheet.DrawingMode == 2; Drawing.ShowNeutralAxis = false;
        Drawing.InvalidateVisual();
    }
    private void Centre_Click(object sender, RoutedEventArgs e) => Drawing.ResetView();
    private void Editor_FocusChanged(object sender, DependencyPropertyChangedEventArgs e)
    { if (sender is TextBox { DataContext: FieldViewModel field } box) { field.IsFocused = box.IsKeyboardFocusWithin; if (field.IsFocused) box.SelectAll(); } }
}
