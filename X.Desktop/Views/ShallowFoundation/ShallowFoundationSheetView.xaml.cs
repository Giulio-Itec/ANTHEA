using System.ComponentModel;
using System.Windows.Controls;
using Anthea.Presentation.Modules.ShallowFoundation;

namespace X.Desktop.Views.ShallowFoundation;

/// <summary>
/// View of <see cref="ShallowFoundationSheetViewModel"/> (F5.9b). The code-behind does only what decision Q3 allows: the technical
/// drawings of FoundationViews.cs, unchanged, receive the plain data of the ViewModel, as the historic FoundationWorkspace gave them:
/// <list type="bullet">
/// <item><see cref="ShallowFoundationSheetViewModel.InputDrawing"/> (after every edit, RefreshVisibility): the side view of the
/// stratigraphy and the plan of the geometry of the «Geometria» tab receive the data and are drawn again; the «Stratigrafia» tab gets a
/// new side view on the data;</item>
/// <item><see cref="ShallowFoundationSheetViewModel.Graphics"/> (AddGraphics): the effective area, the map of the pressures and the two
/// sections of the pressures of the result; none without graphics;</item>
/// <item><see cref="ShallowFoundationSheetViewModel.Mechanism"/>: the failure mechanism of the section shown.</item>
/// </list>
/// The pressure sample under the pointer stays in the drawing. No calculation and no write of the document.
/// </summary>
public partial class ShallowFoundationSheetView : UserControl
{
    private ShallowFoundationSheetViewModel? sheet;

    public ShallowFoundationSheetView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as ShallowFoundationSheetViewModel);
    }

    private void Attach(ShallowFoundationSheetViewModel? next)
    {
        if (sheet is not null) sheet.PropertyChanged -= OnSheetChanged;
        sheet = next;
        if (sheet is not null) sheet.PropertyChanged += OnSheetChanged;
        DrawInputs();
        DrawResults();
        DrawMechanism();
    }

    private void OnSheetChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ShallowFoundationSheetViewModel.InputDrawing): DrawInputs(); break;
            case nameof(ShallowFoundationSheetViewModel.Graphics): DrawResults(); break;
            case nameof(ShallowFoundationSheetViewModel.Mechanism): DrawMechanism(); break;
        }
    }

    private void DrawInputs()
    {
        var data = sheet?.InputDrawing?.Data;
        InputSoil.Data = data; InputSoil.InvalidateVisual();
        InputPlan.Data = data; InputPlan.InvalidateVisual();
        SoilHost.Content = data is null ? null : new FoundationSoilView { Data = data, Height = double.NaN };
    }

    private void DrawResults()
    {
        var graphics = sheet?.Graphics;
        AreaHost.Content = graphics is null ? null : new FoundationPlanView(graphics, false) { Height = double.NaN };
        PressureHost.Content = graphics is null ? null : new FoundationPlanView(graphics, true) { Height = double.NaN };
        SectionXHost.Content = graphics is null ? null : new FoundationPressureSection(graphics, true) { Height = double.NaN };
        SectionYHost.Content = graphics is null ? null : new FoundationPressureSection(graphics, false) { Height = double.NaN };
    }

    private void DrawMechanism() =>
        MechanismHost.Content = sheet?.Mechanism is { } mechanism ? new FoundationMechanismView(mechanism) { Height = double.NaN } : null;
}
