using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Modules.EmbeddedWalls;

namespace Anthea.Presentation.Tests;

internal static partial class EmbeddedWallChecks
{
    private static IEnumerable<EmbeddedWallWorkNode> WorkNodes(IEnumerable<EmbeddedWallWorkNode> nodes)
        => nodes.SelectMany(n => new[] { n }.Concat(WorkNodes(n.Children)));

    private static void WorkTreeChecks(Expect e)
    {
        var data = EmbeddedWallDocument.Create();
        var otherCase = EmbeddedWallDocument.NewRow("cases"); otherCase["id"] = "Servizio"; data["cases"]!.AsArray().Add(otherCase);
        var otherStage = EmbeddedWallDocument.NewRow("stages"); otherStage["case"] = "Servizio"; otherStage["right_ground"] = -3;
        data["stages"]!.AsArray().Add(otherStage);
        foreach (string key in EmbeddedWallDocument.Tables.Where(k => k is not ("stages" or "cases")))
        {
            if (data[key]!.AsArray().Count == 0) data[key]!.AsArray().Add(EmbeddedWallDocument.NewRow(key));
            if (EmbeddedWallDocument.HistoryTables.Contains(key))
            {
                var row = EmbeddedWallDocument.NewRow(key); row["case"] = "Servizio"; data[key]!.AsArray().Add(row);
            }
        }
        var orphan = EmbeddedWallDocument.NewRow("loads"); orphan["case"] = "Inesistente"; orphan["stage"] = "F99";
        data["loads"]!.AsArray().Add(orphan);
        using var sheet = Open(data);
        string original = sheet.Data.ToJsonString(); long revision = sheet.Revision;
        EmbeddedWallWorkNode Node(EmbeddedWallRow row) => WorkNodes(sheet.WorkTree).Single(n => ReferenceEquals(n.Row, row));
        foreach (var table in sheet.Tables)
        foreach (var row in table.Rows)
            e.That(WorkNodes(sheet.WorkTree).Count(n => ReferenceEquals(n.Row, row)) == 1, $"Riga dell'albero persa o duplicata: {table.Key}.");
        var stages = sheet.Tables.Single(t => t.Key == "stages");
        var loads = sheet.Tables.Single(t => t.Key == "loads");
        var serviceStage = stages.Rows.Single(r => r["case"] == "Servizio");
        var serviceLoad = loads.Rows.Single(r => r["case"] == "Servizio");
        e.That(WorkNodes(Node(serviceStage).Children).Any(n => n.Row == serviceLoad)
            && !WorkNodes(Node(stages.Rows[0]).Children).Any(n => n.Row == serviceLoad), "Fasi omonime di casi diversi mescolate nell'albero.");
        sheet.SelectWorkNodeCommand.Execute(Node(serviceLoad));
        e.That(sheet.InputCase == "Servizio" && sheet.InputStage == serviceStage && sheet.InspectorTable == loads
            && loads.Selected == serviceLoad && sheet.WorkspaceTab == 0, "Il nodo carico non apre caso, fase e riga corrispondenti.");
        e.That(sheet.Sketch?.RightGround == -3, "La fase selezionata dall'albero non aggiorna il disegno.");
        sheet.SelectWorkNodeCommand.Execute(WorkNodes(sheet.WorkTree).Single(n => n.Title == "Falda e filtrazione"));
        e.That(sheet.WorkspaceTab == 5, "Il nodo idraulico non apre i parametri di filtrazione.");
        sheet.SelectWorkNodeCommand.Execute(WorkNodes(sheet.WorkTree).Single(n => n.Title == "Ricerca dell'infissione"));
        e.That(sheet.WorkspaceTab == 2, "Il nodo infissione non apre l'editor.");
        sheet.ToggleWorkTreeCommand.Execute(null);
        e.That(sheet.IsWorkTreeCollapsed, "Albero non richiudibile.");
        sheet.ToggleWorkTreeCommand.Execute(null);
        e.That(!sheet.IsWorkTreeCollapsed && sheet.Data.ToJsonString() == original && sheet.Revision == revision,
            "Navigazione o chiusura dell'albero modifica i dati o la revisione.");
        var orphanRow = loads.Rows.Single(r => r["case"] == "Inesistente");
        e.That(WorkNodes(sheet.WorkTree).Any(n => n.Title.Contains("Inesistente") && n.Title.Contains("non definito"))
            && Node(orphanRow).Stage is null, "Riferimento invalido nascosto o attribuito a una fase arbitraria.");
        sheet.SelectWorkNodeCommand.Execute(Node(orphanRow));
        e.That(loads.Selected == orphanRow && sheet.Data.ToJsonString() == original, "Riga invalida non correggibile dall'albero.");
        var soil = sheet.Tables.Single(t => t.Key == "left_soils").Rows[0]; var soilNode = Node(soil);
        var soilGroup = sheet.WorkTree.Single(n => n.Title == "Terreni"); soilGroup.IsExpanded = false;
        soil["name"] = "Sabbia aggiornata";
        e.That(ReferenceEquals(Node(soil), soilNode) && soilNode.Title.StartsWith("Sabbia aggiornata") && !soilGroup.IsExpanded,
            "La modifica ricrea i nodi o perde la chiusura dei rami.");
        var phaseNode = Node(serviceStage); phaseNode.IsExpanded = false;
        stages.Selected = serviceStage; stages.UpCommand.Execute(null);
        e.That(ReferenceEquals(Node(serviceStage), phaseNode) && !phaseNode.IsExpanded, "Il riordino delle fasi perde l'identità o l'espansione del nodo.");
        loads.Selected = orphanRow; var removed = Node(orphanRow); loads.RemoveCommand.Execute(null);
        e.That(!WorkNodes(sheet.WorkTree).Any(n => n.Row == orphanRow), "Nodo eliminato rimasto nell'albero.");
        string afterRemove = sheet.Data.ToJsonString();
        sheet.SelectWorkNodeCommand.Execute(removed);
        e.That(sheet.Data.ToJsonString() == afterRemove && loads.Selected != orphanRow, "Un nodo obsoleto riattiva una riga eliminata.");
        // Duplicate identifiers remain visible; their actions must not be shown under either arbitrary phase.
        var duplicate = EmbeddedWallDocument.NewRow("stages"); duplicate["case"] = "Servizio";
        var duplicateData = sheet.Data.DeepClone().AsObject(); duplicateData["stages"]!.AsArray().Add(duplicate);
        sheet.Reload(duplicateData);
        var duplicateLoads = sheet.Tables.Single(t => t.Key == "loads").Rows.Single(r => r["case"] == "Servizio");
        e.That(Node(duplicateLoads).Stage is null
            && WorkNodes(sheet.WorkTree).Any(n => n.Title == "Riferimenti di fase da completare"), "Fase duplicata trattata come riferimento univoco.");
        sheet.IsWorkTreeCollapsed = true; sheet.Reload(data.DeepClone().AsObject());
        e.That(sheet.IsWorkTreeCollapsed && !WorkNodes(sheet.WorkTree).Contains(soilNode), "Reload perde lo stato del pannello o conserva righe disconnesse.");
        foreach (var table in sheet.Tables)
        foreach (var row in table.Rows)
            e.That(WorkNodes(sheet.WorkTree).Count(n => ReferenceEquals(n.Row, row)) == 1, $"Reload: riga dell'albero persa o duplicata: {table.Key}.");
        using var readOnly = Open(data, readOnly: true);
        var readonlyNode = WorkNodes(readOnly.WorkTree).First(n => n.TableKey == "supports" && n.Row is not null);
        var beforeReadOnly = readOnly.Data.ToJsonString();
        readOnly.SelectWorkNodeCommand.Execute(readonlyNode);
        e.That(readOnly.InspectorTable!.IsReadOnly && !readOnly.InspectorTable.AddCommand.CanExecute(null)
            && readOnly.Data.ToJsonString() == beforeReadOnly, "La navigazione permette modifiche in sola lettura.");
    }
}
