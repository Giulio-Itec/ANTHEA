using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Anthea.Presentation.Modules.PileGroup;

namespace X.Desktop.Views.PileGroup;

/// <summary>
/// View of <see cref="PileGroupSheetViewModel"/> (F5.4 pilot). The code-behind does only what decision Q3 allows:
/// <list type="bullet">
/// <item>the technical drawing: the historic <see cref="PileGroupPlan"/>, unchanged, receives <see cref="PileGroupSheetViewModel.Plan"/>
/// at every change, as the historic view called plan.Set;</item>
/// <item>the cancelled edit of a coordinate cell (Esc): posted to the dispatcher with priority Background, as the historic view did,
/// then <see cref="PileGroupSheetViewModel.CoordinateEditCancelled"/>.</item>
/// </list>
/// No calculation and no write of the document.
/// </summary>
public partial class PileGroupSheetView : UserControl
{
    private PileGroupSheetViewModel? sheet;

    public PileGroupSheetView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as PileGroupSheetViewModel);
    }

    private void Attach(PileGroupSheetViewModel? next)
    {
        if (sheet is not null) sheet.PropertyChanged -= OnSheetChanged;
        sheet = next;
        if (sheet is null) return;
        sheet.PropertyChanged += OnSheetChanged;
        Draw();
    }

    private void OnSheetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PileGroupSheetViewModel.Plan)) Draw();
    }

    private void Draw()
    {
        if (sheet?.Plan is { } plan) PlanDrawing.Set(plan.Data, plan.Selected, plan.Cap);
    }

    private void Coordinates_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Cancel || sheet is not { } current) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(current.CoordinateEditCancelled));
    }
}
