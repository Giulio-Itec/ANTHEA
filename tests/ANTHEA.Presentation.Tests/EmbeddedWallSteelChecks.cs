using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Reporting;
using Anthea.Reports;
using GPC.Checkers.Steel.Members;
using GPC.Model.Checking.Contracts;
using GPC.Model.Design.Steel;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;

namespace Anthea.Presentation.Tests;

internal static partial class EmbeddedWallChecks
{
    internal static JsonObject SteelExample()
    {
        var data = Example(); data["name"] = "Sostegni metallici — prova sintetica";
        data["fix_top_u"] = true; data["width"] = 1.5; data["loads"] = new JsonArray();
        data["steel_standard"] = "NTC 2018"; data["steel_check_kind"] = "StaticUltimate";
        foreach (var item in new[] { ("A", "Anchor", 30d, 2d), ("S", "Strut", 60d, 3d) })
        {
            var support = EmbeddedWallDocument.NewRow("supports"); support["id"] = item.Item1; support["kind"] = item.Item2;
            support["angle"] = item.Item3; support["spacing"] = item.Item4; support["elevation"] = 0; support["stiffness"] = 10000;
            data["supports"]!.AsArray().Add(support);
            var member = EmbeddedWallDocument.NewRow("steel_members"); member["support"] = item.Item1;
            member["diameter"] = 200; member["thickness"] = 8; member["nominal_grade"] = 355;
            member["fy"] = 355; member["fu"] = 510; member["elastic_modulus"] = 210000; member["material_source"] = "S355, proprietà assegnate per il collaudo";
            member["axial_only"] = true; member["length_1"] = 3; member["length_2"] = 4;
            member["length_source"] = "Vincoli ideali di collaudo, due piani"; data["steel_members"]!.AsArray().Add(member);
        }
        var stages = data["stages"]!.AsArray();
        var second = stages[0]!.DeepClone().AsObject(); second["id"] = "F2"; stages.Add(second);
        var third = stages[0]!.DeepClone().AsObject(); third["id"] = "F3"; stages.Add(third);
        foreach (var item in new[] { ("F1", "A", 20d), ("F1", "S", 30d), ("F2", "A", 10d), ("F2", "S", 40d) })
        {
            var row = EmbeddedWallDocument.NewRow("activations"); row["stage"] = item.Item1; row["support"] = item.Item2; row["prestress"] = item.Item3;
            data["activations"]!.AsArray().Add(row);
        }
        return data;
    }

    private static async Task SteelAsync(Expect e, string output)
    {
        var old = Example(); old["schema"] = 4; old.Remove("steel_members"); old.Remove("steel_standard"); old.Remove("steel_check_kind");
        string before = old.ToJsonString(); var upgraded = EmbeddedWallDocument.Snapshot(old);
        e.That(old.ToJsonString() == before && upgraded["schema"]!.ToString() == "11" && upgraded["steel_standard"]!.ToString() == "Non richieste"
            && upgraded["steel_members"]!.AsArray().Count == 0, "Migrazione 4→5 attiva verifiche o modifica gli input originali.");
        var data = SteelExample(); before = data.ToJsonString(); using var sheet = Open(data);
        e.That(await Expect.Within(sheet.CalculateAsync()), "Calcolo con sostegni metallici non pubblicato.");
        var result = sheet.Current!; var supports = sheet.SteelSupports;
        e.That(data.ToJsonString() == before && result.Complete && supports.Count == 4 && supports.All(s => s.CaseId == result.CaseId && s.Stage != "F3"), "Storia o provenienza sostegni alterata.");
        e.That(Math.Abs(supports[0].NormalForce!.Value - 20) < 1e-8 && Math.Abs(supports[1].NormalForce!.Value + 30) < 1e-8
            && Math.Abs(supports[3].NormalForce!.Value + 40) < 1e-8, "Segno o unità delle forze Steel errati.");
        var direct = Ntc2018AxialSteel.Check(new AxialSteelMember(new SteelSection(new SectionCHS(200, 8), new SteelMaterialEN1993("S355", 210000, 355, 510)),
            355, "test", true, 3000, 4000, "test"), -30000, new StandardNTC2018Steel());
        e.That(supports[1].Checks[2].Capacity == direct.Checks[2].Capacity / 1000 && Math.Abs(supports[1].Checks[2].Ratio!.Value - direct.Checks[2].Utilization!.Value) < 1e-12,
            "Passaggio Steel→ANTHEA altera capacità o utilizzo.");
        e.That(supports[1].Checks[2].CriticalForce == direct.Checks[2].CriticalForce / 1000 && supports[1].Checks[2].PartialFactor == 1.05,
            "Ncr o coefficienti non conservati.");
        using var reopened = Open(JsonNode.Parse(sheet.Data.ToJsonString())!.AsObject());
        e.That(JsonSerializer.Serialize(reopened.SteelSupports) == JsonSerializer.Serialize(supports), "Persistenza dei sostegni altera esiti o diagnostica.");
        string csv = EmbeddedWallReport.SteelCsv(result); before = sheet.Data.ToJsonString();
        byte[] docx = sheet.Report.BuildReport("Sostegni metallici della paratia", new HashSet<string>());
        ReportFiles.Write(Path.Combine(output, "paratie", "sostegni-acciaio.docx"), docx, "Sostegni metallici");
        File.WriteAllText(Path.Combine(output, "paratie", "sostegni-acciaio.csv"), csv);
        File.WriteAllText(Path.Combine(output, "paratie", "sostegni-acciaio.json"), sheet.Data.ToJsonString(new() { WriteIndented = true }));
        e.That(sheet.Data.ToJsonString() == before && csv.Contains(result.Fingerprint) && csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 9
            && DocxWriter.Read(docx).GetXml("word/document.xml").ToString().Contains("Instabilità flessionale asse 2"), "Report/CSV Steel diverso dallo snapshot o con ricalcolo.");
        reopened.Tables.Single(t => t.Key == "steel_members").Rows[1]["length_2"] = "5";
        e.That(reopened.Current is null && reopened.SteelSupports.Count == 0 && reopened.SelectedSteelSupport is null && !reopened.SteelCsvCommand.CanExecute(null),
            "Modifica del sostegno non invalida risultati e comando CSV.");
        var service = new EmbeddedWallModule();
        foreach (string missing in new[] { "material_source", "length_2", "fy", "axial_only", "family" })
        {
            var invalid = SteelExample(); invalid["steel_members"]![1]![missing] = missing == "axial_only" ? JsonValue.Create(false) : null;
            var report = service.Calculate(invalid).NativeAs<EmbeddedWallReportData>();
            e.That(report.Complete && report.SteelSupports!.Where(s => s.Support == "S").All(s => s.Outcome == EngineeringOutcome.NotEvaluated), "Dato mancante accettato in Steel: " + missing);
        }
        foreach (string kind in new[] { "SeismicUltimate", "Serviceability", "Analysis" })
        {
            var invalid = SteelExample(); invalid["steel_check_kind"] = kind;
            e.That(service.Calculate(invalid).NativeAs<EmbeddedWallReportData>().SteelSupports!.All(s => s.Outcome == EngineeringOutcome.NotEvaluated), "Combinazione non supportata accettata: " + kind);
        }
        var eurocode = SteelExample(); eurocode["steel_standard"] = "EN 1993-1-1 — profilo non disponibile";
        e.That(service.Calculate(eurocode).NativeAs<EmbeddedWallReportData>().SteelSupports!.All(s => s.Outcome == EngineeringOutcome.NotEvaluated), "Eurocodice sostituito implicitamente con NTC.");
        var unloaded = SteelExample(); unloaded["steel_members"]![1]!["length_2"] = null;
        foreach (var row in unloaded["activations"]!.AsArray().OfType<JsonObject>()) if (row["support"]!.ToString() == "S") row["prestress"] = 0;
        e.That(service.Calculate(unloaded).NativeAs<EmbeddedWallReportData>().SteelSupports!.Where(s => s.Support == "S").All(s => s.NormalForce == 0
            && s.Outcome == EngineeringOutcome.NotEvaluated), "Puntone scarico nasconde lunghezze d'inflessione mancanti.");
        var noBasis = SteelExample(); noBasis["action_basis"] = "Da definire";
        e.That(service.Calculate(noBasis).NativeAs<EmbeddedWallReportData>().SteelSupports!.All(s => s.Outcome == EngineeringOutcome.NotEvaluated), "Azioni non classificate verificate come SLU.");
        var mechanism = SteelExample(); mechanism["fix_top_u"] = false; mechanism["fix_bottom_u"] = false; mechanism["fix_bottom_rotation"] = false;
        var failed = service.Calculate(mechanism).NativeAs<EmbeddedWallReportData>();
        e.That(!failed.Complete && failed.SteelSupports!.All(s => s.Outcome == EngineeringOutcome.NotEvaluated && s.Checks.Count == 0), "Storia non convergente ha prodotto verifiche soddisfatte.");
        e.Throws<OperationCanceledException>(() => service.Calculate(SteelExample(), new CancellationToken(true)), "Annullamento ignorato.");
        var isolated = service.Calculate(SteelExample()).NativeAs<EmbeddedWallReportData>();
        e.That(JsonSerializer.Serialize(isolated.SteelSupports) == JsonSerializer.Serialize(supports), "Analisi Steel contaminate da altre esecuzioni.");
    }
}
