using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.ConcreteSection;

namespace X.Desktop.Views.ConcreteSection;

/// <summary>
/// View of <see cref="ConcreteShearViewModel"/> (tab 05 of the RC section, F5.10 view after S9). The code-behind does only what decision Q3 allows: the
/// section of the historic view (<see cref="ConcreteSectionViewport"/>) fed by <see cref="ConcreteShearViewModel.Drawing"/>, «Adatta», «PNG», «Espandi»
/// and «Modifica staffe nel pannello di controllo →» (the tab 01, as the historic button). No calculation and no write of the document.
/// </summary>
public partial class ConcreteShearView : UserControl
{
    private ConcreteShearViewModel? shear;

    /// <summary>The section with the geometric references of the shear.</summary>
    internal ConcreteSectionViewport Drawing { get; } = new();

    public ConcreteShearView()
    {
        InitializeComponent();
        DrawingHost.Content = Drawing;
        DataContextChanged += (_, _) => Attach(DataContext as ConcreteShearViewModel);
    }

    private void Attach(ConcreteShearViewModel? next)
    {
        if (ReferenceEquals(shear, next)) return;
        if (shear is not null) { shear.Drawing.Changed -= OnDrawingChanged; shear.Drawing.PropertyChanged -= OnDrawingChanged; }
        shear = next;
        if (shear is null) return;
        shear.Drawing.Changed += OnDrawingChanged; shear.Drawing.PropertyChanged += OnDrawingChanged;
        Draw();
    }

    private void OnDrawingChanged(object? sender, EventArgs e) => Draw();

    private void Draw() { if (shear is not null) ConcreteSectionDrawings.Apply(Drawing, shear.Drawing); }

    private void Fit_Click(object sender, RoutedEventArgs e) => Drawing.ResetView();

    private void Png_Click(object sender, RoutedEventArgs e) => ConcreteViewFrames.SavePng(this, Drawing, "Sezione · riferimenti geometrici");

    private void Expand_Click(object sender, RoutedEventArgs e) => ConcreteViewFrames.Expand(this, DrawingHost, Drawing, "Sezione · riferimenti geometrici");

    private void ControlPanel_Click(object sender, RoutedEventArgs e)
    {
        if (ConcreteViewFrames.SheetOf(this)?.DataContext is ConcreteSheetViewModel sheet) sheet.SelectedTab = 0;
    }
}
