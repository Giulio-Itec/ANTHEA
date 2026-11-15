using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.EmbeddedWalls;
using Anthea.Presentation.Shell;

namespace Anthea.Presentation.Tests;

/// <summary>
/// A3 of the paratie review (21-22/10/2026), sheet of 16/11/2026: a phase added in the editor continues ground levels and water table of the
/// previous phase of its case, instead of restarting at level 0 without water. Surcharges, loads and stiffness keep the defaults; a case without
/// phases keeps the defaults of the module; the addition is one named step of the history of the project.
/// </summary>
internal static partial class EmbeddedWallChecks
{
    private static void NewStageChecks(Expect e)
    {
        // The rows mix integer and decimal values: compare their JSON text.
        static string J(JsonObject row, string key) => row[key]?.ToJsonString() ?? "null";
        var data = EmbeddedWallDocument.Create();
        var excavation = EmbeddedWallDocument.NewRow("stages"); excavation["id"] = "F2"; excavation["right_ground"] = -4;
        excavation["left_water"] = -1.5; excavation["right_water"] = -5; excavation["left_surcharge"] = 15; excavation["right_surcharge"] = 5;
        excavation["distributed_load"] = 3; excavation["stiffness_factor"] = .5; data["stages"]!.AsArray().Add(excavation);
        var service = EmbeddedWallDocument.NewRow("cases"); service["id"] = "Servizio"; data["cases"]!.AsArray().Add(service);
        var serviceStage = EmbeddedWallDocument.NewRow("stages"); serviceStage["id"] = "S1"; serviceStage["case"] = "Servizio";
        serviceStage["left_ground"] = -1; serviceStage["right_ground"] = -2; serviceStage["right_water"] = -2.5; data["stages"]!.AsArray().Add(serviceStage);
        string before = data.ToJsonString();
        var main = EmbeddedWallDocument.NewEditingRow(data, "stages", data["case_id"]!.ToString(), null);
        e.That(main["id"]!.ToString() == "F3" && main["case"]!.ToString() == "" && J(main, "left_ground") == "0"
            && J(main, "right_ground") == "-4" && J(main, "left_water") == "-1.5" && J(main, "right_water") == "-5",
            "A3: la fase nuova non continua quote e falda della fase precedente del caso principale.");
        e.That(J(main, "left_surcharge") == "0" && J(main, "right_surcharge") == "0" && J(main, "distributed_load") == "0"
            && J(main, "stiffness_factor") == "1", "A3: la fase nuova copia anche sovraccarichi, carichi o rigidezza.");
        var other = EmbeddedWallDocument.NewEditingRow(data, "stages", "Servizio", null);
        e.That(other["case"]!.ToString() == "Servizio" && J(other, "left_ground") == "-1" && J(other, "right_ground") == "-2"
            && J(other, "left_water") == "null" && J(other, "right_water") == "-2.5", "A3: la fase nuova prende quote e falda da un altro caso.");
        var empty = EmbeddedWallDocument.NewEditingRow(data, "stages", "Nuovo caso", null);
        e.That(J(empty, "left_ground") == "0" && J(empty, "right_ground") == "0" && J(empty, "left_water") == "null" && J(empty, "right_water") == "null",
            "A3: un caso senza fasi non riparte dai valori iniziali del modulo.");
        main["right_ground"] = -6;
        e.That(data.ToJsonString() == before && J(data["stages"]![1]!.AsObject(), "right_ground") == "-4", "A3: la fase nuova condivide i nodi della precedente o la modifica.");
        var load = EmbeddedWallDocument.NewEditingRow(data, "loads", data["case_id"]!.ToString(), "F2");
        var expectedLoad = EmbeddedWallDocument.NewRow("loads"); expectedLoad["case"] = ""; expectedLoad["stage"] = "F2";
        e.That(JsonNode.DeepEquals(load, expectedLoad), "A3: cambiate le righe nuove delle altre tabelle.");

        // In the sheet: «Aggiungi» of the phases is one change, with the levels of the phase shown before.
        using (var sheet = Open(data))
        {
            int changes = 0; sheet.Modified += (_, _) => changes++; long revision = sheet.Revision;
            sheet.EditModelCommand.Execute("stages"); sheet.InspectorTable!.AddCommand.Execute(null);
            e.That(sheet.InputStage?["id"] == "F3" && sheet.InputStage["right_ground"] == "-4" && sheet.InputStage["left_water"] == "-1.5"
                && sheet.Sketch?.RightGround == -4 && sheet.Sketch.LeftWater == -1.5 && sheet.Sketch.RightWater == -5,
                "A3: la fase aggiunta nel foglio non mostra quote e falda della precedente.");
            e.That(changes == 1 && sheet.Revision == revision + 1, "A3: l'aggiunta della fase non è una modifica sola.");
        }

        // In the project: one step of the history, named after the sheet; «Annulla» takes back the phase with its levels, not the edit before.
        var services = Scripted.Services();
        using var workspace = new WorkspaceSession(services, new RegistrySheetFactory(new ModuleRegistry([new EmbeddedWallSheetModule()]), services, new CalculationLimiter(1)),
            new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")));
        var project = workspace.NewProject()!; var section = project.AddSection(project.AddProject()!)!;
        var node = project.AddSheet(EmbeddedWallDocument.Module, section)!; string id = node["id"]!.ToString(), name = ShellTexts.SheetName(node);
        JsonArray Stages() => project.Find(id)!["dati"]!["stages"]!.AsArray();
        var tab = workspace.OpenSheet(node); var opened = (EmbeddedWallSheetViewModel)tab.ViewModel;
        var first = opened.Tables.Single(t => t.Key == "stages").Rows[0]; first["right_ground"] = "-3"; first["right_water"] = "-4";
        workspace.CommitActive();
        string edited = project.Find(id)!["dati"]!.ToJsonString();
        opened.Tables.Single(t => t.Key == "stages").AddCommand.Execute(null);
        workspace.CommitActive();
        e.That(Stages().Count == 2 && Stages()[1]!["right_ground"]!.ToString() == "-3" && Stages()[1]!["right_water"]!.ToString() == "-4"
            && project.UndoLabel == ProjectSession.EditLabel(name), "A3: l'aggiunta della fase non è un passo con il nome del foglio: " + project.UndoLabel);
        project.Restore(false);
        e.That(Stages().Count == 1 && project.Find(id)!["dati"]!.ToJsonString() == edited && project.RedoLabel == ProjectSession.EditLabel(name),
            "A3: «Annulla» non toglie in un passo solo la fase con le quote copiate.");
        project.Restore(true);
        e.That(Stages().Count == 2 && Stages()[1]!["right_ground"]!.ToString() == "-3", "A3: «Ripristina» non riporta la fase con le quote copiate.");
    }
}
