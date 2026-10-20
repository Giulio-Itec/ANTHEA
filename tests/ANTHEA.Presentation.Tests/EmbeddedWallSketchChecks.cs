using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Modules.EmbeddedWalls;

namespace Anthea.Presentation.Tests;

internal static partial class EmbeddedWallChecks
{
    private static void SketchChecks(Expect e)
    {
        var data = EmbeddedWallDocument.Create();
        var second = EmbeddedWallDocument.NewRow("stages"); second["id"] = "Scavo"; second["right_ground"] = -4;
        second["left_water"] = -1.5; second["right_water"] = -5; data["stages"]!.AsArray().Add(second);
        var other = EmbeddedWallDocument.NewRow("cases"); other["id"] = "Servizio"; data["cases"]!.AsArray().Add(other);
        var alternative = second.DeepClone().AsObject(); alternative["case"] = "Servizio"; alternative["right_ground"] = -2;
        data["stages"]!.AsArray().Add(alternative);
        var support = EmbeddedWallDocument.NewRow("supports"); support["id"] = "T1"; support["angle"] = 15;
        data["supports"]!.AsArray().Add(support);
        var activation = EmbeddedWallDocument.NewRow("activations"); activation["stage"] = "Scavo"; activation["prestress"] = 75;
        data["activations"]!.AsArray().Add(activation);
        var load = EmbeddedWallDocument.NewRow("loads"); load["stage"] = "Scavo"; load["horizontal"] = 10; data["loads"]!.AsArray().Add(load);
        using var sheet = Open(data);
        string original = sheet.Data.ToJsonString(); long revision = sheet.Revision;
        e.That(sheet.InputStages.Count == 2 && sheet.Sketch?.RightGround == -4 && sheet.Sketch.LeftWater == -1.5, "Anteprima non disponibile prima del calcolo.");
        e.That(sheet.Current is null && sheet.Sketch.Supports.Single().Prestress == 75 && sheet.Sketch.Loads.Single().HorizontalTop == 10, "Sostegno o azione non associati alla fase.");
        sheet.PreviousInputStageCommand.Execute(null);
        e.That(sheet.Sketch.RightGround == 0 && sheet.Sketch.Supports.Count == 0 && sheet.Sketch.Loads.Count == 0, "Sostegni o carichi ereditati implicitamente nell'anteprima.");
        e.That(!sheet.PreviousInputStageCommand.CanExecute(null) && sheet.NextInputStageCommand.CanExecute(null), "Navigazione oltre gli estremi delle fasi.");
        sheet.InputCase = "Servizio";
        e.That(sheet.Sketch.RightGround == -2 && sheet.Sketch.Supports.Count == 0 && sheet.Sketch.Loads.Count == 0, "Fasi omonime di casi indipendenti confuse.");
        e.That(sheet.InputStages.Count == 1 && !sheet.NextInputStageCommand.CanExecute(null), "Navigazione contamina le storie.");
        sheet.EditModelCommand.Execute("left_soils");
        var soil = sheet.Sketch.Bands.First(b => b.Table == "right_soils");
        sheet.PickModelCommand.Execute(new WallSketchPick(soil.Table, soil.Row));
        e.That(sheet.InspectorTable?.Key == "right_soils" && ReferenceEquals(sheet.InspectorTable.Selected, soil.Row), "Selezione grafica non apre i dati.");
        e.That(sheet.Data.ToJsonString() == original && sheet.Revision == revision, "Navigare modifica documento o revisione.");
        sheet.EditModelCommand.Execute("stages");
        e.That(ReferenceEquals(sheet.InspectorTable?.Selected, sheet.InputStage), "Editor di fase diverso dall'anteprima.");
        sheet.InputStage!["right_ground"] = "-3,5";
        e.That(sheet.Sketch.RightGround == -3.5 && sheet.Sketch.Notices.Count == 0, "Modifica della quota non aggiorna il disegno; separatore decimale locale.");
        sheet.InputStage["right_water"] = "non valido";
        e.That(sheet.Sketch.RightWater is null && sheet.Sketch.Notices.Any(n => n.Contains("Falda a destra")), "Numero errato trasformato in quota zero.");
        sheet.InputStage["right_water"] = "";
        e.That(sheet.Sketch.RightWater is null && sheet.Sketch.Notices.Count == 0, "Assenza di falda confusa con errore.");
        var stageTable = sheet.Tables.Single(t => t.Key == "stages");
        stageTable.Selected = stageTable.Rows[0];
        e.That(sheet.InputCase == data["case_id"]!.ToString() && ReferenceEquals(sheet.InputStage, stageTable.Rows[0]), "Selezione tabellare non aggiorna il caso.");
        sheet.InputStage = sheet.InputStages[1];
        sheet.Tables.Single(t => t.Key == "supports").Rows[0]["direction"] = "0";
        e.That(sheet.Sketch.Supports.Count == 0 && sheet.Sketch.Notices.Any(n => n.Contains("verso")), "Verso non valido rappresentato come sostegno.");
        sheet.Tables.Single(t => t.Key == "left_soils").Rows[0]["bottom"] = "3";
        e.That(sheet.Sketch.Bands.All(b => b.Table != "left_soils") && sheet.Sketch.Notices.Any(n => n.Contains("quota superiore")), "Strato invertito disegnato.");
        sheet.InspectorTable = stageTable;
        sheet.Reload(data.DeepClone().AsObject());
        e.That(sheet.InputStages.Contains(sheet.InspectorTable!.Selected!), "Reload conserva la riga del documento precedente.");
        sheet.InspectorTable.Selected!["right_ground"] = "-6";
        e.That(sheet.Sketch.RightGround == -6 && sheet.Data["stages"]![1]!["right_ground"]!.ToString() == "-6", "Editor dopo reload modifica dati disconnessi.");
        sheet.InputCase = "Servizio"; stageTable = sheet.Tables.Single(t => t.Key == "stages");
        stageTable.Selected = sheet.InputStage; stageTable.RemoveCommand.Execute(null);
        sheet.InputCase = "Servizio";
        e.That(sheet.InputStage is null && sheet.Sketch.StageRow is null && sheet.Sketch.RightGround is null, "Ultima fase rimossa ancora mostrata.");
        using var readOnly = Open(data, readOnly: true);
        readOnly.EditModelCommand.Execute("left_soils");
        e.That(readOnly.InspectorTable!.IsReadOnly && !readOnly.InspectorTable.AddCommand.CanExecute(null), "Anteprima permette la modifica di un foglio in sola lettura.");
        sheet.EditModelCommand.Execute("stages"); sheet.InspectorTable!.AddCommand.Execute(null);
        e.That(sheet.InputStage?["case"] == "Servizio" && sheet.InputStage["id"] == "F2"
            && ReferenceEquals(sheet.InputStage, sheet.InspectorTable.Selected), "Aggiunta fase non seleziona una riga unica del caso corrente.");
        sheet.InputStage!["right_ground"] = "-7";
        e.That(sheet.Sketch.RightGround == -7, "Anteprima della fase aggiunta non segue le modifiche.");
        sheet.EditModelCommand.Execute("loads"); sheet.InspectorTable!.AddCommand.Execute(null);
        e.That(sheet.InspectorTable.Selected!["case"] == "Servizio" && sheet.InspectorTable.Selected["stage"] == "F2", "Carico aggiunto al caso o fase errati.");
    }
}
