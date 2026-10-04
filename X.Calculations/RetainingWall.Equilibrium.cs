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
        notes.Add("Passiva applicata al piano verticale esterno a valle e limitata alla spinta motrice; nessuna maggiorazione favorevole γG>1. Diagrammi del fusto: equilibrio della sezione con terreno sul paramento inclinato e attrito di monte. Ricoprimento q′ di valle usato con iq=(1−|H|/V)²; nessuna maggiorazione di profondità.");
        if (modern) { notes.Add(ApproachHelp); notes.Add("Azioni indipendenti con ψ propri e gruppi correlati. Matrice " + d.S("combination_mode") + ": fanno fede i coefficienti salvati in ogni riga. Urto: forza statica equivalente assegnata per metro; eventi eccezionali separati, ψ₂ delle variabili e γR=1 nel preset. Resistenze del materiale conservate cautelativamente ai valori ordinari."); }
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
        var detailing = d.S("family") == "cantilever" ? CalculateReinforcementDetails(d) : null;
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
    public static Check CheckValue(string name, string combo, double demand, double? resistance, string unit, string unavailable = "Non disponibile")
    {
        double? ratio = resistance is > 0 ? Math.Abs(demand) / resistance : null;
        if (resistance == 0) return new(name, combo, Math.Abs(demand), 0, unit, demand == 0 ? 0 : null, demand == 0 ? "Soddisfatta" : "Non soddisfatta: resistenza nulla");
        return new(name, combo, Math.Abs(demand), resistance, unit, ratio, ratio is null ? unavailable : ratio <= 1 ? "Soddisfatta" : "Non soddisfatta");
    }
}
