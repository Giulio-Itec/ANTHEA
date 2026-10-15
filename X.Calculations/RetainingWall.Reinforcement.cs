using System.Text.Json.Nodes;
using Materiali;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public static string RebarZoneName(string key) => key switch { "stem" => "Fusto inferiore / intero", "stem_upper" => "Fusto superiore", "toe" => "Mensola valle", "heel" => "Mensola monte", _ => key };
    public sealed record BarPoint(double X, double Y);
    public sealed record BarDetail(string Mark, string Zone, string Face, double Diameter, double Count,
        double Spacing, double Area, double Length, double RequiredAnchor, double Anchor,
        double RequiredLap, double Lap, double Mandrel, double Fbd, IReadOnlyList<BarPoint> Path);
    public sealed record ReinforcementDetails(IReadOnlyList<BarDetail> Bars, IReadOnlyList<Check> Checks, double SteelKg, string Note);
    internal const string DetailingScopeNote = "Schema di predimensionamento per metro: principali, rete secondaria e collegamenti della giunzione. Ancoraggi a fyd senza riduzioni favorevoli; 100% giunzioni a h₁. Sfridi, giunti di costruzione, estremità lungo muro e piano di posa delle barre fuori piano richiedono il disegno esecutivo. Non è una distinta di officina.";
    public sealed record ReinforcementProposal(JsonObject Input, IReadOnlyList<Check> Checks, bool Passed, string Message);

    /// <summary>Dettagli d'armatura del muro a mensola (GPC.Design). Il percorso storico è LegacyRetainingWall in tests/ANTHEA.Legacy
    /// (decisione D3 del 15/10/2026).</summary>
    public static ReinforcementDetails CalculateReinforcementDetails(JsonObject input) => CalculateReinforcementDetailsNative(input);

    /// <summary>Ricerca delle armature del muro a mensola (GPC.Design); la ricerca precedente è LegacyRetainingWall.DesignReinforcement.</summary>
    public static ReinforcementProposal DesignReinforcement(JsonObject input, CancellationToken token = default) => DesignReinforcementNative(input, token);
}
