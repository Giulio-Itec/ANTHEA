using System.Windows;
using System.Windows.Controls;

namespace Materiali;

public sealed partial class MaterialView
{
    readonly TextBox minimumConcreteClass=new(){IsReadOnly=true,IsReadOnlyCaretVisible=false,Background=Brush("#EDF5FF"),FontWeight=FontWeights.SemiBold};
    readonly TextBlock minimumConcreteNote=Text("",12);
}
