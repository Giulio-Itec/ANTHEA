using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Anthea.Application.Concrete;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.ConcreteSection;
using X.Core;
using X.Desktop.Views.ConcreteSection;

namespace X.Desktop;

internal sealed partial class ConcreteSectionSmoke
{
    /// <summary>
    /// SLE engine of the direct calls of these checks (refactoring F2.7, commit A5): environment variable ANTHEA_MOTORE_SLE = legacy|libreria,
    /// otherwise the default engine of ConcreteServiceabilityAdapter. The views keep the default engine of the production code.
    /// </summary>
    private static ServiceabilityEngine? HarnessSleEngine() => Environment.GetEnvironmentVariable("ANTHEA_MOTORE_SLE") switch
    {
        null or "" => null, "legacy" => ServiceabilityEngine.Legacy, "libreria" => ServiceabilityEngine.Library,
        var other => throw new ArgumentException("ANTHEA_MOTORE_SLE: legacy o libreria, non " + other)
    };

    /// <summary>
    /// ui/smoke-ca-features (VerifyFeatures of the historic ConcreteFeatureChecks.cs): the same steps and messages on the XAML view. The numeric
    /// presentation on the form and the table of the XAML views (FormViewModel in FieldForm, ConcreteTableGrid: InputForm and JsonGrid of the historic
    /// view); the native objects of the domain (CA-P1) on their observable effects and on the engine called directly, as the serial checks; the cables
    /// added with «Aggiungi cavo» and their materials chosen in the rows (the historic check wrote the rows with custom materials).
    /// </summary>
    internal async Task VerifyFeatures(string directory)
    {
        int count = 0; var sle = HarnessSleEngine();
        void Check(bool value, string message) { if (!value) throw new Exception(message + " · " + Sheet.Status); count++; }
        var numericData = J.Obj(("x", "123.456789"), ("nome", "001"));
        int numericChanges = 0;
        var numericForm = new FormViewModel(numericData, [new("x", "Posizione", "mm"), new("nome", "Nome")], _ => numericChanges++) { CommitOnFocusLoss = true, FormatNumbers = true };
        var numericTable = new ConcreteTableViewModel([new("x", "Posizione"), new("nome", "Nome")], [new JsonRowViewModel(numericData)], () => false);
        var numericGrid = new ConcreteTableGrid { Table = numericTable };
        var leaveInput = new Button { Content = "Focus" };
        var numericWindow = Ui.Dialog(Window, "Verifica precisione", new StackPanel { Children = { new ContentControl { Content = numericForm, Focusable = false }, leaveInput, numericGrid } });
        numericWindow.SetValue(NumericPresentation.EnabledProperty, true);
        try
        {
            numericWindow.Show(); numericWindow.UpdateLayout();
            var numericEditor = FieldEditor<TextBox>(numericForm, "x", numericWindow);
            string Rounded(double n) => n.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
            Check(numericEditor.Text == Rounded(123.46) && numericForm.Get("x") == "123.456789" && numericChanges == 0, "Input arrotondato solo nella presentazione");
            numericGrid.UpdateLayout();
            Check(((TextBlock)numericGrid.Columns[0].GetCellContent(numericTable.Rows[0])).Text == Rounded(123.46), "Coordinate tabella a due decimali");
            Check(FieldEditor<TextBox>(numericForm, "nome", numericWindow).Text == "001", "Identificativi numerici non arrotondati");
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
            await Sheet.CalculateAsync();
            await Settle();
            Check(!Busy && !Queued && Result is not null, "Aggiornamento completo");
        }
        Check(Input.S("shape") == "Rettangolare" && Input.D("transverse_spacing_mm") == 200, "Nuova sezione rettangolare e staffe passo 200");
        var materials = Inputs.Materials;
        var savedMaterials = (JsonObject)Input.DeepClone();
        Check(!materials.Contains("alpha_cc") && !materials.Contains("gamma_c") && !materials.Contains("gamma_s"), "Coefficienti solo nel gruppo normativa");
        foreach (var preset in ConcreteMaterialCatalog.Concrete())
        {
            materials.Set("classe_cls", preset.S("nome"));
            Check(Input.D("fck_mpa") == preset.D("fck_mpa") && ConcreteMaterials.Concrete(Input).Name == preset.S("nome"), "CLS da catalogo DLL " + preset.S("nome"));
        }
        foreach (var preset in ConcreteMaterialCatalog.Steel(false, Settings.S("normativa")))
        {
            materials.Set("classe_acciaio", preset.S("nome"));
            var steel = ConcreteMaterials.Rebar(Input);
            Check(steel.Fyk == preset.D("fyk_mpa") && steel.Fu == preset.D("steel_fu_mpa") && Math.Abs(steel.StrainUTension * 1000 - preset.D("steel_eps_u")) < 1e-9, "Acciaio da catalogo DLL " + preset.S("nome"));
        }
        Tab(0); await Idle();
        Check(FieldEditor<TextBox>(materials, "fyk_mpa", TabContent(0)).IsReadOnly && materials["steel_diagramma"].IsEnabled, "Proprietà di classe protette e legame modificabile");
        // The materials of the catalogue chosen in the column «Materiale» of a cable (ApplyTendonMaterial of the historic check).
        Inputs.AddCableCommand.Execute(null);
        var probe = Inputs.Tendons.Rows[^1];
        foreach (var preset in ConcreteMaterialCatalog.Steel(true).Where(p => ConcreteInputPresentation.TendonMaterials(Settings).Any(m => m.S("nome") == p.S("nome"))))
        {
            probe["materiale"] = preset.S("nome");
            Check(probe.Values.D("Ep") == preset.D("Ep") && probe.Values.D("fpyk") == preset.D("fpyk") && probe.Values.D("fpk") == preset.D("fpk") && probe.Values.D("eps_u") == preset.D("eps_u"), "Trefolo da catalogo DLL " + preset.S("nome"));
        }
        Inputs.Tendons.Selected = probe; Inputs.RemoveCableCommand.Execute(null);
        // The inputs restored (the historic check wrote them back and invalidated: the sheet opens again on the data, question P1).
        Editor.Commit();
        var restoredData = (JsonObject)Data.DeepClone(); restoredData["input"] = savedMaterials.DeepClone();
        Sheet.Reload(restoredData); await Update();
        Tab(0); Layout();
        File.WriteAllBytes(Path.Combine(directory, "pannello_materiali.png"), Ui.Snapshot(Window));
        var preview = View.Preview;
        Check(preview.DimensionLabels.Any(s => s.StartsWith("b =")) && preview.DimensionLabels.Any(s => s.StartsWith("c =")) && preview.DimensionLabels.Any(s => s.StartsWith("i min")), "Quote dimensioni, copriferro e interferro");
        Inputs.ShowDimensions = Inputs.ShowCoverDimensions = Inputs.ShowSpacingDimensions = false; Layout(); _ = preview.Png();
        Check(preview.DimensionLabels.Count == 0, "Quote tutte disattivabili");
        Inputs.ShowDimensions = Inputs.ShowCoverDimensions = Inputs.ShowSpacingDimensions = true;
        int originalBars = Inputs.Drawing.Section!.Bars.Count;
        ButtonBase LayerToggle(string key)
        {
            var layer = Inputs.Layers.Single(l => l.Key == key);
            return Find<Button>(TabContent(0), b => ReferenceEquals(b.DataContext, layer) && ConcreteSectionViewChecks.AutomationName(b) == layer.ToggleText)
                ?? throw new InvalidOperationException("Pulsante dello strato non trovato: " + key);
        }
        foreach (string layer in new[] { "top", "bottom" }) Click(LayerToggle(layer));
        await Update();
        Check(Inputs.Drawing.Section!.Bars.Count == originalBars + (int)Input.D("second_top_count") + (int)Input.D("second_bottom_count"), "Attivazione seconde file dal pannello");
        File.WriteAllBytes(Path.Combine(directory, "secondi_strati_rettangolare.png"), preview.Png());
        Editor.Commit();
        var reopenedLayers = Reopen(JsonNode.Parse(Data.ToJsonString())!.AsObject(), out var layersEditor);
        using (layersEditor)
            Check(reopenedLayers.Inputs.Drawing.Section!.Bars.Count == Inputs.Drawing.Section.Bars.Count && reopenedLayers.Input.B("second_top_enabled"), "Secondi strati persistiti nel foglio");
        foreach (string layer in new[] { "top", "bottom" }) Click(LayerToggle(layer));
        await Update(); Check(Inputs.Drawing.Section!.Bars.Count == originalBars, "Disattivazione seconde file ripristina fila singola");
        foreach (string materialType in new[] { "Calcestruzzo", "Acciaio", "Trefoli" })
        {
            JsonObject? savedMaterial = null;
            ShowDialog(Inputs.CreateMaterialDialog(materialType), (dialog, window) =>
            {
                var material = (ConcreteMaterialDialogViewModel)dialog;
                var materialPlot = Ui.Descendants<ConcreteMaterialDialogView>(window).Single().Plot!;
                Check(materialPlot.Series.Count == 2 && materialPlot.Series.SelectMany(s => s.Points).All(p => p.All(double.IsFinite)), "Curve native caratteristiche/progetto " + materialType);
                string property = materialType == "Calcestruzzo" ? "fck_mpa" : materialType == "Acciaio" ? "fyk_mpa" : "fpyk";
                Check(!material.Form.Contains("base") && material.Form[property].IsEnabled, "Nuovo materiale da proprietà dirette " + materialType);
                if (materialType == "Calcestruzzo")
                {
                    FieldEditor<TextBox>(material.Form, "fck_mpa", window).Text = "40"; material.Form.Commit(); window.UpdateLayout();
                    materialPlot = Ui.Descendants<ConcreteMaterialDialogView>(window).Single().Plot!;
                    Check(materialPlot.NegateAxisLabels && materialPlot.Series.SelectMany(s => s.Points).All(p => p[0] >= 0 && p[1] >= -1e-8), "CLS riflesso soltanto per la visualizzazione");
                }
                else Check(!materialPlot.NegateAxisLabels, "Segni acciaio non riflessi");
                File.WriteAllBytes(Path.Combine(directory, "materiale_" + materialType + ".png"), Ui.Snapshot(window));
                Click(Named<Button>(window, "Salva materiale e applica"));
                savedMaterial = material.Result;
            });
            Check(savedMaterial?.S("origine") == "Personalizzato" && savedMaterial.S("normativa_origine") == Settings.S("normativa"), "Salvataggio proprietà custom e normativa " + materialType);
        }
        var bar0 = Inputs.Bars.Rows[0]; double originalX = bar0.Values.D("x");
        bar0["x"] = ConcreteInputPresentation.Exact(originalX + 5);
        var pending = Pending;
        await Update();
        Check(Input.Array("barre_manuali").Count == Inputs.Bars.Rows.Count && Math.Abs(Inputs.Drawing.Section!.Bars[0].X - originalX - 5) < 1e-8 && pending.Contains("3D:SLU"), "Barre manuali collegate al motore");
        Editor.Commit();
        var reopenedBars = Reopen(JsonNode.Parse(Data.ToJsonString())!.AsObject(), out var barsEditor);
        using (barsEditor) Check(Math.Abs(reopenedBars.Inputs.Bars.Rows[0].Values.D("x") - originalX - 5) < 1e-8, "Coordinate manuali conservate al salvataggio");
        Inputs.RestoreBarsCommand.Execute(null); await Update();
        Check(!Input.ContainsKey("barre_manuali") || Input.Array("barre_manuali").Count == 0, "Barre ripristinate dalla disposizione");
        // The homogenized properties of the section prepared by the engine (the domains share the same prepared section: CA-P1, the native objects of
        // the domains are not exposed by the sheet).
        var nativeProperties = CheckerSection.PrepareModel(Input, Settings).Section.GetHomogeneizedMechanicalProperties();
        Check(nativeProperties.areaH > Inputs.Drawing.Section!.AreaCls && double.IsFinite(nativeProperties.JxxH), "Proprietà omogeneizzate della DLL disponibili");
        Click(Find<Button>(TabContent(0), b => ReferenceEquals(b.Command, Inputs.OpenPropertiesCommand))!);
        var propertyDialog = ViewWindows.Single(w => w.Title == "Proprietà / report della sezione");
        var properties = (ConcreteSectionPropertiesViewModel)((ContentControl)propertyDialog.Content).Content;
        var propertyText = Ui.Descendants<TextBox>(propertyDialog).Single(t => t.IsReadOnly && t.DataContext == properties || t.IsReadOnly && t.Text.Length > 100);
        var propertyDeadline = DateTime.UtcNow.AddSeconds(15);
        while (properties.Text.StartsWith("Calcolo proprietà") && DateTime.UtcNow < propertyDeadline) { await Task.Delay(20); await Idle(); }
        propertyDialog.UpdateLayout();
        Check(propertyText.Text.Contains("SOLO CALCESTRUZZO") && propertyText.Text.Contains("OMOGENEIZZATA AL CLS") && propertyText.Text.Contains("ARMATURE ORDINARIE") && propertyText.Text.Contains("TREFOLI / CAVI"), "Report proprietà completo e disponibile dalla finestra: " + propertyText.Text);
        properties.Form.Set("phi", "2"); propertyDeadline = DateTime.UtcNow.AddSeconds(15);
        while (!properties.Text.Contains("φ = 2.00") && DateTime.UtcNow < propertyDeadline) { await Task.Delay(20); await Idle(); }
        propertyDialog.UpdateLayout();
        Check(propertyText.Text.Contains("φ = 2.00") && Settings["proprieta_sezione"].D("n") > Input.D("steel_modulus_mpa") / ConcreteMaterials.Concrete(Input).E, "Proprietà omogeneizzate con viscosità");
        string byPhi = properties.Text; properties.Form.Set("metodo", "Da n"); await Task.Delay(50);
        propertyDeadline = DateTime.UtcNow.AddSeconds(15);
        while (properties.Text.StartsWith("Calcolo proprietà") && DateTime.UtcNow < propertyDeadline) { await Task.Delay(20); await Idle(); }
        Check(properties.Text == byPhi, "Modalità n equivalente a φ");
        propertyDialog.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "proprieta_sezione.png"), Ui.Snapshot(propertyDialog)); propertyDialog.Close();
        var domainPanel = Three; Tab(1);
        foreach (string divisions in new[] { "8", "20", "40", "64" })
        {
            domainPanel.Form.Set("angoli", divisions); await Update();
            var mesh = domainPanel.View3D!.Mesh!;
            Check(mesh.Vertices.All(v => double.IsFinite(v.N + v.Mx + v.My)) && mesh.Triangles.All(i => i >= 0 && i < mesh.Vertices.Count), "Mesh aggiornata con angoli " + divisions);
            Layout(); File.WriteAllBytes(Path.Combine(directory, "dominio_" + divisions + ".png"), Ui.Snapshot(Window));
        }
        string preservedDomain = Part("domini"), preservedSle = Part("tensioni", "SLE"); var preservedMesh = domainPanel.View3D!.Mesh; var preservedResult = Result;
        domainPanel.Form.Set("interpolazione", "Lineare");
        pending = Pending;
        await Settle();
        Check(pending.Length == 0 && ReferenceEquals(preservedResult, Result) || Part("domini") == preservedDomain && Part("tensioni", "SLE") == preservedSle && Pending.Length == 0,
            "Interpolazione non ricalcola dominio nativo né verifiche " + PendingText(pending));
        Check(!ReferenceEquals(preservedMesh, domainPanel.View3D.Mesh) && domainPanel.View3D.Mesh is not null, "Mesh lineare distinta dalla quadratica");
        domainPanel.MeshCommand.Execute(null); Layout(); File.WriteAllBytes(Path.Combine(directory, "dominio_64_lineare_reticolo.png"), Ui.Snapshot(Window)); domainPanel.MeshCommand.Execute(null);
        DisplayOption(domainPanel, "tutte_rd").IsChecked = true; DisplayOption(domainPanel, "mostra_linee").IsChecked = true;
        Named<Slider>(View.Domain3DView, "Punti Ed · dimensione").Value = 9; Named<Slider>(View.Domain3DView, "Punti Rd · dimensione").Value = 7; await Idle();
        Check(Viewport3D.ActionPointSize == 9 && Viewport3D.ResistancePointSize == 7 && Viewport3D.ShowVerificationLines, "Punti scalabili e linee di verifica");
        if (!domainPanel.Options.B("solo_selezionata")) Click(Find<Button>(View.Domain3DView, b => ConcreteSectionViewChecks.AutomationName(b).StartsWith("Forze", StringComparison.Ordinal))!);
        await Idle();
        Check(Viewport3D.VisibleActionCount == 1 && Viewport3D.Resistances?.Count == Rows("SLU").Count, "Filtro Ed selezionata indipendente da tutte le resistenze");
        Check(domainPanel.Summary.Cards.Count == 2 && Sheet.Summaries["SLE"].Cards.Count == 1 && Sheet.Summaries["SLE_FREQ"].Cards.Count == 1, "Riepiloghi con sole verifiche richieste");
        Inputs.Geometry.Set("shape", "Circolare"); await Update();
        Tab(0); Click(LayerToggle("inner")); await Update();
        Check(Inputs.Drawing.Section!.Bars.Count == (int)(Input.D("longitudinal_bar_count") + Input.D("second_inner_count")), "Attivazione secondo anello circolare");
        File.WriteAllBytes(Path.Combine(directory, "secondo_anello_circolare.png"), preview.Png());
        Click(LayerToggle("inner")); await Update();
        for (int i = 0; i < 8; i++) AddAction("SLE", "Parallela " + i, (-500 - 20 * i).ToString(), (20 + i).ToString(), "10");
        await Update();
        var input = (JsonObject)Input.DeepClone(); var workspace = (JsonObject)Settings.DeepClone(); var options = (JsonObject)Settings["sle"]!["SLE"]!.DeepClone();
        var forces = Rows("SLE").Select(r => new ActionPoint(ConcreteEditor.Number(r.Values.S("N"), "N"), ConcreteEditor.Number(r.Values.S("Mx"), "Mx"), ConcreteEditor.Number(r.Values.S("My"), "My"))).ToArray();
        var expected = await Task.Run(() => { var engine = new CheckerSection(input, workspace, options); return forces.Select(f => ServiceabilityEngines.Stress(engine, f, "SLE", sle)).ToArray(); });
        double Sigma(int i) => StressResults("SLE")[Rows("SLE")[i].Values.S("id")]!["State"]!["sigma_cls"]!.GetValue<double>();
        double[] Bars(int i) => StressResults("SLE")[Rows("SLE")[i].Values.S("id")]!["State"]!["tensioni_barre"]!.AsArray().Select(v => v!.GetValue<double>()).ToArray();
        for (int i = 0; i < expected.Length; i++)
            Check(Math.Abs(Sigma(i) - expected[i].sigma_cls) < 1e-8 && Bars(i).Zip(expected[i].tensioni_barre).All(v => Math.Abs(v.First - v.Second) < 1e-8), "Parallelo SLE identico al seriale");
        var panel = Stress("SLE");
        static string States(JsonNode? rows) => string.Join("|", (rows as JsonObject ?? []).OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => v.Key + ":" + v.Value?["State"]?.ToJsonString()));
        string states = States(Result?["tensioni"]?["SLE"]);
        foreach (var (field, value) in new[] { ("esposizione", "XC2"), ("sensibilita", "Sensibile"), ("durata", "Breve"), ("aderenza", "Liscia"), ("spaziatura_fessure", "180") })
        {
            panel.Form.Set(field, value); await Update();
            string now = States(Result?["tensioni"]?["SLE"]);
            if (now != states) { File.WriteAllText(Path.Combine(directory, "stati-prima.txt"), states); File.WriteAllText(Path.Combine(directory, "stati-dopo-" + field + ".txt"), now); }
            Check(now == states, field + " conserva tensioni native");
        }
        Tab(3); Family("SLE"); await Idle();
        var nonLinearWarning = Named<Border>(StressView("SLE"), "Avviso analisi non lineare");
        var analysisHelp = Named<Button>(StressView("SLE"), "Wiki: Analisi");
        Check(analysisHelp.Visibility == Visibility.Visible && Equals(analysisHelp.ToolTip, WikiContextHelp.StressAnalysis.Title + "\nApri questa sezione della Wiki"), "Analisi tensionale con rimando alla guida");
        Check(nonLinearWarning.Visibility == Visibility.Collapsed, "Nessun avviso con analisi lineare");
        panel.Form.Set("modello", "Non lineare"); await Update(); Layout();
        Check(nonLinearWarning.Visibility == Visibility.Visible, "Avviso con analisi non lineare");
        options = (JsonObject)Settings["sle"]!["SLE"]!.DeepClone();
        var nonlinearExpected = await Task.Run(() => { var engine = new CheckerSection(input, workspace, options); return forces.Select(f => ServiceabilityEngines.Stress(engine, f, "SLE", sle)).ToArray(); });
        for (int i = 0; i < nonlinearExpected.Length; i++) Check(Math.Abs(Sigma(i) - nonlinearExpected[i].sigma_cls) < 1e-8, "Parallelo non lineare identico al seriale");
        panel.Form.Set("modello", "Lineare"); await Update(); Layout();
        Check(nonLinearWarning.Visibility == Visibility.Collapsed, "Avviso tolto tornando all'analisi lineare");
        Check(Inputs.Bars.Rows[0].Values.S("id") == "B01", "Identificativi a due cifre");
        var domain = Three; Tab(1); await Idle();
        DisplayOption(domain, "tutte_rd").IsChecked = true; await Idle();
        Check(Viewport3D.Resistances?.Count > 0, "Punti resistenti multipli");
        DisplayOption(domain, "tutte_rd").IsChecked = false; await Idle();
        Check(Viewport3D.Resistances is null, "Punti resistenti disattivabili");
        Check(UtilizationPalette.Color(.5) != UtilizationPalette.Color(.51) && UtilizationPalette.Color(.7) != UtilizationPalette.Color(.71) && UtilizationPalette.Color(.9) != UtilizationPalette.Color(.91) && UtilizationPalette.Color(1) != UtilizationPalette.Color(1.01), "Cinque fasce resistenza");
        Inputs.Geometry.Set("shape", "A T"); Inputs.Reinforcement.Set("flange_bottom_count", "4"); await Update();
        Check(Inputs.Drawing.Section!.Bars.Count == 18, "Fila intradosso T");
        Tab(0); Layout();
        File.WriteAllBytes(Path.Combine(directory, "quote_T.png"), preview.Png(720, 620));
        Check(preview.DimensionLabels.Any(s => s.StartsWith("bw =")) && preview.DimensionLabels.Any(s => s.StartsWith("hf =")), "Quote anima e soletta sezione T");
        Inputs.Geometry.Set("shape", "Circolare"); await Update(); Layout();
        File.WriteAllBytes(Path.Combine(directory, "quote_circolare.png"), preview.Png(720, 620));
        Check(preview.DimensionLabels.Any(s => s.StartsWith("D =")), "Quota diametro sezione circolare");
        var autoOptions = (JsonObject)Settings["sle"]!["SLE_QP"]!.DeepClone();
        autoOptions["esposizione"] = "XC1"; autoOptions["sensibilita"] = "Poco sensibile"; autoOptions["spaziatura_fessure"] = "";
        var crackingEngine = new CheckerSection(Input, Settings, autoOptions);
        var crackingForce = new ActionPoint(-100, 150, 0);
        var crackingState = ServiceabilityEngines.Stress(crackingEngine, crackingForce, "SLE_QP", sle);
        var autoCrack = ServiceabilityEngines.Cracking(crackingEngine, crackingState, crackingForce, Input, Settings, autoOptions, "SLE_QP", sle);
        Check(autoCrack.Details.Any(d => d.Symbol == "wk") && autoCrack.Details.Any(d => d.Symbol == "hc,eff") && CrackCalculationSummary.Format(autoCrack).Contains("k₂"), "Dettaglio completo della fessurazione");
        Check(autoCrack.BarSpacing > 0 && autoCrack.Width is not null && autoCrack.SpacingSource == "Automatico geometrico", "Spaziatura automatica alimenta fessurazione");
        autoOptions["spaziatura_fessure"] = ConcreteInputPresentation.Exact(autoCrack.BarSpacing!.Value);
        var manualCrack = ServiceabilityEngines.Cracking(crackingEngine, crackingState, crackingForce, Input, Settings, autoOptions, "SLE_QP", sle);
        Check(manualCrack.Width == autoCrack.Width && manualCrack.SpacingSource == "Manuale", "Override manuale coerente con automatico");
        foreach (var (field, value) in new[] { ("esposizione", "XC1"), ("sensibilita", "Poco sensibile"), ("spaziatura_fessure", "") }) panel.Form.Set(field, value);
        var crackRow = AddAction("SLE_QP", "Diagnostica fessure", "-100", "150", "0"); await Update();
        Tab(3); Family("SLE_QP"); await Idle();
        var crackingPanel = Stress("SLE_QP"); crackingPanel.Table.Table.Selected = crackRow; await Idle();
        Check(crackingPanel.CrackDetail.Contains("hc,eff") && crackingPanel.CrackDetail.Contains("Criterio k₂") && crackingPanel.CrackDetail.Contains("Δsm adottata"), "Passaggi numerici presenti nella UI per la combinazione selezionata");
        var verificationTabs = Find<TabControl>(StressView("SLE_QP"), t => t.Items.OfType<TabItem>().Any(i => ConcreteSectionViewChecks.AutomationName(i) == "Dettagli combinazione"))!;
        verificationTabs.SelectedIndex = 0; Layout();
        var traceTabs = Find<TabControl>(StressView("SLE_QP"), t => t.Items.OfType<TabItem>().Any(i => ConcreteSectionViewChecks.AutomationName(i) == "Fessurazione · passaggi"))!;
        traceTabs.SelectedItem = traceTabs.Items.OfType<TabItem>().First(i => ConcreteSectionViewChecks.AutomationName(i) == "Fessurazione · passaggi");
        Layout(); File.WriteAllBytes(Path.Combine(directory, "dettaglio_fessurazione.png"), Ui.Snapshot(Window));
        File.WriteAllText(Path.Combine(directory, "dettaglio_fessurazione.txt"), crackingPanel.CrackDetail);
        foreach (string scheme in new[] { "Bracci paralleli", "Staffe chiuse sovrapposte" })
        {
            Inputs.StirrupScheme.Set("schema_interno", scheme); Inputs.StirrupScheme.Set("rami_interni", "2");
            Tab(0); Layout();
            File.WriteAllBytes(Path.Combine(directory, scheme.StartsWith("Bracci") ? "staffe_parallele.png" : "staffe_chiuse.png"), preview.Png());
        }
        // Two cables with their own materials: added with «Aggiungi cavo», the material and the law chosen in their rows.
        Inputs.Cable.Set("numero", "1"); Inputs.Cable.Set("diametro", ConcreteInputPresentation.Exact(Math.Sqrt(4 * 150 / Math.PI))); Inputs.Cable.Set("sigma0", "1000");
        Inputs.Cable.Set("x", "-100"); Inputs.Cable.Set("y", "0"); Inputs.AddCableCommand.Execute(null);
        Inputs.Cable.Set("x", "100"); Inputs.Cable.Set("y", "0"); Inputs.AddCableCommand.Execute(null);
        var tendonA = Inputs.Tendons.Rows[^2]; var tendonB = Inputs.Tendons.Rows[^1];
        string materialB = tendonB.Values.S("materiale");
        tendonB["diagramma"] = "Elastoplastico";
        tendonA["materiale"] = "Y1770";
        Check(tendonA.Values.D("fpyk") == 1560 && tendonB.Values.S("materiale") == materialB, "Menu materiale modifica soltanto il cavo della riga");
        tendonA["area"] = "300"; Check(Math.Abs(tendonA.Values.D("diametro") - Math.Sqrt(1200 / Math.PI)) < 1e-10, "Area aggiorna diametro equivalente");
        tendonA["diametro"] = "20"; Check(Math.Abs(tendonA.Values.D("area") - 100 * Math.PI) < 1e-10, "Diametro aggiorna area");
        Check(tendonA.Values.S("materiale") == "Y1770" && tendonB.Values.S("materiale") == materialB && materialB != "Y1770", "Materiali distinti per cavo");
        await Update(); Check(StressResults("SLE").All(v => v.Value?["State"] is JsonObject), "SLE con due materiali trefoli");
        foreach (string reportSet in new[] { "SLE", "SLE_FREQ" })
        {
            string path = Path.Combine(directory, "report_" + reportSet + ".docx"); Editor.ExportReport(path, "Verifiche pertinenti", [reportSet]);
            using var zip = ZipFile.OpenRead(path); using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()); string xml = reader.ReadToEnd();
            Check(reportSet == "SLE" ? !xml.Contains("Esito fessurazione") : !xml.Contains("Esito tensioni") && !xml.Contains("Inviluppo tensioni e deformazioni"), "Report esclude verifiche non richieste " + reportSet);
        }
        foreach (int tab in new[] { 2, 3, 4 }) { Tab(tab); Layout(); File.WriteAllBytes(Path.Combine(directory, "layout_tab_" + tab + ".png"), Ui.Snapshot(Window)); }
        Check(!Sheet.Shear.Form.Contains("rami_x") && !Sheet.Shear.Form.Contains("rami_y"), "Rami staffe esposti una sola volta per pagina");
        Check(new[] { Inputs.StirrupScheme, Sheet.Shear.Stirrups.Scheme, Sheet.Detailing.Stirrups.Scheme }.Count(f => f.Contains("rami_x") && !f["rami_x"].IsInputReadOnly) == 1, "Unica sorgente modificabile dei rami staffa");
        Check(Sheet.Detailing.CoverGeometry["cover_mm"].IsInputReadOnly, "Copriferro nei dettagli duplicato come input");
        var exportedTitles = Result!["riepilogo_verifiche"]!.AsObject().Select(p => p.Value!["Title"]!.ToString() + " · " + p.Value!["Status"]!.ToString()).Order(StringComparer.Ordinal).ToArray();
        var shownTitles = Sheet.Summaries.Values.Append(Sheet.ShearDashboard).SelectMany(c => c.Cards).Select(c => c.Heading).Distinct().Order(StringComparer.Ordinal).ToArray();
        Check(exportedTitles.SequenceEqual(shownTitles), "Riepilogo esportato diverso dall'interfaccia: " + string.Join(", ", exportedTitles) + " / " + string.Join(", ", shownTitles));
        var shearReport = Editor.BuildReport("Taglio · grafico unico", ["taglio", "grafici"]);
        File.WriteAllBytes(Path.Combine(directory, "report_taglio.docx"), shearReport);
        using (var zip = new ZipArchive(new MemoryStream(shearReport)))
        {
            Check(zip.Entries.Count(e => e.FullName.StartsWith("word/media/")) == 1, "Figura delle staffe duplicata nel report");
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            Check(reader.ReadToEnd().Contains("Riepilogo delle verifiche"), "Riepilogo comune assente dal report");
        }
        Editor.Commit(); var copy = JsonNode.Parse(Data.ToJsonString())!.AsObject();
        var restored = Reopen(copy, out var restoredEditor);
        using (restoredEditor)
        {
            var cables = restored.Settings.Array("trefoli");
            Check(cables[^2]!.S("materiale") == "Y1770" && cables[^1]!.S("diagramma") == "Elastoplastico" && restored.Input.S("flange_bottom_count") == "4", "Round trip nuovi input");
        }
        File.WriteAllText(Path.Combine(directory, "features.txt"), count + " controlli superati (motore SLE delle chiamate dirette: " + (sle ?? ServiceabilityEngines.Default) + "): parallelo/seriale, cache SLE, geometria, staffe, materiali trefoli, salvataggio.");
    }
}
