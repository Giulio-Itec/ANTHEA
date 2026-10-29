namespace ANTHEA.ModelWorkspace;

/// <summary>1.16.0: names and units of the support reactions of a result case (<see cref="ModelReaction"/>), in global axes.</summary>
public static class ModelReactions
{
    public static readonly IReadOnlyList<string> Components = ["Fx", "Fy", "Fz", "Mx", "My", "Mz"];
    public static string Unit(string component) => component.StartsWith('M') ? "kN·m" : "kN";
}
