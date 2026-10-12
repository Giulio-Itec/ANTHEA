using System.Text.Json;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

static partial class Program
{
    static void SectionInspectionChecks(string output)
    {
        var model=SectionInspectionSample.Model();string before=model.Fingerprint();
        var catalog=ModelSectionCatalog.Create(model);
        var rectangle=catalog.Find(2)!;var box=catalog.Find(3)!;var girder=catalog.Find(4)!;var composite=catalog.Find(100)!;
        Check(rectangle.HasGeometry && rectangle.Insertion==new SectionPoint(100,300),"section catalog converts source metres to mm and resolves top reference");
        Check(rectangle.Quantities.Single(q=>q.Name=="Area").Value==60000,"rectangle independent area is 200 by 300 mm");
        Check(box.Contours.Length==1&&box.Contours[0].Holes.Length==1,"closed section preserves inner void in the shared contour");
        Check(Math.Abs(MeshArea(box)-(200*300-180*272))<1e-7,"box cap triangulation preserves 200x300 outer minus 180x272 hole area");
        Check(Math.Abs(MeshArea(girder)-(200*10+180*8+200*10))<1e-7,"concave I cap uses actual flanges and web, no fan over voids");
        Check(composite.WidthMillimetres==3600&&composite.HeightMillimetres==2280,"inspector dimensions use the resolved full contour rather than an absent legacy bounding rectangle");
        Check(composite.IsComposite&&composite.Parts.Length==2&&composite.Insertion!.Z==2280,"composite physical parts resolve the top of the slab, not an elastic centroid");
        Check(Math.Abs(MeshArea(composite)-(1960*15+600*20+800*20+3600*280))<1e-5,"composite graphical mesh retains independently calculated physical area");
        Check(catalog.Find(5) is {HasGeometry:true,Insertion:null} && !catalog.Find(5)!.CanExtrude(15),"unresolved section offset cannot be replaced by a centred prism");
        Check(catalog.Find(6) is {HasGeometry:false} && catalog.Find(6)!.Diagnostics.Length>0,"unknown section preserves a readable unavailable state");
        Check(rectangle.ElementsWithUnresolvedEndOffset.SequenceEqual([12])&&!rectangle.CanExtrude(12),"end offset assignment prevents misleading extrusion");
        Check(rectangle.Attributes.Any(a=>a.Name=="SECT_BEFORE.OFFSET_PT"&&a.Value=="CT"),"source attributes reuse the original tree projection");
        Check(model.Fingerprint()==before,"inspection and graphical meshes leave saved model and revision unchanged");
        var reopened=ModelSnapshot.FromJson(JsonSerializer.Serialize(model));
        Check(ModelSectionCatalog.Create(reopened).Find(100)!.Insertion==composite.Insertion,"section geometry is reconstructed from preserved source tables after reopen");
        using var vm=new ModelViewerViewModel(model,[],false,new FakeServices(),_=>{},(_,_,_,_)=>{});
        vm.SelectElement(14);vm.OpenSectionDetailsCommand.Execute(null);
        Check(vm.SectionBrowser?.SelectedSection?.Id==4&&vm.IsSectionWindowOpen,"opening from a selected beam chooses its section");
        vm.SectionBrowser!.SelectedSection=vm.SectionCatalog!.Find(100);vm.SectionBrowser.IsolateElementsCommand.Execute(null);
        Check(vm.SelectedElementIds.SetEquals([1000])&&vm.VisibleElementIds!.SetEquals([1000]),"section window isolates precisely the associated elements");
        vm.CloseSectionDetailsCommand.Execute(null);Check(!vm.IsSectionWindowOpen,"closing section window preserves model and visibility state");
        File.WriteAllText(Path.Combine(output,"sezioni.antheamodel"),JsonSerializer.Serialize(model));
    }
    static double MeshArea(ModelSectionDetail detail)
    {
        var mesh=detail.Mesh;if(mesh.Problem!=null)throw new InvalidOperationException(detail.Label+": "+mesh.Problem);
        return mesh.Triangles.Sum(t=>Math.Abs((t.B.Y-t.A.Y)*(t.C.Z-t.A.Z)-(t.C.Y-t.A.Y)*(t.B.Z-t.A.Z))/2);
    }
}
