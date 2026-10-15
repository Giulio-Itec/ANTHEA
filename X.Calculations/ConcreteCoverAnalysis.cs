using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public sealed record ConcreteCoverResult(double Adopted, double MaterialMinimum, double Required, double? MaximumBarDiameter,
    string? ReinforcementError)
{
    public bool? Passed => ConcreteDurabilityAdapter.AssessCover(Adopted, Required, ReinforcementError is null ? Required : null).Passed;
}

public static class ConcreteCoverAnalysis
{
    /// <summary>Checks material-sheet and actual-bar cover minima without changing either input.</summary>
    public static ConcreteCoverResult Calculate(JsonObject input, JsonObject material, double fck)
    {
        double minimum = Materiali.MaterialCover.Required(material, fck);
        double adopted = input.Required("cover_mm");
        double? actualMinimum = null;
        double? diameter = null; string? error = null;
        try
        {
            var section = new SezioneCA(input);
            if (section.Bars.Count == 0) throw new ArgumentException("armatura non definita");
            diameter = section.Bars.Max(b => b.Diametro);
            actualMinimum = Materiali.MaterialCover.Required(material, fck, diameter.Value);
        }
        catch (ArgumentException ex) { error = ex.Message; }
        return Assess(adopted, minimum, actualMinimum, diameter, error);
    }
    /// <summary>Same assessment when the actual reinforcement is already read, such as for a wall.</summary>
    public static ConcreteCoverResult CalculateForBars(double adopted, JsonObject material, double fck,
        double maximumBarDiameter)
        => Assess(adopted, Materiali.MaterialCover.Required(material, fck),
            Materiali.MaterialCover.Required(material, fck, maximumBarDiameter), maximumBarDiameter, null);

    private static ConcreteCoverResult Assess(double adopted, double minimum, double? actualMinimum,
        double? diameter, string? error)
    {
        var assessment = ConcreteDurabilityAdapter.AssessCover(adopted, minimum, actualMinimum);
        return new(adopted, minimum, assessment.Required, diameter, error);
    }

}
