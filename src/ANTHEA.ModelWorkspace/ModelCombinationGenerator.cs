using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using GPC.Model.Design.Combinations;

namespace ANTHEA.ModelWorkspace;

/// <summary>Nature of a static load case of a model for the automatic combinations: excluded, permanent G1 or G2, variable, accidental, seismic.</summary>
public enum ModelCaseNature { Excluded = 0, G1 = 1, G2 = 2, Variable = 3, Accidental = 4, Seismic = 5 }

/// <summary>
/// A static load case with its nature for the generator: the category of a variable case gives its ψ (NTC 2018 Tab. 2.5.I); the group joins
/// correlated cases (one presence and one lead in the enumeration, as the groups of the walls); empty group: G1 and G2 one group each, the other
/// natures one group per case. A seismic case without direction enters the seismic rows alone with both signs; with a direction (decision of the
/// user of 1/11/2026) the directional cases follow the rule 100/30 of NTC 2018 §7.3.5, with the coefficients of Model (SeismicCombinations).
/// </summary>
public sealed record ModelCaseAssignment(string Case, ModelCaseNature Nature, VariableActionCategory? Category = null, string? Group = null,
    SeismicDirection Direction = SeismicDirection.None)
{
    /// <summary>For the interface, which has no types of GPC: the category as its index in VariableActionCategory (NTC 2018 Tab. 2.5.I, A-H, wind,
    /// snow up to and above 1000 m, temperature), the direction as its index (none, X, Y, Z).</summary>
    public int? CategoryIndex => Category is { } c ? (int)c : null;
    public int DirectionIndex => (int)Direction;
    public static int CategoryCount => CombinationRuleSet.Ntc2018GeneralStructuralV1.SupportedVariableCategories.Count;
    public static ModelCaseAssignment FromIndexes(string @case, ModelCaseNature nature, int? category, string? group, int direction) =>
        FromGpc(GPC.Model.Design.Combinations.ModelCombinationGenerator.NormalizeForEditing(new(@case,
            (GPC.Model.Design.Combinations.ModelCaseNature)nature, category is { } c ? (VariableActionCategory)c : null, group, (SeismicDirection)direction)));

    internal GPC.Model.Design.Combinations.ModelCaseAssignment ToGpc() => new(Case,
        (GPC.Model.Design.Combinations.ModelCaseNature)Nature, Category, Group, Direction);
    internal static ModelCaseAssignment FromGpc(GPC.Model.Design.Combinations.ModelCaseAssignment a) =>
        new(a.CaseId, (ModelCaseNature)a.Nature, a.Category, a.Group, a.Direction);
}

/// <summary>
/// The choices of the generator kept in the copy of the model (decision of the user of 1/11/2026), so that a new generation starts from them:
/// names of the enumerations as text (the order of an enumeration may change), the optional ones omitted when empty.
/// </summary>
public sealed record ModelGeneratorCase(string Case, string Nature,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Category = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Group = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Direction = null)
{
    internal static ModelGeneratorCase From(ModelCaseAssignment a) => new(a.Case, a.Nature.ToString(), a.Category?.ToString(),
        string.IsNullOrWhiteSpace(a.Group) ? null : a.Group.Trim(), a.Direction == SeismicDirection.None ? null : a.Direction.ToString());

    /// <summary>The assignment, or null when a name is not one of the enumerations (a document of a newer version).</summary>
    internal ModelCaseAssignment? ToAssignment()
    {
        if (!Enum.TryParse<GPC.Model.Design.Combinations.ModelCaseNature>(Nature, false, out var nature)
            || Category is { } c && !Enum.TryParse<VariableActionCategory>(c, false, out _)
            || Direction is { } d && !Enum.TryParse<SeismicDirection>(d, false, out _)) return null;
        var a = new GPC.Model.Design.Combinations.ModelCaseAssignment(Case, nature,
            Category is null ? null : Enum.Parse<VariableActionCategory>(Category), Group,
            Direction is null ? SeismicDirection.None : Enum.Parse<SeismicDirection>(Direction));
        return GPC.Model.Design.Combinations.ModelCombinationGenerator.IsRecognizedChoice(a) ? ModelCaseAssignment.FromGpc(a) : null;
    }
}

/// <summary>ANTHEA documents and Italian presentation around the application-independent GPCModel generation service.</summary>
public static class ModelCombinationGenerator
{
    public const string IdPrefix = CombinationNaming.AntheaIdPrefix;
    public const int MaximumRows = CombinationGenerationLimits.DefaultMaximumRows;
    const string Status = "Generata · NTC 2018 §2.5.3";

    public static ModelCaseAssignment[] Propose(ModelSnapshot snapshot) =>
        GPC.Model.Design.Combinations.ModelCombinationGenerator.Propose(Descriptors(snapshot)).Select(ModelCaseAssignment.FromGpc).ToArray();

    public static ModelCombinationDefinition[] Generate(ModelSnapshot snapshot, IReadOnlyList<ModelCaseAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(snapshot); ArgumentNullException.ThrowIfNull(assignments);
        var result = GPC.Model.Design.Combinations.ModelCombinationGenerator.Generate(Request(snapshot, assignments));
        if (!result.Success) throw new InvalidDataException(Message(result.Diagnostics[0]));
        return result.Rows.Select(row => new ModelCombinationDefinition(row.Id, row.Name, ModelCombinationKind.Linear, "ANTHEA", Status,
            row.Terms.Select(t => new ModelCombinationTerm(t.CaseId, false, t.Factor)).ToArray(), true)).ToArray();
    }

    public static ModelCaseAssignment[] Choices(ModelSnapshot snapshot) =>
        GPC.Model.Design.Combinations.ModelCombinationGenerator.ReconcileChoices(Descriptors(snapshot),
            (snapshot.GeneratorCases ?? []).Select(c => c.ToAssignment()?.ToGpc()).Where(a => a != null).ToArray()!)
            .Select(ModelCaseAssignment.FromGpc).ToArray();

    public static (bool CanGenerate, int Rows, string Message) Preview(ModelSnapshot snapshot, IReadOnlyList<ModelCaseAssignment> assignments)
    {
        // Preserve the legacy invalid-call exception contract; domain validation and missing-category precedence belong to GPC.
        ArgumentNullException.ThrowIfNull(assignments, "source");
        if (assignments.Any(a => a is null)) throw new NullReferenceException("Object reference not set to an instance of an object.");
        var preview = GPC.Model.Design.Combinations.ModelCombinationGenerator.Preview(Request(snapshot, assignments));
        if (preview.MissingCategoryCaseIds.Count > 0)
            return (false, 0, "Scegli la categoria della Tab. 2.5.I per i carichi variabili: " + string.Join(", ", preview.MissingCategoryCaseIds) + ". La generazione parte solo con tutte le categorie.");
        if (!preview.CanGenerate) return (false, 0, Message(preview.Generation.Diagnostics[0]));
        var counts = preview.Generation.StateCounts.Select(c => CombinationNaming.AntheaNtc2018V1.Label(c.State) + " " + c.Count.ToString(CultureInfo.InvariantCulture));
        return (true, preview.Rows, preview.Rows.ToString(CultureInfo.InvariantCulture) + " combinazioni (" + string.Join(", ", counts) + ", al più " +
            MaximumRows.ToString(CultureInfo.InvariantCulture) + "). Sostituiscono le generate in precedenza; quelle dell'utente restano.");
    }

    static ModelCombinationRequest Request(ModelSnapshot snapshot, IReadOnlyList<ModelCaseAssignment> assignments) => new(Descriptors(snapshot),
        assignments.Select(a => a?.ToGpc())!, CombinationRuleSet.Ntc2018GeneralStructuralV1, CombinationNaming.AntheaNtc2018V1);

    // Import catalogue traversal and storage are ANTHEA responsibilities; the source type codes are advisory hints to GPC.
    static CombinationCaseDescriptor[] Descriptors(ModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        if (snapshot.SourceTables?.TryGetValue("STLD", out var table) == true)
            foreach (var row in table.EnumerateObject())
                if (row.Value.TryGetProperty("NAME", out var name) && name.GetString() is { Length: > 0 } n)
                    types[n] = row.Value.TryGetProperty("TYPE", out var type) && type.ValueKind == JsonValueKind.String ? type.GetString() ?? "" : "";
        return ModelCombinations.StaticCaseNames(snapshot).Select(c => new CombinationCaseDescriptor(c, "MIDAS", types.GetValueOrDefault(c, ""), true)).ToArray();
    }

    static string Message(CombinationGenerationDiagnostic d) => d.Code switch
    {
        CombinationGenerationDiagnosticCode.InvalidAssignment => "Natura dei casi di carico mancante o non valida.",
        CombinationGenerationDiagnosticCode.DuplicateCase => "Ogni caso di carico va indicato una volta sola.",
        CombinationGenerationDiagnosticCode.UnknownCase => "Il caso di carico «" + d.CaseId + "» non è nel modello.",
        CombinationGenerationDiagnosticCode.MissingVariableCategory => "Scegli la categoria del carico variabile «" + d.CaseId + "» (NTC 2018 Tab. 2.5.I): dà i coefficienti ψ.",
        CombinationGenerationDiagnosticCode.NoOrdinaryAction => "Indica almeno un caso permanente o variabile.",
        CombinationGenerationDiagnosticCode.DirectionOnNonSeismicAction => "La direzione del sisma vale solo per i casi sismici («" + d.CaseId + "»).",
        CombinationGenerationDiagnosticCode.TooManyGroups => $"Generazione troppo estesa ({d.Count} gruppi di casi permanenti e variabili, al più {d.Limit}): riunisci i casi correlati nello stesso gruppo o escludi quelli che non servono.",
        CombinationGenerationDiagnosticCode.TooManyRows => $"Oltre {d.Limit} combinazioni ({d.Count} righe): riunisci i casi correlati nello stesso gruppo o escludi quelli che non servono.",
        CombinationGenerationDiagnosticCode.GenerationRejected => "Combinazioni non generate: " + d.Detail,
        CombinationGenerationDiagnosticCode.WorkLimitExceeded => $"Generazione troppo estesa ({d.Count} operazioni, al più {d.Limit}): riunisci i casi correlati nello stesso gruppo o escludi quelli che non servono.",
        _ => "Combinazioni non generate: " + d.Code + (d.CaseId is null ? "." : " («" + d.CaseId + "»).")
    };

    /// <summary>The copy of the model with the generated combinations instead of those of a previous generation; as when a definition is saved,
    /// the results of the definitions that change (and of those that use them) are discarded, the others kept.</summary>
    public static ModelSnapshot Apply(ModelSnapshot snapshot, IReadOnlyList<ModelCombinationDefinition> generated) => Apply(snapshot, generated, null);

    /// <summary>The same, keeping the <paramref name="choices"/> of the generation in the copy (<see cref="ModelSnapshot.GeneratorCases"/>):
    /// a new generation starts from them (<see cref="Choices"/>). Null keeps the choices of the copy.</summary>
    public static ModelSnapshot Apply(ModelSnapshot snapshot, IReadOnlyList<ModelCombinationDefinition> generated, IReadOnlyList<ModelCaseAssignment>? choices)
    {
        var copy = Replace(snapshot, generated);
        if (choices != null) copy.GeneratorCases = choices.Select(ModelGeneratorCase.From).ToArray();
        return copy;
    }

    static ModelSnapshot Replace(ModelSnapshot snapshot, IReadOnlyList<ModelCombinationDefinition> generated)
    {
        ArgumentNullException.ThrowIfNull(snapshot); ArgumentNullException.ThrowIfNull(generated);
        if (generated.Any(d => d is null || !d.Id.StartsWith(IdPrefix, StringComparison.Ordinal) || d.Source != "ANTHEA" || !d.Editable))
            throw new InvalidDataException("Combinazioni generate non valide.");
        var kept = (snapshot.CustomCombinations ?? []).Where(d => !d.Id.StartsWith(IdPrefix, StringComparison.Ordinal)).ToArray();
        if (kept.Any(d => generated.Any(g => g.Name == d.Name)))
            throw new InvalidDataException("Una combinazione ANTHEA ha già il nome di una combinazione generata: rinominala prima.");
        snapshot = ModelCombinations.Upgraded(snapshot, ModelCombinations.Read(snapshot)); // results of 1.16.2 still current (1/11/2026 evening)
        var copy = ModelCombinations.Copy(snapshot);
        copy.CustomCombinations = [.. kept, .. generated];
        // Decision of 1/11/2026: the results of the rows generated again with the same definition stay valid (a revision for each definition).
        copy.Results = ModelCombinations.CurrentResults(copy, ModelCombinations.Read(copy));
        copy.Validate(); ModelCombinations.Read(copy);
        return copy;
    }

}
