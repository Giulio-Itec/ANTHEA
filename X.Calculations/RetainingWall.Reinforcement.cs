using System.Text.Json.Nodes;
using Materiali;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public static string RebarZoneName(string key) => key switch { "stem" => "Fusto inferiore / intero", "stem_upper" => "Fusto superiore", "toe" => "Mensola valle", "heel" => "Mensola monte", _ => key };
    public sealed record BarPoint(double X, double Y);
    public sealed record BarDetail(string Mark, string Zone, string Face, double Diameter, double Count,
        double Spacing, double Area, double Length, double RequiredAnchor, double Anchor,
        double RequiredLap, double Lap, double Mandrel, double Fbd, IReadOnlyList<BarPoint> Path)
    {
        /// <summary>Piede delle barre del fusto: «valle», «monte» o vuoto; «Continuous» per la barra unica di fondazione.</summary>
        public string Foot { get; init; } = "";
        public bool Continuous { get; init; }
    }
    /// <summary>Altre posizioni della sezione (GPC.Design, 1.16.0): forcella di testa, spilli della giunzione, ferri longitudinali.
    /// Kind «Forcella», «Spillo» o «Longitudinale»; Path in m all'asse; Points: righe degli spilli o posizioni dei longitudinali.</summary>
    public sealed record SectionBar(string Mark, string Kind, string Zone, string Face, double Diameter, double Spacing, double Length,
        IReadOnlyList<BarPoint> Path, IReadOnlyList<BarPoint> Points);
    public sealed record ReinforcementDetails(IReadOnlyList<BarDetail> Bars, IReadOnlyList<Check> Checks, double SteelKg, string Note)
    {
        public IReadOnlyList<SectionBar> Others { get; init; } = [];
    }
    internal const string DetailingScopeNote = "Armature per metro di muro: principali al passo 1000/n, piedi del fusto incrociati in fondazione e appoggiati sulla rete inferiore, barre di fondazione con risvolti ai bordi liberi, forcelle di testa, spilli nella giunzione e ferri longitudinali nello strato interno. Ancoraggi a fyd senza riduzioni favorevoli; 100% giunzioni a h₁. Peso netto, senza sfridi.";
    public sealed record ReinforcementProposal(JsonObject Input, IReadOnlyList<Check> Checks, bool Passed, string Message);

    /// <summary>Dettagli d'armatura del muro a mensola (GPC.Design). Il percorso storico è LegacyRetainingWall in tests/ANTHEA.Legacy
    /// (decisione D3 del 15/10/2026).</summary>
    public static ReinforcementDetails CalculateReinforcementDetails(JsonObject input) => CalculateReinforcementDetailsNative(input);

    /// <summary>Ricerca delle armature del muro a mensola (GPC.Design); la ricerca precedente è LegacyRetainingWall.DesignReinforcement.</summary>
    public static ReinforcementProposal DesignReinforcement(JsonObject input, CancellationToken token = default) => DesignReinforcementNative(input, token);
}
