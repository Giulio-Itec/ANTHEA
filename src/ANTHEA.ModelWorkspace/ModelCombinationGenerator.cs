using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using GPC.Model.Design.Combinations;

namespace ANTHEA.ModelWorkspace;

/// <summary>Nature of a static load case of a model for the automatic combinations: excluded, permanent G1 or G2, variable, accidental, seismic.</summary>
public enum ModelCaseNature { Excluded, G1, G2, Variable, Accidental, Seismic }

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
    public static int CategoryCount => Enum.GetValues<VariableActionCategory>().Length;
    public static ModelCaseAssignment FromIndexes(string @case, ModelCaseNature nature, int? category, string? group, int direction) =>
        new(@case, nature, category is { } c ? (VariableActionCategory)c : null, group, (SeismicDirection)direction);
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
    internal ModelCaseAssignment? ToAssignment() =>
        string.IsNullOrWhiteSpace(Case) || !Enum.TryParse<ModelCaseNature>(Nature, false, out var nature) || !Enum.IsDefined(nature) ? null
        : Category is { } c && !(Enum.TryParse<VariableActionCategory>(c, false, out var category) && Enum.IsDefined(typeof(VariableActionCategory), category)) ? null
        : Direction is { } d && !(Enum.TryParse<SeismicDirection>(d, false, out var direction) && Enum.IsDefined(typeof(SeismicDirection), direction)) ? null
        : new(Case, nature, Category is null ? null : Enum.Parse<VariableActionCategory>(Category), Group,
            Direction is null ? SeismicDirection.None : Enum.Parse<SeismicDirection>(Direction));
}

/// <summary>
/// Automatic combinations of the Models module on the generator of Model (ANTHEA census of the combinations, D9 a, decision of the user of
/// 28/10/2026 «Come raccomandato»: the same generator of the walls, GPC.Model.Design.Combinations.GeotechnicalCombinations, with the profile of
/// NTC 2018, D10 a, and no choice of the standard). The static load cases with their nature become the actions of the generator; the rows of
/// NTC 2018 §2.5.3 become linear ANTHEA combinations of the copy of the model, editable in the same catalogue and calculated by GPC like the
/// others: SLU with the structural set A1 (G1 1.3/1.0, G2 1.5/0.8, Q 1.5/0, every pattern of the permanent groups and every presence of the
/// variable groups, every present group leading), SLE characteristic, frequent and quasi permanent, SLV with every seismic case at ±1 and the
/// variable groups present (ψ2) or absent (D6 a), accidental rows with every accidental group at 1. ANTHEA writes no coefficient: they all come
/// from Model. The interface that chooses the natures is the next step of the plan (docs/refactoring/combinazioni-model-censimento.md, D9).
/// </summary>
public static class ModelCombinationGenerator
{
    /// <summary>Prefix of the ids of the generated combinations: a new generation replaces them.</summary>
    public const string IdPrefix = "ANTHEA/NTC2018/";
    /// <summary>At most this many generated combinations, as the rows of the walls.</summary>
    public const int MaximumRows = 4096;
    const string Status = "Generata · NTC 2018 §2.5.3";

    /// <summary>
    /// A proposal of the natures from the MIDAS types of the static load cases (table STLD): D and DC permanent G1, DW G2, W and WL wind, S snow
    /// up to 1000 m, T and TPG thermal variations, E seismic; the live loads (L, LR) are variable without a category, to be chosen; every other
    /// type and the sources without types (Straus7) excluded. A proposal to be confirmed, never a generation by itself.
    /// </summary>
    public static ModelCaseAssignment[] Propose(ModelSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        if (snapshot.SourceTables?.TryGetValue("STLD", out var table) == true)
            foreach (var row in table.EnumerateObject())
                if (row.Value.TryGetProperty("NAME", out var name) && name.GetString() is { Length: > 0 } n)
                    types[n] = row.Value.TryGetProperty("TYPE", out var type) && type.ValueKind == JsonValueKind.String ? type.GetString() ?? "" : "";
        return ModelCombinations.StaticCaseNames(snapshot).Select(c => (types.TryGetValue(c, out var t) ? t : "").ToUpperInvariant() switch
        {
            "D" or "DC" => new ModelCaseAssignment(c, ModelCaseNature.G1),
            "DW" => new ModelCaseAssignment(c, ModelCaseNature.G2),
            "L" or "LR" => new ModelCaseAssignment(c, ModelCaseNature.Variable),
            "W" or "WL" => new ModelCaseAssignment(c, ModelCaseNature.Variable, VariableActionCategory.Wind),
            "S" => new ModelCaseAssignment(c, ModelCaseNature.Variable, VariableActionCategory.SnowUpTo1000m),
            "T" or "TPG" => new ModelCaseAssignment(c, ModelCaseNature.Variable, VariableActionCategory.Temperature),
            "E" => new ModelCaseAssignment(c, ModelCaseNature.Seismic),
            _ => new ModelCaseAssignment(c, ModelCaseNature.Excluded)
        }).ToArray();
    }

    /// <summary>The combinations of the cases with their natures; refused with a message when a case is unknown or repeated, a variable case has
    /// no category, nothing is permanent or variable, or the rows exceed <see cref="MaximumRows"/>.</summary>
    public static ModelCombinationDefinition[] Generate(ModelSnapshot snapshot, IReadOnlyList<ModelCaseAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(snapshot); ArgumentNullException.ThrowIfNull(assignments);
        var known = ModelCombinations.StaticCaseNames(snapshot).ToHashSet(StringComparer.Ordinal);
        if (assignments.Any(a => a is null || string.IsNullOrWhiteSpace(a.Case) || !Enum.IsDefined(a.Nature) || !Enum.IsDefined(a.Direction)
            || a.Category is { } category && !Enum.IsDefined(category)))
            throw new InvalidDataException("Natura dei casi di carico mancante o non valida.");
        if (assignments.Select(a => a.Case).Distinct(StringComparer.Ordinal).Count() != assignments.Count)
            throw new InvalidDataException("Ogni caso di carico va indicato una volta sola.");
        if (assignments.FirstOrDefault(a => !known.Contains(a.Case)) is { } unknown)
            throw new InvalidDataException("Il caso di carico «" + unknown.Case + "» non è nel modello.");
        if (assignments.FirstOrDefault(a => a.Nature == ModelCaseNature.Variable && a.Category is null) is { } missing)
            throw new InvalidDataException("Scegli la categoria del carico variabile «" + missing.Case + "» (NTC 2018 Tab. 2.5.I): dà i coefficienti ψ.");
        var used = assignments.Where(a => a.Nature != ModelCaseNature.Excluded).ToArray();
        if (!used.Any(a => a.Nature is ModelCaseNature.G1 or ModelCaseNature.G2 or ModelCaseNature.Variable))
            throw new InvalidDataException("Indica almeno un caso permanente o variabile.");
        var profile = CombinationProfile.Ntc2018;
        string Group(ModelCaseAssignment a) => a.Nature + "/" + (string.IsNullOrWhiteSpace(a.Group) ? a.Nature is ModelCaseNature.G1 or ModelCaseNature.G2 ? "" : a.Case : a.Group.Trim());
        var actions = used.Where(a => a.Nature != ModelCaseNature.Seismic).Select(a => a.Nature switch
        {
            ModelCaseNature.G1 => new GeotechnicalDesignAction(a.Case, Group(a), GeotechnicalActionNature.G1),
            ModelCaseNature.G2 => new GeotechnicalDesignAction(a.Case, Group(a), GeotechnicalActionNature.G2),
            ModelCaseNature.Variable => profile.Variable(a.Case, Group(a), a.Category!.Value),
            _ => new GeotechnicalDesignAction(a.Case, Group(a), GeotechnicalActionNature.A)
        }).ToArray();
        // The seismic variants and their coefficients come from Model: ±1 for a case without direction, 100/30 for the directional cases.
        if (assignments.FirstOrDefault(a => a.Nature != ModelCaseNature.Seismic && a.Direction != SeismicDirection.None) is { } directed)
            throw new InvalidDataException("La direzione del sisma vale solo per i casi sismici («" + directed.Case + "»).");
        var seismic = SeismicCombinations.Variants(used.Where(a => a.Nature == ModelCaseNature.Seismic).Select(a => new SeismicCase(a.Case, a.Direction)));
        int groups = actions.Where(a => a.Nature != GeotechnicalActionNature.A).Select(a => a.Group).Distinct(StringComparer.Ordinal).Count();
        if (groups > 12) throw new InvalidDataException($"Generazione troppo estesa ({groups} gruppi di casi permanenti e variabili, al più 12): riunisci i casi correlati nello stesso gruppo o escludi quelli che non servono.");
        IReadOnlyList<GeotechnicalCombinationRow> rows;
        try { rows = GeotechnicalCombinations.Generate(new GeotechnicalCombinationRequest(profile.Structural, actions, seismicVariants: seismic.Count)); }
        catch (ArgumentException ex) { throw new InvalidDataException("Combinazioni non generate: " + ex.Message, ex); }
        if (rows.Count > MaximumRows) throw new InvalidDataException($"Oltre {MaximumRows} combinazioni ({rows.Count} righe): riunisci i casi correlati nello stesso gruppo o escludi quelli che non servono.");
        var counters = new Dictionary<GeotechnicalLimitState, int>();
        return rows.Select(row =>
        {
            int n = counters[row.State] = counters.GetValueOrDefault(row.State) + 1;
            string name = Label(row.State) + " " + n.ToString(CultureInfo.InvariantCulture);
            var terms = actions.Where(a => row.Factors[a.Id] != 0).Select(a => new ModelCombinationTerm(a.Id, false, row.Factors[a.Id])).ToList();
            if (row.SeismicVariant >= 0) terms.AddRange(seismic[row.SeismicVariant].Factors.Select(f => new ModelCombinationTerm(f.Key, false, f.Value)));
            return new ModelCombinationDefinition(IdPrefix + name, name, ModelCombinationKind.Linear, "ANTHEA", Status, terms.ToArray(), true);
        }).ToArray();
    }

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

    /// <summary>
    /// The choices to show for a new generation: those kept in the copy for the cases still in the model, the proposal (<see cref="Propose"/>) for
    /// the new cases, in the order of the static cases of the copy.
    /// </summary>
    public static ModelCaseAssignment[] Choices(ModelSnapshot snapshot)
    {
        var kept = (snapshot.GeneratorCases ?? []).Select(c => c.ToAssignment()).OfType<ModelCaseAssignment>()
            .GroupBy(a => a.Case, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        return Propose(snapshot).Select(p => kept.TryGetValue(p.Case, out var k) ? k : p).ToArray();
    }

    /// <summary>A preview of a generation: the number of rows for each limit state, or why it cannot be generated (category to choose, limit of
    /// rows or groups, with the number and the remedy), without changing the copy.</summary>
    public static (bool CanGenerate, int Rows, string Message) Preview(ModelSnapshot snapshot, IReadOnlyList<ModelCaseAssignment> assignments)
    {
        var missing = assignments.Where(a => a.Nature == ModelCaseNature.Variable && a.Category is null).Select(a => a.Case).ToArray();
        if (missing.Length > 0)
            return (false, 0, "Scegli la categoria della Tab. 2.5.I per i carichi variabili: " + string.Join(", ", missing) + ". La generazione parte solo con tutte le categorie.");
        try
        {
            var rows = Generate(snapshot, assignments);
            var counts = rows.GroupBy(r => r.Name[..r.Name.LastIndexOf(' ')]).Select(g => g.Key + " " + g.Count().ToString(CultureInfo.InvariantCulture));
            return (true, rows.Length, rows.Length.ToString(CultureInfo.InvariantCulture) + " combinazioni (" + string.Join(", ", counts) + ", al più " +
                MaximumRows.ToString(CultureInfo.InvariantCulture) + "). Sostituiscono le generate in precedenza; quelle dell'utente restano.");
        }
        catch (InvalidDataException ex) { return (false, 0, ex.Message); }
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

    static string Label(GeotechnicalLimitState state) => state switch
    {
        GeotechnicalLimitState.Ultimate => "SLU",
        GeotechnicalLimitState.Characteristic => "SLE rara",
        GeotechnicalLimitState.Frequent => "SLE frequente",
        GeotechnicalLimitState.QuasiPermanent => "SLE quasi permanente",
        GeotechnicalLimitState.Seismic => "SLV",
        _ => "Eccezionale"
    };
}
