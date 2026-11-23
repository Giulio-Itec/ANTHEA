using System.Text.Json.Nodes;
using Anthea.Application.Concrete;

namespace Anthea.Presentation.Modules.ConcreteSection;

public sealed partial class ConcreteSheetViewModel
{
    private bool showsActions3D;
    private string actions3DSource = "SLU";
    private string? actions3DCombination;
    private IReadOnlyList<ConcreteActionChoice> actions3DChoices = [];
    public bool ShowsActions3D { get => showsActions3D; set => SetProperty(ref showsActions3D, value); }
    public IReadOnlyList<ConcreteActionChoice> Actions3DSources { get; } = [new("SLU", "SLU"), new("SLV", "SLV"),
        new("SLE", "SLE rara"), new("SLE_FREQ", "SLE frequente"), new("SLE_QP", "SLE quasi permanente"), new("Taglio", "Taglio")];
    public string Actions3DSource
    {
        get => actions3DSource;
        set
        {
            if (value == actions3DSource || !Actions3DSources.Any(c => c.Id == value)) return;
            actions3DSource = value; actions3DCombination = null; NotifyActions3D();
        }
    }
    private IEnumerable<JsonObject> Actions3DRows => (actions3DSource == "Taglio" ? shearRows
        : actions.TryGetValue(actions3DSource, out var rows) ? rows : []).Select(r => r.Values);
    public IReadOnlyList<ConcreteActionChoice> Actions3DCombinations => actions3DChoices;
    public string? Actions3DCombinationId
    {
        get => actions3DChoices.FirstOrDefault(c => c.Id == actions3DCombination)?.Id ?? actions3DChoices.FirstOrDefault()?.Id;
        set
        {
            if (value is null || value == Actions3DCombinationId || !actions3DChoices.Any(c => c.Id == value)) return;
            actions3DCombination = value; NotifyActions3D();
        }
    }
    public ConcreteActionScene Actions3DScene => ConcreteActions3D.Read(Actions3DRows, Actions3DCombinationId, actions3DSource == "Taglio");
    private void NotifyActions3D()
    {
        var choices = ConcreteActions3D.Choices(Actions3DRows);
        if (!actions3DChoices.SequenceEqual(choices))
        {
            actions3DChoices = choices; OnPropertyChanged(nameof(Actions3DCombinations));
        }
        OnPropertyChanged(nameof(Actions3DSource)); OnPropertyChanged(nameof(Actions3DCombinationId)); OnPropertyChanged(nameof(Actions3DScene));
    }
}
