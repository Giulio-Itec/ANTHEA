using System.Windows;
using System.Windows.Controls;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>The dialog of the split of a segment (F5.8): the text typed goes to the ViewModel; its error is shown as the historic dialog shows it.</summary>
public partial class ElasticSplitView : UserControl
{
    /// <summary>Caption of the window, as the historic dialog.</summary>
    internal const string Title = "Dividi il tratto";

    public ElasticSplitView() => InitializeComponent();

    /// <summary>«Dividi» with the text typed.</summary>
    internal event Action<string>? SplitRequested;

    /// <summary>The error of the cut: the tool tip of the box, as the historic dialog.</summary>
    internal void ShowError(string error) => Depth.ToolTip = error;

    private void Split_Click(object sender, RoutedEventArgs e) => SplitRequested?.Invoke(Depth.Text);
}
