using System.Windows;
using System.Windows.Controls;

namespace X.Desktop;

internal static class CalculationHelpView
{
    internal static void Show(FrameworkElement owner, bool pile)
    {
        var text = new TextBox { Text = (pile ? CalculationHelp.PileTheory : CalculationHelp.ConcreteTheory) + "\n\nDATI DEL PROGETTO\n\n" + CalculationHelp.ProjectTheory,
            IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 14, BorderThickness = new Thickness(0), Padding = new Thickness(20),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Ui.Dialog(owner, "Modello di calcolo e dati condivisi", text, 850, 500).ShowDialog();
    }
}
