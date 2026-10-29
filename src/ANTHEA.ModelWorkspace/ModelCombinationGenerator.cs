using System.Globalization;
using System.Text.Json;
using GPC.Model.Design.Combinations;

namespace ANTHEA.ModelWorkspace;

/// <summary>Nature of a static load case of a model for the automatic combinations: excluded, permanent G1 or G2, variable, accidental, seismic.</summary>
public enum ModelCaseNature { Excluded, G1, G2, Variable, Accidental, Seismic }

/// <summary>
/// A static load case with its nature for the generator: the category of a variable case gives its ψ (NTC 2018 Tab. 2.5.I); the group joins
/// correlated cases (one presence and one lead in the enumeration, as the groups of the walls); empty group: G1 and G2 one group each, the other
/// natures one group per case. A seismic case enters the seismic rows with both signs.
/// </summary>
public sealed record ModelCaseAssignment(string Case, ModelCaseNature Nature, VariableActionCategory? Category = null, string? Group = null);

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
        if (assignments.Any(a => a is null || string.IsNullOrWhiteSpace(a.Case) || !Enum.IsDefined(a.Nature)))
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
        var seismic = used.Where(a => a.Nature == ModelCaseNature.Seismic).SelectMany(a => new[] { (a.Case, Sign: 1d), (a.Case, Sign: -1d) }).ToArray();
        int groups = actions.Where(a => a.Nature != GeotechnicalActionNature.A).Select(a => a.Group).Distinct(StringComparer.Ordinal).Count();
        if (groups > 12) throw new InvalidDataException($"Generazione troppo estesa ({groups} gruppi di casi permanenti e variabili, al più 12): raggruppa i casi correlati.");
        IReadOnlyList<GeotechnicalCombinationRow> rows;
        try { rows = GeotechnicalCombinations.Generate(new GeotechnicalCombinationRequest(profile.Structural, actions, seismicVariants: seismic.Length)); }
        catch (ArgumentException ex) { throw new InvalidDataException("Combinazioni non generate: " + ex.Message, ex); }
        if (rows.Count > MaximumRows) throw new InvalidDataException($"Oltre {MaximumRows} combinazioni ({rows.Count}): raggruppa i casi correlati.");
        var counters = new Dictionary<GeotechnicalLimitState, int>();
        return rows.Select(row =>
        {
            int n = counters[row.State] = counters.GetValueOrDefault(row.State) + 1;
            string name = Label(row.State) + " " + n.ToString(CultureInfo.InvariantCulture);
            var terms = actions.Where(a => row.Factors[a.Id] != 0).Select(a => new ModelCombinationTerm(a.Id, false, row.Factors[a.Id])).ToList();
            if (row.SeismicVariant >= 0) terms.Add(new ModelCombinationTerm(seismic[row.SeismicVariant].Case, false, seismic[row.SeismicVariant].Sign));
            return new ModelCombinationDefinition(IdPrefix + name, name, ModelCombinationKind.Linear, "ANTHEA", Status, terms.ToArray(), true);
        }).ToArray();
    }

    /// <summary>The copy of the model with the generated combinations instead of those of a previous generation; the results of the combinations
    /// are discarded, as when a definition is saved.</summary>
    public static ModelSnapshot Apply(ModelSnapshot snapshot, IReadOnlyList<ModelCombinationDefinition> generated)
    {
        ArgumentNullException.ThrowIfNull(snapshot); ArgumentNullException.ThrowIfNull(generated);
        if (generated.Any(d => d is null || !d.Id.StartsWith(IdPrefix, StringComparison.Ordinal) || d.Source != "ANTHEA" || !d.Editable))
            throw new InvalidDataException("Combinazioni generate non valide.");
        var kept = (snapshot.CustomCombinations ?? []).Where(d => !d.Id.StartsWith(IdPrefix, StringComparison.Ordinal)).ToArray();
        if (kept.Any(d => generated.Any(g => g.Name == d.Name)))
            throw new InvalidDataException("Una combinazione ANTHEA ha già il nome di una combinazione generata: rinominala prima.");
        var copy = ModelCombinations.Copy(snapshot);
        copy.CustomCombinations = [.. kept, .. generated];
        copy.Results = copy.Results.Where(r => r.Combination == null).ToArray();
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
