using System.Globalization;
using System.Text.Json.Nodes;
using GPC.Checkers.CompositeBridge;
namespace Anthea.Application.Presentation;

public sealed record BridgeSteelProperties(IReadOnlyList<PresentationTable> Tables, string Notice);
public sealed record BridgePhaseProperties(string Title, string Description, string? Homogenization, PresentationTable Properties);

/// <summary>Property tables prepared with the same native section methods as the original view.</summary>
public static class BridgePropertyPresentation
{
    public static string Number(double value) => value.ToString("0.###", CultureInfo.GetCultureInfo("it-IT"));
    private static string F(double value) => Number(value);
    private static PresentationTable Properties(BridgeSectionProperties p, string? title = null, string? note = null, bool collapsible = false)
    {
        string V(double? value) => value.HasValue ? F(value.Value) : "—";
        return new(title ?? p.Name, ["Proprietà", "Valore", "Unità"], [
            ["A", V(p.Area / 100), "cm²"], ["xG", V(p.X), "mm"], ["yG", V(p.Y), "mm"],
            ["Ix", V(p.Ix / 1e4), "cm⁴"], ["Iy", V(p.Iy / 1e4), "cm⁴"], ["Ixy", "0", "cm⁴"],
            ["Wx sup", V(p.WTop / 1000), "cm³"], ["Wx inf", V(p.WBottom / 1000), "cm³"],
            ["ix", V(p.Rx), "mm"], ["iy", V(p.Ry), "mm"]], note, collapsible);
    }
    public static IReadOnlyList<PresentationTable> Slab(JsonObject data, JsonObject options, double modularRatio)
    {
        var g = BridgeSection.Geometry(data); var m = BridgeSection.Materials(data);
        var gross = BridgeSection.RectangleProperties("Solo calcestruzzo · lordo", g.Width, g.SlabHeight, 0, g.Width / 2);
        var transformed = BridgeSection.GrossPhaseProperties(data, options, slabOnly: true);
        var layout = BridgeSection.SlabLayout(data);
        var tables = new List<PresentationTable>();
        if (layout.HasPredalle) tables.Add(new("Quote predalle · solo geometria", ["Riferimento", "y [mm]"], [
            ["Intradosso soletta", "0"], ["Estradosso predalle", F(layout.PredalleThickness)], ["Estradosso soletta", F(layout.Height)]], BridgeSection.PredalleScope));
        tables.Add(Properties(gross));
        tables.Add(new("Armature", ["Fila", "As [cm²]", "y [mm]"], g.Bars.GroupBy(b => b.Y).OrderByDescending(p => p.Key)
            .Select(p => new[] { p.Count() + " barre", F(p.Sum(b => b.Area) / 100), F(p.Key) }).ToArray()));
        tables.Add(Properties(transformed, note: $"n barre = {F(BridgeDerivedResults.RebarModularRatio(modularRatio, m.Rebar.ElasticModulusTension, m.Steel.ElasticModulusTension))} · Ec,eff = {F(BridgeDerivedResults.EffectiveConcreteModulus(m.Steel.ElasticModulusTension, modularRatio))} MPa"));
        return tables;
    }
    public static BridgeSteelProperties Steel(JsonObject data)
    {
        var g = BridgeSection.Geometry(data); var parts = BridgeSection.SteelPartProperties(g);
        var real = BridgeSection.RealSteelSection(g);
        var tables = new List<PresentationTable> { Properties(BridgeSection.CombineProperties("Reale", parts),
            real is null ? "Carpenteria reale" : "Carpenteria · asse orizzontale (calcolo)",
            real is null ? null : "A, yG, Ix e W sono quelli della sezione reale; Iy e iy sono delle anime rese verticali (per la sezione reale vedere sotto).") };
        if (real is not null) tables.Add(new("Sezione reale nel piano (Model)", ["Proprietà", "Valore", "Unità"], [
            ["A", F(real.Area / 100), "cm²"], ["Ix", F(real.Jxx / 1e4), "cm⁴"], ["Iy", F(real.Jyy / 1e4), "cm⁴"],
            ["Ixy", F(real.Jxy / 1e4), "cm⁴"], ["Asse principale 1 da x", F(real.AngleX1 * 180 / Math.PI), "°"]],
            "Il calcolo N–Mx è in flessione retta attorno all'asse orizzontale: Ixy e la rotazione degli assi principali della sola carpenteria non sono considerati."));
        tables.AddRange(parts.Select(p => Properties(p, collapsible: true)));
        tables.Add(Properties(BridgeSection.CombineProperties("Equivalente", BridgeSection.SteelPartProperties(g, true)), "Carpenteria equivalente di calcolo", collapsible: true));
        return new(tables, BridgeSection.IsNonlinear(data) ? "Proprietà geometriche con φ/n memorizzati. Nel non lineare istantaneo il calcolo usa φ=0; i valori adottati sono in Fasi e proprietà." : "Proprietà geometriche, indipendenti dai carichi. Le riduzioni efficaci dipendono dalle sollecitazioni e sono nella scheda Fasi e tensioni.");
    }
    public static BridgePhaseProperties Phase(JsonObject data, JsonObject phase)
    {
        var props = BridgeSection.GrossPhaseProperties(data, phase);
        string? homogenization = null;
        if (BridgeSection.HasConcrete(phase.S("tipo")))
        {
            var h = BridgeSection.Homogenization(data, phase);
            homogenization = $"φ = {F(h.Phi)} · ψL = {F(phase.D("psi"))} · n = {F(h.N)}";
        }
        return new(phase.S("nome"), phase.S("tipo") + (phase.B("attiva") ? " · attiva" : " · esclusa dalla somma"), homogenization,
            Properties(props, note: "Geometria e omogeneizzazione della fase; stessi dati della tabella Sollecitazioni."));
    }
}
