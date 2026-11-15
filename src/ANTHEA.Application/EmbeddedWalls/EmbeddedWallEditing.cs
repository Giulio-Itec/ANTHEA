using System.Text.Json.Nodes;
using Anthea.Application.Stratigraphy;

namespace Anthea.Application.EmbeddedWalls;

public static partial class EmbeddedWallDocument
{
    /// <summary>Values a new phase takes from the previous phase of its case (A3 of the paratie review, sheet of 16/11/2026): ground levels
    /// and water table. Surcharges, loads, stiffness factor, supports and actions keep the defaults of a new row.</summary>
    public static IReadOnlyList<string> ContinuedStageKeys { get; } = Array.AsReadOnly(new[] { "left_ground", "right_ground", "left_water", "right_water" });

    /// <summary>Defaults for a newly added editor row. Assigns identifiers and ownership only, never engineering values, except a new phase:
    /// it continues ground levels and water table of the last phase of the same case (the row is appended, so that phase precedes it in the
    /// history) instead of restarting at level 0 without water. Without a phase in the case the defaults of the module stay.</summary>
    public static JsonObject NewEditingRow(JsonObject data, string table, string caseId, string? stageId)
    {
        var row = NewRow(table);
        if (HistoryTables.Contains(table))
        {
            row["case"] = caseId == data["case_id"]?.ToString() ? "" : caseId;
            if (row.ContainsKey("stage") && stageId is not null) row["stage"] = stageId;
        }
        if (table == "stages" && Rows(data, "stages").LastOrDefault(r => StageCase(data, r) == caseId) is { } previous)
            ContinueStage(row, previous);
        if (row.ContainsKey("id")) row["id"] = FreeIdentifier(data, table, row["id"]!.ToString());
        return row;
    }

    /// <summary>
    /// «Inserisci fase prima della corrente» of the menu Fase (organizzazione del modulo paratie, progetto del 16/11, step 2; D9 with the
    /// recommendation of the note): the same rule as A3. The new phase takes ground levels and water table of the phase that precedes
    /// <paramref name="before"/> in the history of its case, or the defaults of the module when it becomes the first one; its identifier is
    /// free among the phases. It is inserted in the array of the phases just before <paramref name="before"/> and returned. Nothing else
    /// changes: no support, load or check is copied or moved.
    /// </summary>
    public static JsonObject InsertStage(JsonObject data, JsonObject before)
    {
        var stages = data["stages"]!.AsArray();
        int index = IndexOf(stages, before);
        string caseId = StageCase(data, before);
        var row = NewRow("stages");
        row["case"] = caseId == data["case_id"]?.ToString() ? "" : caseId;
        if (Rows(data, "stages").Take(index).LastOrDefault(r => StageCase(data, r) == caseId) is { } previous) ContinueStage(row, previous);
        row["id"] = FreeIdentifier(data, "stages", row["id"]!.ToString());
        stages.Insert(index, row);
        return row;
    }

    /// <summary>
    /// What «Inserisci fase prima della corrente» changes in the history (contract R04 of the review G-208), for the confirmation before the
    /// insertion. The new phase takes from the phase before it only ground levels and water table (A3, D9). A support present in that phase
    /// and in <paramref name="before"/> is missing in the new phase, so in <paramref name="before"/> it is installed again: an empty prestress
    /// there no longer keeps the previous lock-off but starts from 0 kN (<see cref="LockOffs"/>). The surcharges, the uniform load and the EI
    /// factor of the previous phase different from the defaults are not taken either. Nothing is listed for the first phase of a case or when
    /// an identifier is repeated (the references are ambiguous, as for <see cref="StageReferences"/>).
    /// </summary>
    public static IReadOnlyList<string> StageInsertionEffects(JsonObject data, JsonObject before)
    {
        int index = IndexOf(data["stages"]!.AsArray(), before);
        string caseId = StageCase(data, before);
        if (Rows(data, "stages").Take(index).LastOrDefault(r => StageCase(data, r) == caseId) is not { } previous) return [];
        string previousId = Value(previous, "id"), nextId = Value(before, "id");
        var phases = CaseStages(data, caseId);
        bool Unique(string id) => id.Length > 0 && phases.Count(r => Value(r, "id") == id) == 1;
        if (!Unique(previousId) || !Unique(nextId)) return [];
        var effects = new List<string>();
        var activations = Rows(data, "activations").Where(r => StageCase(data, r) == caseId).ToArray();
        foreach (var next in activations.Where(a => Value(a, "stage") == nextId))
        {
            string support = Value(next, "support");
            if (!activations.Any(a => Value(a, "stage") == previousId && Value(a, "support") == support)) continue;
            effects.Add(string.IsNullOrWhiteSpace(Value(next, "prestress"))
                ? $"Il sostegno «{support}», presente in «{previousId}» e in «{nextId}», manca nella fase nuova: in «{nextId}» viene installato di nuovo e la sua pretensione vuota vale 0 kN invece di mantenere quella in vigore."
                : $"Il sostegno «{support}», presente in «{previousId}» e in «{nextId}», manca nella fase nuova: in «{nextId}» viene installato di nuovo con la pretensione assegnata ({Value(next, "prestress")} kN).");
        }
        foreach (var (key, label, initial) in new[] { ("left_surcharge", "il sovraccarico a sinistra", 0d), ("right_surcharge", "il sovraccarico a destra", 0d),
            ("distributed_load", "il carico uniforme sulla parete", 0d), ("stiffness_factor", "il moltiplicatore EI", 1d) })
        {
            double? value;
            try { value = StratigraphyDocument.Number(previous[key], label); } catch (ArgumentException) { value = null; }
            if (value is { } assigned && assigned != initial)
                effects.Add($"La fase nuova non riprende {label} di «{previousId}» ({Value(previous, key)}): vale {initial.ToString(System.Globalization.CultureInfo.CurrentCulture)}.");
        }
        return effects.AsReadOnly();
    }

    /// <summary>
    /// «Rinomina la fase» (step 2): the new identifier of <paramref name="stage"/>, trimmed, and the same identifier in every reference to it
    /// in the history tables of its case. The identifier must be new in the case. References are rewritten only when the old identifier
    /// names one phase of the case: with homonyms they stay where they were, as the work tree shows them, never moved to an arbitrary phase.
    /// A name already cited by rows of the case without such a phase is refused, so that those rows are never attached silently.
    /// Returns the number of references rewritten.
    /// </summary>
    public static int RenameStage(JsonObject data, JsonObject stage, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        IndexOf(data["stages"]!.AsArray(), stage);
        string id = name.Trim(), old = Value(stage, "id"), caseId = StageCase(data, stage);
        if (id.Length == 0) throw new ArgumentException("Assegnare un identificativo alla fase.");
        if (id == old) return 0;
        var phases = CaseStages(data, caseId);
        if (phases.Any(r => Value(r, "id") == id)) throw new ArgumentException($"La fase «{id}» esiste già nel caso «{caseId}».");
        if (References(data, caseId, id).Any())
            throw new ArgumentException($"Alcune righe del caso «{caseId}» citano già la fase «{id}», che non esiste: completare quei riferimenti o scegliere un altro nome.");
        var cited = old.Length > 0 && phases.Count(r => Value(r, "id") == old) == 1 ? References(data, caseId, old).ToArray() : Array.Empty<(string Table, JsonObject Row)>();
        stage["id"] = id;
        foreach (var (_, row) in cited) row["stage"] = id;
        return cited.Length;
    }

    /// <summary>
    /// «Rinomina tutte» (step 2): the phases of the case become F1…Fn in the order of the history, with every reference rewritten. Refused when
    /// a phase has no identifier or a repeated one (their references are ambiguous) and when rows of the case cite a phase that does not exist
    /// under a name of the new series. Returns the number of references rewritten.
    /// </summary>
    public static int RenameAllStages(JsonObject data, string caseId)
    {
        ArgumentNullException.ThrowIfNull(caseId);
        var phases = CaseStages(data, caseId);
        var ids = phases.Select(r => Value(r, "id")).ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new ArgumentException($"Il caso «{caseId}» ha fasi senza identificativo o con identificativi ripetuti: correggerle prima di rinominarle tutte.");
        var names = ids.Select((old, i) => (Old: old, New: "F" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToDictionary(p => p.Old, p => p.New, StringComparer.Ordinal);
        var cited = HistoryTables.Where(t => t != "stages").SelectMany(t => Rows(data, t)).Where(r => StageCase(data, r) == caseId).ToArray();
        if (cited.Any(r => !names.ContainsKey(Value(r, "stage")) && names.ContainsValue(Value(r, "stage"))))
            throw new ArgumentException($"Alcune righe del caso «{caseId}» citano una fase inesistente con un nome della nuova serie F1…F{ids.Length}: completare prima quei riferimenti.");
        int rewritten = 0;
        foreach (var row in cited)
            if (names.TryGetValue(Value(row, "stage"), out var renamed) && renamed != Value(row, "stage")) { row["stage"] = renamed; rewritten++; }
        foreach (var phase in phases) phase["id"] = names[Value(phase, "id")];
        return rewritten;
    }

    /// <summary>
    /// The rows of the history tables that cite <paramref name="stage"/> (step 2, «Elimina la fase»): those of its case with its identifier,
    /// when it names one phase of the case. A phase with a homonym in the case or without identifier has none: those rows stay with the other
    /// phase or among the references to complete.
    /// </summary>
    public static IReadOnlyList<(string Table, JsonObject Row)> StageReferences(JsonObject data, JsonObject stage)
    {
        IndexOf(data["stages"]!.AsArray(), stage);
        string id = Value(stage, "id"), caseId = StageCase(data, stage);
        return id.Length > 0 && CaseStages(data, caseId).Count(r => Value(r, "id") == id) == 1 ? References(data, caseId, id).ToArray() : Array.Empty<(string Table, JsonObject Row)>();
    }

    /// <summary>«Elimina la fase» (step 2): removes the phase and the rows of <see cref="StageReferences"/>, which the user has confirmed.</summary>
    public static void DeleteStage(JsonObject data, JsonObject stage)
    {
        var references = StageReferences(data, stage);
        foreach (var (table, row) in references) data[table]!.AsArray().Remove(row);
        data["stages"]!.AsArray().Remove(stage);
    }

    /// <summary>The history of a row of a history table: an empty case is the main case.</summary>
    private static string StageCase(JsonObject data, JsonObject row)
        => string.IsNullOrWhiteSpace(row["case"]?.ToString()) ? data["case_id"]?.ToString() ?? "" : row["case"]!.ToString();

    private static void ContinueStage(JsonObject row, JsonObject previous)
    {
        foreach (string key in ContinuedStageKeys) row[key] = previous[key]?.DeepClone();
    }

    /// <summary>The identifier of a new row: the default of the table, or the first free one of its series.</summary>
    private static string FreeIdentifier(JsonObject data, string table, string initial)
    {
        var used = Rows(data, table).Select(r => r["id"]?.ToString() ?? "").ToHashSet(StringComparer.Ordinal);
        if (table == "cases") used.Add(data["case_id"]?.ToString() ?? "");
        // A new phase never takes a name already cited by rows without such a phase: they would be attached to it silently.
        if (table == "stages") used.UnionWith(HistoryTables.Where(t => t != "stages").SelectMany(t => Rows(data, t)).Select(r => Value(r, "stage")));
        string candidate = initial;
        int suffix = 1;
        while (used.Contains(candidate))
        {
            suffix++;
            candidate = table switch { "stages" => "F" + suffix, "supports" => "T" + suffix,
                "distributed_loads" => "Q" + suffix, "cases" => "Caso " + suffix, _ => initial + " " + suffix };
        }
        return candidate;
    }

    private static string Value(JsonObject row, string key) => row[key]?.ToString() ?? "";

    private static JsonObject[] CaseStages(JsonObject data, string caseId) => Rows(data, "stages").Where(r => StageCase(data, r) == caseId).ToArray();

    private static IEnumerable<(string Table, JsonObject Row)> References(JsonObject data, string caseId, string stageId)
        => HistoryTables.Where(t => t != "stages").SelectMany(t => Rows(data, t).Where(r => StageCase(data, r) == caseId && Value(r, "stage") == stageId)
            .Select(r => (t, r)));

    private static int IndexOf(JsonArray rows, JsonObject row)
    {
        for (int i = 0; i < rows.Count; i++) if (ReferenceEquals(rows[i], row)) return i;
        throw new ArgumentException("La fase non appartiene al documento.");
    }
}
