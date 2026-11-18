using System.Text.Json;
using ANTHEA.ModelWorkspace;

/// <summary>
/// Set AZ (GPCModel 5.53, GPC.Converter 2.27, 18/11/2026): restraints and springs to ground of a model with several freedom cases carry their
/// freedom case into the copy (ModelBoundary.FreedomCase), and every static case the freedom case under which it is analysed
/// (ModelResultSet.FreedomCase); the viewer draws the supports of the freedom case of the chosen case with those of the default set. Input: the
/// synthetic Straus7 model of Strand7Sample with two freedom cases written as the converter does (FreedomCaseRecord, FreedomCase of the restraint,
/// spring and load case records). Without freedom cases the copy is written as before: no field «FreedomCase».
/// </summary>
static partial class Program
{
    static void FreedomCaseSupportsChecks()
    {
        var plain = Strand7Sample.Snapshot();
        Check(plain.Boundaries!.All(b => b.FreedomCase == null) && plain.Results.All(r => r.FreedomCase == null)
              && !JsonSerializer.Serialize(plain.Boundaries).Contains("FreedomCase", StringComparison.Ordinal)
              && !plain.Results.Any(r => JsonSerializer.Serialize(r).Contains("FreedomCase", StringComparison.Ordinal)),
            "AZ: a copy without freedom cases has no freedom case in supports and result sets, nor the field in its JSON");
        var model = Strand7Sample.Model(freedomCases: true);
        Check(model.GetFreedomCases().Length == 2,"AZ: the converter maps the two freedom cases of the synthetic model");
        var copy = GpcModelSnapshot.Create(model, Strand7Snapshot.Options(model, "freedom-case", "Straus7 · sintetico con due freedom case", Strand7Sample.Dataset,
            true, true, Strand7Snapshot.Conventions(true)));
        string? Of(string record) => copy.Boundaries!.Single(b => b.SourceRecord == record).FreedomCase;
        Check(Of("Restraint/1") == "FC A" && Of("Restraint/4") == "FC B" && Of("NodeSpring/2") == "FC B",
            "AZ: every restraint and spring to ground keeps its freedom case in the copy");
        Check(copy.Boundaries!.Where(b => b.Kind != "RESTRAIN").All(b => b.FreedomCase == null), "AZ: links and releases have no freedom case");
        var dead = copy.Results.Single(r => r.Name.Contains("Dead", StringComparison.Ordinal));
        var live = copy.Results.Single(r => r.Name.Contains("Live", StringComparison.Ordinal));
        Check(dead.FreedomCase == "FC A" && live.FreedomCase == "FC B", "AZ: every static case carries the freedom case under which it is analysed");
        // The viewer: the supports of the chosen case are those without freedom case and those of its freedom case (SceneOptions.Shows, same rule).
        static bool Shown(ModelBoundary b, string? freedomCase) => b.FreedomCase == null || b.FreedomCase == freedomCase;
        Check(copy.Boundaries!.Where(b => Shown(b, dead.FreedomCase)).Select(b => b.SourceRecord).Order().SequenceEqual(
                  copy.Boundaries!.Where(b => b.SourceRecord != "Restraint/4" && b.SourceRecord != "NodeSpring/2").Select(b => b.SourceRecord).Order()),
            "AZ: with the dead load case the view draws the restraint of FC A, not those of FC B");
        var reopened = ModelSnapshot.FromJson(JsonSerializer.Serialize(copy));
        Check(reopened.Fingerprint() == copy.Fingerprint() && reopened.Boundaries!.Single(b => b.SourceRecord == "NodeSpring/2").FreedomCase == "FC B",
            "AZ: the freedom cases survive the round trip of the copy");
        Check(copy.Fingerprint() != Strand7Sample.Snapshot().Fingerprint(), "AZ: the freedom cases enter the fingerprint of the copy");
    }
}
