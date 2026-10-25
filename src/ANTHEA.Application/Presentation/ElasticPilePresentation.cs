using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Modules;
namespace Anthea.Application.Presentation;

/// <summary>The tone of a text of the elastic response and of the reinforcement: the colour the historic view gives it.</summary>
public enum ElasticTone
{
    /// <summary>The ordinary text (Navy).</summary>
    Normal,
    /// <summary>A note (Muted).</summary>
    Muted,
    /// <summary>Not satisfied, an error (Firebrick).</summary>
    Failure,
    /// <summary>To complete (DarkGoldenrod).</summary>
    Pending,
    /// <summary>Satisfied (DarkGreen).</summary>
    Satisfied,
    /// <summary>Outside the scope (SlateGray).</summary>
    Excluded
}

/// <summary>A text of the panel «Sezione e verifiche»: the text, its tone, its size and weight in the historic view.</summary>
public sealed record ElasticPileText(string Text, ElasticTone Tone = ElasticTone.Normal, double Size = 12, bool Bold = false);

/// <summary>A section of a segment as drawn: outline, bars, the circular link with its diameter and name, axes and caption.</summary>
public sealed record ElasticPileSectionView(IReadOnlyList<double[]> Outline, IReadOnlyList<ReinforcementBar> Bars, double LinkRadius, double LinkDiameter,
    string LinkCaption, bool ShowAxes, string? Caption);

/// <summary>The section at the selected depth and its detail.</summary>
public sealed record ElasticPileSelectedSection(ElasticPileSectionView View, string Detail);

/// <summary>A row of «Valori per quota · N–V–M concomitanti»: the columns of the historic table, in their order and with their names.</summary>
public sealed record ElasticPilePointRow(double X_m, double Z_m, double Strato, string Lato, double Kh_kNm3, double K_kNm2, double Y_m, double Theta_rad,
    double N_kN, double V_kN, double M_kNm, double Q_kNm);

/// <summary>A group of the verdict of a segment (not satisfied, to complete, outside the scope): its items, open or closed, its tone.</summary>
public sealed record ElasticPileVerdictGroup(string Title, IReadOnlyList<string> Items, bool Expanded, ElasticTone Tone)
{
    /// <summary>The header of the group with the number of its items.</summary>
    public string Header => $"{Title} · {Items.Count}";

    /// <summary>The items, one per paragraph.</summary>
    public string Text => string.Join("\n\n", Items.Select(v => "• " + v));
}

/// <summary>The verdict of a segment: the state with its tone, the groups and the note when every check performed is satisfied.</summary>
public sealed record ElasticPileVerdict(string State, ElasticTone Tone, IReadOnlyList<ElasticPileVerdictGroup> Groups, string? Satisfied);

/// <summary>The checks of the selected segment in the panel «Sezione e verifiche».</summary>
public sealed record ElasticPileSegmentSummary(ElasticPileSectionView Section, string Details, ElasticPileVerdict Verdict, IReadOnlyList<ElasticPileText> Lines, string Error);

/// <summary>
/// The panel «Sezione e verifiche» of the reinforcement: the count of the segments by outcome (with a result), the segments to choose, the
/// selected one (clamped), the view, and the checks of the selected segment when the result has it.
/// </summary>
public sealed record ElasticPileSummary(ElasticPileText? Header, IReadOnlyList<string> Segments, int Selected, string Mode, ElasticPileSegmentSummary? Segment);

/// <summary>
/// The derivations of the tab «Risposta elastica · trave su molle» of the horizontal pile (ElasticPileWorkspace) and of the editor of its
/// reinforcement (PileReinforcementEditor), moved without changes from the historic view (F5.8): texts of the status, of the selected depth,
/// of the extremes and of the reinforcement, the rows of the table, the section at a depth, the comparison with an old archive, the cards
/// of the segments, the bills and the panel «Sezione e verifiche». The historic view and the ViewModel of F5.8 use them.
/// </summary>
public static class ElasticPilePresentation
{
    /// <summary>The line under the drawing before a depth is selected.</summary>
    public const string SelectionInitial = "Selezionare una quota sul profilo o su un diagramma.";

    /// <summary>The detail of the section before anything is calculated.</summary>
    public const string SectionDetailInitial = "Selezionare una quota per la sezione trasversale.";

    /// <summary>The detail of the section after a change.</summary>
    public const string SectionPending = "Sezione da verificare";

    /// <summary>The detail of the section without a selected depth (or a depth outside the segments of the result).</summary>
    public const string SectionNone = "Nessuna quota selezionata. Fare clic sul profilo o su un diagramma: la linea arancione identifica la sezione trasversale.";

    /// <summary>«Verifiche per tratto e sezioni critiche» after a change.</summary>
    public const string ReinforcementPending = "Verifiche e armature da aggiornare";

    /// <summary>Status after a change that keeps the FEM response (only the reinforcement changed).</summary>
    public const string StatusRetained = "FEM conservato (N, V, M invariati) · verifiche di sezione in coda";

    /// <summary>Status after a change of the shared data.</summary>
    public const string StatusChanged = "Dati modificati · analisi FEM e convergenza in coda";
    /// <summary>F5.15, E4: an archive to compare with the old one, before the choice of the data: nothing is calculated.</summary>
    public const string StatusMigration = "Archivio precedente da confrontare · nessun calcolo finché non si scelgono i dati";

    /// <summary>Status when the FEM starts without a response.</summary>
    public const string StatusResponseRunning = "1/2 · FEM su Winkler: spostamenti, N–V–M, equilibrio e confronto delle mesh";

    /// <summary>Status when the FEM response is ready and the section checks start.</summary>
    public const string StatusResponseReady = "2/2 · FEM aggiornato · preparazione del calcolo parallelo MRd";

    /// <summary>The initial state of the reinforcement editor before any calculation.</summary>
    public const string ReinforcementInitial = "Dati in attesa del ricalcolo automatico";

    /// <summary>The bill of a segment while the result is being updated.</summary>
    public const string BillPending = "Distinta in aggiornamento · vedere avanzamento sopra i tratti.";

    /// <summary>The panel «Sezione e verifiche» of a segment without a result.</summary>
    public const string SegmentPending = "Verifiche da aggiornare. I diagrammi FEM validi rimangono disponibili.";

    /// <summary>The note at the bottom of the panel «Sezione e verifiche».</summary>
    public const string SummaryNote = "Controlli distinti. Giunti, confinamento, SLE e dettagli esecutivi da completare nel campo applicabile.";

    /// <summary>Tool tip of the reinforcement fields of a segment that uses the main section.</summary>
    public const string SharedValue = "Valore condiviso: modificare la sezione principale o scollegare questo tratto.";

    /// <summary>Tool tip of «Fine tratto / barre».</summary>
    public const string SegmentEndToolTip = "Quota di taglio finale dalla testa, in metri; vuoto = punta. La barra successiva inizia a questa quota meno l₀.";

    /// <summary>Tool tip of «Secondo anello interno» of a custom segment.</summary>
    public const string InnerRingToolTip = "Seconda corona di barre all'interno della prima, come nella sezione in c.a.";

    /// <summary>Tool tip of the commands that need the result («Proponi suddivisione…», «Dimensiona…»).</summary>
    public const string ResultCommandToolTip = "Disponibile quando MRd e verifiche sono aggiornati; avanzamento sopra i tratti.";

    /// <summary>The views of the panel «Sezione e verifiche».</summary>
    public static IReadOnlyList<string> SummaryModes { get; } = ["Riepilogo", "N–M", "Taglio", "Interferro", "Minimi armatura", "Dettagli pilastro / trave", "Sisma · testa palo", "Copriferro", "Sviluppo / sovrapposizioni", "Distinta ferri"];

    /// <summary>The kinds of transverse reinforcement.</summary>
    public static IReadOnlyList<string> TransverseKinds { get; } = ["Da definire", "Staffe singole", "Spirale"];

    /// <summary>The check boxes of what the drawing shows: key and label, in their order.</summary>
    public static IReadOnlyList<(string Key, string Label)> Toggles { get; } = [("strati", "Stratigrafia"), ("falda", "Falda"), ("kh", "kh per strato"), ("mesh", "Nodi / discretizzazione"),
        ("molle", "Molle equivalenti"), ("carichi", "Carichi / vincoli"), ("sisma", "Zona sismica 10D"), ("k", "k distribuita"), ("y", "y"), ("theta", "θ"), ("q", "q"), ("N", "N"),
        ("V", "V"), ("M", "M"), ("tratti", "Tratti")];

    /// <summary>Whether the drawing shows <paramref name="key"/>: the saved choice, otherwise everything but the springs.</summary>
    public static bool Shown(JsonObject view, string key) => view[key] == null ? key != "molle" : view.B(key);

    /// <summary>The status after a calculation that failed.</summary>
    public static string Unavailable(string message) => "Calcolo non disponibile: " + message;

    /// <summary>The reinforcement sheet when the calculation failed.</summary>
    public static string SheetUnavailable(string message) => "Distinta non disponibile: " + message;

    /// <summary>The status of the phase of the parallel calculation of MRd.</summary>
    public static string Phase(PileProgress progress) => "2/2 · " + progress.Phase;

    /// <summary>The pieces of bars of the reinforcement sheet of a result (one per piece of every group of the bill).</summary>
    public static int SheetPieces(JsonObject? value) => value?["armature"]?.Array("distinta").OfType<JsonObject>().Sum(run => run.Array("Pieces").OfType<JsonObject>().Count()) ?? 0;

    /// <summary>
    /// The message of the reinforcement sheet when it draws nothing: the reason given, otherwise «Distinta in aggiornamento…»; for a result
    /// without pieces, why each segment has none (PileReinforcementDrawing.Set).
    /// </summary>
    public static string SheetMessage(JsonObject? value, string? unavailableReason = null)
    {
        string message = unavailableReason ?? BillPending;
        if (value != null && SheetPieces(value) == 0)
        {
            var reasons = (value["armature"]?.Array("tratti").OfType<JsonObject>() ?? []).Select(t => $"{t.S("id")}: {t.S("errore", t.S("stato", "Dati del tratto non disponibili."))}").ToArray();
            message = "Distinta non disponibile\n" + (reasons.Length > 0 ? string.Join("\n", reasons) : value["armature"].S("stato", "Completare i dati della sezione e dei tratti."));
        }
        return message;
    }

    /// <summary>The stratigraphy chosen for the response («elastico.stratigrafia», −1 when none).</summary>
    public static int SurveyChoice(JsonObject elastic) => (int)elastic.D("stratigrafia", -1);

    /// <summary>
    /// The point of the response at a click at <paramref name="depth"/> from the head (ElasticPileDrawing.SelectDepth): the nearest point; at
    /// an interface, where two points share the depth, the other side when the first one is <paramref name="selected"/>.
    /// </summary>
    public static JsonNode PointAt(JsonObject shown, double depth, JsonNode? selected)
    {
        var points = shown["risposta"]!.Array("Points"); var nearest = points.OrderBy(p => Math.Abs(p.D("Depth") - depth)).First();
        var pair = points.Where(p => Math.Abs(p.D("Depth") - nearest.D("Depth")) < 1e-9).ToArray();
        return pair.Length > 1 && ReferenceEquals(selected, pair[0]) ? pair[1] : pair[0]!;
    }

    /// <summary>The choices of the stratigraphy of the response.</summary>
    public static IReadOnlyList<string> SurveyChoices(int count) => Enumerable.Range(0, count).Select(i => $"Stratigrafia {i + 1}").ToArray();

    /// <summary>The text of the comparison with an archive of the old elastic input.</summary>
    public static string MigrationText(JsonObject parent)
    {
        var old = parent["elastico_legacy"]!; var g = parent["generali"]!;
        return $"Questo archivio contiene input elastici separati. Confrontare prima di proseguire.\nDati comuni: D={g.S("diametro")} m; L infissa={g.S("lunghezza")} m; H={g.S("azione_orizzontale")} kN; e da p.c.={g.S("eccentricita")} m.\nLegacy: D={old.S("diametro")} m; L totale={old.S("lunghezza")} m; libero={old.S("libero")} m; H={old.S("H")} kN; C={old.S("C")} kNm; e dalla testa={old.S("e")} m; EJ={old.S("EI")} kNm².\nGli originali vengono conservati. Importare aggiunge una stratigrafia e imposta EJ come override: completare poi la classificazione per Broms.";
    }

    /// <summary>«Info · risposta elastica del palo».</summary>
    public static string Info => ElasticPileEditor.Limits + "\n\n" + HorizontalSoilPresentation.Source + "\n\n" + HorizontalSoilPresentation.StratificationConvention + "\n\n" + ElasticPileEditor.Signs + "\n\nFEM Euler–Bernoulli: due gdl/nodo, matrice consistente ∫k NᵀN dx. kh=nh z/D; k=kh D=nh z. 1 N/cm³=1000 kN/m³. K* nodale illustrativa = integrale di k sulle mezze lunghezze degli elementi adiacenti [kN/m]; non sostituisce la matrice del solutore.\n\nCtesta=H(Llibero−e_da_pc): nessun doppio conteggio del braccio. La media degli estremi della singola riga è un valore iniziale del software, non un valore consigliato dall'autore. Fonti e modifiche sono registrate.\n\nEJ: sezione circolare integra lorda con Ecm da Model; CHS solo acciaio. Override motivato nelle opzioni della sezione. Broms è una capacità limite distinta. N=Ntesta+w x: peso proprio dai materiali, senza galleggiamento automatico, attrito laterale o secondo ordine. Le verifiche N–M e taglio sono distinte dai dettagli costruttivi. MRd tratteggiato è interrotto nelle zone prive di sviluppo delle barre. Le fasce colorate richiedono dettagli locali; giunti e confinamento restano preliminari. Minimi pali NTC §7.2.5 selezionabili; zone dissipative, confinamento dei giunti e SLE richiedono completamento. Le proposte non sostituiscono automaticamente i dati personalizzati.";

    /// <summary>The line of a selected point of the response.</summary>
    public static string Selection(JsonNode p) => $"x={p.D("Depth"):G5} m · z={p.D("GroundDepth"):G5} m · strato {p.D("Layer")} · {p.S("Side")} · kh={p.D("Kh"):G6} kN/m³ · k={p.D("DistributedStiffness"):G6} kN/m²\ny={p.D("Displacement"):G6} m · θ={p.D("Rotation"):G6} rad · N={p.D("AxialForce"):G6} kN · V={p.D("Shear"):G6} kN · M={p.D("Moment"):G6} kNm · q={p.D("SoilReaction"):G6} kN/m.";

    /// <summary>The status after a complete result.</summary>
    public static string Completed(JsonObject result)
    {
        var r = result["risposta"]!;
        return $"Stratigrafia {result["dati_condivisi"].D("stratigrafia")} · {r.D("Elements")} elementi · " + (r.B("MeshConverged") ? "convergenza ≤ 0,1%" : "RAFFINARE LA MESH") + $" · y testa {r.D("HeadDisplacement"):G6} m · θ testa {r.D("HeadRotation"):G6} rad";
    }

    /// <summary>«Verifiche per tratto e sezioni critiche» of a result.</summary>
    public static string ReinforcementSummary(JsonObject result) => string.Join("\n", result["armature"]!.Array("tratti").Select(t => { var c = t!["critica"]; var (depth, n, v, m) = Action(c?["Action"]); return $"{t.S("id")} · {t.D("inizio"):G5}–{t.D("fine"):G5} m · {t.S("stato")} {t.S("errore")}\nCritica x={depth:G5} m: N={n:G5} kN, V={v:G5} kN, M={m:G5} kNm; MRd nominale +={c?["MRdPositive"]}, MRd−={c?["MRdNegative"]} kNm; VRd={c?["VRd"]} kN. {c.S("Message")}"; }));

    /// <summary>«Estremi, equilibrio e convergenza» of a response.</summary>
    public static string ResponseSummary(JsonObject response)
    {
        var r = response["risposta"]!;
        var lines = new List<string> { $"Residui ΣF={r.D("ForceResidual"):G4} kN, ΣM={r.D("MomentResidual"):G4} kNm; reazioni C testa={r.D("HeadReactionMoment"):G5} kNm, H punta={r.D("TipReactionForce"):G5} kNm, C punta={r.D("TipReactionMoment"):G5} kNm.", $"Mesh {r.D("ComparisonElements")} / {r.D("Elements")} · Δy={r["RelativeMeshChanges"].D("y"):P4}, ΔM={r["RelativeMeshChanges"].D("M"):P4}, ΔV={r["RelativeMeshChanges"].D("V"):P4}" };
        foreach (var (key, unit) in new[] { ("y", "m"), ("theta", "rad"), ("N", "kN"), ("V", "kN"), ("M", "kNm"), ("q", "kN/m") }) { var ex = r["Extrema"]![key]!; lines.Add($"{key} [{unit}]: min {ex.D("Minimum"):G6} a x={ex.D("MinimumDepth"):G5}; max {ex.D("Maximum"):G6} a x={ex.D("MaximumDepth"):G5}; |max|={ex.D("AbsoluteMaximum"):G6} a x={ex.D("AbsoluteMaximumDepth"):G5} m."); }
        return string.Join("\n", lines);
    }

    /// <summary>The rows of «Valori per quota · N–V–M concomitanti» of a response.</summary>
    public static ElasticPilePointRow[] Points(JsonObject response) => response["risposta"]!.Array("Points").Select(p => new ElasticPilePointRow(p.D("Depth"), p.D("GroundDepth"), p.D("Layer"), p.S("Side"), p.D("Kh"), p.D("DistributedStiffness"), p.D("Displacement"), p.D("Rotation"), p.D("AxialForce"), p.D("Shear"), p.D("Moment"), p.D("SoilReaction"))).ToArray();

    /// <summary>The section of a segment of the result (contorno_sezione, barre_sezione, the link of its section) with a caption.</summary>
    public static ElasticPileSectionView SegmentSection(JsonNode segment, string caption)
    {
        var section = segment["sezione"]!;
        return new(segment.Array("contorno_sezione").Select(a => a!.AsArray().Select(v => J.Number(v) ?? 0).ToArray()).ToList(),
            segment.Array("barre_sezione").Select(a => { var b = a!.AsArray().Select(v => J.Number(v) ?? 0).ToArray(); return new ReinforcementBar(b[0], b[1], b[2], b[3]); }).ToList(),
            segment.D("staffa_raggio_mm"), section.D("transverse_bar_diameter_mm"), section.S("tipo_trasversale") == "Spirale" ? "Spirale" : "Staffa", true, caption);
    }

    /// <summary>
    /// «Sezione trasversale alla quota selezionata»: the segment of the result at the selected point (the lower one on the side «Below» of a
    /// boundary) drawn, with its detail; null when no segment is at the point (the detail is then <see cref="SectionNone"/>).
    /// </summary>
    public static ElasticPileSelectedSection? SectionAt(JsonObject? result, JsonNode? p)
    {
        if (p == null || result?["armature"] is not JsonNode arm) return null;
        double x = p.D("Depth"); var matching = arm.Array("tratti").Where(t => x >= t.D("inizio") && x <= t.D("fine")).ToArray();
        var s = p.S("Side") == "Below" ? matching.LastOrDefault() : matching.FirstOrDefault();
        if (s == null) return null;
        var section = s["sezione"]!;
        return new(SegmentSection(s, $"Sez. {s.S("id")} · x={x:0.000} m dalla testa"),
            $"{s.S("id")} · x={x:G6} m DALLA TESTA · z={p.D("GroundDepth"):G6} m DAL PIANO CAMPAGNA\nLato {p.S("Side")} · riferimento: linea arancione sui diagrammi · {s.S("stato")}\n{section.D("longitudinal_bar_count")} φ{section.D("longitudinal_bar_diameter_mm")} · {section.S("tipo_trasversale", "Staffe singole")} φ{section.D("transverse_bar_diameter_mm")}/{section.D("transverse_spacing_mm")} mm\nCopriferro {section.D("cover_mm")} mm\n" + s["dettaglio"].S("Status"));
    }

    // ---------------------------------------------------------------- reinforcement editor

    /// <summary>The headings of the cards of the segments: identifier, start → end and length, with the end at the tip when not assigned.</summary>
    public static IReadOnlyList<string> SegmentHeaders(JsonObject root)
    {
        var rows = root["elastico"]!.Array("tratti"); var list = new List<string>();
        double start = 0; double total = root["generali"].D("lunghezza") + root["generali"].D("tratto_libero");
        foreach (var node in rows) { var row = node!.AsObject(); double endValue = J.Number(row["fine"]) ?? total; list.Add($"{row.S("id")} · {start:G4} → {endValue:G4} m · L={endValue - start:G4} m"); start = endValue; }
        return list;
    }

    /// <summary>The tool tip of a reinforcement field of a segment (linked: shared; otherwise the label, with a note when it takes the main section).</summary>
    public static string SegmentFieldToolTip(JsonObject row, string key, string label) => row.B("collegato") ? SharedValue : label + (row[key] == null ? " · campo non assegnato: valore dalla sezione principale" : "");

    /// <summary>The bills of a segment: its groups of bars with the links, or why they are not available.</summary>
    public static string BillFor(JsonObject? current, string calculationState, string id)
    {
        if (current == null) return calculationState.StartsWith("Calcolo non disponibile") ? "Distinta non disponibile · " + calculationState : BillPending;
        var arm = current["armature"]; var record = arm?.Array("tratti").FirstOrDefault(t => t.S("id") == id);
        var runs = arm?.Array("distinta").Where(r => r.S("Segment").Split(" / ").Contains(id)).ToArray();
        if (runs?.Length > 0) return string.Join("\n\n", runs.Select(r => Bill(r!))) + LinkBill(record?["distinta_staffe"]);
        string reason = record.S("errore", record.S("stato", arm.S("stato", "Dati del tratto non disponibili.")));
        return "Distinta non disponibile: " + reason + LinkBill(record?["distinta_staffe"]);
    }

    /// <summary>The title of a group of bars, the same in the bill and in «Sviluppo / sovrapposizioni».</summary>
    static string RunTitle(JsonNode run) => $"{run.S("Id")} · {run.D("Count"):0} Ø{run.D("Diameter"):0}";

    static string Bill(JsonNode run) => RunTitle(run) + $" · tratti {run.S("Segment")}\n{run.S("StartKind")} → {run.S("EndKind")}\n" + string.Join("\n", run.Array("Pieces").Select(p => $"{p.S("Id")}: {p.D("Count")} φ{p.D("Diameter")} · x={p.D("Start"):0.00}–{p.D("End"):0.00} m · taglio {p.D("CuttingLength"):0.00} m · commerciale {p.D("StockLength"):0.0} m")) + "\n" + string.Join("\n", run.Array("Joints").Select(JointText)) + "\n" + run.S("Status");

    /// <summary>The text of a joint (lap) of the bars.</summary>
    public static string JointText(JsonNode? j) => $"{j.S("Id")} · {j.S("Kind")} · {j.D("MatchedCount"):0}/{j.D("Count"):0} coppie allineate · {j.S("UpperRun")} ↔ {j.S("LowerRun")}\nx={j.D("Start"):0.00}–{j.D("End"):0.00} m · iniziale {j.D("InitialLength"):0.00} / richiesta {j.D("RequiredLength"):0.00} / adottata {j.D("AdoptedLength"):0.00} / effettiva {j.D("ActualLength"):0.00} m\n{j.S("Note")}";

    static string LinkBill(JsonNode? links) => links == null ? "" : $"\n{links.S("tipo", "Staffe singole")}: {links.D("quantita")} × φ{links.D("diametro_mm")} · passo ≤ {links.D("passo_massimo_mm")} mm · effettivo {links.D("passo_effettivo_mm"):0.#} mm" + (links.S("tipo") == "Spirale" ? $" · {links.D("spire"):0} spire" : "") + $"\nL geometrica totale {links.D("lunghezza_geometrica_m"):0.00} m (esclusi ganci/ancoraggi).\n{links.S("stato")}";

    static string Number(JsonNode? row, string key) => J.Number(row?[key])?.ToString("G5") ?? "—";

    /// <summary>Depth and forces of an action of the verifications, read once (the critical one, or the most stressed of a view).</summary>
    static (double Depth, double N, double V, double M) Action(JsonNode? a) => (a.D("Depth"), a.D("N"), a.D("V"), a.D("M"));

    /// <summary>The tone of the verdict of a segment: rosso, verde, otherwise ambra.</summary>
    public static ElasticTone VerdictTone(JsonNode summary) => summary.S("colore") switch { "rosso" => ElasticTone.Failure, "verde" => ElasticTone.Satisfied, _ => ElasticTone.Pending };

    /// <summary>The verdict of a segment of the result (its «riepilogo», or the one built from its typed issues).</summary>
    public static ElasticPileVerdict Verdict(JsonNode record)
    {
        var value = record["riepilogo"] ?? ElasticPileEditor.ReinforcementSummary(record.AsObject());
        var groups = new List<ElasticPileVerdictGroup>();
        foreach (var (key, title, tone) in new[] { ("non_soddisfatti", "NON SODDISFATTI", ElasticTone.Failure), ("da_completare", "DA COMPLETARE", ElasticTone.Pending), ("esclusi", "FUORI DAL PERIMETRO", ElasticTone.Excluded) })
        {
            var items = value.Array(key); if (items.Count == 0) continue;
            groups.Add(new(title, items.Select(v => v?.ToString() ?? "").ToArray(), key != "esclusi", tone));
        }
        return new(value.S("stato"), VerdictTone(value), groups,
            value.B("eseguiti_soddisfatti") ? "Tutti i controlli eseguiti sul tratto sono soddisfatti. Le verifiche escluse restano elencate separatamente." : null);
    }

    /// <summary>
    /// The panel «Sezione e verifiche»: null without segments (the panel is not rebuilt). <paramref name="selected"/> is clamped to the
    /// segments; <paramref name="calculationState"/> is the state shown above the segments (the bill of a failed calculation).
    /// </summary>
    public static ElasticPileSummary? Summary(JsonObject root, JsonObject? result, int selected, string mode, string calculationState)
    {
        var rows = root["elastico"]!.Array("tratti"); if (rows.Count == 0) return null; selected = Math.Clamp(selected, 0, rows.Count - 1);
        ElasticPileText? header = null;
        var all = result?["armature"]?.Array("tratti");
        if (all != null) { int failed = all.Count(r => r?["riepilogo"].S("colore") == "rosso"), waiting = all.Count(r => r?["riepilogo"].S("colore") == "ambra"); header = new($"PALO · {all.Count} tratti\n{failed} non verificati · {waiting} incompleti · {all.Count - failed - waiting} con controlli soddisfatti", failed > 0 ? ElasticTone.Failure : waiting > 0 ? ElasticTone.Pending : ElasticTone.Satisfied, 12, true); }
        var record = result?["armature"]?.Array("tratti").FirstOrDefault(r => r.S("id") == rows[selected].S("id"));
        ElasticPileSegmentSummary? segment = null;
        if (record != null)
        {
            var section = SegmentSection(record, $"{record.S("id")} · sezione costante x={record.D("inizio"):0.00}–{record.D("fine"):0.00} m");
            string details = "Dettagli: " + record.S("comportamento_dettagli") + " · esposizione " + record["durabilita"].S("esposizione") + " · cmin,dur " + Number(record["durabilita"], "cmin_dur") + " mm";
            segment = new(section, details, Verdict(record), ModeLines(record, mode, result!, calculationState), record.S("errore"));
        }
        return new(header, rows.Select(r => r.S("id")).ToArray(), selected, mode, segment);
    }

    static List<ElasticPileText> ModeLines(JsonNode record, string mode, JsonObject result, string calculationState)
    {
        var lines = new List<ElasticPileText>();
        if (mode == "N–M" || mode == "Taglio")
        {
            string key = mode == "N–M" ? "BendingRatio" : "ShearRatio"; var c = record.Array("verifiche").OrderByDescending(c => J.Number(c?[key]) ?? double.PositiveInfinity).FirstOrDefault(); var (depth, n, v, m) = Action(c?["Action"]);
            lines.Add(new($"Quota critica x={depth:0.00} m\nN={n:G5} kN · V={v:G5} kN\nM={m:G5} kNm\nη={Number(c, key)}\n" + (mode == "N–M" ? $"MRd+={Number(c, "MRdPositive")}\nMRd−={Number(c, "MRdNegative")} kNm" : $"VRd={Number(c, "VRd")} kN")));
        }
        else if (mode == "Sisma · testa palo")
        {
            var seismic = record["sisma_testa"];
            lines.Add(new(seismic.B("Active") ? $"Zona di testa: x=0–{seismic.D("HeadEnd"):0.00} m\n10D = {seismic.D("RequiredHeadLength"):0.00} m\n{seismic.S("Status")}" : "Controlli sismici non attivi. Abilitarli nelle ipotesi Sisma · testa del palo.", ElasticTone.Normal, 12, true));
            foreach (var c in seismic.Array("Checks"))
            {
                string state = c?["Passed"] == null ? "Da completare" : c.B("Passed") ? "Soddisfatto" : "Non soddisfatto";
                lines.Add(new($"{c.S("Title")} · {state}\nValore {Number(c, "Actual")} / limite {Number(c, "Limit")} {c.S("Unit")}" + (c?["Depth"] == null ? "" : $" · x={c.D("Depth"):0.00} m") + $"\n{c.S("Criterion")}", c?["Passed"] != null && !c.B("Passed") ? ElasticTone.Failure : ElasticTone.Normal));
            }
            lines.Add(new(seismic.S("Scope"), ElasticTone.Normal, 11));
        }
        else if (mode == "Distinta ferri") lines.Add(new(BillFor(result, calculationState, record.S("id"))));
        else if (mode == "Sviluppo / sovrapposizioni")
        {
            foreach (var run in record.Array("dettagli_barre"))
            {
                lines.Add(new(RunTitle(run!) + $"\n{run.S("StartKind")} → {run.S("EndKind")}\nlbd={run.D("Anchorage"):0.00} m · aₗ={run.D("Shift"):0.00} m\n{run.S("Status")}"));
                foreach (var joint in run!.Array("Joints")) lines.Add(new(JointText(joint), ElasticTone.Normal, 11));
            }
        }
        else if (mode != "Riepilogo")
            foreach (var c in record.Array("controlli_costruttivi").Where(c => mode == "Interferro" ? c.S("Key") == "ClearSpacing" : mode == "Minimi armatura" ? c.S("Key").StartsWith("Pile") || c.S("Key").Contains("Longitudinal") : mode == "Dettagli pilastro / trave" ? !c.S("Key").Contains("Cover") && c.S("Key") != "ClearSpacing" : c.S("Key").Contains("Cover")))
            {
                string name = ElasticPileEditor.ConstructionLabel(c.S("Key"));
                string verdict = c?["Passed"] == null ? "Da completare" : c.B("Passed") ? "Soddisfatto" : "Non soddisfatto";
                lines.Add(new($"{name} · {verdict}\nValore {Number(c, "Actual")} / limite {Number(c, "Limit")} {c.S("Unit")}\n{c.S("Explanation")}\n{c.S("Reference")}"));
            }
        return lines;
    }
}
