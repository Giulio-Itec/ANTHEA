using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Modules;
using Anthea.Calculations;

namespace Anthea.Application.Presentation;

/// <summary>
/// The Bridge Design sheet (str_bridge_design) as the user reads and edits it (F5.12): the texts of the sheet and of its panels, the
/// metrics, the rows of the tables of the results, the fields shown by the typology, the parameters with their text, value and validity,
/// and the rules of the buttons that change the data (typology, automatic dimensions, scenery, random bridge, alternative A, alternatives,
/// indicative rates, recommended γQ, solution of the search). Moved without changes from the historic view (BridgeDesignWorkspace), which
/// calls them; the ViewModel of F5.12 reads the same functions. The unit factors of the metrics are named constants.
/// </summary>
public static class BridgeDesignPresentation
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private const double EurosPerMillion = 1e6, KilogramsPerTonne = 1000;

    /// <summary>A number in Italian, «N1» by default (the F of the historic view).</summary>
    public static string F(double n, string format = "N1") => n.ToString(format, It);

    /// <summary>Delay of the automatic recalculation after an edit, as the historic view.</summary>
    public static TimeSpan RecalculationDelay { get; } = TimeSpan.FromMilliseconds(450);

    // ---------------------------------------------------------------- header and status

    public const string Title = "BRIDGE DESIGN";
    public const string Subtitle = "Dal sito al ponte · geometria, quantità e ordini di grandezza";
    public const string ScopeLine = "PREDIMENSIONAMENTO   ·   Prezzi e coefficienti modificabili   ·   Importi in EUR, IVA esclusa";
    public const string StatusPending = "Dati modificati · aggiornamento automatico in attesa…";
    public const string StatusRunning = "Aggiornamento del ponte e delle alternative…";

    /// <summary>The titles of the four metrics.</summary>
    public static IReadOnlyList<string> MetricLabels { get; } = ["COSTO INDICATIVO", "IMPRONTA MATERIALI + CANTIERE", "ALTEZZA IMPALCATO", "DURATA INDICATIVA"];

    /// <summary>The value of a metric without a result.</summary>
    public const string MetricEmpty = "—";

    /// <summary>The caption of a metric after the data changed.</summary>
    public const string MetricMissing = "Dati da completare";

    /// <summary>The four metrics of a result: value and caption.</summary>
    public static IReadOnlyList<(string Value, string Caption)> Metrics(BridgeDesignModel.Result r) =>
    [
        (F(r.TotalCost / EurosPerMillion, "0.00") + " M€", $"{F(r.TotalCost / r.Area, "N0")} €/m² · {F(r.CostLow / EurosPerMillion, "0.00")}–{F(r.CostHigh / EurosPerMillion, "0.00")} M€"),
        (F(r.Carbon, "N0") + " tCO₂e", F(r.Carbon * KilogramsPerTonne / r.Area, "N0") + " kgCO₂e/m² · stima parziale"),
        (F(r.Depth, "0.00") + " m", $"L/d ≈ {F(r.Spans.Max() / r.Depth, "0")} · {r.Spans.Length} campate"),
        (F(r.Duration, "0") + " mesi", $"±25% · {r.Supports.Count(s => s.Type != "Spalla")} pile · {r.Supports.Count(s => s.Type == "Spalla")} spalle")
    ];

    /// <summary>The line under the drawing.</summary>
    public static string Configuration(BridgeDesignModel.Result r) => $"L {F(r.Length)} m · W {F(r.Width)} m · {r.Family.Name} · {r.Girders} elementi · {r.Foundation}"
        + (r.PileLength > 0 ? $" × {F(r.PileLength, "0")} m" : "");

    // ---------------------------------------------------------------- inputs

    /// <summary>The tabs of the inputs.</summary>
    public static IReadOnlyList<string> InputTabs { get; } = ["1 · Sito", "2 · Campate", "3 · Sezione", "4 · Pile", "5 · Traffico"];

    /// <summary>The tabs of the results.</summary>
    public static IReadOnlyList<string> OutputTabs { get; } = ["Quantità e costi", "Dettagli del predimensionamento", "Confronto A / B", "Prezzario", "Ipotesi e coefficienti", "Sezioni e quote"];

    public const string ObstacleLabel = "Ostacolo da attraversare", SoilLabel = "Terreno convenzionale", DeckLabel = "Piano carrabile", PierLabel = "Tipologia pila",
        FoundationLabel = "Fondazioni", TrafficLabel = "Modello di traffico";
    public const string ContinuousLabel = "Impalcato continuo", StartPierLabel = "Inizio su pila · viadotto prosegue", EndPierLabel = "Fine su pila · viadotto prosegue";
    public const string SpansNote = "Campate terminali = 0,8 × interne per schemi continui con almeno 3 campate. Le pile vengono spostate fuori dall'ostacolo, ove possibile.";
    public const string TowersNote = "Antenne a due fusti quadrati: la quota sopra impalcato è nella Sezione. La dimensione pila imposta indica il lato del fusto; 0 = predimensionamento.";
    public const string SubstructureNote = "0 mantiene il dimensionamento automatico. Le classi di terreno sono parametri convenzionali modificabili nelle ipotesi.";
    public const string TowersSpansNote = "Tre campate simmetriche: L/4 + L/2 + L/4. Gli appoggi non vengono spostati per aggirare l’ostacolo.";
    public const string ExtendedNote = "Geometria ideale di predimensionamento; parametri e tensioni di riferimento nelle Ipotesi.";
    public const string TrafficFootnote = "Il coefficiente adottato è registrato nel risultato. Le strutture superiori usano azioni globali equivalenti e tandem locali: restano modelli di predimensionamento.";
    public const string AutoSizeButton = "Ripristina dimensioni automatiche", AutoSectionButton = "Sezione automatica", RecommendedTrafficButton = "Applica γQ NTC consigliato";

    /// <summary>The note of the traffic model.</summary>
    public static string TrafficNote(string model) => model == BridgeDesignModel.Schema1
        ? "Schema 1: tandem e distribuito sulle zone sfavorevoli. Lo spartitraffico positivo è assunto con barriera fissa; carreggiate simmetriche. Le barriere sono escluse dalla larghezza carrabile."
        : "Archivio compatibile: uniforme su tutte le campate, senza tandem. Il passaggio allo Schema 1 conserva il coefficiente impostato finché non applichi quello consigliato.";

    /// <summary>The span of a typology on its button.</summary>
    public static string FamilyRange(BridgeDesignModel.Family f) => $"{f.MinSpan:0}–{f.MaxSpan:0} m";

    /// <summary>
    /// The section of the typology of <paramref name="input"/>: whether the deck can be chosen, the parameters of the section shown (in the
    /// order of <see cref="BridgeDesignModel.Section"/>), those of the extended typologies (in the order of <see cref="BridgeDesignModel.AdvancedSection"/>)
    /// and their note (null for the other typologies).
    /// </summary>
    public static (bool Deck, IReadOnlyList<BridgeDesignModel.Parameter> Section, IReadOnlyList<BridgeDesignModel.Parameter> Advanced, string? Note) SectionFields(JsonObject input)
    {
        string id = input.S("family");
        var active = new HashSet<string> { "depth", "fc" };
        if (id != "slab") active.Add("slab");
        if (id is "tee" or "psc_i" or "psc_u" or "steel_i") active.Add("spacing");
        if (id is "tee" or "psc_u" or "psc_box" or "fcm") active.Add("web");
        if (id is "psc_u" or "psc_box" or "fcm") active.Add("bottom");
        if (id is "psc_box" or "fcm") { active.Add("bottom_ratio"); active.Add("cells"); }
        if (id.StartsWith("steel")) foreach (string key in new[] { "flange_width", "flange_mm", "web_mm" }) active.Add(key);
        if (id == "steel_box") { active.Add("boxes"); active.Add("web_slope"); }
        if (id == "psc_i") active.Add("haunch");
        if (id == "psc_u") { active.Add("u_top"); active.Add("u_bottom"); }
        if (BridgeDesignModel.IsExtended(id))
        {
            bool ortho = id == "orthotropic" || BridgeDesignModel.HasUpperStructure(id) && input.S("deck_type") == BridgeDesignModel.DeckTypes[1];
            active.UnionWith(new[] { "flange_mm", "web_mm" });
            if (ortho) { active.Remove("slab"); active.Add("boxes"); }
            else { active.Add("flange_width"); active.Add("spacing"); if (id == "filler_beam") active.Remove("slab"); }
        }
        var section = BridgeDesignModel.Section.Where(p => active.Contains(p.Key)).ToArray();
        if (!BridgeDesignModel.IsExtended(id)) return (BridgeDesignModel.HasUpperStructure(id), section, [], null);
        var extra = new HashSet<string>();
        if (id == "tied_arch") extra.Add("arch_rise");
        if (BridgeDesignModel.HasTowers(id)) extra.Add("tower_height");
        if (id == "suspension") extra.Add("cable_sag");
        if (BridgeDesignModel.HasUpperStructure(id)) extra.Add("suspender_spacing");
        if (id == "truss") extra.Add("truss_ratio");
        if (id == "filler_beam") extra.Add("embedded_cover");
        if (id == "orthotropic" || BridgeDesignModel.HasUpperStructure(id) && input.S("deck_type") == BridgeDesignModel.DeckTypes[1])
            extra.UnionWith(new[] { "deck_plate_mm", "rib_height", "rib_top", "rib_bottom", "rib_mm", "rib_spacing" });
        return (BridgeDesignModel.HasUpperStructure(id), section, BridgeDesignModel.AdvancedSection.Where(p => extra.Contains(p.Key)).ToArray(),
            BridgeDesignModel.HasTowers(id) ? TowersSpansNote : ExtendedNote);
    }

    /// <summary>A choice that rebuilds the inputs after the change (the deck and the traffic model change the fields shown).</summary>
    public static bool ChoiceRebuilds(string key) => key is "deck_type" or "traffic_model";

    /// <summary>The button of a typology: the typology and the rules that come with it (towers, continuity, orthotropic deck).</summary>
    public static void SelectFamily(JsonObject input, string id)
    {
        input["family"] = id;
        if (BridgeDesignModel.HasTowers(id)) { input["spans"] = 3; input["continuous"] = true; input["start_pier"] = false; input["end_pier"] = false; }
        if (id is "tied_arch" or "truss") input["continuous"] = false;
        if (id == "suspension") input["deck_type"] = BridgeDesignModel.DeckTypes[1];
    }

    /// <summary>«Ripristina dimensioni automatiche» and «Sezione automatica»: the dimensions back to 0 (automatic).</summary>
    public static void AutoSize(JsonObject input)
    {
        foreach (string k in new[] { "depth", "slab", "spacing", "web", "bottom", "boxes", "pier_size", "pile_count", "footing_size", "pile_length", "tower_height" }) input[k] = 0;
    }

    /// <summary>«Nuovo paesaggio»: the next scenery of the drawing.</summary>
    public static void NewScene(JsonObject data) => data["scene"] = data.D("scene") + 1;

    /// <summary>
    /// «Ponte casuale»: a typology and a plausible bridge drawn from <paramref name="random"/> (the traffic model is kept), with a new scenery.
    /// The draws are those of the historic view, in the same order.
    /// </summary>
    public static void Randomize(JsonObject data, Random random)
    {
        var f = BridgeDesignModel.Families[random.Next(BridgeDesignModel.Families.Length)];
        var input = data["input"]!.AsObject();
        var defaults = BridgeDesignModel.Defaults()["input"]!.AsObject(); defaults["traffic_model"] = input["traffic_model"]?.DeepClone(); data["input"] = defaults.DeepClone();
        input = data["input"]!.AsObject();
        input["family"] = f.Id; int spans = random.Next(2, 7); input["spans"] = spans;
        input["length"] = Math.Round((f.MinSpan + (f.MaxSpan - f.MinSpan) * (.2 + random.NextDouble() * .45)) * (spans - .4));
        input["height"] = Math.Max(random.Next(8, 31), Math.Ceiling(input.D("length") / (spans - .4) / 18 + 5)); input["lanes"] = random.Next(2, 7); input["obstacle"] = BridgeDesignModel.Obstacles[random.Next(3)];
        input["soil"] = BridgeDesignModel.Soils[random.Next(4)]; input["pier"] = BridgeDesignModel.Piers[random.Next(4)];
        if (BridgeDesignModel.HasUpperStructure(f.Id))
        {
            bool towers = BridgeDesignModel.HasTowers(f.Id); input["spans"] = towers ? 3 : 1; input["continuous"] = towers;
            input["length"] = Math.Round(towers ? Math.Min(450, (f.MinSpan + f.MaxSpan) / 2) * 2 : (f.MinSpan + f.MaxSpan) / 2);
            input["height"] = 30; input["obstacle"] = "Nessuno";
            if (f.Id == "suspension") input["deck_type"] = BridgeDesignModel.DeckTypes[1];
        }
        input["obstacle_width"] = Math.Min(20, input.D("length") / 8); data["scene"] = random.Next(10000);
    }

    // ---------------------------------------------------------------- parameters

    /// <summary>The text of a parameter: the raw value of the document.</summary>
    public static string ParameterText(JsonObject data, string group, string key) => data[group]!.S(key);

    /// <summary>The value of the slider of a parameter (0 when the text is not a number).</summary>
    public static double ParameterValue(JsonObject data, string group, string key) => data[group].D(key);

    /// <summary>The value of a parameter typed by the user, null when it is not a number.</summary>
    public static double? ParameterNumber(JsonObject data, string group, string key) => J.Number(data[group]![key]);

    /// <summary>True when the text typed is not a number or is outside the range of the parameter (the red border of the historic view).</summary>
    public static bool ParameterInvalid(double? value, BridgeDesignModel.Parameter p) => value is null || value < p.Min || value > p.Max;

    /// <summary>The text written by the slider of a parameter.</summary>
    public static string SliderText(double value) => value.ToString("0.###", It);

    /// <summary>The assumptions of the tab «Ipotesi e coefficienti» (the traffic ones are in the tab of the traffic).</summary>
    public static IReadOnlyList<BridgeDesignModel.Parameter> AssumptionParameters() =>
        BridgeDesignModel.Assumptions.Concat(BridgeDesignModel.AdvancedAssumptions).Where(p => p.Key is not "gamma_q" and not "traffic_load").ToArray();

    public const string AssumptionsNote = "Ipotesi del modello di stima. I carichi sono equivalenti uniformi; i moltiplicatori non definiscono una combinazione normativa completa.";

    // ---------------------------------------------------------------- prices

    public const string ManagePricesButton = "Gestisci prezzario…", IndicativeRatesButton = "Applica valori orientativi 2026";
    public const string PriceDocumentNote = "Listino e personalizzazioni sono conservati nel documento. Apri la gestione per cambiare catalogo, controllare le differenze o ripristinare una revisione.";

    /// <summary>The name and the edition of the catalogue of a price document.</summary>
    public static string PriceDocumentTitle(JsonObject pricing) => pricing["catalogue"].S("Name") + " · " + pricing["catalogue"].S("Edition");

    /// <summary>«Applica valori orientativi 2026»: the indicative rates of the model.</summary>
    public static void ApplyIndicativeRates(JsonObject data)
    {
        foreach (var rate in BridgeDesignModel.Rates) data["rates"]![rate.Key] = rate.Default;
    }

    // ---------------------------------------------------------------- results

    public static IReadOnlyList<string> QuantityColumns { get; } = ["Voce", "Parte", "Quantità", "Unità", "Prezzo €", "Importo €"];

    public static string[][] QuantityRows(BridgeDesignModel.Result r) => r.Quantities.Select(q => new[] { q.Item, q.Group, F(q.Amount), q.Unit, F(q.Rate), F(q.Cost, "N0") }).ToArray();

    /// <summary>The line of the total under the quantities: direct cost, site, contingencies.</summary>
    public static string CostSummary(JsonObject data, BridgeDesignModel.Result r)
    {
        double prelims = r.DirectCost * data["assumptions"].D("prelims") / 100;
        return $"Diretto {F(r.DirectCost, "N0")} €   +   cantiere {F(prelims, "N0")} €   +   imprevisti {F(r.TotalCost - r.DirectCost - prelims, "N0")} €   =   {F(r.TotalCost, "N0")} €";
    }

    /// <summary>The scope of the diagram of the details.</summary>
    public static string DetailScope(BridgeDesignModel.Result r) => r.Traffic is { } traffic ? traffic.Scope : r.Stations.Length == 0
        ? "Struttura superiore: equilibrio ideale e quantità. Non viene prodotto un diagramma globale da trave per archi, cavi o reticolari."
        : "Carico uniforme su tutte le campate. Diagramma riferito all'intero impalcato; non è l'inviluppo di un treno di carico.";

    public static IReadOnlyList<string> DetailColumns { get; } = ["Grandezza", "Valore", "Unità", "Regola / ipotesi"];

    public static string[][] DetailRows(BridgeDesignModel.Result r) => r.Details.Select(d => new[] { d.Name, F(d.Value, "N3"), d.Unit, d.Rule }).ToArray();

    public static IReadOnlyList<string> SupportColumns { get; } = ["Appoggio", "x [m]", "Tipo", "R G+Q [kN]", "H [m]", "Plinto B×W [m]", "Pali"];

    public static string[][] SupportRows(BridgeDesignModel.Result r) => r.Supports.Select(s => new[] { (s.Index + 1).ToString(), F(s.X), s.Type, F(s.Reaction), F(s.PierHeight),
        F(s.FootingSize) + "×" + F(s.FootingWidth), s.Piles.ToString() }).ToArray();

    /// <summary>A warning of the result.</summary>
    public static string Warning(string warning) => "• " + warning;

    public const string Exclusions = "Esclusi: sismica, vento, frenatura, urti, fatica, fessurazione, viscosità, perdite di precompressione, instabilità locale, collegamenti, fasi costruttive e verifiche geotecniche complete.";

    // ---------------------------------------------------------------- comparison A / B

    public const string ComparisonPlaceholder = "Fissa una configurazione con “Fissa A”, poi modifica il ponte. Qui appariranno gli scostamenti della soluzione B corrente.";
    public const string ComparisonUnavailable = "Confronto non disponibile.";
    public const string RestoreAButton = "Ripristina A", RemoveComparisonButton = "Rimuovi confronto";

    /// <summary>The text of an alternative A that cannot be calculated.</summary>
    public static string ComparisonError(string message) => "Alternativa A non calcolabile: " + message;

    public static string ComparisonTitle(BridgeDesignModel.Result old, BridgeDesignModel.Result r) =>
        $"A: {old.Family.Name}, {old.Spans.Length} campate  →  B: {r.Family.Name}, {r.Spans.Length} campate";

    public static IReadOnlyList<string> ComparisonColumns { get; } = ["Indicatore", "A salvata", "B corrente", "Δ B − A", "Δ %"];

    public static string[][] ComparisonRows(BridgeDesignModel.Result old, BridgeDesignModel.Result r)
    {
        var rows = new[] { ("Costo totale [€]", old.TotalCost, r.TotalCost), ("CO₂ [t]", old.Carbon, r.Carbon), ("Calcestruzzo [m³]", old.Concrete, r.Concrete),
            ("Acciaio strutturale [t]", old.Steel, r.Steel), ("Durata [mesi]", old.Duration, r.Duration), ("Altezza [m]", old.Depth, r.Depth) };
        return rows.Select(v => new[] { v.Item1, F(v.Item2), F(v.Item3), F(v.Item3 - v.Item2), v.Item2 != 0 ? F((v.Item3 / v.Item2 - 1) * 100) + "%" : "—" }).ToArray();
    }

    /// <summary>«Fissa A»: a copy of the data without the alternative A, to be pinned.</summary>
    public static JsonObject Pinned(JsonObject data)
    {
        var a = (JsonObject)data.DeepClone(); a.Remove("alternative_a"); return a;
    }

    /// <summary>«Ripristina A»: the data become those of <paramref name="copy"/> (a copy of A), keeping the alternative A pinned now.</summary>
    public static void Restore(JsonObject data, JsonObject copy, JsonNode? pinned)
    {
        data.Clear(); foreach (var p in copy) data[p.Key] = p.Value?.DeepClone();
        if (pinned is not null) data["alternative_a"] = pinned;
    }

    // ---------------------------------------------------------------- alternatives

    public const string AdviceTitle = "Alternative per ridurre le quantità e le emissioni";
    public const string AdviceNote = "Confronti sul modello corrente: disponibilità dei materiali, resistenza iniziale e larghezze stradali vanno valutate per il progetto. I prezzi restano quelli del listino impostato.";
    public const string RestoreEnvironmentButton = "Ripristina fattori ambientali ordinari";

    /// <summary>The caption of the button of an alternative.</summary>
    public static string AlternativeCaption(BridgeDesignAlternative alternative) =>
        $"{alternative.Label}   ·   −{F(alternative.CarbonSaving, "N0")} tCO₂e   ·   Δ {F(alternative.CostDifference, "N0")} €";

    /// <summary>The button of an alternative: its changes of the inputs.</summary>
    public static void ApplyAlternative(JsonObject input, BridgeDesignAlternative alternative)
    {
        foreach (var (key, value) in alternative.InputChanges) input[key] = value?.DeepClone();
    }

    /// <summary>«Ripristina fattori ambientali ordinari» is shown when the low-carbon concrete or the recycled steel is chosen.</summary>
    public static bool EnvironmentRestorable(JsonObject input) => input.B("low_carbon") || input.B("recycled");

    /// <summary>«Ripristina fattori ambientali ordinari».</summary>
    public static void RestoreEnvironment(JsonObject input) { input["low_carbon"] = false; input["recycled"] = false; }

    // ---------------------------------------------------------------- sections and dimensions

    public const string TechnicalNote = "Quote del modello usato per calcolare quantità e costi. Le misure in mm identificano gli spessori; le lunghezze sono in m. Schema di predimensionamento, non distinta esecutiva.";
    public const string SpanTitle = "Campate · progressive e sviluppo longitudinale";
    public const string SupportTitle = "Pile, spalle e fondazioni · un record per appoggio";
    public const string SupportNote = "B = dimensione longitudinale, W = trasversale, t = spessore. I pali sono indicati con numero × diametro × lunghezza. I volumi delle spalle sono equivalenti: il modello non definisce una carpenteria completa.";
    public const string TechnicalCsvButton = "Esporta sezioni e quote CSV";

    public static string TechnicalTitle(BridgeDesignModel.Result r) => r.Family.Name + " · dimensioni adottate";

    public static IReadOnlyList<string> TechnicalColumns { get; } = ["Componente", "Simbolo", "Valore", "Unità", "Origine", "Significato"];

    /// <summary>The value of a dimension with the digits of its unit.</summary>
    public static string Dimension(BridgeDesignModel.TechnicalItem t) => F(t.Value, t.Unit == "n." ? "N0" : t.Unit == "mm" ? "N1" : "N3");

    public static string[][] TechnicalRows(JsonObject data, BridgeDesignModel.Result r) => BridgeDesignModel.TechnicalSchedule(data, r).Select(t =>
        new[] { t.Component, t.Symbol, Dimension(t), t.Unit, t.Origin, t.Note }).ToArray();

    public static IReadOnlyList<string> SpanColumns { get; } = ["Campata", "Da x [m]", "A x [m]", "Luce Li [m]", "Elementi n", "n × Li [m]"];

    public static string[][] SpanRows(BridgeDesignModel.Result r) => BridgeDesignModel.SpanSchedule(r).Select(s =>
        new[] { s.Number.ToString(), F(s.Start, "N3"), F(s.End, "N3"), F(s.Length, "N3"), s.Elements.ToString(), F(s.DevelopedLength, "N3") }).ToArray();

    public static IReadOnlyList<string> SupportScheduleColumns { get; } = ["Appoggio / x", "Tipo / fusto", "H [m]", "Pulvino W×B×t [m]", "Fondazione B×W×t [m]", "Pali n×Ø×L [m]"];

    public static string[][] SupportScheduleRows(JsonObject data, BridgeDesignModel.Result r) => BridgeDesignModel.SupportSchedule(data, r).Select(s =>
        new[] { $"{s.Number} · {F(s.X, "N2")} m", s.Type == "Spalla" ? "Spalla equivalente" : s.Type + (s.Type.StartsWith("Antenna") ? $" · {s.Columns}×({F(s.Size, "N2")}×{F(s.Size, "N2")}) m" : s.WallWidth > 0 ? $" · {F(s.Size, "N2")}×{F(s.WallWidth, "N2")} m" : $" · {s.Columns}×Ø{F(s.Size, "N2")} m"),
            F(s.Height, "N2"), s.CapLength == 0 ? "—" : $"{F(s.CapLength, "N2")}×{F(s.CapWidth, "N2")}×{F(s.CapThickness, "N2")}",
            $"{F(s.FootingLength, "N2")}×{F(s.FootingWidth, "N2")}×{F(s.FootingThickness, "N2")}", s.Piles == 0 ? "Diretta" : $"{s.Piles}×Ø{F(s.PileDiameter, "N2")}×{F(s.PileLength, "N2")}" }).ToArray();

    // ---------------------------------------------------------------- print

    /// <summary>The texts of the printed page of «Stampa scheda», in order (the drawing is between the second and the third).</summary>
    public static IReadOnlyList<string> PrintLines(BridgeDesignModel.Result r)
    {
        var lines = new List<string> { "ANTHEA · Bridge Design", BridgeDesignModel.Scope,
            $"{r.Family.Name} · L {F(r.Length)} m · W {F(r.Width)} m · d {F(r.Depth)} m",
            $"Costo {F(r.TotalCost, "N0")} € · CO₂ {F(r.Carbon, "N0")} t · Durata {r.Duration:0} mesi" };
        foreach (var q in r.Quantities) lines.Add($"{q.Group} / {q.Item}: {F(q.Amount)} {q.Unit} × {F(q.Rate)} € = {F(q.Cost, "N0")} €");
        lines.Add("Listino e fattori ambientali indicativi. Dettagli e ipotesi completi nel report Word.");
        return lines;
    }
}
