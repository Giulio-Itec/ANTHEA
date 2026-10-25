using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Anthea.Calculations;

namespace Anthea.Application.Concrete;

/// <summary>A value card of the selected proposal: title, value and explanation (the historic VerificationCards.AddValue).</summary>
public sealed record ConcreteDesignValue(string Title, string Value, string Explanation);

/// <summary>A check of the selected proposal as a detail card: title, outcome, values, explanation and reference.</summary>
public sealed record ConcreteDesignCheck(string Title, bool? Passed, string Values, string Explanation, string Reference);

/// <summary>
/// The texts and the data of the reinforcement design of the RC section (ConcreteDesign.cs of the historic view, moved in F5.10 S8): the fields
/// of the search shown, the signature of the data of a search, the texts of the progress, of the outcome and of the exclusions, the rows of the
/// alternatives, the description of the selected proposal and its application to the data of the sheet («Applica configurazione»).
/// </summary>
public static class ConcreteDesignPresentation
{
    /// <summary>The status before any search.</summary>
    public const string Idle = "Impostare gli elenchi e avviare la ricerca sulle combinazioni del foglio.";
    /// <summary>The description before any proposal.</summary>
    public const string NoProposal = "La configurazione proposta comparirà qui.";
    /// <summary>The description while the search runs.</summary>
    public const string Searching = "Ricerca in corso…";
    /// <summary>The status after a change of the data of a search.</summary>
    public const string Stale = "Dati modificati: ricalcolare la ricerca prima di applicare una proposta.";
    /// <summary>The status of a search stopped.</summary>
    public const string Interrupted = "Ricerca interrotta.";
    /// <summary>The description of a search that could not start.</summary>
    public const string CompleteTheData = "Completare i dati indicati e ripetere la ricerca.";
    /// <summary>The note above the form of the search.</summary>
    public const string Notice = "Geometria, materiali, copriferro e azioni sono quelli del foglio. Il tipo di elemento e l’esposizione vanno compilati nelle schede comuni. Resistenza 3D iterativa a eccentricità costante.";
    /// <summary>The note of the limits.</summary>
    public const string LimitsNotice = "0 < η ≤ 1. La ricerca considera tutte le combinazioni compilate, anche quelle nascoste nei grafici. I dettagli numerici devono rispettare i limiti normativi.";
    /// <summary>The choices of «Mostra le prime».</summary>
    public static ImmutableArray<string> ShownChoices { get; } = ["1", "5", "10", "25", "50", "Tutte"];

    /// <summary>The status of a search that could not start.</summary>
    public static string NotStarted(string error) => "Ricerca non avviata: " + error;

    /// <summary>
    /// The status after «Applica configurazione», with the notice of the manual shear parameters kept (ARM-1). F5.15, S8-c (F5-50): it also says
    /// that the search is superseded by the new arrangement, as the invalidation of the sheet marked it.
    /// </summary>
    public static string Applied(string shearNotice) =>
        "Configurazione applicata. Verifiche del foglio in aggiornamento; riscontri esecutivi da confermare sulla nuova disposizione. "
        + "La ricerca è superata: ripeterla per nuove alternative." + (shearNotice == "" ? "" : " " + shearNotice);

    /// <summary>
    /// The fields of the search shown (RefreshDesignInputs of the historic view): the counts by face for the non circular sections, the circular
    /// counts for the circular ones, the orthogonal bars for slabs and walls, the weights of the objective «Compromesso», the links searched,
    /// the sample size without the complete search.
    /// </summary>
    public static IReadOnlyList<(string Key, bool Shown)> FieldsShown(JsonObject input, JsonObject workspace)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(workspace);
        var options = workspace["calcola_armature"]!.AsObject();
        bool circular = input.S("shape") == "Circolare", plate = workspace["dettagli_costruttivi"].S("elemento") is "Soletta piena" or "Parete";
        var shown = new List<(string, bool)>();
        foreach (string k in new[] { "n_superiori", "n_inferiori", "n_laterali", "simmetrica" }) shown.Add((k, !circular));
        shown.Add(("n_circolari", circular));
        foreach (string k in new[] { "diametri_ortogonali", "passi_ortogonali" }) shown.Add((k, plate));
        foreach (string k in new[] { "peso_peso", "peso_barre", "peso_diametri" }) shown.Add((k, options.S("obiettivo") == "Compromesso"));
        bool varying = options.S("staffe") is "Ottimizza" or "Con e senza staffe";
        shown.Add(("diametri_staffe", varying)); shown.Add(("passi_staffe", varying));
        shown.Add(("max_tentativi", !options.B("ricerca_completa", true)));
        return shown;
    }

    /// <summary>The data a search depends on (DesignFingerprint of the historic view): the inputs, the actions and the settings without the state of the interface.</summary>
    public static string Fingerprint(JsonObject data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var ws = (JsonObject)data["workspace_ca"]!.DeepClone();
        foreach (string key in new[] { "tab", "layout", "resistenze_rapide", "proposta_armature_applicata" }) ws.Remove(key);
        ws["calcola_armature"]!.AsObject().Remove("mostra");
        return data["input"]!.ToJsonString() + data["combinazioni"]!.ToJsonString() + ws.ToJsonString();
    }

    /// <summary>The fraction of the configurations examined (0 when none is planned).</summary>
    public static double Fraction(int evaluated, int planned) => planned == 0 ? 0 : (double)evaluated / planned;

    /// <summary>The status while the search runs.</summary>
    public static string ProgressText(ConcreteResults.DesignProgress p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return $"{p.Evaluated:N0} / {p.Planned:N0} configurazioni · {p.Accepted:N0} ammissibili · {p.Seconds:0.#} s\n{p.Phase}" +
            (p.Phase.StartsWith("Controlli preliminari") ? $" ({p.PreliminaryChecked:N0} esaminati)" : "") + $" · fino a {p.Workers} calcoli contemporanei";
    }

    /// <summary>The status of a search ended.</summary>
    public static string Status(ConcreteResults.DesignResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return (r.Completed ? "Ricerca completata" : "Ricerca parziale · interruzione o limite tentativi") +
            $" · {r.Evaluated:N0}/{r.Planned:N0} configurazioni · {r.Candidates.Count:N0} ammissibili" +
            (r.Performance is { } perf ? $" · {perf.Seconds:0.##} s" : "");
    }

    /// <summary>The coverage of a search ended: complete, or the configurations not calculated.</summary>
    public static string Coverage(ConcreteResults.DesignResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return r.Completed ? $"Esaminate tutte le {r.Planned:N0} configurazioni ammesse." :
            $"Ricerca parziale: {r.Planned - r.Evaluated:N0} configurazioni non calcolate. Attivare la ricerca completa o ripeterla senza interromperla.";
    }

    /// <summary>The description and the scope of a search without admissible configurations.</summary>
    public static (string Description, string Scope) NoCandidates(ConcreteResults.DesignResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return (r.Completed ? "Nessuna configurazione rispetta tutti i vincoli negli elenchi assegnati." : "Nessuna soluzione nel campione esaminato · ricerca incompleta.",
            Coverage(r) + "\n\nPrimi motivi di scarto:\n" + string.Join("\n", r.Exclusions.OrderByDescending(x => x.Value).Take(4).Select(x => $"{x.Value:N0} · {x.Key}")) +
            "\n\nValori, limiti e combinazioni nella scheda Ricerca e scarti.");
    }

    /// <summary>«Ricerca e scarti»: the coverage, the times, every exclusion with its example and the notes of the search.</summary>
    public static string Diagnostics(ConcreteResults.DesignResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        string scarti = string.Join("\n\n", r.Exclusions.OrderByDescending(x => x.Value).Select(x =>
        {
            string text = $"{x.Value:N0} configurazioni · {x.Key}";
            if (r.ExclusionExamples.TryGetValue(x.Key, out var example)) text += "\nEsempio: " + example.Combination +
                (example.Ratio is double eta ? $" · η = {eta:0.###}, obiettivo ≤ {example.Target:0.###}" : "") + "\n" + example.Detail;
            return text;
        }));
        string timing = r.Performance is { } p ? $"\n\nTempi di ricerca: {p.Seconds:0.##} s · fino a {p.Workers} calcoli contemporanei." +
            (p.FirstSolutionSeconds is double first ? $" Prima configurazione ammissibile dopo {first:0.##} s." : "") +
            $"\n{p.LongitudinalCalculations:N0} sezioni calcolate per resistenza/SLE, {p.LongitudinalReuses:N0} risultati riutilizzati per altri passi staffa; {p.ProbeSections} sezioni nelle prove iniziali." +
            "\nTempi per fase (sommati fra i calcoli contemporanei; non sono la durata complessiva):\n" +
            string.Join("\n", p.Stages.Select(s => $"{s.Name}: {s.Seconds:0.##} s · {s.Calls:N0} esecuzioni")) : "";
        return Coverage(r) + timing + "\n\nScarti (primo motivo per configurazione; gli esempi non sono soluzioni accettate):\n\n" +
            (scarti.Length > 0 ? scarti : "Nessuno scarto.") + "\n\n" + string.Join("\n\n", r.Notes);
    }

    /// <summary>The rows of «Alternative»: the first proposals as many as «Mostra le prime» (at least one), with their rank and their metrics.</summary>
    public static IReadOnlyList<JsonObject> RankingRows(ConcreteResults.DesignResult result, JsonObject options)
    {
        ArgumentNullException.ThrowIfNull(result); ArgumentNullException.ThrowIfNull(options);
        int top = options.S("mostra") == "Tutte" ? int.MaxValue : (int)options.D("mostra", 10);
        int rank = 0;
        return result.Candidates.Take(Math.Max(1, top)).Select(c => J.Obj(("id", c.Id.ToString()), ("rank", ++rank), ("config", c.Description),
            ("peso", EngineeringFormat.Number(c.Metrics.KgPerM)), ("barre", c.Metrics.Bars), ("pezzi", EngineeringFormat.Number(c.Metrics.PiecesPerM)),
            ("diametri", c.Metrics.Diameters), ("pareto", c.Pareto ? "Sì" : ""), ("esito", c.NeedsReview ? "Da completare" : "Completati"))).ToList();
    }

    /// <summary>The value cards of the selected proposal: weight, quantities, the check nearest to its target, the comparison with the initial sheet.</summary>
    public static IReadOnlyList<ConcreteDesignValue> Metrics(ConcreteResults.DesignCandidate c, ConcreteResults.DesignResult? result)
    {
        ArgumentNullException.ThrowIfNull(c);
        var values = new List<ConcreteDesignValue>
        {
            new("Peso stimato", $"{c.Metrics.KgPerM:0.###} kg/m", $"Longitudinali {c.Metrics.LongitudinalKgPerM:0.###}; staffe e ortogonali {c.Metrics.TransverseKgPerM:0.###} kg/m. Esclusi sfridi e sovrapposizioni."),
            new("Quantità", $"{c.Metrics.Bars} barre · {c.Metrics.Diameters} diametri", $"As = {c.Metrics.SteelArea:0.#} mm² · {c.Metrics.PiecesPerM:0.##} pezzi/m convenzionali. " + (c.Pareto ? "Soluzione Pareto." : ""))
        };
        var governing = c.Checks.Where(ch => ch.Ratio is not null).MaxBy(ch => ch.Ratio / ch.Target);
        if (governing is not null) values.Add(new("Verifica più vicina all’obiettivo", $"η {governing.Ratio:0.###} / {governing.Target:0.###}", governing.Name + " · " + governing.Combination));
        if (result?.Baseline is { } baseline) values.Add(new("Rispetto al foglio iniziale", $"{c.Metrics.KgPerM - baseline.KgPerM:+0.###;-0.###;0} kg/m", $"Peso iniziale stimato {baseline.KgPerM:0.###} kg/m. Il confronto non attesta la verifica dell’armatura iniziale."));
        return values;
    }

    /// <summary>The scope of the selected proposal: its state, what «Applica» replaces, the orthogonal mesh of slabs and walls.</summary>
    public static string Scope(ConcreteResults.DesignCandidate c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return (c.NeedsReview ? "Controlli numerici soddisfatti; dettagli esecutivi da completare. " : "Controlli richiesti soddisfatti. ") +
            "La proposta sostituisce barre manuali, secondi strati e staffe del foglio solo con Applica. " +
            (c.Detailing.S("elemento") is "Soletta piena" or "Parete" ? $"Rete ortogonale: As totale {c.Detailing.D("as_secondaria"):0.#} mm²/m, metà per faccia; la vista mostra la sezione longitudinale. " : "") +
            "Le schede dei dettagli conservano le verifiche da completare.";
    }

    /// <summary>The checks of the selected proposal, as detail cards.</summary>
    public static IReadOnlyList<ConcreteDesignCheck> Checks(ConcreteResults.DesignCandidate c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Checks.Select(ch => new ConcreteDesignCheck(ch.Name, ch.Passed,
            ch.Combination + (ch.Ratio is double eta ? $" · η = {eta:0.####} ≤ {ch.Target:0.###}" : ""), ch.Detail, "Calcolo della configurazione selezionata")).ToList();
    }

    /// <summary>
    /// «Applica configurazione» on the data of the sheet (ApplyDesign of the historic view): the inputs of the proposal replace those of the sheet
    /// (the same object, cleared and filled), the shear fields of the proposal (not the manual parameters, ARM-1), the orthogonal bars of the
    /// detailing; the manual confirmations of the detailing and of the anchorage back to be confirmed; the proposal applied recorded. Returns the
    /// notice of the manual shear parameters kept («» when none).
    /// </summary>
    public static string Apply(JsonObject input, JsonObject workspace, ConcreteResults.DesignCandidate candidate, bool completed)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(workspace); ArgumentNullException.ThrowIfNull(candidate);
        var shear = workspace["taglio"]!.AsObject(); var detailing = workspace["dettagli_costruttivi"]!.AsObject(); var anchorage = workspace["ancoraggi"]!;
        // Keep bound JsonObject instances alive; replace their values, not the form data sources.
        input.Clear(); foreach (var (k, v) in candidate.Input) input[k] = v?.DeepClone();
        // Con parametri del taglio manuali «Applica» non li sovrascrive (ARM-1): l'avviso dello stato lo dichiara.
        var (shearKeys, shearNotice) = ConcreteEditor.AppliedShearFields(shear, candidate.Shear);
        foreach (string k in shearKeys) shear[k] = candidate.Shear[k]?.DeepClone();
        foreach (string k in new[] { "as_secondaria", "passo_secondaria", "diametro_secondaria" })
            if (candidate.Detailing.ContainsKey(k)) detailing[k] = candidate.Detailing[k]?.DeepClone();
        detailing["barre_trattenute"] = "Da confermare"; detailing["ancoraggio_appoggi"] = "Da confermare";
        anchorage["diametro"] = ""; anchorage["sigma"] = "";
        foreach (string k in new[] { "confinamento", "posizione", "cautele" }) anchorage[k] = "Da verificare";
        workspace["proposta_armature_applicata"] = J.Obj(("descrizione", candidate.Description), ("metriche", J.Node(candidate.Metrics)), ("controlli", J.Node(candidate.Checks)),
            ("ricerca_completa", completed), ("riserva_torsione_percento", workspace["calcola_armature"]!["riserva_torsione"]));
        return shearNotice;
    }
}
