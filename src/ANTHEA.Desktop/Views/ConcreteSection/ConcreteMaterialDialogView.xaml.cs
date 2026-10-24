using System.ComponentModel;
using System.Windows.Controls;
using Anthea.Presentation.Modules.ConcreteSection;

namespace X.Desktop.Views.ConcreteSection;

/// <summary>
/// View of <see cref="ConcreteMaterialDialogViewModel"/> (the window «Nuovo materiale · …», F5.10 view after S9): the σ–ε diagram of the historic window
/// fed by <see cref="ConcreteMaterialDialogViewModel.Curves"/> (Q3). No calculation and no write of the document.
/// </summary>
public partial class ConcreteMaterialDialogView : UserControl
{
    private ConcreteMaterialDialogViewModel? dialog;
    private Plot? plot;

    public ConcreteMaterialDialogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as ConcreteMaterialDialogViewModel);
    }

    /// <summary>The σ–ε diagram.</summary>
    internal Plot? Plot => plot;

    private void Attach(ConcreteMaterialDialogViewModel? next)
    {
        if (ReferenceEquals(dialog, next)) return;
        if (dialog is not null) dialog.PropertyChanged -= OnDialogChanged;
        dialog = next;
        PlotHost.Content = plot = null;
        if (dialog is null) return;
        plot = ConcreteSectionDrawings.MaterialPlot(dialog.NegateAxisLabels, dialog.PlotNote);
        PlotHost.Content = plot;
        dialog.PropertyChanged += OnDialogChanged;
        Draw();
    }

    private void OnDialogChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(ConcreteMaterialDialogViewModel.Curves)) Draw(); }

    private void Draw() { if (dialog is not null && plot is not null) ConcreteSectionDrawings.Apply(plot, dialog.Curves); }
}
