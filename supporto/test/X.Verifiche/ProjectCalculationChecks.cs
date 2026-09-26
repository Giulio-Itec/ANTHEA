using System.Text.Json.Nodes;
using X.Core;

internal static class ProjectCalculationChecks
{
    public static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        int count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
        void Reject(Action action, string message)
        {
            try { action(); } catch (ArgumentException) { count++; return; }
            throw new Exception(message);
        }
        var document = ProjectDocuments.CreateArchive();
        var project = ProjectDocuments.AddProject(document);
        Check(project.Array("fogli").Count == 0 && project.Array("strutture").Count == 0, "Progetto nuovo incompleto");
        var section = ProjectDocuments.AddSection(project);
        var child = ProjectDocuments.AddSection(section);
        Check(ProjectDocuments.AddSection(project).S("nome") == "Sezione 2", "Nomi sezioni non univoci");
        foreach (var module in ModuleCatalog.All)
        {
            var single = Archivio.Documento(module.Id);
            var group = ProjectDocuments.AddSection(project, module.Id);
            var sheet = ProjectDocuments.AddSheet(group, module.Id);
            Check(JsonNode.DeepEquals(single["dati"], sheet["dati"]), "Default progetto e foglio diversi: " + module.Id);
            Archivio.Valida(single); Archivio.Valida(document);
            var another = ModuleCatalog.CreateData(module.Id);
            sheet["dati"]!["nota_test"] = "indipendente";
            Check(another["nota_test"] is null && single["dati"]!["nota_test"] is null, "Dati condivisi per riferimento: " + module.Id);
        }
        Check(ProjectRevisions.Nodes(document).Select(n => n.S("id")).Distinct().Count() == ProjectRevisions.Nodes(document).Count(), "ID duplicati");
        var before = document.ToJsonString();
        Reject(() => ProjectDocuments.AddSheet(section, "inesistente"), "Modulo inesistente accettato");
        Reject(() => Archivio.NuovoFoglio("inesistente"), "Modulo inesistente trasformato in palo");
        Reject(() => CalculationService.Calculate("inesistente", new JsonObject()), "Calcolo sconosciuto accettato");
        Reject(() => ProjectDocuments.AddSection(section, " "), "Nome vuoto accettato");
        Check(before == document.ToJsonString(), "Operazione rifiutata modifica il progetto");
        var invalid = ProjectDocuments.CreateArchive();
        var broken = ProjectDocuments.AddProject(invalid);
        broken.Array("fogli").Add(J.Obj(("nome", "Foglio non supportato"), ("modulo_id", "inesistente")));
        before = invalid.ToJsonString();
        Reject(() => ProjectDocuments.AddSheet(broken, "str_palo"), "Eredità da modulo sconosciuto accettata");
        Check(before == invalid.ToJsonString(), "Creazione fallita lascia un foglio inserito");

        var reference = ProjectDocuments.AddSheet(section, BridgeSection.Module);
        reference["dati"]!["b_cls"] = "4321"; reference["dati"]!["plate2"] = true;
        reference["dati"]!["b_bottom2"] = "456"; reference["dati"]!["rebars_top"] = false;
        reference["dati"]!["fasi"]![0]!["Mx"] = 9876;
        var bridge = ProjectDocuments.AddSheet(child, BridgeSection.Module);
        Check(bridge["dati"].D("b_cls") == 4321 && bridge["dati"].D("b_bottom2") == 456 && bridge["dati"].B("plate2"), "Ponte: geometria non ereditata");
        Check(!bridge["dati"].B("rebars_top", true), "Ponte: armatura nulla non ereditata");
        Check(bridge["dati"]!["fasi"]![0].D("Mx") == 1500, "Ponte: carichi ereditati");
        var unrelated = ProjectDocuments.AddSheet(project.Array("strutture")[1]!.AsObject(), BridgeSection.Module);
        Check(unrelated["dati"].D("b_cls") == 3000, "Ponte: condivisione tra rami indipendenti");
        bridge["dati"]!["b_cls"] = 4444;
        Check(ProjectSharedData.Differences(section).Any(d => d.Key == "Ponte · b_cls"), "Differenza ponte non rilevata");
        var plan = new ProjectReportPlan(section);
        Check(plan.LocalInputs(bridge).Any(i => i.Label.Contains("Larghezza soletta")), "Ponte assente dal confronto/report");
        var previousBridge = (JsonObject)reference.DeepClone();
        reference["dati"]!["plate2"] = false;
        var changedBridgeKeys = ProjectSharedData.ChangedKeys(previousBridge, reference);
        Check(changedBridgeKeys.Contains("Ponte · b_bottom2") && changedBridgeKeys.Contains("Ponte · t_bottom2"), "Ponte: cambio attivazione perde le dimensioni");
        var duplicate = ProjectRevisions.Duplicate(section);
        Check(!ProjectSharedData.SubtreeSheets(duplicate).Select(s => s.S("id")).Intersect(ProjectSharedData.SubtreeSheets(section).Select(s => s.S("id"))).Any(), "Duplica conserva gli ID");
        string path = Path.Combine(directory, "progetti.programma");
        Archivio.Scrivi(path, document); var restored = Archivio.Leggi(path);
        Check(JsonNode.DeepEquals(restored, document), "Salvataggio/riapertura cambia il progetto");

        var ca = Archivio.Documento("str_palo");
        ca["dati"]!["combinazioni"]!["SLU"] = "non un elenco";
        before = ca.ToJsonString();
        Reject(() => Archivio.Valida(ca), "Combinazioni malformate accettate");
        Check(ca.ToJsonString() == before, "Validazione distrugge le combinazioni malformate");
        ca["dati"]!["combinazioni"]!["SLU"] = new JsonArray(J.Obj(("azioni", new JsonArray())));
        Reject(() => Archivio.Valida(ca), "Azioni mancanti accettate");
        foreach (string key in new[] { "taglio", "sle", "coefficienti", "momento_curvatura", "dettagli_costruttivi", "ancoraggi" })
        {
            var malformed = SezioneCA.DefaultData(); malformed["workspace_ca"] = J.Obj((key, "dati malformati"));
            before = malformed.ToJsonString();
            Reject(() => SectionWorkspace.Prepare(malformed), "Contenitore CA non valido accettato: " + key);
            Check(before == malformed.ToJsonString(), "Migrazione parziale prima del rifiuto: " + key);
        }

        var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        var once = data.ToJsonString(); SectionWorkspace.Prepare(data);
        Check(once == data.ToJsonString(), "Preparazione c.a. non idempotente");
        Check(settings["taglio"] is JsonObject && settings["coefficienti"] is JsonObject, "Calcolo dipendente da una scheda WPF");
        input["gamma_c"] = 1.7; settings["coefficienti"]!["GammaC"] = 9; SectionWorkspace.Prepare(data);
        Check(settings["coefficienti"].D("GammaC") == 1.7, "Coefficiente duplicato prevale sulla sezione");
        input["gamma_c"] = 1.5; SectionWorkspace.Prepare(data);
        var shear = settings["taglio"]!.AsObject();
        ConcreteCalculationSettings.UpdateAutomaticShear(input, shear); shear["ancoraggio"] = "Confermato";
        shear["bw_x"] = "800,000"; ConcreteCalculationSettings.UpdateAutomaticShear(input, shear);
        Check(shear.S("ancoraggio") == "Confermato", "Formato numerico invalida l’ancoraggio");
        var action = J.Obj(("N", "-200"), ("Vx", "100"), ("Vy", "50"), ("T", "0"));
        var shearResult = ConcreteShearAnalysis.Calculate(input, settings, shear, action);
        Check(shearResult.Shear.Length == 2 && shearResult.Shear.All(r => r.VRd > 0) && shearResult.Torsion is null, "Taglio comune non calcolato");
        var plain = (JsonObject)shear.DeepClone(); plain["modello"] = "Senza staffe"; plain["ancoraggio"] = "Da verificare";
        Reject(() => ConcreteShearAnalysis.Calculate(input, settings, plain, action), "Ancoraggio senza conferma accettato");
        var torque = (JsonObject)action.DeepClone(); torque["T"] = 10;
        Reject(() => ConcreteShearAnalysis.Calculate(input, settings, shear, torque), "Torsione senza chiusura accettata");
        shear["chiusura_torsione"] = "Confermato"; shear["as_torsione"] = 1000;
        Check(ConcreteShearAnalysis.Calculate(input, settings, shear, torque).Torsion is not null, "Torsione comune non calcolata");
        var session = new ConcreteAnalysisSession();
        var options = settings["sle"]!["SLE"]!.AsObject();
        var rows = new[] { J.Obj(("id", "S1"), ("N", "-200"), ("Mx", "50"), ("My", "0")) };
        var stress = session.Stress(input, settings, options, "SLE", rows);
        Check(stress["S1"].State is not null, "Analisi SLE non visuale assente");
        options["esposizione"] = "XC4";
        var reused = session.Stress(input, settings, options, "SLE", rows);
        Check(ReferenceEquals(stress["S1"].State, reused["S1"].State), "Modifica della verifica ricalcola inutilmente le tensioni");
        rows[0]["N"] = "-400";
        var changed = session.Stress(input, settings, options, "SLE", rows);
        Check(!ReferenceEquals(stress["S1"].State, changed["S1"].State), "Cambio azioni usa tensioni obsolete");
        rows[0]["N"] = "errato";
        Check(session.Stress(input, settings, options, "SLE", rows)["S1"].State is null, "Azione non valida conserva uno stato");
        var canceled = new CancellationToken(true);
        try { session.Stress(input, settings, options, "SLE", rows, canceled); throw new Exception("Annullamento ignorato"); }
        catch (OperationCanceledException) { count++; }

        var exposure = new[] { Materiali.Durability.Exposures.Single(e => e.Code == "XF2") };
        Check(Materiali.NtcCover.Calculate(exposure, 30, new(50, false, false, false, 16, 20, 10, false, 0, 0), false, false).Cover.Nominal == 45, "Copriferro trasferito: riferimento 45 mm");
        Materiali.Durability.Check(); Materiali.NtcCover.Check(); count += 2;
        Check(Math.Abs(ConcreteBond.Strength(2, 20, .7, 1, 1.5) - 2.1) < 1e-12, "Aderenza: atteso 2,1 MPa");
        var anchor = new ConcreteAnchorageCalculator().Calculate(new(20, 300, 2, 1.5, false, 1000, false, 100, 0));
        Check(Math.Abs(anchor.Fbd - 2.1) < 1e-12, "Ancoraggio usa aderenza diversa");
        var materialData = ModuleCatalog.CreateData("mat_calcestruzzo");
        Check(CalculationService.Calculate("mat_calcestruzzo", materialData).D("copriferro_nominale_mm") > 0, "Scheda materiali non calcolabile senza WPF");
        Check(CalculationService.Calculate(RebarMaterial.Module, ModuleCatalog.CreateData(RebarMaterial.Module))["materiale"] is JsonObject, "Scheda acciaio non calcolabile senza WPF");
        settings["dettagli_costruttivi"]!["elemento"] = "Trave";
        settings["sle_comuni"]!["esposizione"] = "XC4";
        before = data.ToJsonString();
        var details = ConcreteDetailingAnalysis.Calculate(input, settings, new[] { J.Obj(("N", "-600")), J.Obj(("N", "150")) });
        Check(details.MaximumCompression == 600 && details.Durability is not null && details.Checks.Count > 0, "Dettagli costruttivi non visuali incoerenti");
        Check(before == data.ToJsonString(), "I dettagli modificano gli input");
        settings["ancoraggi"]!["lunghezza"] = "1500";
        var anchorage = ConcreteDetailingAnalysis.Anchorage(input, settings);
        Check(anchorage.Check.Fbd > 0 && anchorage.Check.Passed, "Ancoraggio comune non disponibile");
        var curve = settings["momento_curvatura"]!.AsObject(); curve["passi"] = "9";
        Reject(() => ConcreteCurvatureAnalysis.Calculate(input, settings, curve), "Curva: passi non validi accettati");
        curve["passi"] = "10"; curve["angoli"] = "16"; curve["frazione"] = ".5"; curve["raffina_snervamento"] = "0";
        before = data.ToJsonString();
        var curvature = ConcreteCurvatureAnalysis.Calculate(input, settings, curve);
        Check(curvature.Points.Count > 1 && curvature.Points.All(p => double.IsFinite(p.Curvature)), "Curva comune non visuale assente");
        Check(before == data.ToJsonString(), "Curva modifica input o impostazioni");
        var headless = SezioneCA.DefaultData();
        var headlessSettings = SectionWorkspace.Prepare(headless);
        foreach (string set in SectionWorkspace.Sets) headless["combinazioni"]![set] = new JsonArray();
        headless["combinazioni"]!["SLE"] = new JsonArray(J.Obj(("id", "SLE-unico"), ("azioni", new[] { "-200", "50", "0" })));
        headlessSettings["dominio3d"]!["angoli"] = "16";
        headlessSettings["dominio2d"]!["angoli"] = "16";
        before = headless.ToJsonString();
        var full = CalculationService.Calculate("str_palo", headless);
        Check(full.S("errore") == "" && full["tensioni"]!["SLE"]!["SLE-unico"]!["State"] is JsonObject, "Ingresso unico CA non calcolabile");
        Check(before == headless.ToJsonString(), "Ingresso unico CA modifica i dati");
        var direct = new ConcreteAnalysisSession().Stress(headless["input"]!.AsObject(), headlessSettings,
            headlessSettings["sle"]!["SLE"]!.AsObject(), "SLE", new[] { J.Obj(("id", "SLE-unico"), ("N", "-200"), ("Mx", "50"), ("My", "0")) });
        Check(JsonNode.DeepEquals(full["tensioni"]!["SLE"], ConcreteAnalysisSession.ExportStress(direct)), "CLI e servizio interattivo danno tensioni differenti");
        headless["combinazioni"]!["SLE"]![0]!["azioni"]![0] = "incompleto";
        var partial = CalculationService.Calculate("str_palo", headless);
        Check(partial.S("errore") != "" && partial["errori_calcolo"]!["SLE/SLE-unico"] is not null, "Errore parziale nascosto dal calcolo CA");
        count += ProjectCoefficientChecks.Run();
        File.WriteAllText(Path.Combine(directory, "controlli.txt"), $"{count} controlli su progetti e servizi comuni superati.");
        Console.WriteLine($"{count} controlli su progetti e servizi comuni superati.");
    }
}
