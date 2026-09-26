using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public sealed record ConcreteCoverResult(double Adopted, double MaterialMinimum, double Required, double? MaximumBarDiameter,
    string? ReinforcementError)
{
    public bool? Passed => Adopted < Required ? false : ReinforcementError is not null ? null : true;
}

public static class ConcreteCoverAnalysis
{
    /// <summary>Checks material-sheet and actual-bar cover minima without changing either input.</summary>
    public static ConcreteCoverResult Calculate(JsonObject input, JsonObject material, double fck)
    {
        double minimum = Materiali.MaterialCover.Required(material, fck);
        double adopted = input.Required("cover_mm"), required = minimum;
        double? diameter = null; string? error = null;
        try
        {
            var section = new SezioneCA(input);
            if (section.Bars.Count == 0) throw new ArgumentException("armatura non definita");
            diameter = section.Bars.Max(b => b.Diametro);
            required = Math.Max(minimum, Materiali.MaterialCover.Required(material, fck, diameter.Value));
        }
        catch (ArgumentException ex) { error = ex.Message; }
        return new(adopted, minimum, required, diameter, error);
    }
}
