using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Reporting;
using Anthea.Reports;
using GPC.Model.Checking.Contracts;

namespace Anthea.Presentation.Tests;

internal static partial class EmbeddedWallChecks
{
    internal static JsonObject RcPileExample()
    {
        var data = Example(); data["name"] = "Paratia di pali in c.a. — mensola sintetica"; data["width"] = 1.5;
        data["action_basis"] = "SLU statico, azioni di progetto sintetiche"; data["rc_pile_standard"] = "NTC 2018"; data["rc_pile_check_kind"] = "Ultimate";
        data["segments"]![0]!["ei_mode"] = "Da palo circolare c.a."; data["segments"]![0]!["ei"] = null;
        data["segments"]![0]!["source"] = "EI lordo della fila unica di pali"; data["loads"]![0]!["horizontal"] = 1;
        var row = EmbeddedWallDocument.NewRow("rc_piles"); row["diameter"] = 800; row["spacing"] = 1; row["cover"] = 50;
        row["bar_count"] = 12; row["bar_diameter"] = 20; row["link_diameter"] = 10; row["link_spacing"] = 150;
        row["fck"] = 30; row["fyk"] = 450; row["creep"] = 0; row["material_source"] = "C30/B450, materiali del collaudo";
        row["single_row"] = true; row["representation_source"] = "Fila unica e ripartizione uniforme attraverso il fronte, ipotesi del caso sintetico";
        row["shear_confirmed"] = true; row["durability_cover"] = 35; row["detailing_mode"] = "Beam";
        row["compression_restrained"] = true; row["end_zones_confirmed"] = true; data["rc_piles"]!.AsArray().Add(row);
        return data;
    }
    private static async Task RcPilesAsync(Expect e, string output)
    {
        var legacy = Example(); legacy["schema"] = 7; legacy.Remove("rc_piles"); legacy.Remove("rc_pile_standard"); legacy.Remove("rc_pile_check_kind");
        string before = legacy.ToJsonString(); var migrated = EmbeddedWallDocument.Snapshot(legacy);
        e.That(legacy.ToJsonString() == before && migrated["schema"]!.ToString() == "11" && migrated["rc_pile_standard"]!.ToString() == "Non richieste",
            "Migrazione 7→8 modifica il documento precedente o attiva verifiche.");
        var data = RcPileExample(); before = data.ToJsonString(); using var sheet = Open(data);
        e.That(await Expect.Within(sheet.CalculateAsync()), "Calcolo palo c.a. non pubblicato."); var result = sheet.Current!;
        e.That(result.Complete && data.ToJsonString() == before && sheet.RcPiles.Count == result.Stages.Sum(s => s.Points.Count), "Tagli del palo persi o input modificati.");
        double rigidity = 22000 * Math.Pow(3.8, .3) * Math.PI * Math.Pow(800, 4) / 64 * 1.5;
        e.That(Math.Abs(sheet.RcPiles[0].Properties!.EquivalentRigidity!.Value - rigidity / 1e9) < 1e-6
            && Math.Abs(result.Stages[0].Points[0].Displacement - 1000 * Math.Pow(6000, 3) / (3 * rigidity)) < 1e-7, "EI o deformata analitica del palo errati.");
        foreach (var point in result.Stages[0].Points)
        {
            var pile = sheet.RcPiles.Single(r => r.Elevation == point.Elevation && r.Side == point.Side);
            e.That(Math.Abs(pile.Normal!.Value + point.Normal * 2 / 3) < 1e-9 && Math.Abs(pile.Shear!.Value - point.Shear * 2 / 3) < 1e-9
                && Math.Abs(pile.Moment!.Value + point.Moment * 2 / 3) < 1e-9 && pile.Stage == result.Stages[0].Id && pile.CaseId == result.CaseId,
                "Azioni Concrete non concomitanti o scalate due volte.");
        }
        e.That(sheet.RcPiles.All(r => r.Outcome == EngineeringOutcome.Satisfied && r.Checks.Count == 2 && r.Details.Count > 0), "Verifiche c.a. o dettagli non disponibili.");
        e.That(sheet.RcPiles.All(r => r.Details.Any(d => d.Method.StartsWith("MinimumLinks", StringComparison.Ordinal)
            && d.Outcome == EngineeringOutcome.NotSatisfied)), "Staffatura minima insufficiente nascosta dalla resistenza nominale soddisfatta.");
        e.That(sheet.RcPiles[0].Properties!.BarRadius == 330 && sheet.RcPiles[0].Properties!.OutlineDivisions == 64, "Geometria della corona non rispettata.");
        e.That(sheet.RcPiles[0].Properties!.GammaC == 1.5 && sheet.RcPiles[0].Properties!.GammaS == 1.15
            && sheet.RcPiles[0].Properties!.AlphaCC == .85, "Coefficienti del profilo NTC non conservati nel risultato.");
        using var reopened = Open(JsonNode.Parse(sheet.Data.ToJsonString())!.AsObject());
        e.That(JsonSerializer.Serialize(reopened.RcPiles) == JsonSerializer.Serialize(sheet.RcPiles), "Riapertura altera i risultati dei pali.");
        string csv = EmbeddedWallReport.RcPileCsv(result);
        using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(new StringReader(csv)) { TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(";"); var rows = new List<string[]>(); while (!parser.EndOfData) rows.Add(parser.ReadFields()!);
        e.That(rows.Count == 1 + sheet.RcPiles.Sum(r => r.Checks.Count + r.Details.Count) && rows.All(r => r.Length == 19)
            && csv.Contains(result.Fingerprint), "CSV del palo perde controlli, dettagli o provenienza.");
        before = sheet.Data.ToJsonString(); var docx = sheet.Report.BuildReport("Paratia di pali circolari in c.a.", new HashSet<string>());
        ReportFiles.Write(Path.Combine(output, "paratie", "sezioni-pali-ca.docx"), docx, "Sezioni circolari Concrete");
        File.WriteAllText(Path.Combine(output, "paratie", "sezioni-pali-ca.csv"), csv);
        File.WriteAllText(Path.Combine(output, "paratie", "sezioni-pali-ca.json"), sheet.Data.ToJsonString(new() { WriteIndented = true }));
        e.That(before == sheet.Data.ToJsonString() && DocxWriter.Read(docx).GetXml("word/document.xml").ToString().Contains("Dettagli costruttivi"), "Report ricalcola o perde i dettagli.");
        reopened.Tables.Single(t => t.Key == "rc_piles").Rows[0]["bar_count"] = "14";
        e.That(reopened.Current is null && reopened.RcPiles.Count == 0 && reopened.SelectedRcPileCheck is null && !reopened.RcPileCsvCommand.CanExecute(null), "Risultati c.a. superati ancora esportabili.");
        var service = new EmbeddedWallModule();
        foreach (var kind in new[] { "Characteristic", "Frequent", "QuasiPermanent" })
        {
            var sle = RcPileExample(); sle["rc_pile_check_kind"] = kind; sle["action_basis"] = "SLE " + kind + ", storia indipendente sintetica";
            var checks = service.Calculate(sle).NativeAs<EmbeddedWallReportData>().RcPiles!;
            e.That(checks.All(r => r.Outcome == EngineeringOutcome.Satisfied && r.Checks.Count == (kind == "Frequent" ? 1 : 2) && r.Details.Count == 0), "SLE del palo non disponibili: " + kind);
        }
        foreach (var variant in new[] { "source", "seismic", "basis", "norm", "missing" })
        {
            var invalid = RcPileExample(); var row = invalid["rc_piles"]![0]!;
            switch (variant)
            {
                case "source": row["material_source"] = ""; break;
                case "seismic": invalid["rc_pile_check_kind"] = "Seismic"; break;
                case "basis": invalid["action_basis"] = "Da definire"; break;
                case "norm": invalid["rc_pile_standard"] = "EN 1992-1-1 — profilo non disponibile"; break;
                case "missing": invalid["segments"]![0]!["ei_mode"] = "Assegnata"; invalid["segments"]![0]!["ei"] = 200000; row["bar_count"] = null; break;
            }
            e.That(service.Calculate(invalid).NativeAs<EmbeddedWallReportData>().RcPiles!.All(r => r.Outcome == EngineeringOutcome.NotEvaluated), "Caso c.a. non supportato certificato: " + variant);
        }
        var noShear = RcPileExample(); noShear["rc_piles"]![0]!["shear_confirmed"] = false;
        e.That(service.Calculate(noShear).NativeAs<EmbeddedWallReportData>().RcPiles!.All(r => r.Checks[1].Outcome == EngineeringOutcome.NotEvaluated), "Taglio senza conferma certificato.");
        var invalidGeometry = RcPileExample(); invalidGeometry["rc_piles"]![0]!["single_row"] = false;
        e.Throws<ArgumentException>(() => service.Calculate(invalidGeometry), "Rigidezza equivalente senza conferma accettata.");
        e.Throws<OperationCanceledException>(() => service.Calculate(RcPileExample(), new CancellationToken(true)), "Annullamento c.a. ignorato.");
        e.That(JsonSerializer.Serialize(service.Calculate(RcPileExample()).NativeAs<EmbeddedWallReportData>().RcPiles) == JsonSerializer.Serialize(result.RcPiles), "Sessioni c.a. contaminate.");
    }
}
