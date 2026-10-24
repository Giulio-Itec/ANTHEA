using System.Windows.Controls;
using Anthea.Presentation.Modules.Materials;

namespace X.Desktop.Views.Materials;

/// <summary>
/// View of <see cref="ConcreteMaterialSheetViewModel"/> (F5.5): the layout of the historic Materiali.MaterialView in XAML. No
/// code-behind besides the initialization (decision Q3): the sheet has no technical drawing, the widths follow the window through
/// <see cref="ConcreteLayoutConverter"/>, the fields are <see cref="ConcreteFieldRow"/>.
/// </summary>
public partial class ConcreteMaterialSheetView : UserControl
{
    public ConcreteMaterialSheetView() => InitializeComponent();
}
