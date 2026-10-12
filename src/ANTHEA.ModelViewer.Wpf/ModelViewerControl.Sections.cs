using System.Windows;

namespace ANTHEA.ModelViewer.Wpf;

public partial class ModelViewerControl
{
    Window? sectionWindow;
    void SectionWindowChanged(object? sender,EventArgs args)
    {
        if(disposed)return;
        if(!viewModel.IsSectionWindowOpen){sectionWindow?.Close();return;}
        if(sectionWindow==null)
        {
            var window=new Window {Title="ANTHEA · Sezioni del modello",Width=1180,Height=850,MinWidth=850,MinHeight=620,
                Owner=Window.GetWindow(this),WindowStartupLocation=WindowStartupLocation.CenterOwner,
                Content=new ModelSectionView {DataContext=viewModel.SectionBrowser}};
            window.SetResourceReference(BackgroundProperty,"AppearancePaper");
            window.SetResourceReference(ForegroundProperty,"AppearanceInk");
            window.Closed+=(_,_)=>{sectionWindow=null;if(!disposed)viewModel.CloseSectionDetailsCommand.Execute(null);};
            sectionWindow=window;window.Show();
        }
        else ((ModelSectionView)sectionWindow.Content).DataContext=viewModel.SectionBrowser;
        sectionWindow.Activate();
    }
}
