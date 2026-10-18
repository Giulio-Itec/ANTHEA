using System.Windows;
using System.Windows.Controls;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>The dialog of the proposal of the segments (F5.8): the text of the proposal and the button that applies it through the ViewModel.</summary>
public partial class ElasticProposalView : UserControl
{
    /// <summary>Caption of the window, as the historic dialog.</summary>
    internal const string Title = "Proposta di suddivisione";

    private readonly Action apply;

    internal ElasticProposalView(string proposal, Action apply)
    {
        this.apply = apply;
        InitializeComponent();
        ProposalText.Text = proposal;
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => apply();
}
