using Anthea.Testing.Normalization;
int checks = 0;
void Check(bool ok) { if (!ok) throw new InvalidOperationException("Identità diagnostica alterata"); checks++; }
foreach (var owner in new[] { "ReportWord", "ReportConcrete", "ReportRetainingWall", "ReportOrizzontale", "ReportElasticPile" })
{
    Check(FallbackCallers.StableIdentity("X.Core." + owner + ".CreateDocument") == "X.Core." + owner + ".Create");
    Check(FallbackCallers.StableIdentity("X.Core." + owner + ".Create") == "X.Core." + owner + ".Create");
}
foreach (var owner in new[] { "ReportConcrete", "ReportRetainingWall", "ReportBridge" })
    Check(FallbackCallers.StableIdentity("X.Core." + owner + ".<CreateDocument>b__") == "X.Core." + owner + ".<Create>b__");
foreach (var value in new[] { "Other.CreateDocument", "X.Core.ReportWord.CreateDocuments", "X.Core.ReportBridge.CreateDocument", "", "X.Core.ReportWord.CreateDocument.Nested" })
    Check(FallbackCallers.StableIdentity(value) == value);
Console.WriteLine($"PASS {checks} identità stabili, nessuna sostituzione generica");
