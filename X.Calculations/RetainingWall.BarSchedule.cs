using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    /// <summary>Tratto all'asse in m. Arc: arco sviluppato (Angle = angolo della piega); tratto rettilineo: estremi nella sezione e angolo
    /// della piega senza raggio al suo inizio (scarpa del fusto).</summary>
    public sealed record BarLeg(string Name, double Length)
    {
        public bool Arc { get; init; }
        public double Angle { get; init; }
        public BarPoint? From { get; init; }
        public BarPoint? To { get; init; }
    }
    public sealed record ScheduledBar(string Mark, string Zone, string Face, string Kind, double Diameter,
        int Quantity, double? CutLength, double Spacing, double Mandrel, double Lap,
        IReadOnlyList<BarLeg> Legs, IReadOnlyList<BarPoint> Shape, string Note)
    {
        public double? TotalLength => Quantity * CutLength;
        public double? Weight => TotalLength * Math.PI * Diameter * Diameter / 4 * .00785;
        /// <summary>Longitudinali: fili nella sezione e pezzi per filo; posizioni nella sezione (anche le righe degli spilli).</summary>
        public int? Lines { get; init; }
        public int? Pieces { get; init; }
        public IReadOnlyList<BarPoint> SectionPoints { get; init; } = [];
    }
    /// <summary>Riepilogo di un diametro: lunghezza [m], massa per metro [kg/m], peso [kg].</summary>
    public sealed record DiameterTotal(double Diameter, double Length, double KgPerMetre, double Weight);
    public sealed record BarSchedule(double PanelLength, double EndCover, double StockLength,
        IReadOnlyList<ScheduledBar> Bars, IReadOnlyList<string> Warnings)
    {
        public double KnownWeight => Bars.Sum(b => b.Weight ?? 0);
        public bool CompleteQuantities => Bars.All(b => b.CutLength is not null);
        public IReadOnlyList<DiameterTotal> Diameters { get; init; } = [];
        /// <summary>Calcestruzzo del tratto [m³] e incidenza [kg/m³].</summary>
        public double ConcreteVolume { get; init; }
        public double Incidence => ConcreteVolume > 0 ? KnownWeight / ConcreteVolume : 0;
    }

    /// <summary>The position in words, as in the legend of a reinforcement drawing.</summary>
    public static string BarPositionName(ScheduledBar bar) => bar.Kind switch
    {
        "Principale" when bar.Zone is "stem" or "stem_upper" => $"{RebarZoneName(bar.Zone)}, faccia {bar.Face}",
        "Principale" => (bar.Note.StartsWith("Barra unica", StringComparison.Ordinal) ? "Fondazione" : RebarZoneName(bar.Zone)) + ", " + bar.Face,
        "Forcella" => "Forcella di testa del fusto",
        "Spillo" => "Spilli della giunzione",
        _ => "Longitudinali " + (bar.Zone is "toe" or "heel" ? "fondazione" : RebarZoneName(bar.Zone).ToLowerInvariant()) + ", " + bar.Face
    };
    /// <summary>The legs of the shape in cm at the centre line («A = 47; arco 90° = 5; …»).</summary>
    public static string BarShapeText(ScheduledBar bar) => string.Join("; ", bar.Legs.Select(l => l.Name + " = " + (l.Length * 100).ToString("0", System.Globalization.CultureInfo.GetCultureInfo("it-IT"))
        + (!l.Arc && l.Angle > .05 && l.Angle < 179 ? " (piega " + l.Angle.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("it-IT")) + "°)" : "")));

    // This take-off is separate from both section checks and rendering. Units: m, mm for diameters/spacing.
    public static BarSchedule CalculateBarSchedule(JsonObject input) => CalculateBarScheduleNative(input);
    public static BarSchedule CalculateBarSchedule(Result result)
    {
        var schedule = CalculateBarSchedule(result.Input);
        int failed = result.Checks.Concat(result.Structural).Count(c => c.Ratio is null || c.Ratio > 1);
        return failed == 0 ? schedule : schedule with { Warnings = new[] { $"Risultato del muro: {failed} verifiche non soddisfatte o non disponibili. La distinta rappresenta i ferri inseriti, non certifica l’esito delle verifiche." }.Concat(schedule.Warnings).ToArray() };
    }
}
