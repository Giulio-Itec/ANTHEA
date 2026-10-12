using System.Windows;

namespace ANTHEA.ModelViewer.Wpf;

public partial class ModelViewerControl
{
    Window? combinationWindow;
    void CombinationWindowChanged(object? sender,EventArgs args)
    {
        if(disposed)return;
        if(!viewModel.IsCombinationDetached){combinationWindow?.Close();return;}
        if(combinationWindow==null)
        {
            var window=new Window {Title="ANTHEA · Combinazioni",Width=1200,Height=860,MinWidth=600,MinHeight=580,
                Owner=Window.GetWindow(this),WindowStartupLocation=WindowStartupLocation.CenterOwner,
                Content=new ModelCombinationsView{DataContext=viewModel}};
            window.SetResourceReference(BackgroundProperty,"AppearancePaper");
            window.SetResourceReference(ForegroundProperty,"AppearanceInk");
            window.Closed+=(_,_)=>
            {
                combinationWindow=null;
                if(!disposed&&viewModel.IsCombinationDetached)viewModel.ToggleCombinationWindowCommand.Execute(null);
            };
            combinationWindow=window;window.Show();
        }
        combinationWindow.Activate();
    }
}
