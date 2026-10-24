using System.Windows.Controls;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>The dialog of the design of a segment (F5.8): the view of <see cref="Anthea.Presentation.Modules.HorizontalPile.Elastic.PileSegmentDesignViewModel"/>.</summary>
public partial class PileSegmentDesignView : UserControl
{
    /// <summary>Caption of the window, as the historic dialog.</summary>
    internal const string Title = "Dimensionamento del tratto · proposta da applicare";

    public PileSegmentDesignView() => InitializeComponent();
}
