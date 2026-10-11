using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using System.Text.Json;

static partial class Program
{
    static void CategoryColorChecks()
    {
        var model=Sample.Model();string before=model.Fingerprint();
        using var vm=new ModelViewerViewModel(model,[],false,new FakeServices(),_=>{},(_,_,_,_)=>{});
        vm.ShowResults=false;vm.SelectedColorMode=1;vm.SelectedColorCategoryKey="PLATE";
        string original=vm.ElementColors[10].Color;vm.CategoryColorText="#abcdef";vm.ApplyCategoryColorCommand.Execute(null);
        Check(vm.ElementColors[10].Color=="#ABCDEF" && vm.ElementColors[12].Color!="#ABCDEF","category override changes only the chosen family and normalizes its RGB value");
        vm.HideColorCategoryCommand.Execute("PLATE");
        Check(vm.ColorLegend.All(c=>c.Key!="PLATE") && vm.ColorCategories.Any(c=>c.Key=="PLATE"),"hidden categories remain available in the editor while the viewport legend shows only visible categories");
        vm.IsolateColorCategoryCommand.Execute("PLATE");
        Check(vm.VisibleElementIds!.SetEquals([10,11]) && vm.ColorLegend.Single().Color=="#ABCDEF","category isolation restores its hidden members and preserves the custom color");
        vm.ShowColorCategoryCommand.Execute("BEAM");vm.SelectColorCategoryCommand.Execute("BEAM");
        Check(vm.SelectedElementIds.SetEquals([12]) && vm.VisibleElementIds!.Count==3,"category actions share visibility and selection with the tree");
        vm.CategoryColorText="red";Check(!vm.ApplyCategoryColorCommand.CanExecute(null) && vm.CategoryColorMessage.Length>0,"invalid colors stay out of rendering and give an inline correction");
        vm.EditColorCategoryCommand.Execute("PLATE");Check(vm.SelectedPanel==2 && vm.ShowDisplayOptions,"legend editing opens the single display settings location");
        var preset=JsonSerializer.Deserialize<ModelViewPreset>(JsonSerializer.Serialize(vm.CaptureView("Colori")))!;
        ModelViewPresetStore.Validate([preset]);vm.ResetCategoryColorsCommand.Execute(null);vm.ApplyView(preset);
        Check(vm.ElementColors[10].Color=="#ABCDEF" && vm.CategoryColorText=="#ABCDEF","saved views restore both rendered and editable category colors");
        vm.SelectedColorMode=3;Check(vm.ElementColors.Values.All(e=>e.Color!="#ABCDEF"),"category color overrides are scoped to the coloring mode");
        vm.SelectedColorMode=1;vm.SelectedColorCategoryKey="PLATE";vm.CompareEnabled=true;
        Check(vm.Comparison!.ElementColors[10].Color=="#ABCDEF","comparison shares geometry category colors with the main scene");
        vm.ResetCategoryColorCommand.Execute(null);Check(vm.ElementColors[10].Color==original,"reset restores the original deterministic category palette");
        foreach(var colors in new ModelCategoryColor[][] {[new(1,"PLATE","transparent")],[new(7,"PLATE","#FFFFFF")],[new(1,"PLATE","#123456"),new(1,"PLATE","#654321")]})
            Reject(()=>ModelViewPresetStore.Validate([preset with {Appearance=preset.Appearance! with {CategoryColors=colors}}]),"invalid or duplicate category overrides are rejected in portable views");
        Check(model.Fingerprint()==before,"category colors and actions leave engineering data unchanged");
    }
}
