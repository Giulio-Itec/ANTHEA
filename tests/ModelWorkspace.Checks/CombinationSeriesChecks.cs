using System.Text.Json;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelWorkspace;

static partial class Program
{
    /// <summary>
    /// Request of the user of 2/11/2026 (series of the combinations): the checks, sizes, definitions and revisions kept for the same references
    /// (ModelSnapshot.Validate, ModelCombinations.SerializedSize, Read and DefinitionRevision) give what the complete computations give, and the
    /// series of the view model gives the results of the same combinations calculated and attached one by one.
    /// </summary>
    static async Task CombinationSeriesChecks()
    {
        var source = ModelOfSize(24, 12);
        var model = AddUserCombinations(ModelCombinationGenerator.Apply(source.Snapshot, ModelCombinationGenerator.Generate(source.Snapshot, source.Natures)), source);
        model.Validate(); model.Validate();
        // The same result sets checked against other nodes and elements are checked again (the copy itself, its parts put back afterwards).
        var (elements, nodes) = (model.Elements, model.Nodes);
        var plate = elements.First(e => e.Type == "PLATE");
        model.Elements = elements.Where(e => e != plate).ToArray();
        bool withoutPlate = Throws(model.Validate);
        model.Elements = elements; model.Nodes = nodes.Where(n => n.Id != plate.Nodes[0]).ToArray();
        bool withoutNode = Throws(model.Validate);
        model.Nodes = nodes; model.Validate();
        Check(withoutPlate && withoutNode, "series: the results checked once are checked again against other elements or nodes");

        var definitions = ModelCombinations.Read(model);
        var again = ModelCombinations.Read(model);
        var imported = definitions.Where(d => d.Source.StartsWith("LCOM", StringComparison.Ordinal)).ToArray();
        Check(imported.Length > 0 && imported.All(d => again.Any(a => ReferenceEquals(a, d))), "series: the definitions of the MIDAS tables are the same records at every reading");
        var fresh = ModelSnapshot.FromJson(JsonSerializer.Serialize(model));
        Check(JsonSerializer.Serialize(ModelCombinations.Read(fresh)) == JsonSerializer.Serialize(definitions) &&
            definitions.All(d => ModelCombinations.DefinitionRevision(model, d.Id) == ModelCombinations.DefinitionRevision(fresh, d.Id)),
            $"series: the definitions and revisions kept equal those read and computed again from the document ({definitions.Length} definitions)");

        // Three generated rows: the series of the view model and the calculation with Attach one by one.
        var service = new GpcModelCombinationService();
        var rows = definitions.Where(d => d.Id.StartsWith(ModelCombinationGenerator.IdPrefix, StringComparison.Ordinal)).Take(3).Select(d => d.Id).ToArray();
        var single = model;
        var size = typeof(ModelCombinations).GetMethod("SerializedSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        bool sizes = true;
        foreach (var id in rows)
        {
            single = ModelCombinations.Attach(single, id, await service.CalculateAsync(single, id));
            sizes &= (long)size.Invoke(null, [single])! == JsonSerializer.SerializeToUtf8Bytes(single).LongLength;
        }
        Check(sizes, "series: the size kept for each derived result equals the complete serialization after every Attach");
        // 3/11/2026 («Sì, opzione 1»): the copy keeps its combinations within the limit given; over it the oldest of other definitions leave first.
        var recalculated = await service.CalculateAsync(single, rows[0]);
        long whole = (long)size.Invoke(null, [single])!;
        string Kept(ModelSnapshot s) => string.Join(",", rows.Select(id => s.Results.Any(r => r.Combination?.DefinitionId == id) ? "1" : "0"));
        var tight = ModelCombinations.Attach(single, rows[0], recalculated, whole - 1);
        var wide = ModelCombinations.Attach(single, rows[0], recalculated, ModelResultArchive.ArchivedCopyBytes);
        Check(Kept(tight) == "1,0,1" && Kept(wide) == "1,1,1" && ModelCombinations.CopyLimit(ModelResultArchive.ArchivedCopyBytes) == "2 GiB" &&
            ModelCombinations.CopyLimit(ModelSnapshotFiles.MaximumPackageBytes) == "256 MiB",
            "series: over the limit of the copy the oldest combination of another definition leaves; with the results archive (2 GiB) every one stays");
        using var vm = new ModelViewerViewModel(model, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
        vm.SelectedCombinationIds = rows;
        await vm.CalculateSelectedCombinationsCommand.ExecuteAsync(null);
        string Results(ModelSnapshot s) => JsonSerializer.Serialize(s.Results.Where(r => r.Combination != null && rows.Contains(r.Combination.DefinitionId)).OrderBy(r => r.Name, StringComparer.Ordinal));
        Check(vm.CombinationMessage.StartsWith("3 di 3 combinazioni calcolate", StringComparison.Ordinal) && Results(vm.Snapshot!) == Results(single),
            "series: «Calcola selezionate» gives the results of the same combinations calculated and attached one by one");
    }

    static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (InvalidDataException) { return true; }
    }
}
