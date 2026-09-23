using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using X.Core;

namespace X.Desktop;
public sealed partial class MainWindow
{
    internal async Task SmokeConcreteFeatures(string directory)
    {
        testing = true; Directory.CreateDirectory(directory);
        document = Archivio.Documento("str_palo"); currentSheet = null; ShowSheet(document);
        await editor!.CalculateAsync();
        await editor.VerifyConcreteFeatures(directory);
        dirty = false;
    }
}
internal sealed partial class SheetEditor
{
    internal Task VerifyConcreteFeatures(string directory) => concrete!.VerifyFeatures(directory);
}
internal sealed partial class ConcreteWorkspace
{
    internal async Task VerifyFeatures(string directory)
    {
        int count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message + " · " + status.Text); count++; }
        var numericData = J.Obj(("x", "123.456789"), ("nome", "001"));
        int numericChanges = 0;
        var numericForm = new InputForm(numericData, [new("x", "Posizione", "mm"), new("nome", "Nome")], _ => numericChanges++);
        numericForm.SetValue(InputForm.CommitOnFocusLossProperty, true);
        var numericGrid = new JsonGrid([new("x", "Posizione"), new("nome", "Nome")]);
        numericGrid.Rows.Add(new JsonRow(numericData));
        var leaveInput = Ui.Button("Focus", () => { });
        var numericWindow = Ui.Dialog(this, "Verifica precisione", Ui.Stack(numericForm, leaveInput, numericGrid));
        try
        {
            numericWindow.Show(); numericWindow.UpdateLayout();
            var numericEditor = (System.Windows.Controls.TextBox)numericForm.Editors["x"];
            string Rounded(double n) => n.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
            Check(numericEditor.Text == Rounded(123.46) && numericForm.Get("x") == "123.456789" && numericChanges == 0, "Input arrotondato solo nella presentazione");
            Check(((System.Windows.Controls.TextBlock)numericGrid.Columns[0].GetCellContent(numericGrid.Rows[0])).Text == Rounded(123.46), "Coordinate tabella a due decimali");
            Check(((System.Windows.Controls.TextBox)numericForm.Editors["nome"]).Text == "001", "Identificativi numerici non arrotondati");
            numericEditor.Focus(); leaveInput.Focus();
            Check(numericData.S("x") == "123.456789" && numericChanges == 0, "Cambio focus conserva precisione senza ricalcolo");
            numericEditor.Focus(); numericEditor.Text = "124.56789"; leaveInput.Focus();
            Check(numericData.S("x") == "124.56789" && numericEditor.Text == Rounded(124.57) && numericChanges == 1, "Modifica salva valore completo e mostra due decimali");
            numericForm.Set("x", "124.57");
            Check(numericForm.Get("x") == "124.57" && numericData.S("x") == "124.57", "Aggiornamento uguale al testo arrotondato non mantiene dati obsoleti");
            Check(NumericPresentation.Format("-0.000000001", "x") == "0", "Posizione quasi nulla senza zero negativo");
            Check(NumericPresentation.Format("0.000123456", "strain").Contains("E"), "Deformazioni piccole leggibili");
        }
        finally { numericWindow.Close(); }
        async Task Update()
        {
            await CalculateAllAsync();
            var until = DateTime.UtcNow.AddSeconds(120);
            while ((Busy || calculationQueued) && DateTime.UtcNow < until) await Task.Delay(20);
            Check(!Busy && !calculationQueued && Result is not null, "Aggiornamento completo");
        }
        Check(Input.S("shape") == "Rettangolare" && Input.D("transverse_spacing_mm") == 200, "Nuova sezione rettangolare e staffe passo 200");
        var panel = stressPanels["SLE"];
        var savedMaterials = (JsonObject)Input.DeepClone();
        Check(!materials.Editors.ContainsKey("alpha_cc") && !materials.Editors.ContainsKey("gamma_c") && !materials.Editors.ContainsKey("gamma_s"), "Coefficienti solo nel gruppo normativa");
        foreach (var preset in ConcreteMaterialCatalog.Concrete())
        {
            materials.Set("classe_cls", preset.S("nome"));
            Check(Input.D("fck_mpa") == preset.D("fck_mpa") && ConcreteMaterials.Concrete(Input).Name == preset.S("nome"), "CLS da catalogo DLL " + preset.S("nome"));
        }
        foreach (var preset in ConcreteMaterialCatalog.Steel(false, settings.S("normativa")))
        {
            materials.Set("classe_acciaio", preset.S("nome"));
            var steel = ConcreteMaterials.Rebar(Input);
            Check(steel.Fyk == preset.D("fyk_mpa") && steel.Fu == preset.D("steel_fu_mpa") && Math.Abs(steel.StrainUTension * 1000 - preset.D("steel_eps_u")) < 1e-9, "Acciaio da catalogo DLL " + preset.S("nome"));
        }
        Check(((System.Windows.Controls.TextBox)materials.Editors["fyk_mpa"]).IsReadOnly && !materials.Editors["steel_diagramma"].IsEnabled, "Proprietà materiali immutabili");
        foreach (var preset in ConcreteMaterialCatalog.Steel(true))
        {
            var cable = new JsonRow(new JsonObject(), _ => { }); ApplyTendonMaterial(cable, preset);
            Check(cable.Values.D("Ep") == preset.D("Ep") && cable.Values.D("fpyk") == preset.D("fpyk") && cable.Values.D("fpk") == preset.D("fpk") && cable.Values.D("eps_u") == preset.D("eps_u"), "Trefolo da catalogo DLL " + preset.S("nome"));
        }
        Input.Clear(); foreach (var (key, value) in savedMaterials) Input[key] = value?.DeepClone();
        foreach (var key in materials.Editors.Keys) materials.Set(key, Input.S(key), true);
        Invalidate(); await Update();
        tabs.SelectedIndex = 0; UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "pannello_materiali.png"), Ui.Snapshot(Window.GetWindow(this)));
        Check(preview.DimensionLabels.Any(s => s.StartsWith("b =")) && preview.DimensionLabels.Any(s => s.StartsWith("c =")) && preview.DimensionLabels.Any(s => s.StartsWith("i min")), "Quote dimensioni, copriferro e interferro");
        preview.Dimensions = preview.CoverDimensions = preview.SpacingDimensions = false; preview.InvalidateVisual(); UpdateLayout(); _ = preview.Png();
        Check(preview.DimensionLabels.Count == 0, "Quote tutte disattivabili");
        preview.Dimensions = preview.CoverDimensions = preview.SpacingDimensions = true;
        int originalBars = preview.Section!.Bars.Count;
        foreach (string layer in new[] { "top", "bottom" }) additionalLayers[layer].Toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        await Update();
        Check(preview.Section!.Bars.Count == originalBars + (int)Input.D("second_top_count") + (int)Input.D("second_bottom_count"), "Attivazione seconde file dal pannello");
        File.WriteAllBytes(Path.Combine(directory, "secondi_strati_rettangolare.png"), preview.Png());
        using (var reopenedLayers = new ConcreteWorkspace(JsonNode.Parse(Data.ToJsonString())!.AsObject()))
            Check(reopenedLayers.preview.Section!.Bars.Count == preview.Section.Bars.Count && reopenedLayers.Input.B("second_top_enabled"), "Secondi strati persistiti nel foglio");
        foreach (string layer in new[] { "top", "bottom" }) additionalLayers[layer].Toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        await Update(); Check(preview.Section!.Bars.Count == originalBars, "Disattivazione seconde file ripristina fila singola");
        foreach (string materialType in new[] { "Calcestruzzo", "Acciaio", "Trefoli" })
        {
            JsonObject? savedMaterial = null;
            var materialDialog = CreateMaterialDialog(materialType, value => savedMaterial = value); materialDialog.Show(); materialDialog.UpdateLayout();
            var materialForm = Ui.Descendants<InputForm>(materialDialog).Single(); var materialPlot = Ui.Descendants<Plot>(materialDialog).Single();
            Check(materialPlot.Series.Count == 2 && materialPlot.Series.SelectMany(s => s.Points).All(p => p.All(double.IsFinite)), "Curve native caratteristiche/progetto " + materialType);
            string property = materialType == "Calcestruzzo" ? "fck_mpa" : materialType == "Acciaio" ? "fyk_mpa" : "fpyk";
            Check(!materialForm.Editors.ContainsKey("base") && materialForm.Editors[property].IsEnabled, "Nuovo materiale da proprietà dirette " + materialType);
            if (materialType == "Calcestruzzo") { materialForm.Set("fck_mpa", "40"); Check(materialPlot.NegateAxisLabels && materialPlot.Series.SelectMany(s => s.Points).All(p => p[0] >= 0 && p[1] >= -1e-8), "CLS riflesso soltanto per la visualizzazione"); }
            else { Check(!materialPlot.NegateAxisLabels, "Segni acciaio non riflessi"); }
            File.WriteAllBytes(Path.Combine(directory, "materiale_" + materialType + ".png"), Ui.Snapshot(materialDialog));
            Ui.Descendants<System.Windows.Controls.Button>(materialDialog).Single(b => b.Content?.ToString() == "Salva materiale e applica").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(savedMaterial?.S("origine") == "Personalizzato" && savedMaterial.S("normativa_origine") == settings.S("normativa"), "Salvataggio proprietà custom e normativa " + materialType);
        }
        var bar0 = barInventory.Rows[0]; double originalX = bar0.Values.D("x");
        bar0["x"] = Exact(originalX + 5); await Update();
        Check(Input.Array("barre_manuali").Count == barInventory.Rows.Count && Math.Abs(checker3D["SLU"].Section.Geometry.Bars[0].X - originalX - 5) < 1e-8, "Barre manuali collegate al motore");
        using (var reopened = new ConcreteWorkspace(JsonNode.Parse(Data.ToJsonString())!.AsObject()))
            Check(Math.Abs(reopened.barInventory.Rows[0].Values.D("x") - originalX - 5) < 1e-8, "Coordinate manuali conservate al salvataggio");
        Input.Remove("barre_manuali"); Invalidate(); await Update();
        var nativeProperties = checker3D["SLU"].Section.Section.GetHomogeneizedMechanicalProperties();
        Check(nativeProperties.areaH > preview.Section!.AreaCls && double.IsFinite(nativeProperties.JxxH), "Proprietà omogeneizzate della DLL disponibili");
        var standaloneProperties = CheckerSection.PrepareModel(Input, settings).Section.GetHomogeneizedMechanicalProperties();
        Check(double.IsFinite(standaloneProperties.JxxH) && Math.Abs(standaloneProperties.areaH - nativeProperties.areaH) < 1e-6, "Proprietà disponibili senza ricostruire un solver");
        ShowSectionProperties();
        var propertyDialog = Application.Current.Windows.OfType<Window>().Single(w => w.Title == "Proprietà / report della sezione");
        var propertyText = Ui.Descendants<System.Windows.Controls.TextBox>(propertyDialog).Single(t => t.IsReadOnly);
        var propertyDeadline = DateTime.UtcNow.AddSeconds(15);
        while (propertyText.Text.StartsWith("Calcolo proprietà") && DateTime.UtcNow < propertyDeadline) await Task.Delay(20);
        Check(propertyText.Text.Contains("SOLO CALCESTRUZZO") && propertyText.Text.Contains("OMOGENEIZZATA AL CLS") && propertyText.Text.Contains("ARMATURE ORDINARIE") && propertyText.Text.Contains("TREFOLI / CAVI"), "Report proprietà completo e disponibile dalla finestra: " + propertyText.Text);
        var propertyForm = Ui.Descendants<InputForm>(propertyDialog).Single();
        propertyForm.Set("phi", "2"); propertyDeadline = DateTime.UtcNow.AddSeconds(15);
        while (!propertyText.Text.Contains("φ = 2.00") && DateTime.UtcNow < propertyDeadline) await Task.Delay(20);
        Check(propertyText.Text.Contains("φ = 2.00") && settings["proprieta_sezione"].D("n") > Input.D("steel_modulus_mpa") / ConcreteMaterials.Concrete(Input).E, "Proprietà omogeneizzate con viscosità");
        string byPhi = propertyText.Text; propertyForm.Set("metodo", "Da n"); await Task.Delay(50);
        Check(propertyText.Text == byPhi, "Modalità n equivalente a φ");
        propertyDialog.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "proprieta_sezione.png"), Ui.Snapshot(propertyDialog)); propertyDialog.Close();
        var domainPanel = domainPanels[0]; tabs.SelectedIndex = 1;
        foreach (string divisions in new[] { "8", "20", "40", "64" })
        {
            domainPanel.Form.Set("angoli", divisions); await Update();
            var mesh = checker3D["SLU"].Mesh;
            Check(mesh.Vertices.All(v => double.IsFinite(v.N + v.Mx + v.My)) && mesh.Triangles.All(i => i >= 0 && i < mesh.Vertices.Count), "Mesh aggiornata con angoli " + divisions);
            UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "dominio_" + divisions + ".png"), Ui.Snapshot(Window.GetWindow(this)));
        }
        var preservedDomain = checker3D["SLU"]; var preservedChecks = domainResults["3D:SLU"]; var preservedSle = stressResults["SLE"];
        domainPanel.Form.Set("interpolazione", "Lineare");
        await Task.Delay(150); RefreshDomainPanel(domainPanel, true);
        Check(ReferenceEquals(preservedDomain, checker3D["SLU"]) && ReferenceEquals(preservedChecks, domainResults["3D:SLU"]) && ReferenceEquals(preservedSle, stressResults["SLE"]) && pendingCalculations.Count == 0, "Interpolazione non ricalcola dominio nativo né verifiche");
        Check(!ReferenceEquals(preservedDomain.Mesh, preservedDomain.DisplayMesh(domainPanel.Options)), "Mesh lineare distinta dalla quadratica");
        domainPanel.View3D!.ToggleWireframe(); UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "dominio_64_lineare_reticolo.png"), Ui.Snapshot(Window.GetWindow(this))); domainPanel.View3D.ToggleWireframe();
        domainPanel.Options["tutte_rd"] = true; domainPanel.Options["mostra_linee"] = true; domainPanel.Options["dimensione_ed"] = 9; domainPanel.Options["dimensione_rd"] = 7;
        UpdateSelection(domainPanel, true);
        Check(domainPanel.View3D!.ActionPointSize == 9 && domainPanel.View3D.ResistancePointSize == 7 && domainPanel.View3D.ShowVerificationLines, "Punti scalabili e linee di verifica");
        domainPanel.Options["solo_selezionata"] = true; UpdateSelection(domainPanel, true);
        Check(domainPanel.View3D.VisibleActionCount == 1 && domainPanel.View3D.Resistances?.Count == actions["SLU"].Count, "Filtro Ed selezionata indipendente da tutte le resistenze");
        Check(domainPanel.Summary.Children.Count == 2 && summaries["SLE"].Children.Count == 1 && summaries["SLE_FREQ"].Children.Count == 1, "Riepiloghi con sole verifiche richieste");
        geometry.Set("shape", "Circolare"); await Update();
        additionalLayers["inner"].Toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Update();
        Check(preview.Section!.Bars.Count == (int)(Input.D("longitudinal_bar_count") + Input.D("second_inner_count")), "Attivazione secondo anello circolare");
        File.WriteAllBytes(Path.Combine(directory, "secondo_anello_circolare.png"), preview.Png());
        additionalLayers["inner"].Toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); await Update();
        for (int i = 0; i < 8; i++) actions["SLE"].Add(CreateAction("SLE", "Parallela " + i, (-500 - 20 * i).ToString(), (20 + i).ToString(), "10"));
        SyncActions("SLE"); InvalidateActions("SLE"); await Update();
        var input = (JsonObject)Input.DeepClone(); var workspace = (JsonObject)settings.DeepClone(); var options = (JsonObject)settings["sle"]!["SLE"]!.DeepClone();
        var forces = actions["SLE"].Select(ReadAction).ToArray();
        var expected = await Task.Run(() => { var engine = new CheckerSection(input, workspace, options); return forces.Select(f => engine.Stress(f, "SLE")).ToArray(); });
        for (int i = 0; i < expected.Length; i++)
        {
            var state = stressResults["SLE"][actions["SLE"][i].Values.S("id")].State;
            Check(state is not null && Math.Abs(state.sigma_cls - expected[i].sigma_cls) < 1e-8 && state.tensioni_barre.Zip(expected[i].tensioni_barre).All(v => Math.Abs(v.First - v.Second) < 1e-8), "Parallelo SLE identico al seriale");
        }
        var states = stressResults["SLE"].ToDictionary(r => r.Key, r => r.Value.State);
        foreach (var (field, value) in new[] { ("esposizione", "XC2"), ("sensibilita", "Sensibile"), ("durata", "Breve"), ("aderenza", "Liscia"), ("spaziatura_fessure", "180") })
        {
            panel.Options.Set(field, value); await Update();
            Check(states.All(s => ReferenceEquals(s.Value, stressResults["SLE"][s.Key].State)), field + " conserva tensioni native");
        }
        panel.Options.Set("modello", "Non lineare"); await Update();
        options = (JsonObject)settings["sle"]!["SLE"]!.DeepClone();
        var nonlinearExpected = await Task.Run(() => { var engine = new CheckerSection(input, workspace, options); return forces.Select(f => engine.Stress(f, "SLE")).ToArray(); });
        for (int i = 0; i < nonlinearExpected.Length; i++) Check(Math.Abs(stressResults["SLE"][actions["SLE"][i].Values.S("id")].State!.sigma_cls - nonlinearExpected[i].sigma_cls) < 1e-8, "Parallelo non lineare identico al seriale");
        panel.Options.Set("modello", "Lineare"); await Update();
        Check(barInventory.Rows[0].Values.S("id") == "B01", "Identificativi a due cifre");
        var domain = domainPanels[0]; tabs.SelectedIndex = 1;
        domain.Options["tutte_rd"] = true; UpdateSelection(domain, true);
        Check(domain.View3D!.Resistances?.Count > 0, "Punti resistenti multipli");
        domain.Options["tutte_rd"] = false; UpdateSelection(domain, true);
        Check(domain.View3D.Resistances is null, "Punti resistenti disattivabili");
        Check(UtilizationPalette.Color(.5) != UtilizationPalette.Color(.51) && UtilizationPalette.Color(.7) != UtilizationPalette.Color(.71) && UtilizationPalette.Color(.9) != UtilizationPalette.Color(.91) && UtilizationPalette.Color(1) != UtilizationPalette.Color(1.01), "Cinque fasce resistenza");
        geometry.Set("shape", "A T"); reinforcement.Set("flange_bottom_count", "4"); await Update();
        Check(preview.Section!.Bars.Count == 18, "Fila intradosso T");
        File.WriteAllBytes(Path.Combine(directory, "quote_T.png"), preview.Png(720, 620));
        Check(preview.DimensionLabels.Any(s => s.StartsWith("bw =")) && preview.DimensionLabels.Any(s => s.StartsWith("hf =")), "Quote anima e soletta sezione T");
        geometry.Set("shape", "Circolare"); await Update();
        File.WriteAllBytes(Path.Combine(directory, "quote_circolare.png"), preview.Png(720, 620));
        Check(preview.DimensionLabels.Any(s => s.StartsWith("D =")), "Quota diametro sezione circolare");
        var autoOptions = (JsonObject)settings["sle"]!["SLE_QP"]!.DeepClone();
        autoOptions["esposizione"] = "XC1"; autoOptions["sensibilita"] = "Poco sensibile"; autoOptions["spaziatura_fessure"] = "";
        var crackingEngine = new CheckerSection(Input, settings, autoOptions);
        var crackingForce = new ActionPoint(-100, 150, 0);
        var crackingState = crackingEngine.Stress(crackingForce, "SLE_QP");
        var autoCrack = Ntc2018Checks.Cracking(crackingEngine, crackingState, crackingForce, Input, settings, autoOptions, "SLE_QP");
        Check(autoCrack.Details.Any(d => d.Symbol == "wk") && autoCrack.Details.Any(d => d.Symbol == "hc,eff") && CrackCalculationSummary.Format(autoCrack).Contains("k₂"), "Dettaglio completo della fessurazione");
        Check(autoCrack.BarSpacing > 0 && autoCrack.Width is not null && autoCrack.SpacingSource == "Automatico geometrico", "Spaziatura automatica alimenta fessurazione");
        autoOptions["spaziatura_fessure"] = Exact(autoCrack.BarSpacing!.Value);
        var manualCrack = Ntc2018Checks.Cracking(crackingEngine, crackingState, crackingForce, Input, settings, autoOptions, "SLE_QP");
        Check(manualCrack.Width == autoCrack.Width && manualCrack.SpacingSource == "Manuale", "Override manuale coerente con automatico");
        foreach (var (field, value) in new[] { ("esposizione", "XC1"), ("sensibilita", "Poco sensibile"), ("spaziatura_fessure", "") }) panel.Options.Set(field, value);
        var crackRow = CreateAction("SLE_QP", "Diagnostica fessure", "-100", "150", "0");
        actions["SLE_QP"].Add(crackRow); SyncActions("SLE_QP"); InvalidateActions("SLE_QP"); await Update();
        tabs.SelectedIndex = 3; sleTabs.SelectedIndex = 2;
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        var crackingPanel = stressPanels["SLE_QP"]; crackingPanel.Grid.SelectedItem = crackRow; UpdateStressSelection("SLE_QP");
        Check(crackingPanel.CrackDetail.Text.Contains("hc,eff") && crackingPanel.CrackDetail.Text.Contains("Criterio k₂") && crackingPanel.CrackDetail.Text.Contains("Δsm adottata"), "Passaggi numerici presenti nella UI per la combinazione selezionata");
        var verificationTabs = Ui.Descendants<System.Windows.Controls.TabControl>(this).First(t => t.Items.OfType<System.Windows.Controls.TabItem>().Any(i => i.Header?.ToString() == "Dettagli combinazione"));
        verificationTabs.SelectedIndex = 0; UpdateLayout();
        var traceTabs = Ui.Descendants<System.Windows.Controls.TabControl>(this).First(t => t.Items.OfType<System.Windows.Controls.TabItem>().Any(i => i.Header?.ToString() == "Fessurazione · passaggi"));
        traceTabs.SelectedItem = traceTabs.Items.OfType<System.Windows.Controls.TabItem>().First(i => i.Header?.ToString() == "Fessurazione · passaggi");
        UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "dettaglio_fessurazione.png"), Ui.Snapshot(Window.GetWindow(this)));
        File.WriteAllText(Path.Combine(directory, "dettaglio_fessurazione.txt"), crackingPanel.CrackDetail.Text);
        foreach (string scheme in new[] { "Bracci paralleli", "Staffe chiuse sovrapposte" })
        {
            ShearOptions["schema_interno"] = scheme; ShearOptions["rami_interni"] = "2"; SynchronizeStirrups();
            tabs.SelectedIndex = 0; UpdateLayout();
            File.WriteAllBytes(Path.Combine(directory, scheme.StartsWith("Bracci") ? "staffe_parallele.png" : "staffe_chiuse.png"), preview.Png());
        }
        var materialA = J.Obj(("nome", "A"), ("id", "A"), ("Ep", "195000"), ("fpyk", "1670"), ("fpk", "1860"), ("eps_u", "35"), ("diagramma", "Incrudente"));
        var materialB = (JsonObject)materialA.DeepClone(); materialB["nome"] = "B"; materialB["id"] = "B"; materialB["diagramma"] = "Elastoplastico";
        var tendonA = TendonRow(J.Obj(("id", "T01"), ("x", "-100"), ("y", "0"), ("area", "150"), ("sigma0", "1000")));
        var tendonB = TendonRow(J.Obj(("id", "T02"), ("x", "100"), ("y", "0"), ("area", "150"), ("sigma0", "1000")));
        ApplyTendonMaterial(tendonA, materialA); ApplyTendonMaterial(tendonB, materialB);
        tendons.Rows.Add(tendonA); tendons.Rows.Add(tendonB);
        tendonA["materiale"] = "Y1770";
        Check(tendonA.Values.D("fpyk") == 1560 && tendonB.Values.S("materiale") == "B", "Menu materiale modifica soltanto il cavo della riga");
        ApplyTendonMaterial(tendonA, materialA);
        tendonA["area"] = "300"; Check(Math.Abs(tendonA.Values.D("diametro") - Math.Sqrt(1200 / Math.PI)) < 1e-10, "Area aggiorna diametro equivalente");
        tendonA["diametro"] = "20"; Check(Math.Abs(tendonA.Values.D("area") - 100 * Math.PI) < 1e-10, "Diametro aggiorna area");
        Check(tendonA.Values.S("materiale") == "A" && tendonB.Values.S("materiale") == "B", "Materiali distinti per cavo");
        await Update(); Check(stressResults["SLE"].Values.All(v => v.State is not null), "SLE con due materiali trefoli");
        foreach (string reportSet in new[] { "SLE", "SLE_FREQ" })
        {
            string path = Path.Combine(directory, "report_" + reportSet + ".docx"); ExportReport(path, "Verifiche pertinenti", [reportSet]);
            using var zip = System.IO.Compression.ZipFile.OpenRead(path); using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()); string xml = reader.ReadToEnd();
            Check(reportSet == "SLE" ? !xml.Contains("Esito fessurazione") : !xml.Contains("Esito tensioni") && !xml.Contains("Inviluppo tensioni e deformazioni"), "Report esclude verifiche non richieste " + reportSet);
        }
        foreach (int tab in new[] { 2, 3, 4 }) { tabs.SelectedIndex = tab; UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "layout_tab_" + tab + ".png"), Ui.Snapshot(Window.GetWindow(this))); }
        Commit(); var copy = JsonNode.Parse(Data.ToJsonString())!.AsObject();
        using var restored = new ConcreteWorkspace(copy);
        Check(restored.tendons.Rows[0].Values.S("materiale") == "A" && restored.tendons.Rows[1].Values.S("diagramma") == "Elastoplastico" && restored.Input.S("flange_bottom_count") == "4", "Round trip nuovi input");
        File.WriteAllText(Path.Combine(directory, "features.txt"), count + " controlli superati: parallelo/seriale, cache SLE, geometria, staffe, materiali trefoli, salvataggio.");
    }
}
