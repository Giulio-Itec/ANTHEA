using System.Globalization;
using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Checkers.Concrete.Walls;
using GPC.Design.Walls;
using GPC.Model;
using GPC.Model.Checking.Contracts;
using GPC.Model.Standards;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    // Resolve the unit factors once and preserve a single multiplication, as in the captured path.
    private static readonly double WallForceToNative = Units.ConvertForceToDefault(1, Units.Knm);
    private static readonly double WallMomentToNative = Units.ConverMomentToDefault(1, Units.Knm);
    /// <summary>Stato della fessurazione di una zona tesa senza armatura quando manca il risultato della sezione (testo delle sezioni, E1-D4).</summary>
    public static readonly string UncontrolledCrackStatus = ConcreteLibraryMapping.OuterStops[CrackOutcome.NoTensileReinforcement] + ConcreteLibraryMapping.UnreinforcedTensileZoneFailure;
    private static WallMember NativeWallMember(string member) => member switch
    {
        "Fusto" => WallMember.Stem, "Valle" => WallMember.Toe, "Monte" => WallMember.Heel,
        _ => throw new ArgumentException("Parte del muro non riconosciuta: " + member)
    };

    /// <summary>Document/unit adapter. Section mechanics, detailing and quantities belong to GPC.
    /// The existing prepared section preserves the mesh and transfers exclusive ownership to its session.</summary>
    private static List<Check> StructuralChecksNative(JsonObject d, List<LoadCase> cases, CancellationToken token, out double steelKg)
    {
        token.ThrowIfCancellationRequested();
        steelKg = WallRebarQuantities.PrincipalSteelKgPerMetre(NativeDetailInput(d, includeInactiveQuantities: true));
        var result = new List<Check>();
        var sessions = new Dictionary<(string, double), WallSectionSession>();
        var material = d["materials"]!;
        foreach (var c in cases)
        foreach (var f in c.Sections.Where(s => s.Position > 0 || s.N != 0 || s.V != 0 || s.M != 0))
        {
            token.ThrowIfCancellationRequested();
            string label = f.Name + " z=" + f.Position.ToString("0.00", CultureInfo.InvariantCulture) + " m";
            if (f.Name != "Fusto" && !c.Contact.Valid)
            {
                result.Add(CheckValue(label + " · resistenza", c.Name, 0, null, "kNm/m", "Reazioni non disponibili: perdita di equilibrio"));
                continue;
            }
            var key = (ReinforcementKey(d, f.Name, f.Position), f.Thickness);
            if (!sessions.TryGetValue(key, out var session))
            {
                var input = SectionInput(d, f.Name, f.Thickness, f.Position);
                var document = SezioneCA.DefaultData(); document["input"] = input.DeepClone();
                var workspace = SectionWorkspace.Prepare(document);
                var standard = ConcreteStandards.Effective(input, workspace) as StandardNTC2018Concrete
                    ?? throw new NotSupportedException("Le sezioni del muro richiedono il profilo NTC 2018.");
                var strip = new WallStrip(NativeWallMember(f.Name), input.Required("width_mm"), input.Required("height_mm"), input.Required("cover_mm"),
                    (int)input.Required("top_bar_count"), input.Required("top_bar_diameter_mm"),
                    (int)input.Required("bottom_bar_count"), input.Required("bottom_bar_diameter_mm"));
                var prepared = CheckerSection.PrepareModel(input, workspace);
                session = new(strip, prepared.Section, standard, material.Required("creep"), material.S("exposure"));
                sessions.Add(key, session);
                var areas = session.CheckLongitudinalAreas();
                Add(areas.MinimumPerFace, "armatura minima per faccia", "Dettagli");
                Add(areas.MaximumTotal, "armatura massima", "Dettagli");
            }
            var actions = new WallSectionActions(-f.N * WallForceToNative,
                f.V * WallForceToNative, f.M * WallMomentToNative);
            if (c.State is "SLU" or "SISMA" or "ECCEZIONALE")
            {
                Add(session.CheckMoment(actions, token), "N–M GPC", c.Name,
                    "GPC: resistenza non convergente o N fuori dominio");
                Add(session.CheckShear(actions, token).Check, "taglio senza staffe", c.Name);
            }
            else
            {
                var combination = c.State switch
                {
                    "SLE" => ServiceabilityCombination.Characteristic,
                    "SLE_FREQ" => ServiceabilityCombination.Frequent,
                    "SLE_QP" => ServiceabilityCombination.QuasiPermanent,
                    _ => throw new ArgumentException("Stato limite del muro non riconosciuto: " + c.State)
                };
                var service = session.CheckServiceability(actions, combination, token);
                if (f.Name == "Fusto" && service.Curvature is double curvature)
                    c.Curvatures.Add(new(d["geometry"]!.Required("height") - f.Position, curvature * Mm));
                foreach (var check in service.Checks)
                {
                    string suffix = check.Method switch
                    {
                        "Wall.ConcreteStress" => "tensione CLS", "Wall.SteelStress" => "tensione acciaio",
                        "Wall.Cracking" => "fessurazione", "Wall.Serviceability" => c.State,
                        _ => throw new NotSupportedException("Verifica GPC del muro non riconosciuta: " + check.Method)
                    };
                    string? status = check.Method == "Wall.Cracking" && service.CrackResponse is not null
                        ? ConcreteLibraryMapping.ToCrackResult(service.CrackResponse, CrackProfile.Ntc2018, [], false).Status : null;
                    // Decisione E1-D4 dell'utente (15/10/2026): le strisce del muro seguono le sezioni. Una zona tesa senza armatura non è più
                    // «non fessurata» per σt,el ≤ fctk,0.05: la verifica è non soddisfatta senza ampiezza (apertura non controllata), con lo
                    // stato delle sezioni e la domanda «—».
                    bool uncontrolled = check.Method == "Wall.Cracking" && check.Outcome == EngineeringOutcome.NotSatisfied && check.Ratio is null;
                    Add(check, suffix, c.Name, status ?? (check.Method == "Wall.Serviceability" ? "Non calcolata: " + check.Diagnostic : null),
                        uncontrolled ? status ?? UncontrolledCrackStatus : null, hasDemand: !uncontrolled);
                }
            }
            void Add(WallSectionCheck check, string suffix, string combination, string? unavailable = null, string? displayStatus = null, bool hasDemand = true)
            {
                double Convert(double value) => check.Unit switch
                {
                    "Nmm" => value / WallMomentToNative,
                    "N" => value / WallForceToNative,
                    "MPa" or "mm" or "mm2" => value,
                    _ => throw new NotSupportedException("Unità della verifica del muro non riconosciuta: " + check.Unit)
                };
                string unit = check.Unit switch { "Nmm" => "kNm/m", "N" => "kN/m", "mm2" => "mm²/m", _ => check.Unit };
                result.Add(new(label + " · " + suffix, combination, Convert(check.Demand), check.Capacity is double capacity ? Convert(capacity) : null,
                    unit, check.Ratio, displayStatus ?? (check.Capacity.HasValue ? NativeOutcomeText(check.Outcome, check.Capacity) : unavailable ?? check.Diagnostic ?? "Non disponibile"))
                    { Member = f.Name, Position = f.Position, Outcome = check.Outcome, Execution = check.Execution, Data = check.Data, HasDemand = hasDemand });
            }
        }
        return result;
    }
}
