using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Help;
using Anthea.Presentation.Modules.EmbeddedWalls;
using Anthea.Reports;
using GPC.Model.Checking.Contracts;

namespace Anthea.Presentation.Tests;

/// <summary>
/// A2 of the paratie review (21-22/10/2026), option (a) of the sheet of 16/11/2026: the service adds no partial factor, so a ULS check runs only
/// on a history the user confirms as already designed. New and saved documents ask for it; nothing becomes confirmed by itself, a case never
/// inherits the confirmation of the main history, the SLE combinations do not need it and every report states the warning.
/// </summary>
internal static partial class EmbeddedWallChecks
{
    private const string DesignKey = EmbeddedWallDocument.DesignHistoryKey;

    private static void DesignHistoryChecks(Expect e)
    {
        var service = new EmbeddedWallModule();
        var created = EmbeddedWallDocument.Create();
        e.That(created["schema"]!.ToString() == "13" && created[DesignKey]!.GetValue<bool>() == false
            && EmbeddedWallDocument.NewRow("cases")[DesignKey]!.GetValue<bool>() == false, "A2: documento o caso nuovo già confermato.");

        // A document saved before (schema 12): the request stays open on the main history and on every case, whatever the provenance says.
        var saved = RcPileExample(); saved["schema"] = 12; saved.Remove(DesignKey);
        var savedCase = EmbeddedWallDocument.NewRow("cases"); savedCase["id"] = "C2"; savedCase.Remove(DesignKey); saved["cases"]!.AsArray().Add(savedCase);
        string savedJson = saved.ToJsonString(); var migrated = EmbeddedWallDocument.Snapshot(saved);
        e.That(saved.ToJsonString() == savedJson && migrated["schema"]!.ToString() == "13" && migrated[DesignKey]!.GetValue<bool>() == false
            && migrated["cases"]![0]![DesignKey]!.GetValue<bool>() == false && migrated["action_basis"]!.ToString() == saved["action_basis"]!.ToString(),
            "A2: la migrazione conferma da sola una storia salvata o modifica l'originale.");
        saved["cases"] = new JsonArray();
        using (var opened = Open(saved))
        {
            var field = opened.General[DesignKey];
            e.That(opened.Data["schema"]!.ToString() == "13" && !field.IsChecked
                && opened.General.Groups.Single(g => g.Title == "Verifiche e combinazioni").Fields.Any(f => f.Key == DesignKey),
                "A2: il documento salvato non mostra la richiesta di conferma fra le verifiche.");
            field.IsChecked = true;
            e.That(opened.Data[DesignKey]!.GetValue<bool>() && opened.Current is null, "A2: la conferma non scrive un booleano o non invalida i risultati.");
        }
        e.That(EmbeddedWallFields.Columns("cases").Any(f => f.Key == DesignKey && f.Bool)
            && HelpTopics.ForField(DesignKey, EmbeddedWallDocument.Module)?.Uri == "guida-paratie#richiedere-le-verifiche",
            "A2: conferma assente dalla tabella dei casi o senza aiuto.");

        // ULS without the confirmation: never verified, with the request; with it the same checks as before.
        var piles = RcPileExample(); piles[DesignKey] = false;
        var pileRows = service.Calculate(piles).NativeAs<EmbeddedWallReportData>().RcPiles!;
        e.That(pileRows.Count > 0 && pileRows.All(r => r.Outcome == EngineeringOutcome.NotEvaluated && r.Checks.Count == 0
            && r.Diagnostic == EmbeddedWallDocument.DesignHistoryRequest(r.CaseId)), "A2: palo SLU verificato senza la conferma della storia.");
        var tubes = TubeExample(); tubes[DesignKey] = false;
        var tubeRows = service.Calculate(tubes).NativeAs<EmbeddedWallReportData>().SteelTubes!;
        e.That(tubeRows.Count > 0 && tubeRows.All(r => r.Outcome == EngineeringOutcome.NotEvaluated
            && r.Diagnostic.EndsWith(EmbeddedWallDocument.DesignHistoryRequest(r.CaseId), StringComparison.Ordinal)), "A2: tubo SLU verificato senza la conferma.");
        foreach (string kind in new[] { "StaticUltimate", "SeismicUltimate" })
        {
            var steel = SteelExample(); steel[DesignKey] = false; steel["steel_check_kind"] = kind;
            var steelRows = service.Calculate(steel).NativeAs<EmbeddedWallReportData>().SteelSupports!;
            e.That(steelRows.Count > 0 && steelRows.All(r => r.Outcome == EngineeringOutcome.NotEvaluated && r.Checks.Count == 0
                && r.Diagnostic.EndsWith(EmbeddedWallDocument.DesignHistoryRequest(r.CaseId), StringComparison.Ordinal)), "A2: sostegno SLU verificato senza la conferma: " + kind);
        }
        var strip = Example(); strip["check_kind"] = "Ultimate"; strip["rc_sections"] = new JsonArray(EmbeddedWallDocument.NewRow("rc_sections"));
        // B2 (review of 15/11): without the confirmation the ULS strips are not verified, but the analysis and the other checks run (here the SLE
        // of the piles of the same wall), and the scope gives the request.
        var stripAndPiles = RcPileExample(); stripAndPiles[DesignKey] = false; stripAndPiles["rc_pile_check_kind"] = "Characteristic";
        stripAndPiles["check_kind"] = "Ultimate"; stripAndPiles["rc_sections"] = new JsonArray(EmbeddedWallDocument.NewRow("rc_sections"));
        var unconfirmedStrip = service.Calculate(stripAndPiles).NativeAs<EmbeddedWallReportData>();
        e.That(unconfirmedStrip.Complete && unconfirmedStrip.Checks.Count == 0 && unconfirmedStrip.Scope.Contains("Sezioni in c.a.: " + EmbeddedWallDocument.DesignHistoryRequest(unconfirmedStrip.CaseId))
            && unconfirmedStrip.RcPiles!.Count > 0 && unconfirmedStrip.RcPiles.All(r => r.Outcome == EngineeringOutcome.Satisfied),
            "A2 (B2): fascia c.a. SLU senza conferma verificata, oppure analisi e SLE dei pali fermate.");
        strip[DesignKey] = true;
        e.That(service.Calculate(strip).NativeAs<EmbeddedWallReportData>().Checks.Any(c => c.Outcome != EngineeringOutcome.NotEvaluated),
            "A2: fascia c.a. SLU confermata non verificata.");
        strip[DesignKey] = "sì";
        e.Throws<ArgumentException>(() => service.Calculate(strip), "A2: conferma non booleana accettata.");

        // The SLE combinations do not need the confirmation.
        var sle = RcPileExample(); sle[DesignKey] = false; sle["rc_pile_check_kind"] = "Characteristic";
        e.That(service.Calculate(sle).NativeAs<EmbeddedWallReportData>().RcPiles!.All(r => r.Outcome == EngineeringOutcome.Satisfied), "A2: SLE del palo bloccato dalla conferma SLU.");
        var stripSle = Example(); stripSle["check_kind"] = "Characteristic"; stripSle["rc_sections"] = new JsonArray(EmbeddedWallDocument.NewRow("rc_sections"));
        e.That(service.Calculate(stripSle).NativeAs<EmbeddedWallReportData>().Checks.Count > 0, "A2: SLE della fascia bloccato dalla conferma SLU.");

        // Every history has its own confirmation: a case without it never takes that of the main history.
        var multi = RcPileExample(); multi["case_id"] = "C1"; var second = AddHistory(multi, "C2"); second.Remove(DesignKey);
        var split = EmbeddedWallDocument.SplitHistories(multi);
        e.That(split[0][DesignKey]!.GetValue<bool>() && !split[1][DesignKey]!.GetValue<bool>(), "A2: il caso eredita la conferma della storia principale.");
        var both = service.Calculate(multi).NativeAs<EmbeddedWallReportData>();
        e.That(both.Cases![0].RcPiles!.Any(r => r.Outcome != EngineeringOutcome.NotEvaluated)
            && both.Cases[0].RcPiles!.All(r => r.Diagnostic != EmbeddedWallDocument.DesignHistoryRequest(r.CaseId))
            && both.Cases[1].RcPiles!.Count > 0 && both.Cases[1].RcPiles!.All(r => r.Outcome == EngineeringOutcome.NotEvaluated && r.Diagnostic == EmbeddedWallDocument.DesignHistoryRequest(r.CaseId)),
            "A2: conferme dei casi mescolate.");
        // B1 (review of 15/11): the message names the case without confirmation; a strip of that case does not stop the confirmed one.
        e.That(both.Cases[1].RcPiles![0].Diagnostic.StartsWith("Verifica SLU non eseguita nel caso «C2»", StringComparison.Ordinal),
            "A2 (B1): il messaggio non nomina il caso senza conferma.");
        var multiStrip = RcPileExample(); multiStrip["case_id"] = "C1"; multiStrip["check_kind"] = "Ultimate";
        var wideStrip = EmbeddedWallDocument.NewRow("rc_sections"); wideStrip["width"] = 1500; multiStrip["rc_sections"] = new JsonArray(wideStrip); var stripCase = AddHistory(multiStrip, "C2"); stripCase[DesignKey] = false;
        var stripCases = service.Calculate(multiStrip).NativeAs<EmbeddedWallReportData>();
        e.That(stripCases.Cases![0].Scope.All(s => !s.Contains("nel caso «C1»")) && stripCases.Cases[1].Scope.Contains("Sezioni in c.a.: " + EmbeddedWallDocument.DesignHistoryRequest("C2"))
            && stripCases.Cases[0].RcPiles!.Any(r => r.Outcome != EngineeringOutcome.NotEvaluated), "A2 (B1): la fascia del caso senza conferma ferma o confonde il caso confermato.");

        // The report: the warning under the actions of every history, with the state of the confirmation, and the field among the data.
        string confirmedXml = EmbeddedWallReport.Create("A2 confermata", service.Calculate(RcPileExample()).NativeAs<EmbeddedWallReportData>()).GetXml("word/document.xml").ToString();
        string requestXml = EmbeddedWallReport.Create("A2 da confermare", service.Calculate(piles).NativeAs<EmbeddedWallReportData>()).GetXml("word/document.xml").ToString();
        const string warning = "Avvertenza: per fasce in c.a., pali circolari, tubi CHS e sostegni metallici il servizio non aggiunge coefficienti parziali né alle azioni né agli effetti;";
        const string unconfirmed = "Storia delle fasi non confermata come di progetto: queste verifiche SLU non sono eseguite.";
        e.That(confirmedXml.Contains(warning) && confirmedXml.Contains("Storia delle fasi confermata come di progetto dall'utente.")
            && requestXml.Contains(warning) && requestXml.Contains(unconfirmed)
            && confirmedXml.Contains("Storia già di progetto per le verifiche SLU"), "A2: avvertenza o conferma assenti dalla relazione.");
        string multiXml = EmbeddedWallReport.Create("A2 casi", both).GetXml("word/document.xml").ToString();
        e.That(multiXml.Split(warning).Length - 1 == 2, "A2: l'avvertenza non è ripetuta per ogni storia.");
        // M2 (review of 15/11): exact per history. Tie rods, UPL/HYD and global stability have their own factors; without one of the four ULS
        // checks nothing is said to be missing, and a snapshot without the field reads as not confirmed.
        foreach (var (name, input) in new[] { ("tiranti", AnchorExample()), ("UPL/HYD", SafetyExample()), ("solo analisi", Example()) })
        {
            input[DesignKey] = false;
            string text = EmbeddedWallReport.PartialFactorsWarning(input);
            e.That(text.Contains("Nessuna di queste verifiche SLU è richiesta.") && !text.Contains("non confermata")
                && text.EndsWith("Sfilamento dei tiranti, UPL/HYD e stabilità globale hanno coefficienti e conferme propri, indicati nelle loro sezioni.", StringComparison.Ordinal),
                "A2 (M2): avvertenza non esatta senza verifiche SLU di fasce, pali, tubi o sostegni: " + name);
        }
        var noField = RcPileExample(); noField.Remove(DesignKey);
        e.That(EmbeddedWallReport.PartialFactorsWarning(noField).Contains(unconfirmed), "A2: un risultato salvato senza il campo risulta confermato.");
    }
}
