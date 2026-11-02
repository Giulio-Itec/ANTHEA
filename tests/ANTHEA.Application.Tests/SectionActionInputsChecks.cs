using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Calculations;
using X.Core;

internal static class SectionActionInputsChecks
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
        void Reject(Action action) { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception("Input invalido accettato"); }
        var old = SezioneCA.DefaultData(); SectionWorkspace.Prepare(old);
        string before = old.ToJsonString();
        Check(!SectionActionInputs.Unified(old), "Legacy convertito senza consenso");
        SectionActionInputs.ShearRows(old);
        Check(before == old.ToJsonString(), "Lettura muta il legacy");
        var data = DocumentService.CreateSheet("str_palo");
        Check(SectionActionInputs.Unified(data), "Nuovo foglio non unificato");
        Check(data["combinazioni"]!.AsObject().All(p => p.Value!.AsArray().Count == 0), "Nuovo foglio contiene forze inventate");
        JsonObject Row(string state, string element) => J.Obj(("id", Guid.NewGuid().ToString("N")), ("stato", state), ("elemento", element), ("nome", "C1"), ("N", "-120.25"), ("Vx", "12.5"), ("Vy", "-8"), ("T", "3"), ("Mx", "41.25"), ("My", "-9.75"));
        var rows = SectionWorkspace.Sets.Select((s,i) => Row(s, i == 0 ? "" : "E" + i)).ToArray();
        SectionActionInputs.Replace(data, rows);
        ModuleCatalog.ValidateData("str_palo", data);
        var actions = ConcreteSheetPreparation.ReadActions(data);
        Check(actions.All(p => p.Value.Length == 1), "Instradamento stato limite");
        Check(SectionActionInputs.ShearRows(data).Select(r => r.S("id")).SequenceEqual(rows.Take(2).Select(r => r.S("id"))), "Taglio non usa SLU e SLV");
        Check(SectionActionInputs.ShearRows(data)[1].S("My") == "-9.75", "Componenti concomitanti alterate");
        Check(actions["SLU"][0].S("elemento") == "", "Elemento obbligatorio");
        Check(actions["SLV"][0].S("elemento") == "E1", "Elemento esplicito perso");
        before = data.ToJsonString(); ConcreteSheetPreparation.CommitRows(data);
        Check(before == data.ToJsonString(), "Roundtrip perde componenti/elemento");
        var calculation = SectionActionInputs.CalculationCopy(data);
        Check(calculation["workspace_ca"]!["taglio"]!.Array("azioni").Count == 2, "Optimizer senza azioni SLU/SLV");
        Check(data["workspace_ca"]!["taglio"]!.Array("azioni").Count == 0, "Duplicazione permanente delle sollecitazioni");
        rows[0]["T"] = ""; Reject(() => SectionActionInputs.Replace(data, rows));
        Check(before == data.ToJsonString(), "Errore lascia import parziale");
        rows[0]["T"] = "0"; rows[0]["stato"] = "Taglio"; Reject(() => SectionActionInputs.Replace(data, rows));
        var same = new[] { Row("SLU", "E1"), Row("SLU", "E2") }; SectionActionInputs.Replace(data, same);
        Check(ConcreteSheetPreparation.ReadActions(data)["SLU"].Length == 2, "Nome uguale fonde elementi differenti");
        var settings = old["workspace_ca"]!.AsObject(); var options = settings["dominio2d"]!.AsObject(); var input = old["input"]!.AsObject();
        var plastic = new CheckerSection(input, settings, options, "SLU").Checker.SectionCheckerOptions.FailureDomainType;
        var elastic = new CheckerSection(input, settings, options, "SLV").Checker.SectionCheckerOptions.FailureDomainType;
        Check(plastic != elastic, "Fixture legacy non distingue i criteri");
        foreach (string state in new[] { "SLU", "SLV" })
        {
            options["modello_resistente"] = "Plastico";
            Check(new CheckerSection(input, settings, options, state).Checker.SectionCheckerOptions.FailureDomainType == plastic, "Plastico dipende dallo stato limite");
            options["modello_resistente"] = "Elastico";
            Check(new CheckerSection(input, settings, options, state).Checker.SectionCheckerOptions.FailureDomainType == elastic, "Elastico dipende dallo stato limite");
        }
        options["modello_resistente"] = "sconosciuto"; Reject(() => new CheckerSection(input, settings, options));
        ResistanceModels(Check, Reject, old);
        return count;
    }

    // 1.17.1: one resistance model per limit state, shared by the 3D and 2D domains (workspace_ca.modello_resistente), with the migration
    // «Conserva i numeri» of the sheets saved up to 1.17.0 (user decision of 2/11/2026).
    private static void ResistanceModels(Action<bool, string> Check, Action<Action> Reject, JsonObject legacy)
    {
        Check(SectionActionInputs.ResistanceModel(legacy["workspace_ca"]!.AsObject(), "SLV") is null && legacy["workspace_ca"]!["modello_resistente"] is null,
            "Criterio per stato nei fogli storici: lo stato dà il criterio");
        var fresh = DocumentService.CreateSheet("str_palo"); var ws = fresh["workspace_ca"]!.AsObject();
        Check(ws["modello_resistente"]?.ToJsonString() == """{"SLU":"Plastico","SLV":"Elastico"}""", "Nuovo foglio: SLU plastico e SLV elastico");
        Check(ws["dominio3d"]!["modello_resistente"] is null && ws["dominio2d"]!["modello_resistente"] is null, "Criterio ancora nei pannelli");
        var input = fresh["input"]!.AsObject();
        foreach (var (state, expected) in new[] { ("SLU", "Plastico"), ("SLV", "Elastico") })
            foreach (string panel in new[] { "dominio3d", "dominio2d" })
            {
                var o = SectionActionInputs.DomainOptions(ws, ws[panel]!.AsObject(), state);
                Check(o.S("modello_resistente") == expected, $"{panel} {state}: criterio {o.S("modello_resistente")}");
                string type = new CheckerSection(input, ws, o, state).Checker.SectionCheckerOptions.FailureDomainType.ToString();
                Check(type == (expected == "Elastico" ? "Elastic" : "Plastic"), $"{panel} {state}: dominio {type}");
            }
        Check(ws["dominio3d"]!["modello_resistente"] is null, "Le opzioni per stato scrivono nel pannello");
        // A unified sheet of 1.17.0: the single model of the 3D panel for both states; the 2D panel had another one.
        JsonObject Saved(string? three, string? two)
        {
            var data = (JsonObject)fresh.DeepClone(); var settings = data["workspace_ca"]!.AsObject(); settings.Remove("modello_resistente");
            if (three is not null) settings["dominio3d"]!["modello_resistente"] = three;
            if (two is not null) settings["dominio2d"]!["modello_resistente"] = two;
            SectionWorkspace.Prepare(data); return settings;
        }
        var mixed = Saved("Plastico", "Elastico");
        Check(mixed["modello_resistente"]?.ToJsonString() == """{"SLU":"Plastico","SLV":"Plastico"}""", "Migrazione: criterio del 3D per SLU e SLV");
        Check(mixed["migrazione_criterio"]?.ToJsonString() == """{"SLU":"Elastico","SLV":"Elastico"}""", "Migrazione: criterio del 2D non annotato");
        Check(mixed["dominio3d"]!["modello_resistente"] is null && mixed["dominio2d"]!["modello_resistente"] is null, "Migrazione: criterio rimasto nei pannelli");
        var same = Saved("Elastico", "Elastico");
        Check(same["modello_resistente"]?.ToJsonString() == """{"SLU":"Elastico","SLV":"Elastico"}""" && same["migrazione_criterio"] is null, "Migrazione: stessi criteri annotati");
        var none = Saved(null, "Elastico");
        Check(none["modello_resistente"]?.ToJsonString() == """{"SLU":"Plastico","SLV":"Elastico"}""", "Migrazione senza criterio: predefiniti per stato");
        Check(none["migrazione_criterio"]?.ToJsonString() == """{"SLU":"Elastico"}""", "Migrazione: solo lo stato cambiato del 2D");
        string once = none.ToJsonString(); SectionWorkspace.Prepare(none.Parent!.AsObject());
        Check(once == none.ToJsonString(), "Migrazione ripetuta all'apertura");
        // The actions written again keep the models chosen.
        none["modello_resistente"]!["SLU"] = "Elastico";
        SectionActionInputs.Replace(none.Parent!.AsObject(), []);
        Check(none["modello_resistente"]!.S("SLU") == "Elastico", "Le azioni riscritte cambiano il criterio");
        var broken = (JsonObject)fresh.DeepClone(); broken["workspace_ca"]!["modello_resistente"] = "Plastico";
        Reject(() => ModuleCatalog.ValidateData("str_palo", broken));
    }
}
