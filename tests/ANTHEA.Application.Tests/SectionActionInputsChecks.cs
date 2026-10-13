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
        return count;
    }
}
