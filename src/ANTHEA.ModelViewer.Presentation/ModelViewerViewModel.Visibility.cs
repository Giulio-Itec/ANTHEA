using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ANTHEA.ModelViewer.Presentation;

public sealed record VisibilityCriterion(string Key, string Label, IReadOnlySet<int> Elements)
{
    public override string ToString() => $"{Label} · {Elements.Count:N0}";
}

public sealed record SavedVisibility(string Name, IReadOnlySet<int> Elements)
{
    public override string ToString() => $"{Name} · {Elements.Count:N0}";
}

public sealed partial class ModelViewerViewModel
{
    public IReadOnlyList<string> VisibilityCombinations { get; } = ["Tutti i criteri · intersezione", "Almeno un criterio · unione"];
    public ObservableCollection<VisibilityCriterion> VisibilityCriteria { get; } = [];
    public ObservableCollection<SavedVisibility> SavedVisibilities { get; } = [];
    [ObservableProperty] private IReadOnlyList<VisibilityCriterion> visibilityChoices = [];
    [ObservableProperty] private VisibilityCriterion? selectedVisibilityChoice;
    [ObservableProperty] private int visibilityCombination;
    [ObservableProperty] private bool showVisibilityOptions;
    [ObservableProperty] private string visibilityName = "";
    [ObservableProperty] private SavedVisibility? selectedSavedVisibility;
    [ObservableProperty] private string visibilityMessage = "Viste temporanee, disponibili finché il modello resta aperto.";
    public string VisibilityFilterTitle => VisibilityCriteria.Count == 0 ? "Filtri e viste" : $"Filtri e viste · {VisibilityCriteria.Count} criteri";

    void InitializeVisibility()
    {
        ClearVisibilityCriteria(); SavedVisibilities.Clear(); SelectedSavedVisibility = null; VisibilityName = "";
        VisibilityMessage = "Viste temporanee, disponibili finché il modello resta aperto.";
        var ids = Snapshot!.Elements.Select(e => e.Id).ToHashSet();
        VisibilityChoices = ModelTreeBuilder.Descendants(allTree)
            .Where(n => FamilyKeys.Contains(n.Key) || n.Id.HasValue && n.Table is "MATL" or "SECT" or "THIK" or "GRUP")
            .DistinctBy(n => n.Key)
            .Select(n => new VisibilityCriterion(n.Key, (n.Table switch { "MATL" => "Materiale", "SECT" => "Sezione", "THIK" => "Spessore", "GRUP" => "Gruppo", _ => "Famiglia" }) + " · " + n.Label,
                n.Elements.Where(ids.Contains).ToHashSet())).ToArray();
        SelectedVisibilityChoice = VisibilityChoices.FirstOrDefault();
    }
    void ClearVisibilityCriteria()
    {
        VisibilityCriteria.Clear(); OnPropertyChanged(nameof(VisibilityFilterTitle));
    }
    partial void OnVisibilityCombinationChanged(int value) { if (VisibilityCriteria.Count > 0) ApplyVisibilityCriteria(); }
    partial void OnSelectedVisibilityChoiceChanged(VisibilityCriterion? value) => AddVisibilityCriterionCommand.NotifyCanExecuteChanged();
    bool CanAddVisibilityCriterion() => !disposed && SelectedVisibilityChoice != null;
    [RelayCommand(CanExecute = nameof(CanAddVisibilityCriterion))]
    void AddVisibilityCriterion()
    {
        if (SelectedVisibilityChoice is not { } item || VisibilityCriteria.Any(c => c.Key == item.Key)) return;
        VisibilityCriteria.Add(item); ApplyVisibilityCriteria();
    }
    [RelayCommand] void RemoveVisibilityCriterion(VisibilityCriterion? item)
    {
        if (item != null && VisibilityCriteria.Remove(item)) ApplyVisibilityCriteria();
    }
    [RelayCommand] void ResetVisibilityCriteria() { ClearVisibilityCriteria(); isolatedElements = null; hiddenElements.Clear(); ApplyVisibility(); }
    void ApplyVisibilityCriteria()
    {
        isolatedElements = VisibilityCriteria.Count == 0 ? null : VisibilityCriteria[0].Elements.ToHashSet();
        foreach (var criterion in VisibilityCriteria.Skip(1))
            if (VisibilityCombination == 0) isolatedElements!.IntersectWith(criterion.Elements);
            else isolatedElements!.UnionWith(criterion.Elements);
        hiddenElements.Clear(); OnPropertyChanged(nameof(VisibilityFilterTitle)); ApplyVisibility();
        VisibilityMessage = VisibilityCriteria.Count == 0 ? "Nessun filtro per attributi." : VisibilitySummary;
    }
    [RelayCommand] void SaveVisibility()
    {
        if (disposed || Snapshot == null) return;
        string name = VisibilityName.Trim();
        if (name.Length == 0 || name.Length > 80) { VisibilityMessage = "Assegna alla vista un nome da 1 a 80 caratteri."; return; }
        if (SavedVisibilities.Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { VisibilityMessage = "Nome già presente: scegli un nome diverso."; return; }
        var saved = new SavedVisibility(name, (VisibleElementIds ?? Snapshot.Elements.Select(e => e.Id).ToHashSet()).ToHashSet());
        SavedVisibilities.Add(saved); SelectedSavedVisibility = saved; VisibilityName = "";
        VisibilityMessage = "Visibilità memorizzata per questa sessione. Orientamento e risultati restano indipendenti.";
    }
    [RelayCommand] void RecallVisibility()
    {
        if (disposed || SelectedSavedVisibility is not { } saved || !SavedVisibilities.Contains(saved)) return;
        ClearVisibilityCriteria(); hiddenElements.Clear(); isolatedElements = saved.Elements.ToHashSet(); ApplyVisibility();
        VisibilityMessage = $"Vista «{saved.Name}» · {VisibilitySummary}";
    }
    [RelayCommand] void RemoveSavedVisibility()
    {
        if (SelectedSavedVisibility is not { } saved) return;
        SavedVisibilities.Remove(saved); SelectedSavedVisibility = SavedVisibilities.FirstOrDefault();
    }
}
