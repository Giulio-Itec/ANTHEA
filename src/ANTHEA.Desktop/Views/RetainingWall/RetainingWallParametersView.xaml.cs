using System.Windows.Controls;

namespace X.Desktop.Views.RetainingWall;

/// <summary>View of <see cref="Anthea.Presentation.Modules.RetainingWall.RetainingWallParametersViewModel"/> (F5.9, MU10): bindings only.</summary>
public partial class RetainingWallParametersView : UserControl
{
    public RetainingWallParametersView()
    {
        InitializeComponent();
        // As the historic window, which does not take the numeric presentation of the sheet: the values as they are written.
        SetValue(NumericPresentation.EnabledProperty, false);
    }
}
