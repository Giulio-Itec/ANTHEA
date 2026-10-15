using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;
using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Walls;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public static double[][] Outline(JsonObject d)
    {
        var g = d["geometry"]!; double h = g.D("height"), t = g.D("slab"), a = g.D("toe"), s = g.D("stem_base"), top = g.D("stem_top"), b = a + s + g.D("heel");
        return [[0, 0], [b, 0], [b, t], [a + s, t], [a + s, t + h], [a + s - top, t + h], [a, t], [0, t]];
    }

    /// <summary>
    /// The wall of the document: GPCChecker.Geotechnics calculates pressures, equilibrium, bearing, seismic bearing and internal forces of every
    /// combination (RetainingWallAnalysis), serviceability and global stability; ANTHEA checks the document, writes the results in m and kN and adds
    /// the reinforced concrete or gravity checks of the sections.
    /// </summary>
    public static Result Calculate(JsonObject data, CancellationToken token = default)
    {
        ValidateShape(data); var d = (JsonObject)data.DeepClone(); CompleteSoilInput(d); CompleteAdvancedInput(d); CompleteMaterialInput(d); ResolveSeismic(d); Validate(d);
        bool modern = d.D("version") >= 2;
        JsonArray? definitions = modern ? d.S("combination_mode") == "Automatiche" ? GenerateCombinations(d) : d.Array("combinations") : null;
        if (modern) d["combinations"] = definitions!.DeepClone();
        var input = ToWallInput(d);
        var rows = modern ? definitions!.OfType<JsonObject>().Where(c => c.B("enabled")).Select(c => (Definition: (JsonObject?)c, Combination: ToCombination(c), Live: 1d)).ToList()
            : LegacyCombinations(d).Select(c => (Definition: (JsonObject?)null, c.Combination, c.Live)).ToList();
        var wall = RetainingWallAnalysis.Calculate(input, rows.Select(r => r.Combination), token);
        var cases = wall.Cases.Select((c, i) => ToLoadCase(d, c, rows[i].Definition, rows[i].Live)).ToList();
        var checks = wall.Checks.Select(c => ToCheck(c)).ToList();
        bool wood = input.Seismic?.Method == WallSeismicMethod.Wood, water = input.Water is not null;
        var notes = new List<string> { Scope, Limits, Method, "GPC.Geometry: area e baricentro; GPC.Model e GPCChecker.Concrete: materiali, equilibrio N–M e tensioni delle sezioni armate. Pressioni geotecniche, equilibrio del muro, combinazioni, esercizio e stabilità globale: GPCChecker.Geotechnics (GPC.Checkers.Geotechnics.Walls)." };
        notes.Add(SoilHelp); notes.Add(FrictionHelp);
        notes.Add("Passiva sul piano verticale esterno a valle, come resistenza di progetto: Ed di scorrimento e ribaltamento non è ridotta dalla passiva; nell’equilibrio, nel contatto e nelle sollecitazioni del fusto la passiva di progetto è limitata alla spinta motrice; nessuna maggiorazione favorevole γG>1. Diagrammi del fusto: equilibrio della sezione con terreno sul paramento inclinato e attrito di monte. Ricoprimento q′ di valle usato con iq=(1−|H|/V)²; nessuna maggiorazione di profondità.");
        if (modern) { notes.Add(ApproachHelp); notes.Add("Azioni indipendenti con ψ propri e gruppi correlati. Matrice " + d.S("combination_mode") + ": fanno fede i coefficienti salvati in ogni riga. Urto: forza statica equivalente assegnata per metro; eventi eccezionali separati, ψ₂ delle variabili e γR=1 nel preset. Resistenze del materiale conservate cautelativamente ai valori ordinari."); }
        if (modern && d.S("combination_mode") != "Automatiche" && MissingHeelTwins(input, rows.Select(r => r.Combination)) is { Length: > 0 } missing)
            notes.Add("Matrice personalizzata: " + string.Join(", ", missing.Take(5)) + (missing.Length > 5 ? $" e altre {missing.Length - 5}" : "") + " con il sovraccarico variabile anche sulla mensola a monte e senza la gemella che lo esclude (colonna «q var. mensola» non spuntata): la disposizione con γQ = 0 sulla parte favorevole (NTC 2018 Tab. 2.6.I) non è verificata. Rigenerare le combinazioni automatiche o aggiungere le righe «b».");
        if (wood) notes.Add(SeismicHelp);
        if (water) notes.Add("Sottospinta lineare fra i battenti a valle e a monte, senza riduzioni da drenaggi. Peso saturo totale sopra la mensola; pressione efficace nel terreno. Per la portanza si adotta cautelativamente γ′ su tutta la zona di rottura.");
        if (d["seismic"].B("enabled"))
        {
            notes.Add("Pseudostatica locale " + (wood ? "Wood semplificato" : "Mononobe–Okabe") + ": incremento dinamico a metà altezza, inerzia di muro e terreno sulla mensola, ±kv. Direzione verso valle. Portanza sismica Annex F con accelerazione del terreno indipendente dal kh ridotto del muro: interazione N–V–M, γRD e γR espliciti; ricoprimento favorevole escluso. Spostamenti Newmark e stabilità globale Bishop richiedono l’attivazione dei rispettivi calcoli separati.");
            if (DeriveSeismic(d) is { } derivation) notes.Add(derivation.Description + " Casi generali e ribaltamento separati; γR del preset SLV: scorrimento 1, ribaltamento 1, portanza 1,2. Matrici personalizzate: fanno fede kh, kv e coefficienti di ogni riga; l’accelerazione della fondazione è definita separatamente.");
            else notes.Add("kh e kv assegnati: il preset manuale mantiene γR statici e non maggiora kh per il ribaltamento. Il progettista deve predisporre le combinazioni appropriate o usare Da parametri del sito (SLV).");
        }
        notes.AddRange(wall.Warnings);
        var structural = StructuralChecks(d, cases.Where(c => c.Factors.S("purpose") != "Ribaltamento").ToList(), token, out double steel);
        var service = CalculateService(d, input, wall, cases, checks, token);
        // Dettagli storici solo con l'aggancio delle prove (decisione D3 del 15/10/2026).
        var detailing = d.S("family") == "cantilever" ? EngineHook.Get<ILegacyWalls>()?.ReinforcementDetails(d) ?? CalculateReinforcementDetails(d) : null;
        if (d["detailing"].B("enabled") && detailing is not null) { structural.AddRange(detailing.Checks); steel = detailing.SteelKg; }
        double area = wall.Area / (Mm * Mm);
        Geotechnics.SlopeResult? global = null; string? globalError = null;
        if (d["global_stability"].B("enabled"))
        {
            try { global = CalculateGlobal(d, token); checks.AddRange(GlobalChecks(global)); }
            catch (ArgumentException ex) { globalError = ex.Message; checks.Add(new("Stabilità globale · Bishop", "Globale", 0, null, "−", null, "Dati da correggere: " + ex.Message)); }
        }
        return new(d, wall.Width / Mm, area, area, steel, cases, checks, structural, notes) { GlobalStability = global, GlobalError = globalError, Serviceability = service, Detailing = detailing };
    }
    /// <summary>
    /// The enabled ULS rows of a matrix with a variable uniform surcharge also on the heel (MUR-1) whose twin without it is missing: same state,
    /// factors and coefficients with <see cref="WallCombination.VariableSurchargeOnHeel"/> false. Only with a heel; no calculation.
    /// </summary>
    static string[] MissingHeelTwins(WallInput input, IEnumerable<WallCombination> rows)
    {
        if (!(input.Geometry.Heel > 0)) return [];
        var surcharges = input.Actions.Where(a => a.Enabled && a.Type == WallActionType.UniformSurcharge && a.Category == WallActionCategory.Q).Select(a => a.Id).ToArray();
        var ultimate = rows.Where(c => c.State == WallLimitState.Ultimate).ToArray();
        string Key(WallCombination c) => string.Join("|", new[] { c.Wall, c.Soil, c.ValleySoil, c.Water, c.FrictionFactor, c.SlidingFactor, c.OverturningFactor, c.BearingFactor, c.PassiveFactor }
            .Concat(input.Actions.Select(a => c.Coefficients.TryGetValue(a.Id, out double f) ? f : 0)).Select(v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        var twins = ultimate.Where(c => !c.VariableSurchargeOnHeel).Select(Key).ToHashSet();
        return ultimate.Where(c => c.VariableSurchargeOnHeel && surcharges.Any(id => c.Coefficients.TryGetValue(id, out double f) && f > 0) && !twins.Contains(Key(c))).Select(c => c.Name).ToArray();
    }
    public static Check CheckValue(string name, string combo, double demand, double? resistance, string unit, string unavailable = "Non disponibile")
    {
        double? ratio = resistance is > 0 ? Math.Abs(demand) / resistance : null;
        if (resistance == 0) return new(name, combo, Math.Abs(demand), 0, unit, demand == 0 ? 0 : null, demand == 0 ? "Soddisfatta" : "Non soddisfatta: resistenza nulla") { Outcome = demand == 0 ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied, Execution = ExecutionStatus.Completed, Data = DataStatus.Ready };
        return new(name, combo, Math.Abs(demand), resistance, unit, ratio, ratio is null ? unavailable : ratio <= 1 ? "Soddisfatta" : "Non soddisfatta") { Outcome = ratio is null ? EngineeringOutcome.NotEvaluated : ratio <= 1 ? EngineeringOutcome.Satisfied : EngineeringOutcome.NotSatisfied, Execution = ratio is null ? ExecutionStatus.NotExecuted : ExecutionStatus.Completed, Data = ratio is null ? DataStatus.Insufficient : DataStatus.Ready };
    }
}
