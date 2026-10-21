using System.Text.Json.Nodes;

namespace Anthea.Application.BridgeDesign;

/// <summary>
/// «Annulla» of the Bridge Design sheet (F5.12): the states of the data before the edits of the user, the most recent first. Moved without
/// changes from the historic view (BridgeDesignWorkspace), which uses it; the ViewModel of F5.12 uses the same class.
/// </summary>
public sealed class BridgeDesignHistory
{
    private const int Limit = 80, Kept = 60;
    private readonly Stack<JsonObject> states = new();
    private JsonObject previous;

    /// <param name="data">The data at the opening: the state that the first edit puts on the stack.</param>
    public BridgeDesignHistory(JsonObject data) => previous = (JsonObject)data.DeepClone();

    /// <summary>The states that «Annulla» can restore.</summary>
    public int Count => states.Count;

    /// <summary>
    /// After an edit: when the data differ from the last state recorded, that state goes on the stack (beyond 80 states only the 60 most
    /// recent are kept) and the data become the last state.
    /// </summary>
    public void Record(JsonObject data)
    {
        if (JsonNode.DeepEquals(previous, data)) return;
        states.Push(previous);
        if (states.Count > Limit) { var recent = states.Take(Kept).Reverse().ToArray(); states.Clear(); foreach (var state in recent) states.Push(state); }
        previous = (JsonObject)data.DeepClone();
    }

    /// <summary>«Annulla»: the data become the last state on the stack, which becomes the last state recorded; false with an empty stack.</summary>
    public bool Undo(JsonObject data)
    {
        if (!states.TryPop(out var snapshot)) return false;
        data.Clear(); foreach (var pair in snapshot) data[pair.Key] = pair.Value?.DeepClone();
        previous = (JsonObject)data.DeepClone();
        return true;
    }
}
