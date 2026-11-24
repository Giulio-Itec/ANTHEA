using Anthea.Application.Concrete;

namespace Anthea.Presentation.Modules.QuickSection;

public sealed partial class QuickSectionSheetViewModel
{
    private bool showActions;
    private string actionsSource = "Momento";
    private string? actionsCombination;
    private IReadOnlyList<ConcreteActionChoice> actionCombinations = [];
    public IReadOnlyList<string> ActionSources { get; } = ["Momento", "Taglio"];
    public bool ShowsActions => showActions;
    public bool ShowsChart => !showSection && !showActions;
    public string ActionsSource
    {
        get => actionsSource;
        set
        {
            if (value == actionsSource || !ActionSources.Contains(value)) return;
            actionsSource = value; actionsCombination = null; NotifyActions();
        }
    }
    public IReadOnlyList<ConcreteActionChoice> ActionCombinations => actionCombinations;
    public string? ActionCombinationId
    {
        get
        {
            var choices = ActionCombinations;
            string? current = actionsCombination ?? (actionsSource == "Taglio" ? SelectedShearCombinationId : SelectedCombinationId);
            return choices.FirstOrDefault(c => c.Id == current)?.Id ?? choices.FirstOrDefault()?.Id;
        }
        set
        {
            if (value is null || value == ActionCombinationId || !ActionCombinations.Any(c => c.Id == value)) return;
            actionsCombination = value; NotifyActions();
        }
    }
    public ConcreteActionScene ActionsScene => ConcreteQuickActions.Read(Data, actionsSource == "Taglio", ActionCombinationId);
    private void NotifyActions()
    {
        var choices = ConcreteQuickActions.Choices(Data, actionsSource == "Taglio");
        if (!actionCombinations.SequenceEqual(choices))
        {
            actionCombinations = choices;
            OnPropertyChanged(nameof(ActionCombinations));
        }
        foreach (string property in new[] { nameof(ActionsSource), nameof(ActionCombinationId), nameof(ActionsScene) }) OnPropertyChanged(property);
    }
    private void FollowSelectedActionCombination(string source)
    {
        if (actionsSource == source) actionsCombination = null;
        NotifyActions();
    }
}
