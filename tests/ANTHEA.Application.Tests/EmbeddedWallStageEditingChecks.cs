using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;

/// <summary>
/// Rules of the menu Fase of the paratie sheet (organizzazione del modulo paratie, progetto del 16/11, step 2), in ANTHEA.Application:
/// insert before the current phase with the rule of A3 (D9), rename one phase or all of them with their references, references and removal
/// of a phase. Phases of other cases, homonyms and references to complete are never touched or attached silently. Only identifiers and
/// rows move: no engineering value is computed or invented.
/// </summary>
internal static class EmbeddedWallStageEditingChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        void Reject(Action action, string before, Func<string> after, string message)
        {
            try { action(); } catch (ArgumentException) { Check(after() == before, message + " (documento modificato dal rifiuto)"); return; }
            throw new InvalidOperationException(message);
        }
        static string J(JsonNode? node) => node?.ToJsonString() ?? "null";
        static string[] Ids(JsonObject data, string caseId = "") => data["stages"]!.AsArray().Cast<JsonObject>()
            .Where(r => (r["case"]?.ToString() ?? "") == caseId).Select(r => r["id"]!.ToString()).ToArray();
        JsonObject Sample()
        {
            var data = EmbeddedWallDocument.Create();
            var f1 = data["stages"]![0]!.AsObject(); f1["left_water"] = -2;
            var f2 = EmbeddedWallDocument.NewRow("stages"); f2["id"] = "F2"; f2["right_ground"] = -4; f2["left_water"] = -1.5; f2["right_water"] = -5;
            f2["left_surcharge"] = 10; data["stages"]!.AsArray().Add(f2);
            var service = EmbeddedWallDocument.NewRow("cases"); service["id"] = "Servizio"; data["cases"]!.AsArray().Add(service);
            var s1 = EmbeddedWallDocument.NewRow("stages"); s1["id"] = "F2"; s1["case"] = "Servizio"; s1["right_ground"] = -2; data["stages"]!.AsArray().Add(s1);
            var support = EmbeddedWallDocument.NewRow("supports"); data["supports"]!.AsArray().Add(support);
            foreach (var (caseId, stage) in new[] { ("", "F2"), ("Servizio", "F2") })
            {
                var activation = EmbeddedWallDocument.NewRow("activations"); activation["case"] = caseId; activation["stage"] = stage; data["activations"]!.AsArray().Add(activation);
                var load = EmbeddedWallDocument.NewRow("loads"); load["case"] = caseId; load["stage"] = stage; load["horizontal"] = 5; data["loads"]!.AsArray().Add(load);
            }
            var upl = EmbeddedWallDocument.NewRow("hydraulic_upl"); upl["stage"] = "F2"; data["hydraulic_upl"]!.AsArray().Add(upl);
            var action = EmbeddedWallDocument.NewRow("hydraulic_actions"); action["stage"] = "F2"; data["hydraulic_actions"]!.AsArray().Add(action);
            var global = EmbeddedWallDocument.NewRow("global_checks"); global["stage"] = "F1"; data["global_checks"]!.AsArray().Add(global);
            return data;
        }

        // Insert before the current phase (D9): the levels of the phase before it in the same case, or the defaults as the first one.
        var data = Sample(); var stages = data["stages"]!.AsArray();
        var f2row = stages[1]!.AsObject();
        var inserted = EmbeddedWallDocument.InsertStage(data, f2row);
        Check(ReferenceEquals(stages[1], inserted) && Ids(data).SequenceEqual(["F1", "F3", "F2"]) && inserted["case"]!.ToString() == "",
            "Inserisci: fase non inserita prima della corrente, nel caso principale, con identificativo libero.");
        Check(J(inserted["left_ground"]) == "0" && J(inserted["right_ground"]) == "0" && J(inserted["left_water"]) == "-2" && J(inserted["right_water"]) == "null",
            "Inserisci: la fase nuova non riprende quote e falda della precedente del caso (D9).");
        Check(J(inserted["left_surcharge"]) == "0" && J(inserted["stiffness_factor"]) == "1", "Inserisci: copiati sovraccarichi o rigidezza.");
        inserted["left_water"] = -9;
        Check(J(stages[0]!["left_water"]) == "-2", "Inserisci: la fase nuova condivide i nodi della precedente.");
        var first = EmbeddedWallDocument.InsertStage(data, stages[0]!.AsObject());
        Check(ReferenceEquals(stages[0], first) && J(first["left_water"]) == "null" && J(first["right_ground"]) == "0" && first["id"]!.ToString() == "F4",
            "Inserisci: prima della prima fase non valgono i valori iniziali del modulo.");
        var service = stages.Cast<JsonObject>().Single(r => r["case"]?.ToString() == "Servizio");
        var serviceFirst = EmbeddedWallDocument.InsertStage(data, service);
        Check(serviceFirst["case"]!.ToString() == "Servizio" && J(serviceFirst["right_ground"]) == "0",
            "Inserisci: la fase di un altro caso riprende le quote del caso principale.");
        var appended = EmbeddedWallDocument.NewEditingRow(data, "stages", "Servizio", null);
        Check(J(appended["right_ground"]) == "-2", "A3 cambiata dall'estrazione della regola comune.");
        Reject(() => EmbeddedWallDocument.InsertStage(data, EmbeddedWallDocument.NewRow("stages")), data.ToJsonString(), data.ToJsonString,
            "Inserisci: accettata una fase che non appartiene al documento.");

        // Rename one phase: its references in its case follow, the homonym of another case and its rows do not.
        data = Sample(); stages = data["stages"]!.AsArray(); f2row = stages[1]!.AsObject();
        int moved = EmbeddedWallDocument.RenameStage(data, f2row, "  Scavo  ");
        Check(moved == 4 && f2row["id"]!.ToString() == "Scavo", "Rinomina: identificativo non assegnato o riferimenti non contati: " + moved);
        Check(data["activations"]!.AsArray().Cast<JsonObject>().Select(r => r["stage"]!.ToString()).SequenceEqual(["Scavo", "F2"])
            && data["loads"]!.AsArray().Cast<JsonObject>().Select(r => r["stage"]!.ToString()).SequenceEqual(["Scavo", "F2"])
            && data["hydraulic_upl"]![0]!["stage"]!.ToString() == "Scavo" && data["hydraulic_actions"]![0]!["stage"]!.ToString() == "Scavo"
            && data["global_checks"]![0]!["stage"]!.ToString() == "F1" && stages[2]!["id"]!.ToString() == "F2",
            "Rinomina: riferimenti del caso non aggiornati, o toccati quelli di un altro caso o di un'altra fase.");
        string renamed = data.ToJsonString();
        Reject(() => EmbeddedWallDocument.RenameStage(data, f2row, "F1"), renamed, data.ToJsonString, "Rinomina: accettato un identificativo già usato nel caso.");
        Reject(() => EmbeddedWallDocument.RenameStage(data, f2row, "   "), renamed, data.ToJsonString, "Rinomina: accettato un identificativo vuoto.");
        Check(EmbeddedWallDocument.RenameStage(data, f2row, "Scavo") == 0 && data.ToJsonString() == renamed, "Rinomina: lo stesso nome modifica il documento.");
        Check(EmbeddedWallDocument.RenameStage(data, stages[2]!.AsObject(), "F1") == 2 && stages[2]!["id"]!.ToString() == "F1",
            "Rinomina: un nome del caso principale è rifiutato in un altro caso.");
        var orphan = EmbeddedWallDocument.NewRow("loads"); orphan["stage"] = "F9"; data["loads"]!.AsArray().Add(orphan);
        renamed = data.ToJsonString();
        Reject(() => EmbeddedWallDocument.RenameStage(data, f2row, "F9"), renamed, data.ToJsonString,
            "Rinomina: righe da completare collegate in silenzio alla fase rinominata.");
        var duplicate = EmbeddedWallDocument.NewRow("stages"); duplicate["id"] = "Scavo"; stages.Add(duplicate);
        Check(EmbeddedWallDocument.RenameStage(data, f2row, "Scavo bis") == 0 && data["activations"]![0]!["stage"]!.ToString() == "Scavo",
            "Rinomina: con un omonimo i riferimenti sono stati spostati a una fase arbitraria.");
        Check(EmbeddedWallDocument.StageReferences(data, duplicate).Count == 4, "Rinomina: tolto l'omonimo, i riferimenti non restano alla fase rimasta.");

        // Rename all: F1…Fn in the order of the history, references by the old names, at the same time (no chain F2 → F1 → …).
        data = Sample(); stages = data["stages"]!.AsArray();
        EmbeddedWallDocument.InsertStage(data, stages[0]!.AsObject());
        Check(Ids(data).SequenceEqual(["F3", "F1", "F2"]), "Rinomina tutte: preparazione errata.");
        int rewritten = EmbeddedWallDocument.RenameAllStages(data, data["case_id"]!.ToString());
        Check(Ids(data).SequenceEqual(["F1", "F2", "F3"]) && rewritten == 5, "Rinomina tutte: serie o conteggio errati: " + rewritten);
        Check(data["activations"]![0]!["stage"]!.ToString() == "F3" && data["global_checks"]![0]!["stage"]!.ToString() == "F2"
            && data["activations"]![1]!["stage"]!.ToString() == "F2" && Ids(data, "Servizio").SequenceEqual(["F2"]),
            "Rinomina tutte: riferimenti scambiati a catena o toccato un altro caso.");
        Check(EmbeddedWallDocument.RenameAllStages(data, "Servizio") == 2 && Ids(data, "Servizio").SequenceEqual(["F1"])
            && data["loads"]![1]!["stage"]!.ToString() == "F1", "Rinomina tutte: il secondo caso non segue la sua storia.");
        var repeated = EmbeddedWallDocument.NewRow("stages"); repeated["id"] = "F1"; stages.Add(repeated);
        string before = data.ToJsonString();
        Reject(() => EmbeddedWallDocument.RenameAllStages(data, data["case_id"]!.ToString()), before, data.ToJsonString,
            "Rinomina tutte: accettati identificativi ripetuti.");
        // The global check cites «F1», which no longer exists once F1 is called «Iniziale»: F1 is a name of the new series.
        data = Sample(); data["stages"]![0]!["id"] = "Iniziale"; before = data.ToJsonString();
        Reject(() => EmbeddedWallDocument.RenameAllStages(data, data["case_id"]!.ToString()), before, data.ToJsonString,
            "Rinomina tutte: un riferimento da completare collegato in silenzio a una fase della nuova serie.");
        data = Sample(); data["global_checks"]![0]!["stage"] = "F9"; data["stages"]![0]!["id"] = "Iniziale";
        Check(EmbeddedWallDocument.RenameAllStages(data, data["case_id"]!.ToString()) == 0 && Ids(data).SequenceEqual(["F1", "F2"])
            && data["global_checks"]![0]!["stage"]!.ToString() == "F9", "Rinomina tutte: toccato un riferimento da completare fuori dalla nuova serie.");

        // A new phase never takes a name that rows of the document already cite without such a phase (Aggiungi as Inserisci).
        data = Sample(); orphan = EmbeddedWallDocument.NewRow("loads"); orphan["stage"] = "F3"; data["loads"]!.AsArray().Add(orphan);
        Check(EmbeddedWallDocument.NewEditingRow(data, "stages", data["case_id"]!.ToString(), null)["id"]!.ToString() == "F4"
            && EmbeddedWallDocument.InsertStage(data, data["stages"]![0]!.AsObject())["id"]!.ToString() == "F4",
            "Fase nuova: preso un identificativo già citato da righe da completare.");

        // Delete: the rows that cite the phase in its case go with it, nothing else.
        data = Sample(); stages = data["stages"]!.AsArray(); f2row = stages[1]!.AsObject();
        var references = EmbeddedWallDocument.StageReferences(data, f2row);
        Check(references.Select(r => r.Table).SequenceEqual(["activations", "loads", "hydraulic_upl", "hydraulic_actions"]),
            "Elimina: righe che citano la fase errate: " + string.Join(", ", references.Select(r => r.Table)));
        EmbeddedWallDocument.DeleteStage(data, f2row);
        Check(Ids(data).SequenceEqual(["F1"]) && Ids(data, "Servizio").SequenceEqual(["F2"]) && data["activations"]!.AsArray().Count == 1
            && data["activations"]![0]!["case"]!.ToString() == "Servizio" && data["loads"]!.AsArray().Count == 1 && data["hydraulic_upl"]!.AsArray().Count == 0
            && data["global_checks"]!.AsArray().Count == 1 && data["supports"]!.AsArray().Count == 1, "Elimina: tolte righe di altri casi o altre fasi, o rimaste righe orfane.");
        EmbeddedWallDocument.Validate(data);
        data = Sample(); duplicate = EmbeddedWallDocument.NewRow("stages"); duplicate["id"] = "F2"; data["stages"]!.AsArray().Add(duplicate);
        Check(EmbeddedWallDocument.StageReferences(data, duplicate).Count == 0, "Elimina: una fase con omonimo porta via le righe dell'altra.");
        return checks;
    }
}
