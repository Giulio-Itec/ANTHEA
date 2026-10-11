using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    readonly Dictionary<(int Mode,string Key),ModelCategoryColor> categoryColors=[];
    public IReadOnlyList<string> CategoryPalette {get;}=["#71B6E8","#E7AF66","#73C5AC","#BB9FE4","#E58685","#A6C878","#67C3D1","#D9A3CC","#EBEDF2","#707C90","#3478C8","#C76141"];
    [ObservableProperty] IReadOnlyList<DisplayCategory> colorCategories=[];
    [ObservableProperty] string? selectedColorCategoryKey;
    [ObservableProperty] string categoryColorText="#71B6E8";
    [ObservableProperty] string categoryColorMessage="";
    public string CategoryColorPreview=>ModelCategoryColor.IsColor(CategoryColorText)?CategoryColorText:"#8994A3";
    public string CategorySelectionSummary
    {
        get
        {
            var ids=CategoryIds(SelectedColorCategoryKey);
            int visible=ids.Count(id=>VisibleElementIds==null||VisibleElementIds.Contains(id));
            return $"{visible:N0} visibili su {ids.Length:N0} associati. Seleziona agisce sui visibili; Isola, Nascondi e Mostra sull’intera categoria.";
        }
    }
    partial void OnSelectedColorCategoryKeyChanged(string? value)=>UpdateCategoryEditor();
    partial void OnCategoryColorTextChanged(string value)
    {
        OnPropertyChanged(nameof(CategoryColorPreview));ApplyCategoryColorCommand.NotifyCanExecuteChanged();
        CategoryColorMessage=ModelCategoryColor.IsColor(value)?"":"Usa un colore #RRGGBB, per esempio #71B6E8.";
    }
    void UpdateColorCategories()
    {
        var categories=ElementColors.Values.GroupBy(e=>e.Key).OrderBy(g=>g.Key,StringComparer.Ordinal)
            .Select(g=>new DisplayCategory(g.Key,g.First().Label,g.First().Color,g.Count())).ToArray();
        string? key=SelectedColorCategoryKey;
        string? previousColor=ColorCategories.FirstOrDefault(c=>c.Key==key)?.Color;
        if(!ColorCategories.SequenceEqual(categories))ColorCategories=categories;
        SelectedColorCategoryKey=categories.Any(c=>c.Key==key)?key:categories.FirstOrDefault()?.Key;
        if(previousColor!=categories.FirstOrDefault(c=>c.Key==SelectedColorCategoryKey)?.Color)UpdateCategoryEditor();
        OnPropertyChanged(nameof(CategorySelectionSummary));
        ResetCategoryColorsCommand.NotifyCanExecuteChanged();
    }
    void UpdateCategoryEditor()
    {
        CategoryColorText=ColorCategories.FirstOrDefault(c=>c.Key==SelectedColorCategoryKey)?.Color??"#71B6E8";
        CategoryColorMessage="";
        OnPropertyChanged(nameof(CategorySelectionSummary));
        ApplyCategoryColorCommand.NotifyCanExecuteChanged();ResetCategoryColorCommand.NotifyCanExecuteChanged();
    }
    int[] CategoryIds(string? key)=>ElementColors.Where(e=>e.Value.Key==key).Select(e=>e.Key).ToArray();
    bool CanApplyCategoryColor()=>SelectedColorCategoryKey!=null&&ModelCategoryColor.IsColor(CategoryColorText);
    bool CanResetCategoryColor()=>SelectedColorCategoryKey!=null&&categoryColors.ContainsKey((SelectedColorMode,SelectedColorCategoryKey));
    bool CanResetCategoryColors()=>categoryColors.Count>0;
    [RelayCommand(CanExecute=nameof(CanApplyCategoryColor))] void ApplyCategoryColor()
    {
        string key=SelectedColorCategoryKey!;
        categoryColors[(SelectedColorMode,key)]=new(SelectedColorMode,key,CategoryColorText.ToUpperInvariant());
        RefreshScene();UpdateCategoryEditor();
    }
    [RelayCommand] void ChooseCategoryColor(string? color)
    {
        if(!ModelCategoryColor.IsColor(color))return;
        CategoryColorText=color!;if(CanApplyCategoryColor())ApplyCategoryColor();
    }
    [RelayCommand(CanExecute=nameof(CanResetCategoryColor))] void ResetCategoryColor()
    {categoryColors.Remove((SelectedColorMode,SelectedColorCategoryKey!));RefreshScene();UpdateCategoryEditor();}
    [RelayCommand(CanExecute=nameof(CanResetCategoryColors))] void ResetCategoryColors()
    {categoryColors.Clear();RefreshScene();UpdateCategoryEditor();}
    [RelayCommand] void EditColorCategory(string? key)
    {if(!ElementColors.Values.Any(v=>v.Key==key))return;SelectedColorCategoryKey=key;SelectedPanel=2;ShowDisplayOptions=true;}
    [RelayCommand] void SelectColorCategory(string? key)
    {SelectedColorCategoryKey=key;SelectionTarget=0;SelectElements(CategoryIds(key));}
    [RelayCommand] void IsolateColorCategory(string? key)
    {
        var ids=CategoryIds(key);if(ids.Length==0)return;
        SelectedColorCategoryKey=key;ClearVisibilityCriteria();isolatedElements=ids.ToHashSet();hiddenElements.ExceptWith(ids);ApplyVisibility();
    }
    [RelayCommand] void HideColorCategory(string? key)
    {SelectedColorCategoryKey=key;hiddenElements.UnionWith(CategoryIds(key));ApplyVisibility();}
    [RelayCommand] void ShowColorCategory(string? key)
    {
        var ids=CategoryIds(key);SelectedColorCategoryKey=key;ClearVisibilityCriteria();
        hiddenElements.ExceptWith(ids);isolatedElements?.UnionWith(ids);ApplyVisibility();
    }
}
