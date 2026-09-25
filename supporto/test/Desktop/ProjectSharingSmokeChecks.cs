using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeSharing(string directory)
    {
        testing = true; Directory.CreateDirectory(directory);
        CheckComparisonOrder();
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        JsonObject Sheet(string module, string name) => J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module)));
        if (Environment.GetEnvironmentVariable("ANTHEA_SOIL_REGRESSION_FILE") is string regressionPath)
        {
            var regression = Archivio.Leggi(regressionPath);
            var regressionSection = regression.Array("progetti")[0]!["strutture"]![0]!.AsObject();
            var geo = regressionSection.Array("fogli").OfType<JsonObject>().Where(ProjectSharedData.SupportsSharedSoils).ToArray();
            if (Environment.GetEnvironmentVariable("ANTHEA_SOIL_EXPECT_CONFLICT") is string expectedKey)
            {
                Check(ProjectSharedData.Differences(regressionSection).Any(d => d.Key == expectedKey), "Conflitto del file reale non rilevato");
                document = regression; ShowProjects();
                _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    var window = Application.Current.Windows.OfType<Window>().Single(w => w != this && w.Title.StartsWith("Confronto"));
                    window.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "file-reale-conflitto.png"), Ui.Snapshot(window));
                    var button = Ui.Descendants<System.Windows.Controls.Button>(window).Single(b => b.Tag is ValueTuple<JsonObject, string> t && ReferenceEquals(t.Item1, geo[1]) && t.Item2 == expectedKey);
                    var choice = Ui.Descendants<System.Windows.Controls.ComboBox>(window).Single(c => c.Tag is ValueTuple<JsonObject, string> t && ReferenceEquals(t.Item1, geo[1]) && t.Item2 == expectedKey);
                    choice.SelectedItem = "Coesivo";
                    button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    Check(geo.All(s => s["dati"]!["stratigrafie"]![0]![0].S("tipologia") == "Coesivo"), "Scelta Coesivo non applicata a entrambi i fogli");
                    ShowSheet(geo[0]); Commit();
                    Check(editor!.Data["stratigrafie"]![0]![0].S("tipologia") == "Coesivo", "Riapertura perde la tipologia Coesivo");
                    ProjectSharedData.SetSoilType(geo[1], expectedKey, "Granulare");
                    ProjectSharedData.Apply(geo[1], regressionSection, new HashSet<string> { "Terreno" }, keys: new HashSet<string> { expectedKey });
                    editor.Dispose(); editor = null; ShowSheet(geo[0]); Commit();
                    Check(geo.All(s => s["dati"]!["stratigrafie"]![0]![0].S("tipologia") == "Granulare"), "Ripristino Granulare non conservato");
                    Check(!ProjectSharedData.Differences(regressionSection).Any(d => d.Key == expectedKey), "Uniforma del file reale non risolve il conflitto");
                    window.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "file-reale-risolto.png"), Ui.Snapshot(window));
                    window.Close();
                }));
                ShowCoherence(regressionSection);
            }
            else
            {
            Check(geo.Length == 2 && ProjectSharedData.SameCommonSoilData(geo[0], geo[1]), "Test 1: stratigrafie comuni ancora differenti");
            Check(!ProjectSharedData.Limitations(regressionSection).Any(s => s.Contains("suddivisione degli strati")), "Test 1: falso avviso stratigrafico");
            Check(!ProjectSharedData.Differences(regressionSection).Any(d => d.Key.StartsWith("Strato · ") || d.Key == "profondita_falda"), "Test 1: conflitto stratigrafico inatteso");
            }
        }
        if (Environment.GetEnvironmentVariable("ANTHEA_SOIL_MISSING_FILE") is string missingPath)
        {
            editor?.Dispose(); editor = null; currentSheet = null;
            document = Archivio.Leggi(missingPath);
            var actualSection = document.Array("progetti")[0]!["strutture"]![0]!.AsObject();
            var actualSheets = actualSection.Array("fogli").OfType<JsonObject>().Where(ProjectSharedData.SupportsSharedSoils).ToArray();
            Check(ProjectSharedData.MissingSoilLayers(actualSection).Count > 0, "Test 3: strato mancante non rilevato");
            ShowSheet(actualSheets[1]); ShowProjects();
            Exception? missingFailure = null;
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var window = Application.Current.Windows.OfType<Window>().Single(w => w != this && w.Title.StartsWith("Confronto"));
                try
                {
                window.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "test3-prima.png"), Ui.Snapshot(window));
                var button = Ui.Descendants<System.Windows.Controls.Button>(window).Single(b => b.Tag is ValueTuple<JsonObject, string> t && t.Item2 == "Strato mancante · 1/1");
                var selectType = Ui.Descendants<System.Windows.Controls.ComboBox>(window).Single(c => c.Tag is ValueTuple<JsonObject, string> t && t.Item2 == "Strato · 1/1/tipologia");
                selectType.SelectedItem = "Coesivo";
                button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                Check(actualSheets.All(s => s["dati"]!["stratigrafie"]![0]![0].S("tipologia") == "Coesivo"), "Test 3: tipologia scelta non condivisa");
                Check(ProjectSharedData.MissingSoilLayers(actualSection).Count == 0, "Test 3: Uniforma non aggiunge lo strato");
                Check(editor!.Data["stratigrafie"]![0]!.AsArray().Count == 1, "Test 3: tabella aperta rimasta vuota");
                Check(actualSheets[1]["dati"]!["stratigrafie"]![0]![0]!["nc"] is null, "Test 3: copiato dato esclusivo verticale");
                Commit();
                window.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "test3-dopo.png"), Ui.Snapshot(window));
                }
                catch (Exception error) { missingFailure = error; }
                finally { window.Close(); }
            }));
            ShowCoherence(actualSection);
            if (missingFailure is not null) throw missingFailure;
            string savedPath = Path.Combine(directory, "test3-risolto.programma"); Archivio.Scrivi(savedPath, document);
            var reopened = Archivio.Leggi(savedPath).Array("progetti")[0]!["strutture"]![0]!.AsObject();
            Check(ProjectSharedData.MissingSoilLayers(reopened).Count == 0, "Test 3: strato perso al salvataggio");
        }
        editor?.Dispose(); editor = null; currentSheet = null;
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray()));
        var project = J.Obj(("nome", "Progetto condiviso"), ("strutture", new JsonArray())); document.Array("progetti").Add(project);
        var section = CreateProjectSection(project, "Pila P1"); var independent = CreateProjectSection(project, "Pila P2");
        var material = Sheet("mat_calcestruzzo", "CLS di riferimento");
        var secondMaterial = Sheet("mat_calcestruzzo", "CLS seconda verifica");
        var rc = Sheet("str_palo", "Verifica sezione"); var rc2 = Sheet("str_palo", "Seconda verifica");
        var horizontal = Sheet(PaloOrizzontale.Module, "Palo orizzontale"); var vertical = Sheet("geo_palo_verticale", "Palo verticale");
        var outside = Sheet("mat_calcestruzzo", "Altro elemento"); independent.Array("fogli").Add(outside);
        foreach (var s in new[] { material, secondMaterial, rc, rc2, horizontal, vertical }) section.Array("fogli").Add(s);
        // Real creation flow: a horizontal pile precedes a default rectangular RC sheet.
        horizontal["dati"]!["generali"]!["diametro"] = "1.4";
        horizontal["dati"]!["sezione"]!["longitudinal_bar_count"] = "24";
        Check(ProjectSharedData.Differences(section).Any(d => d.Key == "shape"), "Forma incompatibile non segnalata");
        ProjectSharedData.Apply(horizontal, section, new HashSet<string> { "Geometria", "Armatura" }, rc);
        Check(rc["dati"]!["input"].S("shape") == "Circolare" && rc["dati"]!["input"].D("diameter_mm") == 1400 &&
            rc["dati"]!["input"].D("longitudinal_bar_count") == 24, "Palo orizzontale non definisce la sezione CA predefinita");
        var pileInput = (JsonObject)horizontal["dati"]!["sezione"]!.DeepClone();
        pileInput["shape"] = "Circolare"; pileInput["diameter_mm"] = horizontal["dati"]!["generali"].D("diametro") * 1000;
        var pileSection = new SezioneCA(pileInput);
        var verificationSection = new SezioneCA(rc["dati"]!["input"]!.AsObject());
        Check(Math.Abs(pileSection.AreaCls - verificationSection.AreaCls) < 1e-6 &&
            pileSection.Bars.SequenceEqual(verificationSection.Bars), "Sezioni effettive diverse dopo sincronizzazione");
        var inheritedRc = Sheet("str_palo", "Verifica ereditata");
        var isolated = J.Obj(("fogli", new JsonArray()));
        isolated.Array("fogli").Add(horizontal.DeepClone()); isolated.Array("fogli").Add(inheritedRc);
        InheritSectionData(inheritedRc, isolated);
        Check(inheritedRc["dati"]!["input"].S("shape") == "Circolare" && inheritedRc["dati"]!["input"].D("longitudinal_bar_count") == 24,
            "Nuova verifica CA non eredita forma e armatura del palo");
        rc["dati"]!["input"]!["shape"] = "Circolare"; rc2["dati"]!["input"]!["shape"] = "Circolare";
        var actions = rc["dati"]!["combinazioni"]!.DeepClone(); var horizontalActions = horizontal["dati"]!["generali"]!["azione_orizzontale"]!.DeepClone();
        ShowProjects(); ShowSheet(material);
        int questions = 0; sharedChoiceForTest = () => { questions++; return true; };
        var state = editor!.materials!.CaptureState(); state["classe"] = "C40/50"; editor.materials.RestoreState(state);
        Commit();
        Check(questions == 1, "Conferma condivisa assente");
        Check(secondMaterial["dati"].S("classe") == "C40/50" && rc["dati"]!["input"].D("fck_mpa") == 40 && horizontal["dati"]!["sezione"].D("fck_mpa") == 40, "CLS non condiviso tra moduli");
        Check(outside["dati"].S("classe", "C30/37") == "C30/37", "Aggiornata altra sezione");
        Commit(); Check(questions == 1, "Conferma ripetuta senza modifiche");
        sharedChoiceForTest = () => { questions++; return false; };
        state = editor.materials.CaptureState(); state["classe"] = "C25/30"; editor.materials.RestoreState(state); Commit();
        Check(rc["dati"]!["input"].D("fck_mpa") == 40 && material["dati"].S("classe") == "C25/30", "Scelta locale non rispettata");
        Check(ProjectSharedData.Differences(section).Any(d => d.Group == "Materiali"), "Differenza locale non segnalata");
        var groups = new HashSet<string> { "Materiali" }; ProjectSharedData.Apply(material, section, groups);
        Check(rc2["dati"]!["input"].D("fck_mpa") == 25, "Riallineamento CLS");
        rc["dati"]!["input"]!["diameter_mm"] = "1200"; rc["dati"]!["input"]!["longitudinal_bar_count"] = "20";
        rc["dati"]!["input"]!["transverse_spacing_mm"] = "150";
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Geometria", "Armatura" });
        Check(horizontal["dati"]!["generali"].D("diametro") == 1.2 && vertical["dati"]!["generali"].D("diametro") == 1.2, "Conversione mm/m errata");
        Check(rc2["dati"]!["input"].D("diameter_mm") == 1200 && horizontal["dati"]!["sezione"].D("longitudinal_bar_count") == 20, "Geometria o armatura non condivisa");
        Check(JsonNode.DeepEquals(actions, rc["dati"]!["combinazioni"]) && JsonNode.DeepEquals(horizontalActions, horizontal["dati"]!["generali"]!["azione_orizzontale"]), "Azioni alterate");
        vertical["dati"]!["generali"]!["diametro"] = "0,9";
        ProjectSharedData.Apply(vertical, section, new HashSet<string> { "Geometria" });
        Check(rc["dati"]!["input"].D("diameter_mm") == 900, "Aggiornamento inverso m/mm");
        var before = (JsonObject)rc.DeepClone(); rc["dati"]!["input"]!["diameter_mm"] = "900.0";
        Check(ProjectSharedData.ChangedGroups(before, rc).Count == 0, "Differenze spurie per formato numerico");
        rc["dati"]!["input"]!["shape"] = "Rettangolare"; rc["dati"]!["input"]!["width_mm"] = "750";
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Geometria" });
        Check(rc2["dati"]!["input"].S("shape") == "Rettangolare" && horizontal["dati"]!["generali"].D("diametro") == .9, "Conversione geometrica incompatibile");
        var newSheet = Sheet("str_palo", "Nuova verifica"); section.Array("fogli").Add(newSheet);
        InheritSectionData(newSheet, section);
        Check(newSheet["dati"]!["input"].D("width_mm") == 600, "Conflitto tra forme risolto senza scelta del riferimento");
        var manual = new JsonArray(J.Obj(("x", "0"), ("y", "10"), ("phi", "20")));
        rc["dati"]!["input"]!["barre_manuali"] = manual;
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Armatura" });
        Check(JsonNode.DeepEquals(rc2["dati"]!["input"]!["barre_manuali"], manual), "Barre manuali non condivise");
        rc["dati"]!["input"]!.AsObject().Remove("barre_manuali"); ProjectSharedData.Apply(rc, section, new HashSet<string> { "Armatura" });
        Check(rc2["dati"]!["input"]!["barre_manuali"] is null, "Ripristino armatura parametrica non condiviso");
        // Regression: editing cover must not overwrite an intentional local bar-count difference.
        rc["dati"]!["input"]!["shape"] = "Circolare";
        var coverBefore = (JsonObject)rc.DeepClone();
        rc["dati"]!["input"]!["cover_mm"] = "45";
        horizontal["dati"]!["sezione"]!["longitudinal_bar_count"] = "28";
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Armatura" }, horizontal, ProjectSharedData.ChangedKeys(coverBefore, rc));
        Check(horizontal["dati"]!["sezione"].D("cover_mm") == 45 && horizontal["dati"]!["sezione"].D("longitudinal_bar_count") == 28, "Copriferro sovrascrive armatura locale");
        state = editor!.materials!.CaptureState(); state["esposizione_principale"] = "XC4";
        state["esposizioni"] = new JsonArray("XC4", "XS3"); editor.materials.RestoreState(state);
        state = editor.materials.CaptureState();
        Check(state["esposizioni"]!.AsArray().Count == 1 && state["esposizioni"]![0]!.ToString() == "XC4", "Esposizione non unica");
        material["dati"] = state.DeepClone();
        ProjectSharedData.Apply(material, section, new HashSet<string> { "Materiali" });
        Check(rc["dati"]!["workspace_ca"]!["sle_comuni"].S("esposizione") == "XC4" && SectionWorkspace.Sets.Skip(2).All(k => rc["dati"]!["workspace_ca"]!["sle"]![k].S("esposizione") == "XC4"), "Esposizione non arriva al motore SLE");
        Check(horizontal["dati"]!["sezione"].S("esposizione") == "XC4", "Esposizione assente nel palo");
        rc["dati"]!["workspace_ca"]!["sle_comuni"]!["esposizione"] = "XS1";
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Materiali" }, keys: new HashSet<string> { "esposizione" });
        Check(material["dati"].S("esposizione_principale") == "XS1" && material["dati"]!["esposizioni"]!.AsArray().Count == 1, "Esposizione inversa CA Materiali");
        double required = Materiali.MaterialCover.Required(material["dati"]!.AsObject(), rc["dati"]!["input"].D("fck_mpa"), 16);
        rc["dati"]!["input"]!["cover_mm"] = required;
        Check(CoverChecks(section, rc).All(c => c.Passed == true), "Copriferro al minimo deve passare");
        rc["dati"]!["input"]!["cover_mm"] = required - 1;
        Check(CoverChecks(section, rc).All(c => c.Passed == false), "Copriferro insufficiente non segnalato");
        material["dati"]!["numeri"]!["aggregate"] = "abc";
        Check(CoverChecks(section, rc).Any(c => c.Passed is null), "Input copriferro invalido produce un esito positivo");
        material["dati"]!["numeri"]!["aggregate"] = "20";
        var microV = Sheet("geo_micropalo_verticale", "Micropalo verticale"); var microH = Sheet(MicropaloOrizzontale.Module, "Micropalo orizzontale");
        var micros = new JsonObject { ["fogli"] = new JsonArray(microV, microH) };
        Check(!ProjectSharedData.Common(rc, microV).Any(p => p.Source.Key == "diameter_mm"), "Perforazione confusa con sezione CA");
        microV["dati"]!["generali"]!["diametro"] = "0,32"; microV["dati"]!["generali"]!["profilo_chs"] = "CHS 139.7 × 8";
        ProjectSharedData.Apply(microV, micros, new HashSet<string> { "Geometria", "Armatura" });
        Check(microH["dati"]!["generali"].D("diametro") == .32, "Diametro perforazione non condiviso: " + microH["dati"]!["generali"]!["diametro"] + " / " + ProjectSharedData.Fields(microV)["perforazione_mm"].Value);
        microH["dati"]!["sezione"]!["profilo_chs"] = Chs.Catalogo.Keys.First(k => k != "CHS 139.7 × 8");
        ProjectSharedData.Apply(microH, micros, new HashSet<string> { "Armatura" });
        Check(microV["dati"]!["generali"].S("profilo_chs") == microH["dati"]!["sezione"].S("profilo_chs"), "CHS inverso non condiviso");
        vertical["dati"]!["generali"]!["presenza_falda"] = true; vertical["dati"]!["generali"]!["profondita_falda"] = "2.3";
        vertical["dati"]!["generali"]!["verticali_indagate"] = "4";
        ProjectSharedData.Apply(vertical, section, new HashSet<string> { "Terreno" });
        Check(horizontal["dati"]!["generali"].D("profondita_falda") == 2.3 && horizontal["dati"]!["verifica"].D("verticali_indagate") == 4, "Falda o indagini non condivise");
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
        // Test 1.programma: identical dry strata, gamma-sat blank vertically and zero horizontally.
        vLayer["peso_specifico_saturo"] = ""; hLayer["peso_specifico_saturo"] = "0";
        unlinkedV["dati"]!["generali"]!["profondita_falda"] = "";
        unlinkedH["dati"]!["generali"]!["profondita_falda"] = "0";
        Check(!ProjectSharedData.Differences(soilPair).Any(d => d.Key == "profondita_falda"), "Quota falda inattiva genera conflitto");
        unlinkedH["dati"]!["generali"]!["presenza_falda"] = true;
        Check(!ProjectSharedData.SameCommonSoilData(unlinkedV, unlinkedH), "Gamma saturo ignorato con falda attiva");
        unlinkedH["dati"]!["generali"]!["presenza_falda"] = false;
        Check(ProjectSharedData.SameCommonSoilData(unlinkedV, unlinkedH), "Campi specifici generano un falso conflitto stratigrafico");
        Check(!ProjectSharedData.Limitations(soilPair).Any(s => s.Contains("strati")), "Dati comuni uguali segnalati solo per mancanza di collegamento");
        hLayer.Remove("angolo_attrito");
        Check(!ProjectSharedData.SameCommonSoilData(unlinkedV, unlinkedH), "Parametro comune mancante confuso con parametro specifico");
        hLayer["angolo_attrito"] = "30";
        // Compare and resolve every numbered layer, without shared IDs or a prior link.
        unlinkedV["dati"]!["stratigrafie"]![0]!.AsArray().Add(J.Obj(("spessore", "4"), ("tipologia", "Granulare"), ("angolo_attrito", "28"), ("nc", "12")));
        unlinkedH["dati"]!["stratigrafie"]![0]!.AsArray().Add(J.Obj(("spessore", "4"), ("tipologia", "Granulare"), ("angolo_attrito", "32")));
        unlinkedV["dati"]!["stratigrafie"]!.AsArray().Add(new JsonArray(J.Obj(("spessore", "3"), ("tipologia", ""))));
        unlinkedH["dati"]!["stratigrafie"]!.AsArray().Add(new JsonArray(J.Obj(("spessore", "3"), ("tipologia", "Granulare"))));
        string phiKey = "Strato · 1/2/angolo_attrito", typeKey = "Strato · 2/1/tipologia";
        var initialSoilDifferences = ProjectSharedData.Differences(soilPair);
        Check(initialSoilDifferences.Any(d => d.Key == phiKey) && initialSoilDifferences.Any(d => d.Key == typeKey), "Strati o sondaggi non collegati non confrontati");
        ProjectSharedData.Apply(unlinkedH, soilPair, new HashSet<string> { "Terreno" }, keys: new HashSet<string> { phiKey });
        Check(!ProjectSharedData.Differences(soilPair).Any(d => d.Key == phiKey) && ProjectSharedData.Differences(soilPair).Any(d => d.Key == typeKey), "Uniformazione modifica un altro conflitto");
        Check(unlinkedV["dati"]!["stratigrafie"]![0]![1].D("nc") == 12, "Parametro esclusivo perso nel secondo strato");
        ProjectSharedData.Apply(unlinkedH, soilPair, new HashSet<string> { "Terreno" }, keys: new HashSet<string> { typeKey });
        unlinkedH["dati"]!["stratigrafie"]![0]!.AsArray().Add(J.Obj(("spessore", "2"), ("tipologia", "Coesivo")));
        Check(ProjectSharedData.Limitations(soilPair).Any(s => s.Contains("strato 3 assente")), "Strato mancante non segnalato");
        unlinkedH["dati"]!["stratigrafie"]![0]!.AsArray().RemoveAt(2);
        ProjectSharedData.LinkSoils(unlinkedV, unlinkedH);
        unlinkedH["dati"]!["stratigrafie"]![0]![0]!["peso_specifico_saturo"] = "0";
        Check(!ProjectSharedData.Differences(soilPair).Any(d => d.Key.StartsWith("Strato · ")), "Differenze spurie dopo collegamento");
        unlinkedH["dati"]!["generali"]!["presenza_falda"] = true;
        Check(ProjectSharedData.Differences(soilPair).Any(d => d.Key.EndsWith("/peso_specifico_saturo")), "Gamma saturo collegato non confrontato quando attivo");
        unlinkedH["dati"]!["generali"]!["presenza_falda"] = false;
        unlinkedH["dati"]!["stratigrafie"]![0]![0]!["angolo_attrito"] = "35";
        Check(ProjectSharedData.Differences(soilPair).Count(d => d.Key.StartsWith("Strato · ")) == 1, "Differenza reale del parametro comune non isolata");
        ProjectSharedData.Apply(unlinkedH, soilPair, new HashSet<string> { "Terreno" });
        Check(unlinkedV["dati"]!["stratigrafie"]![0]![0].D("nc") == 12 && unlinkedH["dati"]!["stratigrafie"]![0]![0]!["nc"] is null, "Parametri specifici copiati o persi");
        ProjectSharedData.LinkSoils(vertical, horizontal);
        vertical["dati"]!["stratigrafie"]![0]![0]!["nc"] = "12";
        var soilBefore = (JsonObject)horizontal.DeepClone();
        horizontal["dati"]!["stratigrafie"]![0]![0]!["angolo_attrito"] = "34";
        ProjectSharedData.Apply(horizontal, section, new HashSet<string> { "Terreno" }, keys: ProjectSharedData.ChangedKeys(soilBefore, horizontal));
        Check(vertical["dati"]!["stratigrafie"]![0]![0].D("angolo_attrito") == 34 && vertical["dati"]!["stratigrafie"]![0]![0].D("nc") == 12, "Terreni non condivisi o parametri locali persi");
        rc["dati"]!["workspace_ca"]!["taglio"] = J.Obj(("rami_x", "4"));
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Armatura" }, rc2);
        Check(rc2["dati"]!["workspace_ca"]!["taglio"].D("rami_x") == 4, "Dettagli staffe non condivisi");
        // Check the visible status using a fresh editor, including the saved Materiali inputs.
        editor?.Dispose(); editor = null; currentSheet = null;
        ShowSheet(rc);
        Check(sharedStatus.Visibility == Visibility.Visible && sharedStatus.Text.Contains("NON RISPETTATO"), "Avviso copriferro non visibile nella scheda");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "copriferro.png"), Ui.Snapshot(this));
        // Keep a deliberate local difference visible in the final screenshot.
        rc2["dati"]!["input"]!["width_mm"] = "800";
        sharedChoiceForTest = null; ShowProjects();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "condivisione.png"), Ui.Snapshot(this));
        bool compared = false;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var compare = Application.Current.Windows.OfType<Window>().Single(w => w != this && w.Title.StartsWith("Confronto"));
            compare.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "confronto.png"), Ui.Snapshot(compare));
            var unrelated = section.Array("fogli").OfType<JsonObject>().Select(s =>
                (Sheet: s, Fields: ProjectSharedData.Fields(s).Where(p => p.Key != "cover_mm").ToDictionary(p => p.Key, p => p.Value.Value?.DeepClone()))).ToArray();
            var align = Ui.Descendants<System.Windows.Controls.Button>(compare).Single(b =>
                b.Tag is ValueTuple<JsonObject, string> t && ReferenceEquals(t.Item1, rc) && t.Item2 == "cover_mm");
            align.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(!ProjectSharedData.Differences(section).Any(d => d.Key == "cover_mm"), "Uniforma a questo non risolve il copriferro");
            Check(!Ui.Descendants<System.Windows.Controls.Button>(compare).Any(b => b.Tag is ValueTuple<JsonObject, string> t && t.Item2 == "cover_mm"), "Conflitto risolto ancora visibile");
            foreach (var saved in unrelated)
                foreach (var (key, value) in saved.Fields)
                    Check(ProjectSharedData.Equal(value, ProjectSharedData.Fields(saved.Sheet)[key].Value), "Uniforma modifica un'altra proprietà: " + key);
            Check(editor!.Data["input"].D("cover_mm") == rc["dati"]!["input"].D("cover_mm"), "Editor non aggiornato dopo uniformazione");
            Check(Ui.Descendants<System.Windows.Controls.TextBlock>(compare).Any(t => t.Text == "Avvisi"), "Avvisi scomparsi dopo uniformazione");
            compare.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "confronto-dopo-uniforma.png"), Ui.Snapshot(compare));
            compared = true; compare.Close();
        }));
        ShowCoherence(section);
        Check(compared, "Finestra confronto non aperta");
        string archive = Path.Combine(directory, "condivisione.programma"); Archivio.Scrivi(archive, document);
        Check(JsonNode.DeepEquals(document, Archivio.Leggi(archive)), "Salvataggio condivisione non riuscito");
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK: scelta tutti/locale, nessuna richiesta ripetuta, CLS tra materiali e verifiche, mm/m in entrambe le direzioni, geometrie incompatibili, armatura e reset, carichi invariati, sezioni indipendenti, ereditarietà nuovi fogli, confronto e persistenza.");
    }
}
