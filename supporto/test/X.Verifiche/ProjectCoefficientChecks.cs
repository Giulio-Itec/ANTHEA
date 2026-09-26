using System.Text.Json.Nodes;
using X.Core;

internal static class ProjectCoefficientChecks
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
        var archive = ProjectDocuments.CreateArchive(); var root = ProjectDocuments.AddProject(archive);
        var source = ProjectDocuments.AddSheet(root, "str_palo");
        var sourceData = source["dati"]!.AsObject(); var settings = SectionWorkspace.Prepare(sourceData);
        sourceData["input"]!["gamma_c"] = "1.75"; settings["coefficienti"]!["AlphaCT"] = ".9";
        var child = ProjectDocuments.AddSection(root); var target = ProjectDocuments.AddSheet(child, "str_palo");
        Check(target["dati"]!["input"].D("gamma_c") == 1.75, "γc non ereditato");
        Check(target["dati"]!["workspace_ca"]!["coefficienti"].D("GammaC") == 1.75, "Alias γc non sincronizzato");
        Check(target["dati"]!["workspace_ca"]!["coefficienti"].D("AlphaCT") == .9, "αct non ereditato");
        Check(ProjectSharedData.Fields(target)["gamma_c"].Group == "Coefficienti", "Gruppo coefficienti incoerente");
        Check(ProjectSharedData.Fields(target).Values.Count(f => f.Path == "input/gamma_c") == 1, "γc esposto due volte");
        var plan = new ProjectReportPlan(root);
        Check(plan.Common.Count(v => v.Field.Key == "gamma_c") == 1, "γc comune ripetuto nel report");
        Check(!plan.LocalInputs(target).Any(v => v.Label == CalculationCoefficients.Label("gamma_c")), "γc comune ripetuto nei dati locali");
        Check(ProjectReportPlan.Label("Normativa · str_palo") == "Normativa · Sezione in c.a.", "Etichetta normativa errata");
        Check(!plan.Fields[target].ContainsKey("CA · GammaSPrestress"), "Coefficiente trefoli inattivo nel report");
        target["dati"]!["input"]!["gamma_c"] = 1.6;
        Check(ProjectSharedData.Differences(root).Any(d => d.Key == "gamma_c" && d.Group == "Coefficienti"), "Conflitto γc non rilevato");
        ProjectSharedData.ApplyHierarchy(source, ["Coefficienti"]);
        Check(target["dati"]!["input"].D("gamma_c") == 1.75, "Propagazione γc fallita");
        var old = (JsonObject)source.DeepClone(); settings["normativa"] = "EN 1992-1-1";
        var keys = ProjectSharedData.ChangedKeys(old, source);
        Check(keys.Contains("gamma_c") && keys.Contains("CA · AlphaCT"), "Cambio normativa non espande i coefficienti");
        Check(!ProjectSharedData.ComparableFields(source, target).Any(p => p.Source.Key == "gamma_c"), "Confronto coefficienti di normative diverse");
        Check(ProjectSharedData.ApplyHierarchy(source, ["Coefficienti"]) == 0, "Coefficienti trasferiti senza trasferire la normativa");
        ProjectSharedData.ApplyHierarchy(source, ["Normativa", "Coefficienti"]);
        Check(target["dati"]!["workspace_ca"].S("normativa") == "EN 1992-1-1", "Normativa non propagata");
        Check(target["dati"]!["workspace_ca"]!["coefficienti"].D("AlphaCT") == .9, "Cambio normativa perde il coefficiente personalizzato");
        var inherited = ProjectDocuments.AddSheet(ProjectDocuments.AddSection(child), "str_palo");
        Check(inherited["dati"]!["workspace_ca"].S("normativa") == "EN 1992-1-1", "Nuovo foglio perde normativa");
        Check(inherited["dati"]!["input"].D("gamma_c") == 1.75, "Nuovo foglio EC2 perde γc");
        var pile = ProjectDocuments.AddSheet(child, PaloOrizzontale.Module);
        Check(pile["dati"]!["sezione"].D("gamma_c") == 1.5, "Palo NTC eredita γc EC2");
        var bridge = ProjectDocuments.AddSheet(root, BridgeSection.Module); bridge["dati"]!["gamma_m0"] = 1.2;
        var bridgeChild = ProjectDocuments.AddSheet(child, BridgeSection.Module);
        Check(bridgeChild["dati"].D("gamma_m0") == 1.2, "Ponte: γM0 non ereditato");
        Check(!ProjectSharedData.Fields(bridge).ContainsKey("CHS · gamma_m0"), "γM0 ponte confuso con CHS");
        bridgeChild["dati"]!["gamma_m0"] = "errato";
        Check(ProjectValidation.Warnings(root).Any(s => s.Contains("γM0") && s.Contains("positivo")), "Input invalido assente dagli avvisi del report");
        string before = archive.ToJsonString(); ProjectValidation.Warnings(root);
        Check(before == archive.ToJsonString(), "Controllo progetto modifica i dati");
        foreach (var m in ModuleCatalog.All) Check(m.Name == m.ProjectTitle && m.Name == m.Element && m.Description == m.ProjectSubtitle, "Nomi divergenti: " + m.Id);
        ProjectDocuments.Rename(child, "   Impalcato   "); Check(child.S("nome") == "Impalcato", "Rinomina non normalizzata");
        before = child.ToJsonString();
        try { ProjectDocuments.Rename(child, "  "); throw new Exception("Nome vuoto accettato"); } catch (ArgumentException) { count++; }
        Check(child.ToJsonString() == before, "Rinomina rifiutata modifica il nodo");
        return count;
    }
}
