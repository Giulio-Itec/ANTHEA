using System.ComponentModel;
using System.Windows.Controls;
using Anthea.Presentation.Modules.Materials;
using X.Desktop.Themes;

namespace X.Desktop.Views.Materials;

/// <summary>
/// View of <see cref="RebarMaterialSheetViewModel"/> (F5.5). The code-behind does only what decision Q3 allows: the technical drawing.
/// The historic <see cref="Plot"/> of RebarMaterialView, with its titles, receives the curve of the ViewModel at every change
/// (<see cref="RebarMaterialSheetViewModel.Diagram"/>, <see cref="RebarMaterialSheetViewModel.DiagramName"/>,
/// <see cref="RebarMaterialSheetViewModel.HasDiagram"/>) and resets its zoom, as Refresh of the historic view did. The curve has the
/// colour of the drawings (Drawing.Accent, the same in every appearance, as Ui.Blue on the white plot). No write of the document.
/// </summary>
public partial class RebarMaterialSheetView : UserControl
{
    private RebarMaterialSheetViewModel? sheet;

    public RebarMaterialSheetView()
    {
        InitializeComponent();
        DiagramHost.Child = Diagram;
        DataContextChanged += (_, _) => Attach(DataContext as RebarMaterialSheetViewModel);
    }

    /// <summary>The drawing of the material, with the titles of the historic view (its constructor is not public: created here).</summary>
    internal Plot Diagram { get; } = new()
    {
        Height = 295, Title = "Legame caratteristico σ–ε", XLabel = "Deformazione ε [‰]", YLabel = "Tensione σ [MPa]", InvertY = false,
        EmptyMessage = "Completare i dati per visualizzare il diagramma."
    };

    private void Attach(RebarMaterialSheetViewModel? next)
    {
        if (sheet is not null) sheet.PropertyChanged -= OnSheetChanged;
        sheet = next;
        if (sheet is null) return;
        sheet.PropertyChanged += OnSheetChanged;
        Draw();
    }

    private void OnSheetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RebarMaterialSheetViewModel.Diagram) or nameof(RebarMaterialSheetViewModel.DiagramName) or nameof(RebarMaterialSheetViewModel.HasDiagram)) Draw();
    }

    private void Draw()
    {
        if (sheet is null) return;
        Diagram.Series = sheet.HasDiagram ? [new Serie(sheet.DiagramName, sheet.Diagram.ToList(), ThemeTokens.Brush("Drawing.Accent"))] : [];
        Diagram.ResetView();
    }
}
