using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Application.Reporting;
using Anthea.Reports;
using GPC.Model.Checking.Contracts;

namespace Anthea.Presentation.Tests;

internal static partial class EmbeddedWallChecks
{
    internal static JsonObject TubeExample()
    {
        var data = Example(); data["name"] = "Paratia tubolare — mensola sintetica"; data["width"] = 1.5;
        data["action_basis"] = "SLU statico, forze di progetto sintetiche"; data["tube_standard"] = "NTC 2018"; data["tube_check_kind"] = "StaticUltimate";
        data["segments"]![0]!["ei_mode"] = "Da tubo CHS"; data["segments"]![0]!["ei"] = null;
        data["segments"]![0]!["source"] = "Rigidezza lorda della fila di tubi, senza effetto arco";
        data["loads"]![0]!["horizontal"] = .2; data["loads"]![0]!["downward"] = 5;
        var tube = EmbeddedWallDocument.NewRow("steel_tubes"); tube["diameter"] = 200; tube["thickness"] = 8; tube["spacing"] = 2;
        tube["nominal_grade"] = 355; tube["fy"] = 355; tube["fu"] = 510; tube["elastic_modulus"] = 210000;
        tube["material_source"] = "S355, resistenze assegnate per il caso sintetico"; tube["unperforated"] = true; tube["single_row"] = true;
        tube["representation_source"] = "Fila unica; fronte con ripartizione uniforme delle azioni ai tubi, ipotesi del collaudo";
        data["steel_tubes"]!.AsArray().Add(tube); return data;
    }
    private static async Task TubesAsync(Expect e, string output)
    {
        var legacy = Example(); legacy["schema"] = 6; legacy.Remove("steel_tubes"); legacy.Remove("tube_standard"); legacy.Remove("tube_check_kind");
        legacy["segments"]![0]!.AsObject().Remove("ei_mode"); string before = legacy.ToJsonString(); var migrated = EmbeddedWallDocument.Snapshot(legacy);
        e.That(legacy.ToJsonString() == before && migrated["schema"]!.ToString() == "10" && migrated["tube_standard"]!.ToString() == "Non richieste"
            && migrated["segments"]![0]!["ei_mode"]!.ToString() == "Assegnata", "Migrazione 6→7 modifica dati o attiva nuove verifiche.");
        var data = TubeExample(); before = data.ToJsonString(); using var sheet = Open(data);
        e.That(await Expect.Within(sheet.CalculateAsync()), "Calcolo tubolare non pubblicato."); var result = sheet.Current!;
        e.That(result.Complete && data.ToJsonString() == before && sheet.SteelTubes.Count == result.Stages.Sum(s => s.Points.Count), "Stazioni tubolari perse o input modificati.");
        double inertia = Math.PI / 64 * (Math.Pow(200, 4) - Math.Pow(184, 4)); double rigidity = 210000 * inertia * 1.5 / 2;
        e.That(Math.Abs(sheet.SteelTubes[0].Properties!.EquivalentRigidity!.Value - rigidity / 1e9) < 1e-9
            && Math.Abs(result.Stages[0].Points[0].Displacement - 200 * Math.Pow(6000, 3) / (3 * rigidity)) < 1e-7,
            "EI equivalente o deformata analitica errati.");
        foreach (var point in result.Stages[0].Points)
        {
            var tube = sheet.SteelTubes.Single(r => r.Elevation == point.Elevation && r.Side == point.Side);
            e.That(Math.Abs(tube.Normal!.Value + point.Normal * 4 / 3) < 1e-9 && Math.Abs(tube.Shear!.Value - point.Shear * 4 / 3) < 1e-9
                && Math.Abs(tube.Moment!.Value + point.Moment * 4 / 3) < 1e-9 && tube.Stage == result.Stages[0].Id && tube.CaseId == result.CaseId,
                "N–M–V non concomitanti o conversione fascia/interasse ripetuta.");
        }
        e.That(sheet.SteelTubes.All(r => r.SectionClass == 1 && r.PartialFactor == 1.05 && r.Checks.Count == 3 && r.Outcome == EngineeringOutcome.Satisfied),
            "Verifiche locali Steel non disponibili.");
        using var reopened = Open(JsonNode.Parse(sheet.Data.ToJsonString())!.AsObject());
        e.That(JsonSerializer.Serialize(reopened.SteelTubes) == JsonSerializer.Serialize(sheet.SteelTubes), "Risultati tubolari alterati dalla riapertura.");
        string csv = EmbeddedWallReport.TubeCsv(result);
        using var parser = new Microsoft.VisualBasic.FileIO.TextFieldParser(new StringReader(csv)) { TextFieldType = Microsoft.VisualBasic.FileIO.FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(";"); var rows = new List<string[]>(); while (!parser.EndOfData) rows.Add(parser.ReadFields()!);
        e.That(rows.Count == 1 + 3 * sheet.SteelTubes.Count && rows.All(r => r.Length == 23) && csv.Contains(result.Fingerprint), "CSV perde azioni, verifiche o provenienza.");
        before = sheet.Data.ToJsonString(); var docx = sheet.Report.BuildReport("Sezioni tubolari della paratia", new HashSet<string>());
        ReportFiles.Write(Path.Combine(output, "paratie", "sezioni-tubolari.docx"), docx, "Sezioni tubolari N-M-V");
        File.WriteAllText(Path.Combine(output, "paratie", "sezioni-tubolari.csv"), csv);
        File.WriteAllText(Path.Combine(output, "paratie", "sezioni-tubolari.json"), sheet.Data.ToJsonString(new() { WriteIndented = true }));
        e.That(before == sheet.Data.ToJsonString() && DocxWriter.Read(docx).GetXml("word/document.xml").ToString().Contains("EI lordo fascia"), "Report altera il risultato o perde la rigidezza GPC.");
        reopened.Tables.Single(t => t.Key == "steel_tubes").Rows[0]["thickness"] = "7";
        e.That(reopened.Current is null && reopened.SteelTubes.Count == 0 && reopened.SelectedTubeCheck is null && !reopened.TubeCsvCommand.CanExecute(null), "Verifiche tubolari superate ancora esportabili.");
        var service = new EmbeddedWallModule();
        foreach (var variant in new[] { "class3", "holes", "source", "seismic", "basis", "norm", "missing" })
        {
            var invalid = TubeExample(); var tube = invalid["steel_tubes"]![0]!;
            switch (variant)
            {
                case "class3": tube["thickness"] = 4; break;
                case "holes": tube["unperforated"] = false; break;
                case "source": tube["material_source"] = ""; break;
                case "seismic": invalid["tube_check_kind"] = "SeismicUltimate"; break;
                case "basis": invalid["action_basis"] = "Da definire"; break;
                case "norm": invalid["tube_standard"] = "EN 1993-1-1 — profilo non disponibile"; break;
                case "missing": invalid["segments"]![0]!["ei_mode"] = "Assegnata"; invalid["segments"]![0]!["ei"] = 200000; tube["fy"] = null; break;
            }
            e.That(service.Calculate(invalid).NativeAs<EmbeddedWallReportData>().SteelTubes!.All(r => r.Outcome == EngineeringOutcome.NotEvaluated), "Sezione/caso non supportato certificato: " + variant);
        }
        var invalidRow = TubeExample(); invalidRow["steel_tubes"]![0]!["single_row"] = false;
        e.Throws<ArgumentException>(() => service.Calculate(invalidRow), "EI equivalente senza ipotesi di ripartizione accettata.");
        e.Throws<OperationCanceledException>(() => service.Calculate(TubeExample(), new CancellationToken(true)), "Annullamento tubi ignorato.");
        var isolated = service.Calculate(TubeExample()).NativeAs<EmbeddedWallReportData>();
        e.That(JsonSerializer.Serialize(isolated.SteelTubes) == JsonSerializer.Serialize(result.SteelTubes), "Analisi tubolari contaminate fra loro.");
    }
}
