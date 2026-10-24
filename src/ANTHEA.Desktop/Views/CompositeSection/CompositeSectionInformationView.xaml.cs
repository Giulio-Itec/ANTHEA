using System.Windows;
using System.Windows.Controls;

namespace X.Desktop.Views.CompositeSection;

/// <summary>
/// «Info modello…» (F5.11): the fixed texts of the historic window with the four sketches of the method, drawn in code (Q3); «Chiudi» closes the
/// window that hosts the view.
/// </summary>
public partial class CompositeSectionInformationView : UserControl
{
    public CompositeSectionInformationView()
    {
        InitializeComponent();
        Sketch0.Content = new BridgeMethodSketch(0); Sketch1.Content = new BridgeMethodSketch(1);
        Sketch2.Content = new BridgeMethodSketch(2); Sketch3.Content = new BridgeMethodSketch(3);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();
}
