using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.ConcreteSection;

namespace X.Desktop.Views.ConcreteSection;

/// <summary>
/// View of <see cref="SectionActionsViewModel"/> (the window «Sollecitazioni della sezione», F5.10 view after S9): the items of the column «Stato limite»,
/// the column «Origine precedente» shown only with former data, the edits of the table committed before «Applica», as the historic window. No
/// calculation and no write of the document.
/// </summary>
public partial class SectionActionsDialogView : UserControl
{
    private SectionActionsViewModel? actions;

    public SectionActionsDialogView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as SectionActionsViewModel);
    }

    private void Attach(SectionActionsViewModel? next)
    {
        if (ReferenceEquals(actions, next)) return;
        if (actions is not null) actions.PropertyChanged -= OnActionsChanged;
        actions = next;
        if (actions is null) return;
        StateColumn.ItemsSource = actions.States;
        actions.PropertyChanged += OnActionsChanged;
        ShowOrigin();
    }

    private void OnActionsChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(SectionActionsViewModel.HasOrigins)) ShowOrigin(); }

    private void ShowOrigin() => OriginColumn.Visibility = actions?.HasOrigins == true ? Visibility.Visible : Visibility.Collapsed;

    // The Commit of the historic window: the cell and the row being edited go into the rows before «Applica» (the Click comes before the command).
    private void Commit_Click(object sender, RoutedEventArgs e)
    {
        ActionsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ActionsGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }
}
