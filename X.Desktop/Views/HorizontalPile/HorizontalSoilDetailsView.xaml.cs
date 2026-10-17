using System.Windows.Controls;
using Anthea.Application.Presentation;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>
/// The dialog of the details of the soil editor of a layer (F5.7): the view of <see cref="Anthea.Presentation.Modules.HorizontalPile.HorizontalSoilRowViewModel"/>
/// for its details. The code-behind gives the two tables of the catalogue (HorizontalSoilEditor.Tables of the historic view), plain data.
/// </summary>
public partial class HorizontalSoilDetailsView : UserControl
{
    /// <summary>Caption of the window, as the historic dialog.</summary>
    internal const string Title = "Parametro dello strato · origine e applicabilità";

    public HorizontalSoilDetailsView()
    {
        InitializeComponent();
        SandTable.ItemsSource = HorizontalSoilPresentation.SandTable();
        CohesiveTable.ItemsSource = HorizontalSoilPresentation.CohesiveTable();
    }
}
