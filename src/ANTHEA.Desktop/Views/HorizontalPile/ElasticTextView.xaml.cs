using System.Windows.Controls;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>A text shown in a dialog of the view of the elastic response (Info, «Mostra confronto completo», «Info sezioni»).</summary>
public partial class ElasticTextView : UserControl
{
    public ElasticTextView() => InitializeComponent();

    /// <summary>The text shown.</summary>
    public string Text { get => Box.Text; set => Box.Text = value; }
}
