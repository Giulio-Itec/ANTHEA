using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Application.Documents;
using Anthea.Presentation.Projects;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Navigation;
using Anthea.Presentation.Shell.Projects;
using X.Core;

namespace X.Desktop;

internal static partial class ShellSmoke
{
    /// <summary>
    /// ui/shell-smoke-sharing: ProjectSharingSmokeChecks (MainWindow.SmokeSharing) with CheckCoverWarnings and CheckComparisonOrder on the new shell: the choice
    /// «all sheets» or «only this one» of the shared data, no repeated question, the concrete shared between materials and checks, mm/m both ways, incompatible
    /// geometries, reinforcement and its reset, loads unchanged, independent sections, inheritance of new sheets, the exposure of the menu, the minimum cover of
    /// Materiali on the status line of the sheets, in the overview, in the badge and in the comparison, «Uniforma a questo» in the comparison, persistence. The
    /// historic suite read the editor of the window; this one reads the tabs of the shell, which reload when their data change outside them (SH17), and closes
    /// them where MainWindow disposed its editor before writing the document by hand.
    /// </summary>
    private static async Task Sharing(ShellSmokeBench b)
    {
        string directory = b.Output;
        var shell = b.Shell; var workspace = b.Workspace;
        await CoverWarnings(b);
        ComparisonOrder(b);
        JsonObject Sheet(string module, string name) => J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module)));
        if (Environment.GetEnvironmentVariable("ANTHEA_SOIL_REGRESSION_FILE") is string regressionPath) await SoilRegression(b, regressionPath);
        if (Environment.GetEnvironmentVariable("ANTHEA_SOIL_MISSING_FILE") is string missingPath) await SoilMissing(b, missingPath);
        CloseTabs(b);
        var document = ProjectDocuments.CreateArchive();
        var project = J.Obj(("nome", "Progetto condiviso"), ("strutture", new JsonArray())); document.Array("progetti").Add(project);
        var section = ProjectDocuments.AddSection(project, "Pila P1"); var independent = ProjectDocuments.AddSection(project, "Pila P2");
        var material = Sheet("mat_calcestruzzo", "CLS di riferimento");
        var secondMaterial = Sheet("mat_calcestruzzo", "CLS seconda verifica");
        var rc = Sheet("str_palo", "Verifica sezione"); var rc2 = Sheet("str_palo", "Seconda verifica");
        var horizontal = Sheet(PaloOrizzontale.Module, "Palo orizzontale"); var vertical = Sheet("geo_palo_verticale", "Palo verticale");
        var outside = Sheet("mat_calcestruzzo", "Altro elemento"); independent.Array("fogli").Add(outside);
        foreach (var s in new[] { material, secondMaterial, rc, rc2, horizontal, vertical }) section.Array("fogli").Add(s);
        // Real creation flow: a horizontal pile precedes a default rectangular RC sheet.
        horizontal["dati"]!["generali"]!["diametro"] = "1.4";
        horizontal["dati"]!["sezione"]!["longitudinal_bar_count"] = "24";
        b.Check(ProjectSharedData.Differences(section).Any(d => d.Key == "shape"), "Forma incompatibile segnalata");
        ProjectSharedData.Apply(horizontal, section, new HashSet<string> { "Geometria", "Armatura" }, rc);
        b.Check(rc["dati"]!["input"].S("shape") == "Circolare" && rc["dati"]!["input"].D("diameter_mm") == 1400 && rc["dati"]!["input"].D("longitudinal_bar_count") == 24, "Il palo orizzontale definisce la sezione CA predefinita");
        var pileInput = (JsonObject)horizontal["dati"]!["sezione"]!.DeepClone();
        pileInput["shape"] = "Circolare"; pileInput["diameter_mm"] = horizontal["dati"]!["generali"].D("diametro") * 1000;
        var pileSection = new SezioneCA(pileInput);
        var verificationSection = new SezioneCA(rc["dati"]!["input"]!.AsObject());
        b.Check(Math.Abs(pileSection.AreaCls - verificationSection.AreaCls) < 1e-6 && pileSection.Bars.SequenceEqual(verificationSection.Bars), "Sezioni effettive uguali dopo la sincronizzazione");
        var inheritedRc = Sheet("str_palo", "Verifica ereditata");
        var isolated = J.Obj(("fogli", new JsonArray()));
        isolated.Array("fogli").Add(horizontal.DeepClone()); isolated.Array("fogli").Add(inheritedRc);
        ProjectSharedData.InheritHierarchy(inheritedRc, isolated);
        b.Check(inheritedRc["dati"]!["input"].S("shape") == "Circolare" && inheritedRc["dati"]!["input"].D("longitudinal_bar_count") == 24, "Nuova verifica CA eredita forma e armatura del palo");
        rc["dati"]!["input"]!["shape"] = "Circolare"; rc2["dati"]!["input"]!["shape"] = "Circolare";
        var actions = rc["dati"]!["combinazioni"]!.DeepClone(); var horizontalActions = horizontal["dati"]!["generali"]!["azione_orizzontale"]!.DeepClone();
        b.Services.Confirmations.SaveChanges = () => SaveChangesAnswer.Discard;
        workspace.OpenDocument(document, null, false);
        b.Services.Confirmations.SaveChanges = null;
        workspace.OpenSheet(material); await b.Idle();
        int questions = 0; b.Services.Confirmations.SharedUpdate = () => { questions++; return true; };
        var state = (JsonObject)b.Sheet.ViewModel.PreviewData.DeepClone(); state["classe"] = "C40/50"; b.Sheet.SetConcreteState(state);
        workspace.CommitActive();
        b.Check(questions == 1, "Conferma condivisa chiesta");
        b.Check(secondMaterial["dati"].S("classe") == "C40/50" && rc["dati"]!["input"].D("fck_mpa") == 40 && horizontal["dati"]!["sezione"].D("fck_mpa") == 40, "CLS condiviso tra moduli");
        b.Check(outside["dati"].S("classe", "C30/37") == "C30/37", "Altra sezione non aggiornata");
        workspace.CommitActive(); b.Check(questions == 1, "Nessuna conferma ripetuta senza modifiche");
        b.Services.Confirmations.SharedUpdate = () => { questions++; return false; };
        state = (JsonObject)b.Sheet.ViewModel.PreviewData.DeepClone(); state["classe"] = "C25/30"; b.Sheet.SetConcreteState(state); workspace.CommitActive();
        b.Check(rc["dati"]!["input"].D("fck_mpa") == 40 && material["dati"].S("classe") == "C25/30", "Scelta locale rispettata");
        b.Check(ProjectSharedData.Differences(section).Any(d => d.Group == "Materiali"), "Differenza locale segnalata");
        // MainWindow wrote the document by hand from here, with the editor of the sheet alive: the tabs close first (confirmed, nothing pending).
        CloseTabs(b);
        var groups = new HashSet<string> { "Materiali" }; ProjectSharedData.Apply(material, section, groups);
        b.Check(rc2["dati"]!["input"].D("fck_mpa") == 25, "Riallineamento CLS");
        rc["dati"]!["input"]!["diameter_mm"] = "1200"; rc["dati"]!["input"]!["longitudinal_bar_count"] = "20";
        rc["dati"]!["input"]!["transverse_spacing_mm"] = "150";
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Geometria", "Armatura" });
        b.Check(horizontal["dati"]!["generali"].D("diametro") == 1.2 && vertical["dati"]!["generali"].D("diametro") == 1.2, "Conversione mm/m");
        b.Check(rc2["dati"]!["input"].D("diameter_mm") == 1200 && horizontal["dati"]!["sezione"].D("longitudinal_bar_count") == 20, "Geometria e armatura condivise");
        b.Check(JsonNode.DeepEquals(actions, rc["dati"]!["combinazioni"]) && JsonNode.DeepEquals(horizontalActions, horizontal["dati"]!["generali"]!["azione_orizzontale"]), "Azioni invariate");
        vertical["dati"]!["generali"]!["diametro"] = "0,9";
        ProjectSharedData.Apply(vertical, section, new HashSet<string> { "Geometria" });
        b.Check(rc["dati"]!["input"].D("diameter_mm") == 900, "Aggiornamento inverso m/mm");
        var before = (JsonObject)rc.DeepClone(); rc["dati"]!["input"]!["diameter_mm"] = "900.0";
        b.Check(ProjectSharedData.ChangedGroups(before, rc).Count == 0, "Nessuna differenza spuria per il formato numerico");
        rc["dati"]!["input"]!["shape"] = "Rettangolare"; rc["dati"]!["input"]!["width_mm"] = "750";
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Geometria" });
        b.Check(rc2["dati"]!["input"].S("shape") == "Rettangolare" && horizontal["dati"]!["generali"].D("diametro") == .9, "Conversione geometrica incompatibile");
        var newSheet = Sheet("str_palo", "Nuova verifica"); section.Array("fogli").Add(newSheet);
        ProjectSharedData.InheritHierarchy(newSheet, section);
        b.Check(newSheet["dati"]!["input"].D("width_mm") == 600, "Conflitto tra forme non risolto senza scelta del riferimento");
        var manual = new JsonArray(J.Obj(("x", "0"), ("y", "10"), ("phi", "20")));
        rc["dati"]!["input"]!["barre_manuali"] = manual;
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Armatura" });
        b.Check(JsonNode.DeepEquals(rc2["dati"]!["input"]!["barre_manuali"], manual), "Barre manuali condivise");
        rc["dati"]!["input"]!.AsObject().Remove("barre_manuali"); ProjectSharedData.Apply(rc, section, new HashSet<string> { "Armatura" });
        b.Check(rc2["dati"]!["input"]!["barre_manuali"] is null, "Ripristino dell'armatura parametrica condiviso");
        // Regression: editing the cover must not overwrite an intentional local bar-count difference.
        rc["dati"]!["input"]!["shape"] = "Circolare";
        var coverBefore = (JsonObject)rc.DeepClone();
        rc["dati"]!["input"]!["cover_mm"] = "45";
        horizontal["dati"]!["sezione"]!["longitudinal_bar_count"] = "28";
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Armatura" }, horizontal, ProjectSharedData.ChangedKeys(coverBefore, rc));
        b.Check(horizontal["dati"]!["sezione"].D("cover_mm") == 45 && horizontal["dati"]!["sezione"].D("longitudinal_bar_count") == 28, "Il copriferro non sovrascrive l'armatura locale");
        state = (JsonObject)material["dati"]!.DeepClone(); state["esposizione_principale"] = "XC4";
        state["esposizioni"] = new JsonArray("XC4", "XS3");
        // A state with two exposures (previous version) opens with the exposure of the menu only (the sheet of the module as the shell opens it).
        using (var reopened = ModuleHostBench.SheetEditors().Open("mat_calcestruzzo", state)) state = (JsonObject)reopened.PreviewData.DeepClone();
        b.Check(state["esposizioni"]!.AsArray().Count == 1 && state["esposizioni"]![0]!.ToString() == "XC4", "Esposizione unica");
        material["dati"] = state.DeepClone();
        ProjectSharedData.Apply(material, section, new HashSet<string> { "Materiali" });
        b.Check(rc["dati"]!["workspace_ca"]!["sle_comuni"].S("esposizione") == "XC4" && SectionWorkspace.Sets.Skip(2).All(k => rc["dati"]!["workspace_ca"]!["sle"]![k].S("esposizione") == "XC4"), "Esposizione fino al motore SLE");
        b.Check(horizontal["dati"]!["sezione"].S("esposizione") == "XC4", "Esposizione nel palo");
        rc["dati"]!["workspace_ca"]!["sle_comuni"]!["esposizione"] = "XS1";
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Materiali" }, keys: new HashSet<string> { "esposizione" });
        b.Check(material["dati"].S("esposizione_principale") == "XS1" && material["dati"]!["esposizioni"]!.AsArray().Count == 1, "Esposizione inversa CA → Materiali");
        double required = Materiali.MaterialCover.Required(material["dati"]!.AsObject(), rc["dati"]!["input"].D("fck_mpa"), 16);
        rc["dati"]!["input"]!["cover_mm"] = required;
        b.Check(ProjectValidation.CoverChecks(section, rc).All(c => c.Passed == true), "Copriferro al minimo rispettato");
        rc["dati"]!["input"]!["cover_mm"] = required - 1;
        b.Check(ProjectValidation.CoverChecks(section, rc).All(c => c.Passed == false), "Copriferro insufficiente segnalato");
        material["dati"]!["numeri"]!["aggregate"] = "abc";
        b.Check(ProjectValidation.CoverChecks(section, rc).Any(c => c.Passed is null), "Input del copriferro non valido senza esito positivo");
        material["dati"]!["numeri"]!["aggregate"] = "20";
        var microV = Sheet("geo_micropalo_verticale", "Micropalo verticale"); var microH = Sheet(MicropaloOrizzontale.Module, "Micropalo orizzontale");
        var micros = new JsonObject { ["fogli"] = new JsonArray(microV, microH) };
        b.Check(!ProjectSharedData.Common(rc, microV).Any(p => p.Source.Key == "diameter_mm"), "Perforazione distinta dalla sezione CA");
        microV["dati"]!["generali"]!["diametro"] = "0,32"; microV["dati"]!["generali"]!["profilo_chs"] = "CHS 139.7 × 8";
        ProjectSharedData.Apply(microV, micros, new HashSet<string> { "Geometria", "Armatura" });
        b.Check(microH["dati"]!["generali"].D("diametro") == .32, "Diametro di perforazione condiviso");
        microH["dati"]!["sezione"]!["profilo_chs"] = Chs.Catalogo.Keys.First(k => k != "CHS 139.7 × 8");
        ProjectSharedData.Apply(microH, micros, new HashSet<string> { "Armatura" });
        b.Check(microV["dati"]!["generali"].S("profilo_chs") == microH["dati"]!["sezione"].S("profilo_chs"), "CHS inverso condiviso");
        vertical["dati"]!["generali"]!["presenza_falda"] = true; vertical["dati"]!["generali"]!["profondita_falda"] = "2.3";
        vertical["dati"]!["generali"]!["verticali_indagate"] = "4";
        ProjectSharedData.Apply(vertical, section, new HashSet<string> { "Terreno" });
        b.Check(horizontal["dati"]!["generali"].D("profondita_falda") == 2.3 && horizontal["dati"]!["verifica"].D("verticali_indagate") == 4, "Falda e indagini condivise");
        SoilLayers(b, section, vertical, horizontal, Sheet);
        rc["dati"]!["workspace_ca"]!["taglio"] = J.Obj(("rami_x", "4"));
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Armatura" }, rc2);
        b.Check(rc2["dati"]!["workspace_ca"]!["taglio"].D("rami_x") == 4, "Dettagli delle staffe condivisi");
        // The visible status on a fresh tab, with the saved inputs of Materiali.
        shell.Tree.Refresh();
        workspace.OpenSheet(rc); await b.Idle();
        b.Check(shell.SharedStatus.IsVisible && shell.SharedStatus.Text.Contains("NON RISPETTATO") && StatusShown(b), "Avviso copriferro visibile nella scheda");
        await b.Capture("copriferro.png");
        // A deliberate local difference stays visible in the last image.
        rc2["dati"]!["input"]!["width_mm"] = "800";
        b.Services.Confirmations.SharedUpdate = null;
        shell.Navigation.Select(ShellPage.Projects); shell.Tree.Refresh();
        await b.Capture("condivisione.png");
        bool compared = false;
        b.Services.DialogAsync = async d =>
        {
            var compare = (CoherenceViewModel)d;
            await b.CaptureDialog(compare, compare.Title, "confronto.png");
            var unrelated = section.Array("fogli").OfType<JsonObject>().Select(s =>
                (Sheet: s, Fields: ProjectSharedData.Fields(s).Where(p => p.Key != "cover_mm").ToDictionary(p => p.Key, p => p.Value.Value?.DeepClone()))).ToArray();
            var align = compare.Conflicts.SelectMany(c => c.Sheets).Single(s => ReferenceEquals(s.Sheet, rc) && s.Key == "cover_mm");
            align.AlignCommand.Execute(align);
            b.Check(!ProjectSharedData.Differences(section).Any(d => d.Key == "cover_mm"), "«Uniforma a questo» risolve il copriferro");
            b.Check(!compare.Conflicts.SelectMany(c => c.Sheets).Any(s => s.Key == "cover_mm"), "Conflitto risolto non più visibile");
            foreach (var saved in unrelated)
                foreach (var (key, value) in saved.Fields)
                    b.Check(ProjectSharedData.Equal(value, ProjectSharedData.Fields(saved.Sheet)[key].Value), "«Uniforma» non modifica un'altra proprietà: " + key);
            b.Check(workspace.SheetOf(rc)?.ViewModel.Data["input"].D("cover_mm") == rc["dati"]!["input"].D("cover_mm"), "Scheda aperta aggiornata dopo l'uniformazione");
            await b.CaptureDialog(compare, compare.Title, "confronto-dopo-uniforma.png",
                window => b.Check(ShellSmokeBench.Descendants<TextBlock>(window).Any(t => t.Text == "Avvisi"), "Avvisi presenti dopo l'uniformazione"));
            compared = true;
            return compare.Result;
        };
        try { await shell.CompareCommand.ExecuteAsync(section); }
        finally { b.Services.DialogAsync = null; }
        b.Check(compared, "Finestra del confronto aperta");
        string archive = Path.Combine(directory, "condivisione.programma");
        b.Services.SaveFile = _ => archive;
        b.Check(shell.Commands.Save(true), "Salvataggio della condivisione");
        b.Services.SaveFile = null;
        b.Check(ConcretePersistenceComparison.Equivalent(b.Project.Document, DocumentService.Open(archive)), "Salvataggio della condivisione riuscito");
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK (shell nuova): scelta tutti/locale, nessuna richiesta ripetuta, CLS tra materiali e verifiche, mm/m in entrambe le direzioni, geometrie incompatibili, armatura e reset, carichi invariati, sezioni indipendenti, ereditarietà nuovi fogli, confronto e persistenza.");
    }

    /// <summary>The status line of the shared data shown above the sheet (the view of SharedStatusViewModel), with its text.</summary>
    private static bool StatusShown(ShellSmokeBench b) =>
        ShellSmokeBench.Descendants<TextBlock>(b.Window).Any(t => t.IsVisible && t.Text == b.Shell.SharedStatus.Text);

    /// <summary>Closes every tab of the shell (each sheet confirmed, as when the user closes it).</summary>
    private static void CloseTabs(ShellSmokeBench b)
    {
        foreach (var open in b.Workspace.Sheets.ToArray()) b.Workspace.CloseSheet(open);
    }

    /// <summary>The comparison of the layers of the soil of the historic suite (code of the application, the same calls and assertions).</summary>
    private static void SoilLayers(ShellSmokeBench b, JsonObject section, JsonObject vertical, JsonObject horizontal, Func<string, string, JsonObject> Sheet)
    {
        var soil = Archivio.NuovoStratoPalo(); soil["spessore"] = "5"; soil["tipologia"] = "Granulare"; soil["angolo_attrito"] = "30";
        vertical["dati"]!["stratigrafie"] = new JsonArray(new JsonArray(soil));
        horizontal["dati"]!["stratigrafie"] = new JsonArray(new JsonArray());
        var unlinkedV = Sheet("geo_palo_verticale", "Terreno verticale");
        var unlinkedH = Sheet(PaloOrizzontale.Module, "Terreno orizzontale");
        var vLayer = (JsonObject)soil.DeepClone();
        var hLayer = (JsonObject)soil.DeepClone();
        vLayer["nc"] = "12"; vLayer["addensamento"] = "Denso"; vLayer["laterale_attiva"] = false;
        hLayer.Remove("nc"); hLayer.Remove("addensamento"); hLayer.Remove("laterale_attiva");
        hLayer["__kp"] = "3"; hLayer["angolo_attrito"] = "30,0";
        unlinkedV["dati"]!["stratigrafie"] = new JsonArray(new JsonArray(vLayer));
        unlinkedH["dati"]!["stratigrafie"] = new JsonArray(new JsonArray(hLayer));
        var soilPair = new JsonObject { ["fogli"] = new JsonArray(unlinkedV, unlinkedH) };
        vLayer["peso_specifico_saturo"] = ""; hLayer["peso_specifico_saturo"] = "0";
        unlinkedV["dati"]!["generali"]!["profondita_falda"] = "";
        unlinkedH["dati"]!["generali"]!["profondita_falda"] = "0";
        b.Check(!ProjectSharedData.Differences(soilPair).Any(d => d.Key == "profondita_falda"), "Quota della falda inattiva senza conflitto");
        unlinkedH["dati"]!["generali"]!["presenza_falda"] = true;
        b.Check(!ProjectSharedDataProbe.SameCommonSoilData(unlinkedV, unlinkedH), "Gamma saturo considerato con la falda attiva");
        unlinkedH["dati"]!["generali"]!["presenza_falda"] = false;
        b.Check(ProjectSharedDataProbe.SameCommonSoilData(unlinkedV, unlinkedH), "Campi specifici senza falso conflitto stratigrafico");
        b.Check(!ProjectSharedData.Limitations(soilPair).Any(s => s.Contains("strati")), "Dati comuni uguali non segnalati per la sola mancanza di collegamento");
        hLayer.Remove("angolo_attrito");
        b.Check(!ProjectSharedDataProbe.SameCommonSoilData(unlinkedV, unlinkedH), "Parametro comune mancante distinto da uno specifico");
        hLayer["angolo_attrito"] = "30";
        unlinkedV["dati"]!["stratigrafie"]![0]!.AsArray().Add(J.Obj(("spessore", "4"), ("tipologia", "Granulare"), ("angolo_attrito", "28"), ("nc", "12")));
        unlinkedH["dati"]!["stratigrafie"]![0]!.AsArray().Add(J.Obj(("spessore", "4"), ("tipologia", "Granulare"), ("angolo_attrito", "32")));
        unlinkedV["dati"]!["stratigrafie"]!.AsArray().Add(new JsonArray(J.Obj(("spessore", "3"), ("tipologia", ""))));
        unlinkedH["dati"]!["stratigrafie"]!.AsArray().Add(new JsonArray(J.Obj(("spessore", "3"), ("tipologia", "Granulare"))));
        string phiKey = "Strato · 1/2/angolo_attrito", typeKey = "Strato · 2/1/tipologia";
        var initialSoilDifferences = ProjectSharedData.Differences(soilPair);
        b.Check(initialSoilDifferences.Any(d => d.Key == phiKey) && initialSoilDifferences.Any(d => d.Key == typeKey), "Strati e sondaggi non collegati confrontati");
        ProjectSharedData.Apply(unlinkedH, soilPair, new HashSet<string> { "Terreno" }, keys: new HashSet<string> { phiKey });
        b.Check(!ProjectSharedData.Differences(soilPair).Any(d => d.Key == phiKey) && ProjectSharedData.Differences(soilPair).Any(d => d.Key == typeKey), "L'uniformazione non modifica un altro conflitto");
        b.Check(unlinkedV["dati"]!["stratigrafie"]![0]![1].D("nc") == 12, "Parametro esclusivo conservato nel secondo strato");
        ProjectSharedData.Apply(unlinkedH, soilPair, new HashSet<string> { "Terreno" }, keys: new HashSet<string> { typeKey });
        unlinkedH["dati"]!["stratigrafie"]![0]!.AsArray().Add(J.Obj(("spessore", "2"), ("tipologia", "Coesivo")));
        b.Check(ProjectSharedData.Limitations(soilPair).Any(s => s.Contains("strato 3 assente")), "Strato mancante segnalato");
        unlinkedH["dati"]!["stratigrafie"]![0]!.AsArray().RemoveAt(2);
        ProjectSharedData.LinkSoils(unlinkedV, unlinkedH);
        unlinkedH["dati"]!["stratigrafie"]![0]![0]!["peso_specifico_saturo"] = "0";
        b.Check(!ProjectSharedData.Differences(soilPair).Any(d => d.Key.StartsWith("Strato · ")), "Nessuna differenza spuria dopo il collegamento");
        unlinkedH["dati"]!["generali"]!["presenza_falda"] = true;
        b.Check(ProjectSharedData.Differences(soilPair).Any(d => d.Key.EndsWith("/peso_specifico_saturo")), "Gamma saturo collegato confrontato quando attivo");
        unlinkedH["dati"]!["generali"]!["presenza_falda"] = false;
        unlinkedH["dati"]!["stratigrafie"]![0]![0]!["angolo_attrito"] = "35";
        b.Check(ProjectSharedData.Differences(soilPair).Count(d => d.Key.StartsWith("Strato · ")) == 1, "Differenza reale del parametro comune isolata");
        ProjectSharedData.Apply(unlinkedH, soilPair, new HashSet<string> { "Terreno" });
        b.Check(unlinkedV["dati"]!["stratigrafie"]![0]![0].D("nc") == 12 && unlinkedH["dati"]!["stratigrafie"]![0]![0]!["nc"] is null, "Parametri specifici né copiati né persi");
        ProjectSharedData.LinkSoils(vertical, horizontal);
        vertical["dati"]!["stratigrafie"]![0]![0]!["nc"] = "12";
        var soilBefore = (JsonObject)horizontal.DeepClone();
        horizontal["dati"]!["stratigrafie"]![0]![0]!["angolo_attrito"] = "34";
        ProjectSharedData.Apply(horizontal, section, new HashSet<string> { "Terreno" }, keys: ProjectSharedData.ChangedKeys(soilBefore, horizontal));
        b.Check(vertical["dati"]!["stratigrafie"]![0]![0].D("angolo_attrito") == 34 && vertical["dati"]!["stratigrafie"]![0]![0].D("nc") == 12, "Terreni condivisi, parametri locali conservati");
    }

    /// <summary>
    /// CheckCoverWarnings on the new shell: the minimum cover of Materiali against the reinforcement and the exposure, on the status line of the RC section,
    /// of the pile and of the material (cnom), updated while the user types, in the overview, in the badge and in the comparison; conflicts of peers from both
    /// sheets; thresholds and incomplete input; through an empty parent group and after reopening.
    /// </summary>
    private static async Task CoverWarnings(ShellSmokeBench b)
    {
        string directory = b.Output;
        var shell = b.Shell; var workspace = b.Workspace;
        static JsonObject Sheet(string module, string name) => J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module)));
        var document = ProjectDocuments.CreateArchive();
        var project = J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", "Controllo copriferro"), ("fogli", new JsonArray()), ("strutture", new JsonArray()));
        document.Array("progetti").Add(project);
        var section = ProjectDocuments.AddSection(project, "Palo P1");
        var material = Sheet("mat_calcestruzzo", "CLS di riferimento");
        var rc = Sheet("str_palo", "Verifica CA"); var pile = Sheet(PaloOrizzontale.Module, "Palo orizzontale");
        foreach (var sheet in new[] { material, rc, pile }) section.Array("fogli").Add(sheet);
        material["dati"] = J.Obj(("versione_materiali", 1), ("classe", "C30/37"), ("esposizione_principale", "XC1"), ("numeri", J.Obj(("diameter", "40"), ("aggregate", "20"))));
        rc["dati"]!["input"]!["shape"] = "Circolare";
        rc["dati"]!["input"]!["longitudinal_bar_diameter_mm"] = "16";
        pile["dati"]!["sezione"]!["longitudinal_bar_diameter_mm"] = "16";
        ProjectSharedData.Apply(material, section, new HashSet<string> { "Materiali" });
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Materiali", "Geometria", "Armatura" }, pile);
        rc["dati"]!["input"]!["cover_mm"] = "35";
        pile["dati"]!["sezione"]!["cover_mm"] = "35";
        var before = document.DeepClone();
        var failures = new List<string>();
        if (ProjectValidation.CoverChecks(section, null).Any(c => c.Passed != false || !c.Text.Contains("NON RISPETTATO")))
            failures.Add("Copriferro 35 mm accettato nonostante i 50 mm mostrati in Materiali (barra di riferimento 40 mm, barre reali 16 mm)");
        rc["dati"]!["workspace_ca"]!["sle_comuni"]!["esposizione"] = "XC4";
        rc["dati"]!["input"]!["cover_mm"] = "15";
        if (ProjectValidation.CoverChecks(section, rc).Single() is not { Passed: false } conflict || !conflict.Text.Contains("NON RISPETTATO"))
            failures.Add("Il conflitto di esposizione nasconde il mancato rispetto del minimo di Materiali");
        rc["dati"]!["workspace_ca"]!["sle_comuni"]!["esposizione"] = "XC1";
        rc["dati"]!["input"]!["cover_mm"] = "35";
        b.Check(JsonNode.DeepEquals(before, document), "Il controllo non modifica i dati del progetto");
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
        rc["dati"]!["workspace_ca"]!["dettagli_costruttivi"] = J.Obj(("vita_durabilita", "100"));
        b.Check(ProjectValidation.CoverChecks(section, rc).Single() is { Passed: false } low && low.Text.Contains("vita utile"), "Il conflitto di durabilità non nasconde il minimo");
        rc["dati"]!["input"]!["cover_mm"] = "50";
        b.Check(ProjectValidation.CoverChecks(section, rc).Single().Passed is null, "Dati in conflitto senza esito positivo");
        rc["dati"]!["workspace_ca"]!.AsObject().Remove("dettagli_costruttivi");
        rc["dati"]!["input"]!["barre_manuali"] = new JsonArray();
        b.Check(ProjectValidation.CoverChecks(section, rc).Single().Passed is null, "Armatura incompleta senza esito positivo");
        rc["dati"]!["input"]!["cover_mm"] = "35";
        b.Check(ProjectValidation.CoverChecks(section, rc).Single().Passed == false, "Armatura incompleta: il minimo di Materiali resta");
        rc["dati"]!["input"]!.AsObject().Remove("barre_manuali");
        b.Services.Confirmations.SaveChanges = () => SaveChangesAnswer.Discard;
        workspace.OpenDocument(document, null, false);
        b.Services.Confirmations.SaveChanges = null;
        foreach (var sheet in new[] { rc, pile, material })
        {
            workspace.OpenSheet(sheet); await b.Idle();
            b.Check(shell.SharedStatus.IsVisible && shell.SharedStatus.Text.Contains("NON RISPETTATO") && shell.SharedStatus.Text.Contains("50 mm") && StatusShown(b),
                "Avviso di Materiali visibile in " + sheet.S("nome"));
            if (ReferenceEquals(sheet, material))
                b.Check(ShellSmokeBench.Descendants<TextBlock>(b.Sheet.View).Any(t => t.Text == "cnom = 50 mm"), "Il riferimento coincide con il valore mostrato in Materiali");
        }
        // Editing the material updates the warning at once, before the confirmation.
        b.Sheet.TypeConcreteNumber("diameter", "16"); await b.Idle();
        b.Check(!shell.SharedStatus.Text.Contains("NON RISPETTATO"), "Avviso aggiornato dopo la riduzione del minimo");
        b.Sheet.TypeConcreteNumber("diameter", "40"); await b.Idle();
        b.Check(shell.SharedStatus.Text.Contains("NON RISPETTATO"), "Modifica del minimo segnalata subito");
        workspace.CommitActive(); shell.Navigation.Select(ShellPage.Projects); shell.ShowOverview(section); await b.Idle();
        b.Check(ProjectValidation.Warnings(section).Count(s => s.Contains("NON RISPETTATO")) == 2 && shell.Overview!.Notices.Count(s => s.Contains("NON RISPETTATO")) == 2, "Avvisi nel riepilogo e nel report");
        b.Check(b.Node(section).Badge is { } badge && badge.Title.Contains("avvisi"), "Avvisi nel badge");
        b.Services.DialogAsync = async d =>
        {
            var coherence = (CoherenceViewModel)d;
            await b.CaptureDialog(coherence, coherence.Title, "copriferro-minimo-confronto.png",
                window => b.Check(ShellSmokeBench.Descendants<TextBlock>(window).Count(t => t.Text.Contains("NON RISPETTATO")) == 2, "Avvisi nel confronto"));
            return coherence.Result;
        };
        try { await shell.CompareCommand.ExecuteAsync(section); }
        finally { b.Services.DialogAsync = null; }
        // Peer conflicts visible from both sheets, independently of their order (the open tabs reload on the data changed outside them).
        b.Project.ChangeData(() => { rc["dati"]!["input"]!["cover_mm"] = "50"; return true; }, rc);
        foreach (var sheet in new[] { rc, pile })
        {
            workspace.OpenSheet(sheet); await b.Idle();
            b.Check(shell.SharedStatus.Text.Contains("Dati diversi tra i fogli") && shell.SharedStatus.Text.Contains(SharedDataCoordinator.FieldLabel("cover_mm")), "Copriferro diverso segnalato in " + sheet.S("nome"));
        }
        CloseTabs(b);
        pile["dati"]!["sezione"]!["cover_mm"] = "50";
        b.Check(ProjectValidation.CoverChecks(section, null).All(c => c.Passed == true), "Copriferro al limite rispettato");
        // Larger actual bars control when more demanding than the material reference.
        material["dati"]!["numeri"]!["diameter"] = "16";
        rc["dati"]!["input"]!["longitudinal_bar_diameter_mm"] = "40";
        rc["dati"]!["input"]!["cover_mm"] = "49";
        b.Check(ProjectValidation.CoverChecks(section, rc).Single().Passed == false, "Barre reali maggiori considerate");
        rc["dati"]!["input"]!["cover_mm"] = "50";
        b.Check(ProjectValidation.CoverChecks(section, rc).Single().Passed == true, "Barre reali: soglia esatta accettata");
        material["dati"]!["numeri"]!["aggregate"] = "abc";
        b.Check(ProjectValidation.CoverChecks(section, rc).Single().Passed is null, "Materiale incompleto non segnalato come rispettato");
        material["dati"]!["numeri"]!["aggregate"] = "20";
        // The same checks through empty parent groups, and after reopening.
        section.Array("fogli").Remove(material); project.Array("fogli").Add(material);
        rc["dati"]!["input"]!["cover_mm"] = "20";
        b.Shell.Tree.Refresh();
        workspace.OpenSheet(rc); await b.Idle();
        b.Check(shell.SharedStatus.Text.Contains("NON RISPETTATO"), "Materiale superiore considerato");
        workspace.CommitActive();
        var filename = Path.Combine(directory, "copriferro-minimo.programma");
        b.Services.SaveFile = _ => filename;
        b.Check(shell.Commands.Save(true), "Salvataggio del copriferro");
        b.Services.SaveFile = null;
        var reopened = DocumentService.Open(filename);
        b.Check(ProjectValidation.CoverChecks(reopened.Array("progetti")[0]!.AsObject(), null).Any(c => c.Passed == false), "Avviso conservato alla riapertura");
        await b.Capture("copriferro-minimo-scheda.png");
        File.WriteAllText(Path.Combine(directory, "copriferro-minimo.txt"), "OK (shell nuova): minimo Materiali, barre effettive, conflitto esposizione, soglia, input incompleti, avviso nei due fogli e in Materiali, modifiche live, badge, confronto, report, gerarchia, persistenza.");
        CloseTabs(b);
    }

    /// <summary>
    /// CheckComparisonOrder: the comparison and its warnings do not depend on the order of the sheets nor on the direction of the pairs (code of the
    /// application, the same calls); the badge of the section is the one of the tree of the shell (SharedDataCoordinator.Coherence, the title is its
    /// automation name), as MainWindow.AddCoherenceBadge.
    /// </summary>
    private static void ComparisonOrder(ShellSmokeBench b)
    {
        static JsonObject Sheet(string id, string module) => new() { ["id"] = id, ["nome"] = id, ["modulo_id"] = module, ["dati"] = Archivio.NuovoFoglio(module) };
        var material = Sheet("mat", "mat_calcestruzzo");
        var rc = Sheet("ca", "str_palo");
        var horizontal = Sheet("po", PaloOrizzontale.Module);
        var vertical = Sheet("pv", "geo_palo_verticale");
        var microV = Sheet("mv", "geo_micropalo_verticale");
        var microH = Sheet("mo", MicropaloOrizzontale.Module);
        vertical["dati"]!["stratigrafie"] = new JsonArray(new JsonArray(Archivio.NuovoStratoPalo()));
        horizontal["dati"]!["stratigrafie"] = new JsonArray(new JsonArray(PaloOrizzontale.Layer()));
        horizontal["dati"]!["generali"]!["diametro"] = "1.4";
        JsonObject[] all = [material, rc, horizontal, vertical, microV, microH, Sheet("steel", RebarMaterial.Module)];
        for (int i = 0; i < all.Length; i++) for (int j = i + 1; j < all.Length; j++)
        {
            var forward = ProjectSharedData.ComparableFields(all[i], all[j]).Select(p => p.Source.Key).Order().ToArray();
            var backward = ProjectSharedData.ComparableFields(all[j], all[i]).Select(p => p.Source.Key).Order().ToArray();
            b.Check(forward.SequenceEqual(backward), "Campi confrontati indipendenti dalla direzione");
        }
        b.Check(ProjectSharedData.ComparableFields(rc, material).Any(p => p.Source.Key == "esposizione"), "Esposizione mancante inclusa nel confronto");
        b.Check(!ProjectSharedData.Common(rc, material).Any(p => p.Source.Key == "esposizione"), "Il confronto non abilita un trasferimento non valido");
        b.Check(ProjectSharedData.ComparableFields(microV, microH).Any(p => p.Source.Key == "CHS · profilo_chs"), "Profilo CHS mancante incluso nel confronto");
        b.Check(!ProjectSharedData.Common(microV, microH).Any(p => p.Source.Key == "CHS · profilo_chs"), "Profilo vuoto non trasferibile al catalogo");
        var section = new JsonObject { ["nome"] = "Ordine indipendente", ["fogli"] = new JsonArray(material, rc, horizontal, vertical) };
        var sheets = section["fogli"]!.AsArray();
        string Signature() => string.Join("\n", ProjectSharedData.Differences(section).Select(d => $"{d.First.S("id")}|{d.Second.S("id")}|{d.Key}|{d.Left}|{d.Right}"));
        var expected = Signature();
        var expectedWarnings = ProjectSharedData.Limitations(section).ToArray();
        var expectedCover = ProjectValidation.CoverChecks(section, null).Select(c => c.Text).Order().ToArray();
        var savedInputs = all.ToDictionary(s => s.S("id"), s => s["dati"]!.DeepClone());
        int orders = 0;
        void Permute(JsonObject[] ordered, int start)
        {
            if (start < ordered.Length)
            {
                for (int i = start; i < ordered.Length; i++)
                { (ordered[start], ordered[i]) = (ordered[i], ordered[start]); Permute(ordered, start + 1); (ordered[start], ordered[i]) = (ordered[i], ordered[start]); }
                return;
            }
            sheets.Clear(); foreach (var sheet in ordered) sheets.Add(sheet);
            b.Check(Signature() == expected, "Conflitti uguali dopo il riordino dei fogli");
            b.Check(ProjectSharedData.Limitations(section).SequenceEqual(expectedWarnings), "Avvisi uguali dopo il riordino");
            b.Check(ProjectValidation.CoverChecks(section, null).Select(c => c.Text).Order().SequenceEqual(expectedCover), "Avvisi del copriferro uguali dopo il riordino");
            b.Check(SharedDataCoordinator.Coherence(section)!.Title.StartsWith("⚠ Differenze tra fogli", StringComparison.Ordinal), "Indicatore di conflitto indipendente dall'ordine");
            orders++;
        }
        Permute([material, rc, horizontal, vertical], 0);
        b.Check(orders == 24, "Permutazioni complete");
        foreach (var sheet in all) b.Check(JsonNode.DeepEquals(savedInputs[sheet.S("id")], sheet["dati"]), "Il confronto non altera gli input");
        var single = new JsonObject { ["fogli"] = new JsonArray(rc.DeepClone()) };
        b.Check(SharedDataCoordinator.Coherence(single)!.Title == "⚠ 1 avviso", "Avviso del copriferro per un foglio solo");
        var consistent = new JsonObject { ["fogli"] = new JsonArray(rc.DeepClone(), rc.DeepClone()) };
        b.Check(SharedDataCoordinator.Coherence(consistent)!.Title.Contains("Dati comuni coerenti · ⚠"), "Dati coerenti non nascondono gli avvisi");
        foreach (bool reverse in new[] { false, true })
        {
            var m = (JsonObject)material.DeepClone(); var c = (JsonObject)rc.DeepClone();
            var pair = new JsonObject { ["fogli"] = reverse ? new JsonArray(c, m) : new JsonArray(m, c) };
            var before = c["dati"]!["input"]!.DeepClone();
            ProjectSharedData.Apply(m, pair, new HashSet<string> { "Materiali" }, keys: new HashSet<string> { "esposizione" });
            b.Check(!ProjectSharedData.Differences(pair).Any(d => d.Key == "esposizione"), "Esposizione risolta dopo il riordino");
            b.Check(JsonNode.DeepEquals(before, c["dati"]!["input"]), "L'uniformazione dell'esposizione non altera altri input");
        }
        var coverMaterial = Sheet("cover-material", "mat_calcestruzzo");
        coverMaterial["dati"] = J.Obj(("classe", "C35/45"), ("esposizione_principale", "XC1"), ("numeri", J.Obj(("diameter", "16"), ("aggregate", "20"))));
        var coverSection = Sheet("cover-section", "str_palo");
        var coverData = coverSection["dati"]!.AsObject(); SectionWorkspace.Prepare(coverData);
        coverData["workspace_ca"]!["sle_comuni"]!["esposizione"] = "XC1";
        coverData["input"]!["barre_manuali"] = new JsonArray(J.Obj(("x", "-100"), ("y", "-200"), ("phi", "40")), J.Obj(("x", "100"), ("y", "200"), ("phi", "40")));
        double actualRequired = Materiali.MaterialCover.Required(coverMaterial["dati"]!.AsObject(), 35, 40);
        coverData["input"]!["cover_mm"] = actualRequired - 1;
        var coverProject = new JsonObject { ["fogli"] = new JsonArray(coverMaterial, coverSection) };
        b.Check(ProjectValidation.CoverChecks(coverProject, null).Single().Passed == false, "Copriferro controllato sulle barre reali");
        coverData["input"]!["cover_mm"] = actualRequired;
        b.Check(ProjectValidation.CoverChecks(coverProject, null).Single().Passed == true, "Copriferro effettivo al limite verificato");
        coverData["workspace_ca"]!["dettagli_costruttivi"] = J.Obj(("vita_durabilita", "100"));
        b.Check(ProjectValidation.CoverChecks(coverProject, null).Single().Passed is null && ProjectSharedData.Differences(coverProject).Any(d => d.Key == "Durabilità · vita utile [anni]"), "Vite utili differenti senza falso esito positivo");
    }

    /// <summary>ANTHEA_SOIL_REGRESSION_FILE (as the historic suite, only with the variable): a real file, the conflict of a soil type resolved in the comparison.</summary>
    private static async Task SoilRegression(ShellSmokeBench b, string path)
    {
        var regression = DocumentService.Open(path);
        var regressionSection = regression.Array("progetti")[0]!["strutture"]![0]!.AsObject();
        var geo = regressionSection.Array("fogli").OfType<JsonObject>().Where(ProjectSharedData.SupportsSharedSoils).ToArray();
        if (Environment.GetEnvironmentVariable("ANTHEA_SOIL_EXPECT_CONFLICT") is not string expectedKey)
        {
            b.Check(geo.Length == 2 && ProjectSharedDataProbe.SameCommonSoilData(geo[0], geo[1]), "Test 1: stratigrafie comuni uguali");
            b.Check(!ProjectSharedData.Limitations(regressionSection).Any(s => s.Contains("suddivisione degli strati")), "Test 1: nessun falso avviso stratigrafico");
            b.Check(!ProjectSharedData.Differences(regressionSection).Any(d => d.Key.StartsWith("Strato · ") || d.Key == "profondita_falda"), "Test 1: nessun conflitto stratigrafico");
            return;
        }
        b.Check(ProjectSharedData.Differences(regressionSection).Any(d => d.Key == expectedKey), "Conflitto del file reale rilevato");
        b.Workspace.OpenDocument(regression, path, false);
        var section = b.Project.Find(regressionSection.S("id"))!;
        b.Services.DialogAsync = async d =>
        {
            var coherence = (CoherenceViewModel)d;
            await b.CaptureDialog(coherence, coherence.Title, "file-reale-conflitto.png");
            var item = coherence.Conflicts.SelectMany(c => c.Sheets).Single(s => ReferenceEquals(s.Sheet, geo[1]) && s.Key == expectedKey);
            item.SoilType = "Coesivo";
            item.AlignCommand.Execute(item);
            b.Check(geo.All(s => s["dati"]!["stratigrafie"]![0]![0].S("tipologia") == "Coesivo"), "Scelta Coesivo applicata a entrambi i fogli");
            await b.CaptureDialog(coherence, coherence.Title, "file-reale-risolto.png");
            return coherence.Result;
        };
        try { await b.Shell.CompareCommand.ExecuteAsync(section); }
        finally { b.Services.DialogAsync = null; }
        b.Check(!ProjectSharedData.Differences(section).Any(d => d.Key == expectedKey), "«Uniforma» del file reale risolve il conflitto");
    }

    /// <summary>ANTHEA_SOIL_MISSING_FILE (as the historic suite, only with the variable): a real file, a layer missing copied with its soil type.</summary>
    private static async Task SoilMissing(ShellSmokeBench b, string path)
    {
        b.Services.Confirmations.SaveChanges = () => SaveChangesAnswer.Discard;
        b.Workspace.OpenDocument(DocumentService.Open(path), null, false);
        b.Services.Confirmations.SaveChanges = null;
        var actualSection = b.Project.Document.Array("progetti")[0]!["strutture"]![0]!.AsObject();
        var actualSheets = actualSection.Array("fogli").OfType<JsonObject>().Where(ProjectSharedData.SupportsSharedSoils).ToArray();
        b.Check(ProjectSharedData.MissingSoilLayers(actualSection).Count > 0, "Test 3: strato mancante rilevato");
        b.Workspace.OpenSheet(actualSheets[1]); b.Shell.Navigation.Select(ShellPage.Projects);
        b.Services.DialogAsync = async d =>
        {
            var coherence = (CoherenceViewModel)d;
            await b.CaptureDialog(coherence, coherence.Title, "test3-prima.png");
            var source = coherence.MissingLayers.SelectMany(m => m.Sources).First();
            source.SoilType = "Coesivo";
            source.CopyCommand.Execute(source);
            b.Check(actualSheets.All(s => s["dati"]!["stratigrafie"]![0]![0].S("tipologia") == "Coesivo"), "Test 3: tipologia scelta condivisa");
            b.Check(ProjectSharedData.MissingSoilLayers(actualSection).Count == 0, "Test 3: «Uniforma» aggiunge lo strato");
            b.Check(b.Workspace.SheetOf(actualSheets[1])?.ViewModel.Data["stratigrafie"]![0]!.AsArray().Count == 1, "Test 3: tabella aperta aggiornata");
            b.Check(actualSheets[1]["dati"]!["stratigrafie"]![0]![0]!["nc"] is null, "Test 3: nessun dato esclusivo del verticale copiato");
            await b.CaptureDialog(coherence, coherence.Title, "test3-dopo.png");
            return coherence.Result;
        };
        try { await b.Shell.CompareCommand.ExecuteAsync(actualSection); }
        finally { b.Services.DialogAsync = null; }
        string savedPath = Path.Combine(b.Output, "test3-risolto.programma");
        b.Services.SaveFile = _ => savedPath;
        b.Shell.Commands.Save(true);
        b.Services.SaveFile = null;
        var reopened = DocumentService.Open(savedPath).Array("progetti")[0]!["strutture"]![0]!.AsObject();
        b.Check(ProjectSharedData.MissingSoilLayers(reopened).Count == 0, "Test 3: strato conservato al salvataggio");
    }
}
