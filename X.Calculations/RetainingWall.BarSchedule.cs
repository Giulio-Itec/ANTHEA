using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public sealed record BarLeg(string Name, double Length);
    public sealed record ScheduledBar(string Mark, string Zone, string Face, string Kind, double Diameter,
        int Quantity, double? CutLength, double Spacing, double Mandrel, double Lap,
        IReadOnlyList<BarLeg> Legs, IReadOnlyList<BarPoint> Shape, string Note)
    {
        public double? TotalLength => Quantity * CutLength;
        public double? Weight => TotalLength * Math.PI * Diameter * Diameter / 4 * .00785;
    }
    public sealed record BarSchedule(double PanelLength, double EndCover, double StockLength,
        IReadOnlyList<ScheduledBar> Bars, IReadOnlyList<string> Warnings)
    {
        public double KnownWeight => Bars.Sum(b => b.Weight ?? 0);
        public bool CompleteQuantities => Bars.All(b => b.CutLength is not null);
    }

    // This take-off is separate from both section checks and rendering. Units: m, mm for diameters/spacing.
    public static BarSchedule CalculateBarSchedule(JsonObject input) => CalculateBarScheduleNative(input);
    public static BarSchedule CalculateBarSchedule(Result result)
    {
        var schedule = CalculateBarSchedule(result.Input);
        int failed = result.Checks.Concat(result.Structural).Count(c => c.Ratio is null || c.Ratio > 1);
        return failed == 0 ? schedule : schedule with { Warnings = new[] { $"Risultato del muro: {failed} verifiche non soddisfatte o non disponibili. La distinta rappresenta i ferri inseriti, non certifica l’esito delle verifiche." }.Concat(schedule.Warnings).ToArray() };
    }
}
