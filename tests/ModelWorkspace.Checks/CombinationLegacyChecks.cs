using System.Text.Json;
using System.Text.Json.Nodes;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelWorkspace;

/// <summary>
/// Decision of the user of 1/11/2026 evening («Combinazioni calcolate con le versioni precedenti → Calcolate + avviso»), after COMBO-REV-01:
/// the results of the combinations of a project saved until 1.16.2 (revision of format 1, the whole copy) stay «Calcolata» while they are
/// still valid, with the same values; a copy changed since gives them «Da calcolare», never an error. The fixture is the container of a model
/// written by the code of 1.16.2 itself (the DLLs of the installer of 1.16.2: synthetic model 24 × 12, «Somma utente» and the imported
/// envelope «cENV» with its nested definition calculated).
/// </summary>
static partial class Program
{
    static void LegacyRevisionChecks()
    {
        const string sum = "ANTHEA/utente-somma", envelope = "LCOM-GEN/cENV";
        string State(ModelSnapshot s, string id) => ModelCombinations.Inspect(s).Single(c => c.Definition.Id == id).State;
        // The values of a derived result without its revision: equal JSON, equal bits (the doubles are written round-trip, -0 included).
        string Values(ModelResultSet r) => JsonSerializer.Serialize(r with { Combination = r.Combination! with { InputRevision = "" } });
        var container = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "tests", "fixtures", "model-viewer-combinations-1.16.2", "progetto-1.16.2.json")))!;
        string id = ProjectModelStore.Models(container).Single()["id"]!.GetValue<string>();
        var saved = ProjectModelStore.Read(container, id)!;
        var derived = saved.Results.Where(r => r.Combination != null).ToArray();
        // The revision of 1.16.2, recomputed here as 1.16.2 did (ModelCombinations.InputRevision): the canonical JSON of the copy without the
        // derived results, its fingerprint.
        var whole = ModelSnapshot.FromJson(JsonSerializer.Serialize(saved)); whole.Results = [.. whole.Results.Where(r => r.Combination == null)];
        Check(derived.Length == 3 && derived.All(r => r.Combination!.InputRevision == whole.Fingerprint() && r.Combination.PrincipalGoverning == null) &&
            derived.Count(r => r.Combination!.DefinitionId == envelope) == 2, "legacy revision: the fixture of 1.16.2 has three derived results of the revision of the whole copy");

        var current = ModelCombinations.WithCurrentResults(saved);
        var upgraded = current.Results.Where(r => r.Combination != null).ToArray();
        Check(upgraded.Length == 3 && upgraded.Select(Values).SequenceEqual(derived.Select(Values)) &&
            upgraded.All(r => r.Combination!.InputRevision == ModelCombinations.DefinitionRevision(current, r.Combination.DefinitionId)) &&
            current.Results.Where(r => r.Combination == null).SequenceEqual(saved.Results.Where(r => r.Combination == null)),
            "legacy revision: the results of 1.16.2 still valid keep their values bit for bit and take the revision of their definition");
        Check(State(current, sum) == "Calcolata" && State(current, envelope) == "Calcolata" && State(saved, sum) == "Calcolata" && State(saved, envelope) == "Calcolata",
            "legacy revision: the catalogue shows the combinations calculated with 1.16.2 «Calcolata»");
        using (var vm = new ModelViewerViewModel(saved, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { }))
        {
            string max = upgraded.First(r => r.Combination!.DefinitionId == envelope && r.Name.EndsWith("(max)", StringComparison.Ordinal)).Name;
            vm.SelectedCase = max; vm.SelectedFamily = 0; vm.SelectedAxes = 1;
            Check(vm.Combinations.Count(c => c.State == "Calcolata") == 2 && vm.Cases.Contains(max) &&
                vm.ResultAvailability.StartsWith("Componenti principali non calcolate per questa combinazione", StringComparison.Ordinal),
                "legacy revision: the viewer opens them «Calcolata»; asking the principal components proposes the calculation again");
        }
        // Written once, the document has the revisions of the definitions: nothing to convert at the next opening.
        ProjectModelStore.Set(container, current, id);
        var rewritten = ProjectModelStore.Read((JsonObject)JsonNode.Parse(container.ToJsonString())!, id)!;
        Check(ReferenceEquals(ModelCombinations.WithCurrentResults(rewritten), rewritten) && rewritten.Results.Where(r => r.Combination != null).Select(Values).SequenceEqual(derived.Select(Values)),
            "legacy revision: the first write of the document stores the revision of each definition, same values");
        // After the conversion the revision is per definition: saving a definition the two do not use keeps both.
        var other = ModelCombinations.Read(saved).First(d => d.Id.StartsWith(ModelCombinationGenerator.IdPrefix, StringComparison.Ordinal));
        var edited = ModelCombinations.Save(saved, other with { Terms = [.. other.Terms.Select((t, i) => i == 0 ? t with { Factor = t.Factor + .5 } : t)] });
        Check(edited.Results.Count(r => r.Combination != null) == 3 && State(edited, sum) == "Calcolata" && State(edited, envelope) == "Calcolata",
            "legacy revision: saving another definition of a document of 1.16.2 keeps its results");

        // The same project with a definition changed while its results kept the revision of 1.16.2: the copy is not the one they were calculated
        // from, «Da calcolare», no error at the opening.
        var changed = ModelSnapshot.FromJson(JsonSerializer.Serialize(saved));
        changed.CustomCombinations = [.. changed.CustomCombinations!.Select(d => d.Id == sum ? d with { Terms = [.. d.Terms.Select((t, i) => i == 0 ? t with { Factor = t.Factor + .25 } : t)] } : d)];
        var changedContainer = new JsonObject(); string changedId = ProjectModelStore.Set(changedContainer, changed);
        var reopened = ProjectModelStore.Read((JsonObject)JsonNode.Parse(changedContainer.ToJsonString())!, changedId)!;
        var shown = ModelCombinations.WithCurrentResults(reopened);
        Check(reopened.Results.Count(r => r.Combination != null) == 3 && shown.Results.All(r => r.Combination == null) && State(reopened, sum) == "Da calcolare" &&
            State(reopened, envelope) == "Da calcolare", "legacy revision: a project of 1.16.2 with a definition changed reopens its combinations «Da calcolare»");
        using var changedVm = new ModelViewerViewModel(reopened, [], false, new FakeServices(), _ => { }, (_, _, _, _) => { });
        Check(changedVm.Snapshot!.Results.All(r => r.Combination == null) && changedVm.Combinations.Count(c => c.State == "Calcolata") == 0,
            "legacy revision: the viewer opens the changed project without its old results and without errors");
    }
}
