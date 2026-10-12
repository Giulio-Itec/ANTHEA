using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

/// <summary>Independent, read-only section browser. Selection commands operate on the owning model view.</summary>
public sealed partial class ModelSectionViewModel : ObservableObject
{
    private readonly Action<int,bool> select;
    public ModelSectionViewModel(ModelSectionCatalog catalog,int? selectedId,Action<int,bool> select)
    {
        Sections=catalog.Sections;this.select=select;
        selectedSection=selectedId.HasValue?catalog.Find(selectedId.Value):null;
        selectedSection??=Sections.FirstOrDefault();
    }
    public IReadOnlyList<ModelSectionDetail> Sections { get; }
    [ObservableProperty] private ModelSectionDetail? selectedSection;
    [ObservableProperty] private bool showDimensions=true;
    [ObservableProperty] private bool showLocalAxes=true;
    [ObservableProperty] private bool showPrincipalAxes;
    [ObservableProperty] private bool showCentroid=true;
    [ObservableProperty] private bool showInsertion=true;
    public string Usage => SelectedSection==null?"Nessuna sezione":$"{SelectedSection.Elements.Length:N0} elementi associati";
    public string Materials => SelectedSection?.Materials.Length>0?string.Join(" · ",SelectedSection.Materials):"Materiale assegnato non disponibile nella copia";
    public string Diagnostics => SelectedSection==null?"":string.Join("\n",SelectedSection.Diagnostics);
    public string CentroidLabel => SelectedSection?.IsComposite==true?"Baricentro geometrico delle parti":"Baricentro";
    private bool CanSelect() => SelectedSection?.Elements.Length>0;
    partial void OnSelectedSectionChanged(ModelSectionDetail? value)
    {
        foreach(var p in new[]{nameof(Usage),nameof(Materials),nameof(Diagnostics),nameof(CentroidLabel)})OnPropertyChanged(p);
        SelectElementsCommand.NotifyCanExecuteChanged();IsolateElementsCommand.NotifyCanExecuteChanged();
    }
    [RelayCommand(CanExecute=nameof(CanSelect))] private void SelectElements()=>select(SelectedSection!.Id,false);
    [RelayCommand(CanExecute=nameof(CanSelect))] private void IsolateElements()=>select(SelectedSection!.Id,true);
}

public sealed partial class ModelViewerViewModel
{
    public ModelSectionCatalog? SectionCatalog { get; private set; }
    [ObservableProperty] private ModelSectionViewModel? sectionBrowser;
    [ObservableProperty] private bool isSectionWindowOpen;
    public event EventHandler? SectionWindowChanged;
    public ModelSectionDetail? SelectedSectionDetail
    {
        get
        {
            if(SelectedTreeItem?.Table=="SECT"&&SelectedTreeItem.Id is int id)return SectionCatalog?.Find(id);
            var ids=Snapshot?.Elements.Where(e=>SelectedElementIds.Contains(e.Id)&&ElementFamilies.IsLine(e.Type)).Select(e=>e.Property).Distinct().Take(2).ToArray();
            return ids?.Length==1?SectionCatalog?.Find(ids[0]):null;
        }
    }
    private bool CanOpenSections()=>SectionCatalog?.Sections.Count>0&&!disposed;
    partial void OnIsSectionWindowOpenChanged(bool value)=>SectionWindowChanged?.Invoke(this,EventArgs.Empty);
    [RelayCommand(CanExecute=nameof(CanOpenSections))]
    private void OpenSectionDetails()
    {
        if(SectionBrowser==null)SectionBrowser=new(SectionCatalog!,SelectedSectionDetail?.Id,SelectSectionElements);
        else if(SelectedSectionDetail is {} detail)SectionBrowser.SelectedSection=detail;
        IsSectionWindowOpen=true;SectionWindowChanged?.Invoke(this,EventArgs.Empty);
    }
    [RelayCommand] private void CloseSectionDetails()=>IsSectionWindowOpen=false;
    private void SelectSectionElements(int id,bool isolate)
    {
        if(FindTreeItem("SECT:"+id) is not {} item)return;
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
