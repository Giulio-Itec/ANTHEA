using System.Windows.Controls;
using System.Windows;
using ANTHEA.ModelViewer.Presentation;
namespace ANTHEA.ModelViewer.Wpf;
public partial class ModelImportOptionsView:UserControl
{
    public ModelImportOptionsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => MidasKey.Password = (DataContext as ModelViewerViewModel)?.MidasApiKey ?? "";
    }
    // PasswordBox has no bindable Password dependency property. This only transfers UI input;
    // connection state, validation, commands and cancellation all remain in the viewmodel.
    void MidasKeyChanged(object sender, RoutedEventArgs e)
    { if (DataContext is ModelViewerViewModel vm) vm.MidasApiKey = MidasKey.Password; }
}
