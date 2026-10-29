using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Walls;

namespace Anthea.Application.Presentation;

/// <summary>Colour of the status line of the wall sheet after a calculation: navy, dark goldenrod (checks not available) or firebrick
/// (checks not satisfied). The historic view sets it only when a calculation is published without errors.</summary>
public enum WallStatusSeverity { Normal, Warning, Failure }

/// <summary>
/// Presentation of the retaining wall sheet (F5.9): the texts, the rows of the tables and the rules that the historic view
/// RetainingWallWorkspace computed itself, moved without changes so that the view and its ViewModel give the same output. The
/// numbers are written as the view wrote them (<see cref="WallSheetPresentation.Number"/>, the culture of the thread where the view
/// interpolated). No calculation: everything reads the document and the results of <see cref="WallModel"/>.
/// </summary>
public static partial class RetainingWallPresentation
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    private static string F(double d) => WallSheetPresentation.Number(d);

    // ---------------------------------------------------------------- status

    public const string StatusToVerify = "Dati da verificare";
    public const string StatusPending = "Aggiornamento in attesa…";
    public const string StatusRunning = "Calcolo di equilibrio e verifiche GPC…";
    public const string ResultsToUpdate = "Risultati da aggiornare";
    public const string DesignRunning = "Ricerca armature: controlli N–M GPC, taglio, SLE e dettagli…";
    public const string DesignInterrupted = "Ricerca interrotta; armature inserite conservate.";
    public const string DesignStale = "Dati modificati: ricalcolare la proposta.";
    public const string GlobalRunning = "Ricerca delle superfici globali…";
    public const string WaitBeforeExport = "Attendere il calcolo e correggere i dati prima di esportare.";
    public const string WaitBeforeGlobalWord = "Completare prima il calcolo globale.";

    /// <summary>The status after a calculation that stopped (an error of the engine or of the data).</summary>
    public static string StatusError(string message) => "Dati da correggere: " + message;

    /// <summary>The status after the search of the reinforcement failed.</summary>
    public static string DesignError(string message) => "Predimensionamento: " + message;

    /// <summary>The status after the independent global calculation.</summary>
    public static string GlobalUpdated(int cases) => "Stabilità globale aggiornata · " + cases + " combinazioni. Le verifiche locali hanno un calcolo indipendente.";

    /// <summary>The status after the independent global calculation failed.</summary>
    public static string GlobalFailed(string message) => "Stabilità globale: " + message;

    /// <summary>The status after a calculation: combinations, checks not satisfied and not available, and its colour.</summary>
    public static (string Text, WallStatusSeverity Severity) Status(WallModel.Result result)
    {
        var all = result.Checks.Concat(result.Structural).ToArray();
        int failed = all.Count(c => c.IsNotSatisfied), missing = all.Count(c => c.IsNotEvaluated);
        return ($"Calcolo aggiornato · {result.Cases.Count} combinazioni · {failed} controlli non soddisfatti · {missing} non disponibili",
            failed > 0 ? WallStatusSeverity.Failure : missing > 0 ? WallStatusSeverity.Warning : WallStatusSeverity.Normal);
    }

    // ---------------------------------------------------------------- header

    /// <summary>The «Sisma» button: its text, the warning colour (enabled without seismic combinations) and its tooltip.</summary>
    public static (string Content, bool Warning, string ToolTip) SeismicStatus(JsonObject data, WallModel.Result? result)
    {
        var seismic = data["seismic"];
        bool enabled = seismic.B("enabled"); int? cases = result?.Cases.Count(c => c.State == "SISMA");
        string content = !enabled ? "Sisma: non attivo · imposta" : cases is null ? "Sisma: attivo · da ricalcolare" : cases == 0 ? "Sisma: attivo · nessuna combinazione" : $"Sisma: attivo · {cases} combinazioni";
        string tip = "Apri Input → Azioni → Sisma. " + (enabled ? $"{seismic.S("method")} · kh={seismic.D("kh"):0.###} · |kv|={seismic.D("kv"):0.###}. " : "Abilita l’azione sismica e assegna modello, kh e |kv|. ")
            + "Con matrice personalizzata controllare le righe SISMA; Genera / ripristina automatiche ricrea le combinazioni dai parametri inseriti.";
        return (content, enabled && cases == 0, tip);
    }

    /// <summary>The «Stabilità globale» button of the header.</summary>
    public static string GlobalSetupText(JsonObject data, bool hasResult) => !data["global_stability"].B("enabled") ? "Stabilità globale: non attiva · imposta"
        : !hasResult ? "Stabilità globale: dati / calcolo" : "Stabilità globale: calcolata · imposta";

    public const string GlobalSetupToolTip = "Apri il percorso guidato: profilo del terreno, strati profondi, falda, ricerca e verifica.";

    /// <summary>The summary under the drawing of the inputs for the combination shown.</summary>
    public static string Summary(JsonObject data, WallModel.Result r, WallModel.LoadCase c)
        => $"B {F(r.Width)} m · cls {F(r.Volume)} m³/m" + (data.S("family") == "cantilever" ? $" · acciaio stimato {F(r.SteelKg)} kg/m" : "")
            + $"\nH {F(c.Horizontal)} kN/m · V′ {F(c.Vertical)} kN/m · e {F(c.Eccentricity)} m";

    /// <summary>The text of the selected structural section, or the hint to select one.</summary>
    public static string SectionSelection((string Member, double Position, string Combination)? selected)
        => selected is { } s ? $"{s.Member} · z/l={s.Position:0.######} m · {s.Combination}" : "Selezionare una riga strutturale o una sezione nella tabella delle sollecitazioni.";

    /// <summary>The name of the concrete section sent to the module c.a.</summary>
    public static string SectionName((string Member, double Position, string Combination) selected) => $"Muro · {selected.Member} · z/l {selected.Position:0.###} m";

    // ---------------------------------------------------------------- checks

    public static IReadOnlyList<string> CheckColumns { get; } = ["Verifica", "Combinazione", "Ed", "Rd", "Unità", "Ed/Rd", "Esito"];

    /// <summary>The rows of a table of checks.</summary>
    public static IReadOnlyList<string[]> CheckRows(IEnumerable<WallModel.Check> checks) => checks.Select(c => new[] { c.Name, c.Combination, c.HasDemand ? F(c.Demand) : "—",
        c.Resistance is double r ? F(r) : "—", c.Unit, c.Ratio is double q ? q.ToString("0.000", It) : "—", c.Status }).ToArray();

    /// <summary>The checks of «Riepilogo e dettagli» for a filter of the checks: the combination shown (with the details), all, or the envelope.</summary>
    public static IReadOnlyList<WallModel.Check> FilterChecks(WallModel.Result result, string filter, string combination)
    {
        var all = result.Checks.Concat(result.Structural);
        return (filter switch { "Combinazione selezionata" => all.Where(x => x.Combination == combination || x.Combination == "Dettagli"), "Tutti i controlli" => all, _ => WallExports.Envelope(all) }).ToArray();
    }

    /// <summary>The section of a check that the user can send to the module c.a., or null: a check of a member at a position.</summary>
    public static (string Member, double Position, string Combination)? CheckSection(WallModel.Check check, WallModel.LoadCase shown)
        => check is { Member: not null, Position: double z } ? (check.Member, z, check.Combination == "Dettagli" ? shown.Name : check.Combination) : null;

    /// <summary>The factors of the combination shown, above the table of the checks.</summary>
    public static string CheckHeader(WallModel.LoadCase c)
    {
        var factor = c.Factors;
        return $"{c.Name}{(c.VariableSurchargeOnHeel ? "" : " · q variabile solo nella spinta, assente sulla mensola")} · {factor.S("approach")} · γMφ={factor.D("mphi", 1):0.##}; γR scorr./ribalt./port.={factor.D("rslide", 1):0.##}/{factor.D("rover", 1):0.##}/{factor.D("rbearing", 1):0.##}; γR,e passiva={c.PassiveFactor:0.##}"
            + $"\nScorrimento Ed/Rd={F(c.SlidingDemand)}/{F(c.SlidingResistance)} kN/m · ribaltamento Ed/MRd={F(c.OverturningDemand)}/{F(c.OverturningResistance)} kNm/m · Rd port.={(c.BearingResistance is double b ? F(b) : "da verificare")} kN/m · U={F(c.Uplift)} kN/m";
    }

    public const string CheckFooter = "Riepilogo: inviluppo delle verifiche locali e della stabilità globale, se attivata. Un controllo non disponibile resta visibile. Per la globale selezionare la vista dedicata e la sua combinazione. Cedimenti da valutare separatamente.";

    public static IReadOnlyList<string> SectionColumns { get; } = ["Elemento", "z/l [m]", "s [m]", "N [kN/m]", "M [kNm/m]", "V [kN/m]", "Armatura"];

    /// <summary>The internal forces of the combination shown («Sollecitazioni numeriche»), with the reinforcement of the zone.</summary>
    public static IReadOnlyList<string[]> SectionRows(JsonObject data, WallModel.LoadCase c) => c.Sections.Select(s => new[] { s.Name, F(s.Position), F(s.Thickness),
        F(s.N), F(s.M), F(s.V), WallModel.RebarLabel(data, WallModel.ReinforcementKey(data, s.Name, s.Position)) }).ToArray();

    /// <summary>The section of a row of the internal forces that the user can send: the stem always, the slabs with a valid contact.</summary>
    public static (string Member, double Position, string Combination)? ForceSection(WallModel.LoadCase c, int row)
    {
        var f = row >= 0 ? c.Sections[row] : null;
        return f is not null && (f.Name == "Fusto" || c.Contact.Valid) ? (f.Name, f.Position, c.Name) : null;
    }

    // ---------------------------------------------------------------- pressures

    public const string PressureHint = "Scegliere la combinazione nella barra superiore. z è la profondità dalla sommità; Ht = H+t. Selezionare una riga per evidenziare il tratto.";
    public const string PressureStemTitle = "Fusto · coefficienti con attrito al paramento";
    public const string PressureValleyTitle = "Valle · passiva mobilitata (z dalla sommità muro)";

    /// <summary>The formula of the pressures: Wood when the seismic action is enabled with that model, otherwise Coulomb / Mononobe–Okabe.</summary>
    public static string PressureFormula(JsonObject data)
    {
        bool wood = data["seismic"].B("enabled") && data["seismic"].S("method") == "Wood semplificato";
        return wood ? "K₀=1−sinφd; Δp=kh·γ·Ht (Wood semplificato, uniforme); p=K₀σ′v+K₀Σ(fᵢqᵢ)+u+Δp." : "Coulomb/MO: Kh=cos²(φd−θ)cosδd/{cosθ cos(δd+θ)[1+√(sin(φd+δd)sin(φd−θ)/cos(δd+θ))]²}; θ=atan[kh/(1−kv)]. Con δ=θ=0: Rankine. Δp=(Ke−K)(1−kv)γHt/2. Piano virtuale con mensola: δ=0; fusto: δ muro.";
    }

    public static string PressureHeader(WallModel.LoadCase c)
        => $"{c.Name} · kh={c.Kh:0.###}, kv={c.Kv:0.###}; θ={WallSheetPresentation.SeismicAngleDegrees(c.Kh, c.Kv):0.###}°. σ′v incorpora γG e (1−kv). Tutte le pressioni in kPa. Azioni dirette trattate separatamente.";

    public static IReadOnlyList<string> PressureColumns { get; } = ["z₀ [m]", "z₁ [m]", "φk [°]", "φd [°]", "Ka / K₀", "Kae", "σ′v₀ [kPa]", "σ′v₁ [kPa]", "p terra₀", "p terra₁", "p q", "u₀", "u₁", "Δp sisma", "p tot₀", "p tot₁"];

    public static IReadOnlyList<string[]> PressureRows(WallModel.LoadCase c) => c.PressureDetails.Select(p => new[] { F(p.Z0), F(p.Z1), F(p.Phi), F(p.PhiDesign),
        p.K.ToString("0.0000", It), p.Ke.ToString("0.0000", It), F(p.Sigma0), F(p.Sigma1), F(p.Soil0), F(p.Soil1), F(p.Surcharge), F(p.Water0), F(p.Water1), F(p.Dynamic),
        F(p.Total0), F(p.Total1) }).ToArray();

    public static IReadOnlyList<string> StemPressureColumns { get; } = ["Fusto z₀–z₁ [m]", "φd [°]", "K", "Ke", "p₀–p₁ [kPa]"];

    public static IReadOnlyList<string[]> StemPressureRows(WallModel.LoadCase c) => c.StemPressureDetails.Select(p => new[] { F(p.Z0) + "–" + F(p.Z1), F(p.PhiDesign),
        p.K.ToString("0.0000", It), p.Ke.ToString("0.0000", It), F(p.Total0) + "–" + F(p.Total1) }).ToArray();

    public static IReadOnlyList<string> ValleyPressureColumns { get; } = ["Valle z₀–z₁ [m]", "Pressione usata p₀–p₁ [kPa]"];

    public static IReadOnlyList<string[]> ValleyPressureRows(WallModel.LoadCase c) => c.ValleyPressures.Select(p => new[] { F(p.Z0) + "–" + F(p.Z1), F(p.P0) + "–" + F(p.P1) }).ToArray();

    /// <summary>The net integral of the pressures over H + t (uphill, lateral actions and passive in front) and its resultant.</summary>
    public static string PressureThrust(JsonObject data, WallModel.LoadCase c)
    {
        double ht = WallSheetPresentation.TotalHeight(data);
        var thrust = WallModel.Integrate(c.Pressures, 0, ht, ht);
        return $"Integrale netto delle pressioni (monte + azioni laterali − passiva): P={F(thrust.Force)} kN/m; momento al piano di posa={F(-thrust.Moment)} kNm/m; quota risultante={(thrust.Force > 0 ? F(-thrust.Moment / thrust.Force) : "—")} m.";
    }

    // ---------------------------------------------------------------- advanced results

    public const string DetailingUnavailable = "Armature disponibili per muri a mensola in c.a.";

    public static IReadOnlyList<string> DetailingColumns { get; } = ["Pos. / zona", "Faccia", "n Ø", "As [mm²/m]", "L barra [m]", "lbd richiesto/usato [mm]", "l0 richiesto/usato [mm]", "Mandrino [mm]", "fbd [MPa]"];

    public static IReadOnlyList<string[]> DetailingRows(WallModel.ReinforcementDetails detail) => detail.Bars.Select(b => new[] { b.Mark + " " + WallModel.RebarZoneName(b.Zone),
        b.Face, $"{b.Count} Ø{b.Diameter}", F(b.Area), F(b.Length), F(b.RequiredAnchor) + " / " + F(b.Anchor), F(b.RequiredLap) + " / " + F(b.Lap), F(b.Mandrel), F(b.Fbd) }).ToArray();

    public static string DetailingSteel(WallModel.ReinforcementDetails detail) => $"Acciaio stimato incluse sovrapposizioni, ancoraggi e secondarie: {detail.SteelKg:0.0} kg/m";

    public const string SeismicBearingSelect = "Selezionare una combinazione SISMA.";
    public const string SeismicBearingUnavailable = "Portanza sismica non disponibile.";
    public const string SeismicBearingMethod = "EN 1998-5 allegato F · coefficienti: a=c=0,92; b=d=1,25; e=0,41; f=0,32; m=0,96; k=1; k′=0,39; cT=1,14; cM=c′M=1,01; β=2,90; γ=2,80. N,V,M normalizzati con γRD·γR; F=γRD·ah/(g tanφ). Ricerca della capacità lungo il raggio N,V,M.";

    public static IReadOnlyList<string> SeismicBearingColumns { get; } = ["Nmax [kN/m]", "F", "N̄", "V̄", "M̄", "N̄ limite", "Interazione ≤1", "η proporzionale", "Esito"];

    public static string[] SeismicBearingRow(SeismicBearing b) => [F(b.NMax), F(b.SoilInertia), F(b.NBar), F(b.VBar), F(b.MBar), F(b.VerticalLimit),
        b.Interaction?.ToString("0.000") ?? "Fuori dominio", b.Ratio?.ToString("0.000") ?? "—", b.Status];

    public const string ServiceUnavailable = "Attivare le verifiche in Terreno.";

    public static IReadOnlyList<string> ServiceColumns { get; } = ["Caso", "s valle/centro/monte [mm]", "Rotazione [rad]", "u fusto/totale [mm]", "Stato"];

    public static IReadOnlyList<string[]> ServiceRows(WallModel.ServiceResult service) => service.Cases.Select(s => new[] { s.Combination,
        $"{s.ToeSettlement:0.###} / {s.CentreSettlement:0.###} / {s.HeelSettlement:0.###}", $"{s.FoundationRotation:0.000000}", $"{s.StemDisplacement:0.###} / {s.HeadDisplacement:0.###}", s.Status }).ToArray();

    public const string SettlementTitle = "Integrazione cedimento · verticale centrale", ShapeTitle = "Curvature e spostamenti del fusto";

    public static IReadOnlyList<string> SettlementColumns { get; } = ["Strato", "z₀–z₁ [m]", "Δσ [kPa]", "M [kPa]", "Δs [mm]"];

    public static IReadOnlyList<string[]> SettlementRows(WallModel.ServiceCase current) => current.Slices.Select(s => new[] { s.Soil, $"{s.Top:0.###}–{s.Bottom:0.###}",
        F(s.Stress), F(s.Modulus), F(s.SettlementMm) }).ToArray();

    public static IReadOnlyList<string> ShapeColumns { get; } = ["y dal piede [m]", "κ [1/m]", "θ [rad]", "u [mm]"];

    public static IReadOnlyList<string[]> ShapeRows(WallModel.ServiceCase current) => current.Shape.Select(s => new[] { F(s.Y), $"{s.Curvature:0.000000}", $"{s.Rotation:0.000000}", F(s.DisplacementMm) }).ToArray();

    public static string EarthquakeText(WallModel.SeismicDisplacementCase history)
        => $"{history.Name} · {history.State} · ky/g={history.YieldG:0.####}; scala={history.Scale:0.###}; d={history.History?.DisplacementMm:0.###} mm / limite={history.LimitMm} mm · {history.Status}";

    public static string EarthquakeTitle(WallModel.SeismicDisplacementCase history) => "Storia integrata " + history.Name;

    public static IReadOnlyList<string> EarthquakeColumns { get; } = ["t [s]", "a/g", "v [m/s]", "d [mm]"];

    public static IReadOnlyList<string[]> EarthquakeRows(SlidingHistory values) => values.Points.Select(p => new[] { F(p.Time), F(p.AccelerationG), F(p.Velocity), F(p.DisplacementMm) }).ToArray();

    // ---------------------------------------------------------------- search of the reinforcement

    public static IReadOnlyList<string> ProposalColumns { get; } = ["Zona", "Proposta per faccia", "Secondarie Ø / passo"];

    /// <summary>The bars proposed for each zone (the upper zone of the stem only with two zones).</summary>
    public static IReadOnlyList<string[]> ProposalRows(WallModel.ReinforcementProposal proposal) => new[] { "stem", "stem_upper", "toe", "heel" }
        .Where(k => k != "stem_upper" || proposal.Input["reinforcement"].B("two_zones")).Select(k =>
        {
            var a = proposal.Input["reinforcement"]![k]!;
            return new[] { WallModel.RebarZoneName(k), $"{a.D("count")} Ø{a.D("diameter")}/m", $"{a.D("secondary_diameter")} / {a.D("secondary_spacing")} mm" };
        }).ToArray();

    // ---------------------------------------------------------------- values used in the calculation

    public const string DerivedMissing = "Ricalcolare per interrogare i valori derivati.";
    public const string ParametersHint = "Modificare gli input nella colonna Valore. Per cambiare un coefficiente derivato selezionare la relativa modalità Assegnato. Le forze risultanti sono consultabili; si aggiornano ricalcolando le loro cause.";
    public const string ParametersChecking = "Controllo dati e ricalcolo…";
    public const string ParametersChanged = "Il foglio è cambiato: chiudere e riaprire il riepilogo.";
    public const string ParametersTitle = "Muro · valori utilizzati nel calcolo";

    public static IReadOnlyList<string> DerivedColumns { get; } = ["Valore", "Risultato utilizzato", "Origine / significato"];

    /// <summary>The values used in the calculation of a combination: soil audit, factors, pressures, equilibrium, checks, foundation and internal forces.</summary>
    public static IReadOnlyList<string[]> DerivedRows(WallModel.LoadCase c)
    {
        var values = new List<string[]>();
        void Add(string key, string value, string source) => values.Add([key, value, source]);
        foreach (var p in c.SoilAudit) { var meaning = WallModel.AuditMeaning(p.Key); Add(meaning.Label, (J.Number(p.Value) is double n ? n.ToString("0.#####", It) : p.Value?.ToString() ?? "") + " " + meaning.Unit, meaning.Origin); }
        foreach (var p in c.Factors ?? new()) if (p.Value is JsonValue) Add(p.Key, p.Value.ToString(), "Coefficiente della combinazione selezionata");
        foreach (var p in c.PressureDetails) Add($"Monte z={F(p.Z0)}–{F(p.Z1)} m", $"φd={F(p.PhiDesign)}°; K={p.K:0.00000}; Ke={p.Ke:0.00000}; σ′={F(p.Sigma0)}–{F(p.Sigma1)}; p={F(p.Total0)}–{F(p.Total1)} kPa", "Piano esterno di equilibrio; acqua e incremento inclusi");
        foreach (var p in c.StemPressureDetails) Add($"Coefficienti fusto z={F(p.Z0)}–{F(p.Z1)} m", $"φd={F(p.PhiDesign)}°; K={p.K:0.00000}; Ke={p.Ke:0.00000}; Δp={F(p.Dynamic)} kPa", "Coulomb/MO con δ muro, oppure K₀ Wood");
        foreach (var p in c.StemPressures) Add($"Fusto z={F(p.Z0)}–{F(p.Z1)} m", $"p={F(p.P0)}–{F(p.P1)} kPa", "Coulomb/MO al paramento + acqua + azioni − passiva");
        foreach (var p in c.ValleyPressures) Add($"Valle z={F(p.Z0)}–{F(p.Z1)} m", $"p={F(p.P0)}–{F(p.P1)} kPa", "Passiva effettivamente utilizzata · z dalla sommità muro");
        Add("Equilibrio", $"H={F(c.Horizontal)}; V′={F(c.Vertical)}; U={F(c.Uplift)} kN/m; Mstab={F(c.Stabilizing)}; Mrib={F(c.Overturning)} kNm/m", "Somma forze e momenti; H e Mrib al netto della passiva di progetto usata");
        Add("Verifiche", $"Ed scorr.={F(c.SlidingDemand)} kN/m; Ed ribalt.={F(c.OverturningDemand)} kNm/m; MRd ribalt.={F(c.OverturningResistance)} kNm/m; γR,e passiva={F(c.PassiveFactor)}", "Azioni delle verifiche senza la passiva, che è nelle resistenze");
        Add("Fondazione", $"e={F(c.Eccentricity)} m; B′={F(c.EffectiveWidth)} m; Rsc={F(c.SlidingResistance)} kN/m; Rport={c.BearingResistance?.ToString("0.###") ?? "non disponibile"} kN/m", "Contatto e resistenze");
        foreach (var p in c.Sections) Add($"{p.Name} z/l={F(p.Position)} m", $"N={F(p.N)} kN/m; M={F(p.M)} kNm/m; V={F(p.V)} kN/m", "Equilibrio della sezione · modificare le cause negli input");
        return values;
    }
}
