namespace Anthea.Testing.Normalization;

/// <summary>Stable identities of report entry points moved in F3. Raw TSV keeps the actual caller.</summary>
public static class FallbackCallers
{
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["X.Core.ReportWord.CreateDocument"] = "X.Core.ReportWord.Create",
        ["X.Core.ReportConcrete.CreateDocument"] = "X.Core.ReportConcrete.Create",
        ["X.Core.ReportConcrete.<CreateDocument>b__"] = "X.Core.ReportConcrete.<Create>b__",
        ["X.Core.ReportRetainingWall.CreateDocument"] = "X.Core.ReportRetainingWall.Create",
        ["X.Core.ReportRetainingWall.<CreateDocument>b__"] = "X.Core.ReportRetainingWall.<Create>b__",
        ["X.Core.ReportOrizzontale.CreateDocument"] = "X.Core.ReportOrizzontale.Create",
        ["X.Core.ReportElasticPile.CreateDocument"] = "X.Core.ReportElasticPile.Create",
        ["X.Core.ReportBridge.<CreateDocument>b__"] = "X.Core.ReportBridge.<Create>b__"
    };
    public static string StableIdentity(string caller) => Aliases.GetValueOrDefault(caller, caller);
}
