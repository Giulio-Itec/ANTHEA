using System.IO;
using System.Numerics;
using ANTHEA.ModelWorkspace;
using HelixToolkit.Wpf.SharpDX;
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
        var renderer=typeof(ModelViewerControl).GetField("renderer",Flags)!.GetValue(control)!;
        Check(ReferenceEquals(renderer.GetType().GetField("sections",Flags)!.GetValue(renderer),vm.SectionCatalog),"2D inspector and renderer share one catalog instance");
        var section=vm.SectionCatalog.Find(100)!;
        var element=model.Elements.Single(e=>e.Id==1000);
        var profile=Call(renderer,"LineProfile",element,new Vector3[]{Vector3.Zero,new(2,0,0)})!;
        var caps=(Vector3[][])profile.GetType().GetProperty("Caps")!.GetValue(profile)!;
        var loops=(Vector3[][])profile.GetType().GetProperty("Loops")!.GetValue(profile)!;
        var points=loops.SelectMany(p=>p).ToArray();
        Check(Math.Abs(points.Min(p=>p.Z)+2.28)<1e-6&&Math.Abs(points.Max(p=>p.Z))<1e-6&&Math.Abs(points.Min(p=>p.Y)+1.8)<1e-6,
            "3D composite uses source top insertion and converts the shared mm coordinates to metres");
        double area=caps.Sum(t=>Vector3.Cross(t[1]-t[0],t[2]-t[0]).Length()/2);
        Check(Math.Abs(area-1.0654)<1e-6&&caps.All(t=>Vector3.Cross(t[1]-t[0],t[2]-t[0]).X>0),"3D caps retain independent physical area and consistent outward winding");
        Check(Call(renderer,"LineProfile",model.Elements.Single(e=>e.Id==12),new Vector3[]{Vector3.Zero,Vector3.UnitY})==null,
            "unresolved end offset cannot silently produce a centred solid in the renderer");
        vm.SelectedRepresentation=1;vm.ShowResults=false;vm.ShowHiddenWireframe=false;Wait(120);
        var viewport=Children<Viewport3DX>(control).Single();
        var positions=viewport.Items.OfType<MeshGeometryModel3D>().Where(m=>m.Geometry?.Positions?.Count>0).SelectMany(m=>m.Geometry!.Positions!).ToArray();
        Check(positions.Length>0&&Math.Abs(positions.Min(p=>p.Z)+2.28)<1e-6&&Math.Abs(positions.Max(p=>p.Z))<1e-6,
            "actual GPU geometry applies the shared insertion offset");
        window.Close();vm.SelectedFamily=1;vm.SelectedComponent="My";vm.ShowResults=true;vm.ShowLineContour=true;vm.FitCommand.Execute(null);Wait(180);
        Check(vm.HasResults&&viewport.Items.OfType<MeshGeometryModel3D>().Any(m=>m.Material is ColorStripeMaterial),"beam contouring renders on the composite solid");
        Capture(control,viewport,Path.Combine(output,"sezioni-composta-contouring-3d.png"));
        window.Close();Check(!vm.IsSectionWindowOpen,"native close updates section window state");
        host.Close();SetAppearance("Light");
    }
}
