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
var timestamps = new TextNormalizer(day: new DateOnly(2026, 10, 13));
foreach (string title in new[] { "ANTHEA", "Bridge Design", "Micropalo — Bustamante–Doix", "Palo — capacità portante" })
    Check(timestamps.NormalizeReportTimestamps($"<w:t>{title} · 13/10/2026 04:58</w:t>") == timestamps.NormalizeReportTimestamps($"<w:t>{title} · 13/10/2026 04:59</w:t>"));
string line = "<w:t xml:space=\"preserve\">ANTHEA · 13/10/2026 23:59</w:t>";
Check(timestamps.NormalizeReportTimestamps(line) == timestamps.NormalizeReportTimestamps(line.Replace("13/10/2026 23:59", "14/10/2026 00:00")));
string engineering = "<w:t>fck = 35 MPa · φ = 2,50 · 13/10/2026 04:58</w:t>";
Check(timestamps.NormalizeReportTimestamps(engineering) == engineering);
Check(timestamps.NormalizeReportTimestamps(line + engineering) != timestamps.NormalizeReportTimestamps(line + engineering.Replace("35 MPa", "36 MPa")));
Check(timestamps.NormalizeReportTimestamps(line.Replace("13/10/2026", "17/01/2018")) == line.Replace("13/10/2026", "17/01/2018"));
Check(timestamps.NormalizeReportTimestamps(line.Replace("ANTHEA", "Nota utente")) == line.Replace("ANTHEA", "Nota utente"));
Console.WriteLine($"PASS {checks} identità diagnostiche e orari di generazione circoscritti");
// Aggancio dei ripieghi per contesto (decisione E5 del 15/10/2026, F2.11c).
Console.WriteLine($"PASS {FallbackHookChecks.Run()} controlli dell'aggancio dei ripieghi per contesto");
// Admitted transitions of the reference comparisons (decision D2 of 15/10/2026): negative checks.
return Anthea.Testing.Comparison.TransitionChecks.Run() == 0 ? 0 : 1;
