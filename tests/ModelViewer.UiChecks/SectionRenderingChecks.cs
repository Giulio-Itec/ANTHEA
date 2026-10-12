using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;

static partial class Program
{
    static void SectionRenderingChecks(string output)
    {
        var model=SectionInspectionSample.Model();
        using var control=new ModelViewerControl(model,[],false,_=>{},(_,_,_,_)=>{});
        var host=new Window {Title="Collaudo sezioni",Width=1200,Height=900,Content=control,ShowActivated=false,ShowInTaskbar=false};
        WatchAppearance(host);host.Show();Wait(180);
        var vm=(ModelViewerViewModel)control.DataContext;
        vm.SelectElement(1000);vm.OpenSectionDetailsCommand.Execute(null);Wait(100);
        var window=(Window)typeof(ModelViewerControl).GetField("sectionWindow",Flags)!.GetValue(control)!;
        WatchAppearance(window);var view=(ModelSectionView)window.Content;
        Check(ReferenceEquals(view.DataContext,vm.SectionBrowser)&&vm.SectionBrowser!.SelectedSection?.Id==100,"real detached section view follows beam assignment");
        Check(vm.SectionBrowser!.SelectedSection!.ToString()=="100 · Campata · concio 1","section choice has a readable label in themed combo templates");
        foreach(string theme in new[]{"Light","Dark","VeryDark"})
        {
            SetAppearance(theme);Wait(110);window.UpdateLayout();
            Check(Children<ModelSectionPreview>(view).Single().ActualWidth>300,"section preview retains usable width in "+theme);
            CapturePage(view,Path.Combine(output,"sezioni-composta-"+theme+".png"));
        }
        vm.SectionBrowser!.ShowPrincipalAxes=true;vm.SectionBrowser.ShowDimensions=false;vm.SectionBrowser.ShowInsertion=false;
        var preview=Children<ModelSectionPreview>(view).Single();Wait(40);
        Check(preview.ShowPrincipalAxes&&!preview.ShowDimensions&&!preview.ShowInsertion,"preview switches bind independently to the ViewModel");
        vm.SectionBrowser.ShowDimensions=true;vm.SectionBrowser.ShowInsertion=true;vm.SectionBrowser.SelectedSection=vm.SectionCatalog!.Find(3);
        window.Width=850;window.Height=620;Wait(100);CapturePage(view,Path.Combine(output,"sezioni-scatolare-compatta.png"));
        Check(preview.Detail!.Contours[0].Holes.Length==1,"cavity survives through the actual WPF preview binding");
        vm.SectionBrowser.SelectedSection=vm.SectionCatalog.Find(5);Wait(40);
        Check(preview.Detail!.Insertion==null,"unknown offset is not marked at the centroid in the rendered view");
        vm.SectionBrowser.SelectedSection=vm.SectionCatalog.Find(100);vm.SectionBrowser.IsolateElementsCommand.Execute(null);Wait(50);
        Check(vm.VisibleElementIds!.SetEquals([1000]),"detached view isolation updates the owning model viewport");
        window.Close();Check(!vm.IsSectionWindowOpen,"native close updates section window state");
        host.Close();SetAppearance("Light");
    }
}
