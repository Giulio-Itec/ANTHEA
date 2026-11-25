using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.EmbeddedWalls;
using Anthea.Presentation.Shell;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Organizzazione del modulo paratie (progetto del 16/11), step 3: the boxes Scavo, Falda and Sovraccarico of the command bar show the phase of
/// the tabs and write its row of «Fasi costruttive» exactly as the field of the table does (same JSON), one change per confirmed value; the
/// table editor and the drawing follow. Without a phase or in a read-only sheet nothing is written.
/// </summary>
internal static partial class EmbeddedWallChecks
{
    private static void StageLevelsChecks(Expect e)
    {
        var data = EmbeddedWallDocument.Create();
        var f2 = EmbeddedWallDocument.NewRow("stages"); f2["id"] = "F2"; f2["right_ground"] = -4; f2["left_water"] = -1.5; f2["right_water"] = -5;
        f2["left_surcharge"] = 15; f2["distributed_load"] = 2; data["stages"]!.AsArray().Add(f2);
        var service = EmbeddedWallDocument.NewRow("cases"); service["id"] = "Servizio"; data["cases"]!.AsArray().Add(service);
        using var sheet = Open(data);
        var levels = sheet.StageLevels; var stages = sheet.Tables.Single(t => t.Key == "stages");
        int changes = 0; sheet.Modified += (_, _) => changes++;
        e.That(levels.Stage == "F2" && levels.RightGround == "-4" && levels.LeftGround == "0" && levels.LeftWater == "-1.5" && levels.RightWater == "-5"
            && levels.LeftSurcharge == "15" && levels.RightSurcharge == "0" && levels.WallLoad == "2" && !levels.IsReadOnly,
            "Passo 3: le caselle non mostrano i valori della fase scelta.");
        sheet.InputStage = sheet.InputStages[0];
        e.That(levels.Stage == "F1" && levels.RightGround == "0" && levels.LeftWater == "", "Passo 3: le caselle non seguono la scheda della fase.");
        sheet.EditModelCommand.Execute("stages");
        levels.RightGround = "-3";
        e.That(stages.Rows[0]["right_ground"] == "-3" && sheet.Sketch?.RightGround == -3 && changes == 1 && stages.Editor?["right_ground"].Text == "-3",
            "Passo 3: lo scavo scritto nella barra non arriva alla fase, al disegno o al campo della tabella.");
        levels.RightGround = "-3";
        e.That(changes == 1, "Passo 3: lo stesso valore è una modifica.");
        sheet.InputStage = sheet.InputStages[1]; levels.LeftWater = "";
        e.That(sheet.Sketch?.LeftWater is null && sheet.Sketch.RightWater == -5 && changes == 2, "Passo 3: la falda vuota non vale «assente».");
        // The same value through the field of the table gives the same document.
        using var byTable = Open(data);
        byTable.EditModelCommand.Execute("stages"); var tableStages = byTable.Tables.Single(t => t.Key == "stages");
        tableStages.Selected = tableStages.Rows[0]; tableStages.Editor!["right_ground"].EditText = "-3";
        tableStages.Selected = tableStages.Rows[1]; tableStages.Editor!["left_water"].EditText = "";
        e.That(JsonNode.DeepEquals(byTable.Data, sheet.Data), "Passo 3: la barra scrive la fase in modo diverso dal campo della tabella.");
        levels.LeftSurcharge = "12,5";
        e.That(sheet.Sketch?.LeftSurcharge == 12.5 && sheet.Data["stages"]![1]!["left_surcharge"]!.ToString() == "12,5", "Passo 3: separatore decimale locale non accettato.");
        // A case without phases: no phase, nothing written.
        sheet.InputCase = "Servizio";
        string before = sheet.Data.ToJsonString(); int count = changes;
        levels.RightGround = "-9";
        e.That(!levels.HasStage && levels.IsReadOnly && levels.RightGround == "" && sheet.Data.ToJsonString() == before && changes == count,
            "Passo 3: senza fase le caselle scrivono o mostrano valori.");
        using var readOnly = Open(data, readOnly: true);
        before = readOnly.Data.ToJsonString(); readOnly.StageLevels.RightGround = "-9";
        e.That(readOnly.StageLevels.IsReadOnly && readOnly.StageLevels.RightGround == "-4" && readOnly.Data.ToJsonString() == before,
            "Passo 3: le caselle modificano un foglio in sola lettura.");

        // Contract C1 in the project: every confirmed value is one step of the history, with no other confirmation of the sheet.
        var services = Scripted.Services();
        using var workspace = new WorkspaceSession(services, new RegistrySheetFactory(new ModuleRegistry([new EmbeddedWallSheetModule()]), services, new CalculationLimiter(1)),
            new DocumentFiles(Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")));
        var project = workspace.NewProject()!; var section = project.AddSection(project.AddProject()!)!;
        var node = project.AddSheet(EmbeddedWallDocument.Module, section)!; string id = node["id"]!.ToString();
        var opened = (EmbeddedWallSheetViewModel)workspace.OpenSheet(node).ViewModel;
        string Stored(string key) => project.Find(id)!["dati"]!["stages"]![0]![key]?.ToString() ?? "";
        opened.StageLevels.RightGround = "-2";
        e.That(Stored("right_ground") == "-2" && project.UndoLabel == ProjectSession.EditLabel(ShellTexts.SheetName(node)), "C1: la casella confermata non è un passo della cronologia.");
        opened.StageLevels.LeftWater = "-1";
        e.That(Stored("left_water") == "-1", "C1: la seconda casella non è entrata nel progetto.");
        project.Restore(false);
        e.That(Stored("left_water") == "" && Stored("right_ground") == "-2", "C1: «Annulla» non toglie una casella alla volta.");
    }
}
