using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

/// <summary>Independent, read-only section browser: the sections of the members and, since 2/11/2026, the thicknesses of the plates.
/// Selection commands operate on the owning model view.</summary>
public sealed partial class ModelSectionViewModel : ObservableObject
{
    private readonly Action<ModelSectionDetail,bool> select;
    public ModelSectionViewModel(ModelSectionCatalog catalog,ModelSectionDetail? selected,Action<ModelSectionDetail,bool> select)
    {
        Sections=[..catalog.Sections,..catalog.Thicknesses];this.select=select;
        selectedSection=selected??Sections.FirstOrDefault();
    }
    public IReadOnlyList<ModelSectionDetail> Sections { get; }
    [ObservableProperty] private ModelSectionDetail? selectedSection;
    [ObservableProperty] private bool showDimensions=true;
    [ObservableProperty] private bool showLocalAxes=true;
    [ObservableProperty] private bool showPrincipalAxes;
    [ObservableProperty] private bool showCentroid=true;
    [ObservableProperty] private bool showInsertion=true;
    public string Usage => SelectedSection==null?"Nessuna sezione":$"{SelectedSection.Elements.Length:N0} {(SelectedSection.IsPlateThickness?"piastre associate":"elementi associati")}";
    public string Materials => SelectedSection?.Materials.Length>0?string.Join(" · ",SelectedSection.Materials):"Materiale assegnato non disponibile nella copia";
    public string Diagnostics => SelectedSection==null?"":string.Join("\n",SelectedSection.Diagnostics);
    public string CentroidLabel => SelectedSection?.IsComposite==true?"Baricentro geometrico delle parti":"Baricentro";
    private bool CanSelect() => SelectedSection?.Elements.Length>0;
    partial void OnSelectedSectionChanged(ModelSectionDetail? value)
    {
        foreach(var p in new[]{nameof(Usage),nameof(Materials),nameof(Diagnostics),nameof(CentroidLabel)})OnPropertyChanged(p);
        SelectElementsCommand.NotifyCanExecuteChanged();IsolateElementsCommand.NotifyCanExecuteChanged();
    }
    [RelayCommand(CanExecute=nameof(CanSelect))] private void SelectElements()=>select(SelectedSection!,false);
    [RelayCommand(CanExecute=nameof(CanSelect))] private void IsolateElements()=>select(SelectedSection!,true);
}

public sealed partial class ModelViewerViewModel
{
    public ModelSectionCatalog? SectionCatalog { get; private set; }
    [ObservableProperty] private ModelSectionViewModel? sectionBrowser;
    [ObservableProperty] private bool isSectionWindowOpen;
    public event EventHandler? SectionWindowChanged;
    /// <summary>The section of the selected members, or the thickness of the selected plates when only plates of one property are selected.</summary>
    public ModelSectionDetail? SelectedSectionDetail
    {
        get
        {
            if(SelectedTreeItem?.Table=="SECT"&&SelectedTreeItem.Id is int id)return SectionCatalog?.Find(id);
            if(SelectedTreeItem?.Table=="THIK"&&SelectedTreeItem.Id is int plate)return SectionCatalog?.FindThickness(plate);
            var selected=Snapshot?.Elements.Where(e=>SelectedElementIds.Contains(e.Id)).ToArray()??[];
            var ids=selected.Where(e=>ElementFamilies.IsLine(e.Type)).Select(e=>e.Property).Distinct().Take(2).ToArray();
            if(ids.Length==1)return SectionCatalog?.Find(ids[0]);
            var thicknesses=selected.Where(e=>e.Type=="PLATE").Select(e=>e.Property).Distinct().Take(2).ToArray();
            return ids.Length==0&&thicknesses.Length==1?SectionCatalog?.FindThickness(thicknesses[0]):null;
        }
    }
    private bool CanOpenSections()=>(SectionCatalog?.Sections.Count>0||SectionCatalog?.Thicknesses.Count>0)&&!disposed;
    partial void OnIsSectionWindowOpenChanged(bool value)=>SectionWindowChanged?.Invoke(this,EventArgs.Empty);
    [RelayCommand(CanExecute=nameof(CanOpenSections))]
    private void OpenSectionDetails()
    {
        if(SectionBrowser==null)SectionBrowser=new(SectionCatalog!,SelectedSectionDetail,SelectSectionElements);
        else if(SelectedSectionDetail is {} detail)SectionBrowser.SelectedSection=detail;
        IsSectionWindowOpen=true;SectionWindowChanged?.Invoke(this,EventArgs.Empty);
    }
    [RelayCommand] private void CloseSectionDetails()=>IsSectionWindowOpen=false;
    private void SelectSectionElements(ModelSectionDetail detail,bool isolate)
    {
        if(FindTreeItem(detail.Table+":"+detail.Id) is not {} item)return;
        SelectedTreeItem=item;ShowInspector=true;
        if(isolate)IsolateCommand.Execute(null);
    }
    private void InitializeSections()
    {
        SectionCatalog=Snapshot==null?null:ModelSectionCatalog.Create(Snapshot);
        SectionBrowser=SectionCatalog==null?null:new(SectionCatalog,null,SelectSectionElements);
        OnPropertyChanged(nameof(SectionCatalog));OnPropertyChanged(nameof(SelectedSectionDetail));
        OpenSectionDetailsCommand.NotifyCanExecuteChanged();
        if(IsSectionWindowOpen)SectionWindowChanged?.Invoke(this,EventArgs.Empty);
    }
}
