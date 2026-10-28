using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Anthea.Application.Documents;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Testing;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteSectionSmoke
{
    /// <summary>
    /// ui/smoke, the c.a. section (VerifyWorkspace of the historic ConcreteSmokeChecks.cs, in the window of the suite): the same steps and the same
    /// messages on the XAML view. Changed with the view: the checks on the native objects in cache (CA-P1) on the checks queued after the change and on
    /// the values kept. Left with the historic view, because they checked code of the checks only (decision D3 of 15/10/2026), never the code of the
    /// product: ParsePaste (its cases are those of the paste below), the import and the export of the actions of ApplyImport and ExportActionRows (the
    /// product imports and exports through the window «Sollecitazioni della sezione», checked by ui/smoke-section-actions and by the traces azioni/*);
    /// the Excel file chosen with «Sfoglia…» stays, shared by every table.
    /// </summary>
    internal async Task VerifyWorkspace(string directory)
    {
        int checks = 0;
        void Assert(bool value, string name) { if (!value) throw new Exception("Workspace Checker: " + name + " · " + Sheet.Status); checks++; }
        // The rows of the tables in block and their notifications (JsonRows and JsonRow of the historic grid: JsonRowCollection and JsonRowViewModel).
        var batch = new JsonRowCollection(); int resets = 0, notifications = 0;
        batch.CollectionChanged += (_, e) => { Assert(e.Action == NotifyCollectionChangedAction.Reset, "Import in blocco usa Reset"); resets++; };
        using (batch.DeferRefresh())
        {
            using (batch.DeferRefresh()) for (int i = 0; i < 10000; i++) batch.Add(new JsonRowViewModel(J.Obj(("id", i))));
            Assert(resets == 0, "Nessuna notifica durante il caricamento in blocco");
        }
        Assert(resets == 1 && batch.Count == 10000, "Un solo refresh per 10000 righe");
        var notified = batch[0]; notified.PropertyChanged += (_, _) => notifications++;
        using (JsonRowViewModel.DeferNotifications([notified]))
        {
            notified.Output("eta", 1);
            using (JsonRowViewModel.DeferNotifications([notified])) notified.Output("esito", "OK");
            Assert(notifications == 0, "Notifiche risultati sospese anche in scope annidati");
        }
        Assert(notifications == 1, "Un aggiornamento binding per riga");
        using (JsonRowViewModel.DeferNotifications([notified])) notified.Output("eta", 1);
        Assert(notifications == 1, "Risultati invariati senza notifiche");
        Task Capture(string name) => this.Capture(directory, "ca_" + name);
        // Nine CA tabs since 1.15.0: the eighth is the model column, the ninth the reinforcement design ("Calcola armature", 895bf31).
        Assert(View.Tabs.Items.Count == 9 && ConcreteSectionViewChecks.AutomationName((TabItem)View.Tabs.Items[7]) == "Colonna modello"
            && ConcreteSectionViewChecks.AutomationName((TabItem)View.Tabs.Items[8]) == "Calcola armature" && View.StressTabs.Items.Count == 3,
            "Schede CA, dettagli, momento–curvatura e calcolo armature");
        Assert(Rows("SLE_FREQ").Count == 0 && Rows("SLE_QP").Count == 0, "Nessuna combinazione inventata");
        Assert(Rows("SLU")[0].Values.D("N") < 0, "Compressione negativa");
        string[] domainNames = ["3D:SLU", "2D:SLU", "3D:SLV", "2D:SLV"];
        Assert(domainNames.All(n => Result?["domini"]?[n] is JsonObject) && !domainNames.Any(Sheet.Calculation.Errors.ContainsKey), "Domini nativi 3D e 2D");
        var three = Three; var two = Two;
        foreach (var mode in new[] { "SLU", "SLV" })
        {
            Tab(1); Choose(three.Form, "stato", mode, View.Domain3DView); three.Table.Table.Selected = Rows(mode)[0];
            await Idle();
            Assert(Viewport3D.TriangleCount > 100 && Viewport3D.SelectedResistance is not null, "Mesh e resistenza " + mode);
            Assert(Utilizations("3D:" + mode)[0] is > 0, "Tasso nativo " + mode);
            await Capture("dominio3d_" + mode);
        }
        Choose(three.Form, "stato", "SLU", View.Domain3DView);
        three.MeshCommand.Execute(null); Named<ComboBox>(View.Domain3DView, "Vista del dominio").SelectedItem = "N–Mx";
        await Capture("mesh_frontale"); three.MeshCommand.Execute(null);
        Assert(ReferenceEquals(three.Table.Table.Shown[0], two.Table.Table.Shown[0]), "Azioni condivise");
        three.Form.Set("filtro", "Da controllare"); Assert(three.Table.Table.Shown.Count == 0 && Grid(View.Domain3DView, three.Table).Items.Count == 0, "Filtro esiti"); three.Form.Set("filtro", "Tutte");
        Layout();
        var grid = Grid(View.Domain3DView, three.Table); var first = Rows("SLU")[0];
        grid.Focus(); grid.CurrentCell = new DataGridCellInfo(first, grid.Columns.First(c => c.SortMemberPath == "N"));
        Assert(grid.BeginEdit(), "Editing WPF"); await Idle();
        var edit = Ui.Descendants<TextBox>(grid).First(t => t.IsVisible && !t.IsReadOnly); edit.Text = "-2600";
        Assert(first.Values.S("N") == "-2500" && Result is not null, "Digitazione non confermata conserva i risultati fino al focus/commit");
        grid.CommitEdit(DataGridEditingUnit.Row, true); Assert(Data["combinazioni"]!["SLU"]![0]!["azioni"]![0]!.ToString() == "-2600", "Salvataggio N");
        first["N"] = "-2500"; await Sheet.CalculateAsync();
        var compactExport = Result!;
        Assert(Sheet.HasResults && ReferenceEquals(compactExport, Result), "Calcolo completato senza serializzare risultati");
        var exportedState = compactExport["tensioni"]!["SLE"]!.AsObject().First().Value!["State"]!.AsObject();
        Assert(!exportedState.ContainsKey("FiberStresses") && !exportedState.ContainsKey("FiberStrains") && !exportedState.ContainsKey("Raster") && !exportedState.ContainsKey("Native"), "Export minimo senza array grafici o oggetti nativi");
        Assert(exportedState.ContainsKey("Response") && exportedState.ContainsKey("tensioni_barre") && exportedState.ContainsKey("ConcreteVertices") && ReferenceEquals(compactExport, Result), "Export su richiesta memorizzato e dettagli report conservati");
        two.Form.Set("tipo", "Mx–My"); two.Form.Set("N", "-2500"); Tab(2);
        await Automatic();
        Assert(two.Chart!.Segments.Count > 5 && Utilizations("2D:SLU")[0] is > 0, "Dominio 2D nativo");
        await Capture("dominio2d");
        two.Form.Set("N", "-2000"); await Automatic();
        Assert(Utilizations("2D:SLU")[0] is null, "Fuori piano escluso");
        two.Form.Set("proietta", "Sì"); await Automatic();
        Assert(DomainResults("2D:SLU").First().Value!["Status"]!.ToString().Contains("proiettata"), "Proiezione esplicita");
        foreach (var key in new[] { "SLE_FREQ", "SLE_QP" }) AddAction(key, key, "-100", "150", "0");
        Tab(3);
        var linear = new Dictionary<string, double>();
        foreach (var key in new[] { "SLE", "SLE_FREQ", "SLE_QP" })
        {
            var panel = Stress(key); Family(key); panel.Table.Table.Selected = Rows(key)[0];
            panel.Form.Set("esposizione", "XC1"); panel.Form.Set("spaziatura_fessure", "150");
            await Automatic(); await Idle();
            var check = StressResults(key).First().Value!.AsObject();
            Assert(check["State"]?["sigma_cls"] is JsonValue sigma && sigma.GetValue<double>() < 0, "Tensioni Checker " + key);
            linear[key] = check["State"]!["sigma_cls"]!.GetValue<double>();
            // one row per bar of the section (the default rectangular section has 4 + 6 + 2 × 2 = 14 bars: the test expected 16, those of the circular one)
            panel = Stress(key);
            int sectionBars = panel.Map.Section?.Bars.Count ?? 0;
            Assert(sectionBars > 0 && panel.Bars.Rows.Count == sectionBars && panel.Map.Stress is not null, "Viewport " + key + $" ({panel.Bars.Rows.Count} righe per {sectionBars} barre)");
            Assert(key != "SLE_FREQ" || check["Ratio"] is null, "Frequente non dichiara falso pass tensionale");
            if (key == "SLE") Assert(check["CrackResult"]?["Passed"] is null, "Fessurazione rara non applicabile");
            Assert(!Rows(key)[0].Values.S("wk").Contains("collegare"), "Fessurazione collegata alla tabella " + key);
            await Capture("tensioni_" + key);
        }
        Stress("SLE").Form.Set("modello", "Non lineare"); await Automatic();
        Assert(Stress("SLE").NonLinearNotice && StressResults("SLE").First().Value!["State"]!["sigma_cls"]!.GetValue<double>() != linear["SLE"], "Non lineare nativo");
        Stress("SLE").Form.Set("modello", "Lineare");
        Rows("SLE")[0]["N"] = "-2510";
        var pending = Pending;
        Assert(Result is null && !pending.Any(p => p.Contains("D:")) && pending.Contains("SLE") && three.View3D!.Mesh is not null,
            "Invalidazione SLE conserva domini indipendenti " + PendingText(pending));
        // The cable of the historic check, written directly into the data without its material (question P1: the sheet opens again on the data).
        Editor.Commit();
        var withCable = (JsonObject)Data.DeepClone();
        withCable["workspace_ca"]!["trefoli"] = new JsonArray(J.Obj(("id", "T1"), ("x", "0"), ("y", "-100"), ("area", "150"), ("sigma0", "1000")));
        Sheet.Reload(withCable); await Idle();
        Assert(Sheet.StressPanels.All(p => p.Form["n_trefoli"].IsVisible && p.ValueOptions.Single(o => o.Key == "testi_trefoli").IsShown), "Inserire trefolo rende visibili le opzioni dedicate");
        await Sheet.CalculateAsync(); Assert(Result is null && Inputs.Drawing.Tendons.Count == 1, "Materiale trefolo incompleto non ignorato");
        Inputs.Tendons.Selected = Inputs.Tendons.Rows[0]; Inputs.RemoveCableCommand.Execute(null);
        three = Three; two = Two;
        foreach (var shape in new[] { "Rettangolare", "A T", "Circolare" })
        {
            Inputs.Geometry.Set("shape", shape);
            Assert(Inputs.Drawing.Section is not null && Inputs.Bars.Rows.Count > 0, "Geometria " + shape); Tab(0); await Capture("sezione_" + shape.Replace(' ', '_'));
        }
        Inputs.Geometry.Set("shape", "Rettangolare"); Tab(4);
        foreach (var axis in new[] { "x", "y" }) { Sheet.Shear.Form.Set("bw_" + axis, "300"); Sheet.Shear.Form.Set("d_" + axis, "450"); Sheet.Shear.Form.Set("rami_" + axis, "2"); }
        var shearRow = AddShear("Taglio test", "-500", "50", "100");
        await Automatic();
        Assert(ShearResults.Count == 1 && ShearResults[shearRow.Values.S("id")]!.AsArray().All(r => r?["Ratio"] is JsonValue ratio && ratio.GetValue<double>() > 0), "Taglio NTC"); await Capture("taglio");
        Editor.Commit(); var copy = (JsonObject)Data.DeepClone();
        var restored = Reopen(copy, out var restoredEditor);
        using (restoredEditor)
        {
            Assert(restored.ActionRowSets["SLE_QP"].Count == 1 && restored.Settings.D("versione") == 2, "Riapertura senza doppio cambio segno");
            Assert(restored.ShearRowCollection.Count == 1, "Salvataggio tabella taglio");
        }
        await Sheet.CalculateAsync(); Assert(Result is not null && !Result.B("verifica_normativa_completa", true), "Export senza certificazione globale");
        var owner = Window; owner.Width = 1450; owner.Height = 900;
        for (int i = 0; i < 5; i++) { Tab(i); await Capture("tab_" + (i + 1)); }
        Tab(0); await Idle();
        Click(Named<Button>(View.PreviewToolbar, "Espandi"));
        var expandedWindow = ViewWindows.LastOrDefault(w => ReferenceEquals(w.Content, View.Preview));
        bool expanded = expandedWindow is not null; expandedWindow?.Close();
        Assert(expanded && ReferenceEquals(View.PreviewHost.Content, View.Preview), "Espansione viewport");
        // Ultima modifica: two edits at once, one calculation, the result of the last; the 3D domain not built again (the native section prepared
        // once for the inputs, the domains not queued).
        string domain3D = Part("domini", "3D:SLU");
        Rows("SLE")[0]["N"] = "-2511"; Rows("SLE")[0]["N"] = "-2512";
        pending = Pending;
        Assert(Result is null && Queued, "Conferma input invalida e pianifica il calcolo");
        var statuses = await Statuses(Automatic);
        statuses = statuses.Concat([Sheet.Status]).ToList();
        Assert(Result?["dati"]?["combinazioni"]?["SLE"]?[0]?["azioni"]?[0]?.ToString() == "-2512" && StressResults("SLE").First().Value!["State"] is JsonObject, "Ultima modifica ricalcolata senza pulsanti");
        Assert(pending.SequenceEqual(["SLE"]) && Part("domini", "3D:SLU") == domain3D && !statuses.Any(s => s.Contains("preparazione unica della sezione")),
            "Cambio azioni riusa il dominio nativo " + PendingText(pending));
        Rows("SLU")[0]["N"] = "numero incompleto"; await Automatic();
        // the summary uses the common wording of VerificationSummary for missing outcomes ("DA COMPLETARE"; before, "da controllare")
        Assert((Result is null || Utilizations("3D:SLU").All(u => u is null)) && Rows("SLU").All(r => r.Values.S("eta3d") is "—" or "") && Sheet.Summaries["SLU"].Text.Contains("DA COMPLETARE"),
            "Input invalido non mostra resistenze precedenti");
        Rows("SLU")[0]["N"] = "-2500";
        statuses = await Statuses(Automatic);
        Assert(Utilizations("3D:SLU").All(u => u is > 0), "Correzione input recupera automaticamente");
        // A calculation in progress (held at its start) superseded by a new edit: the inputs stay usable, the outdated result is discarded.
        using (var held = await HoldCalculations())
        {
            Rows("SLE")[0]["N"] = "-2514"; await Idle();
            Assert(Busy && View.Tabs.IsEnabled && !Inputs.Geometry["width_mm"].IsInputReadOnly, "Input utilizzabili durante il calcolo");
            Rows("SLE")[0]["N"] = "-2513";
        }
        await Automatic();
        Assert(Result?["dati"]?["combinazioni"]?["SLE"]?[0]?["azioni"]?[0]?.ToString() == "-2513" && StressResults("SLE").First().Value!["State"] is JsonObject, "Risultato superato scartato");
        for (int i = 0; i < 5; i++)
        {
            Tab(i); await Idle();
            Assert(!Ui.Descendants<Button>(View).Any(b => b.IsVisible && b.Content?.ToString()?.StartsWith("Calcola") == true), "Nessun Calcola nella scheda " + i);
        }
        var threeForm = Find<ContentControl>(View.Domain3DView, c => ReferenceEquals(c.Content, three.Form))!;
        Assert(three.Form.Groups.Count == 2 && Ui.Descendants<Expander>(threeForm).Count() == 2 && Ui.Descendants<Expander>(threeForm).All(e => !e.IsExpanded), "Opzioni avanzate richiudibili");
        Tab(1); Choose(three.Form, "stato", "SLU", View.Domain3DView); three.Table.Table.Selected = Rows("SLU")[0];
        three.Table.CurrentColumn = "N";
        void Paste(string text, bool append = false) { Services.Clipboard!.Text = text; (append ? three.Table.AppendCommand : three.Table.PasteCommand).Execute(null); }
        int warnings = Services.Messages.Shown.Count;
        Paste("-100,5\t20\t30");
        Assert(Rows("SLU")[0].Values.D("N") == -100.5 && Rows("SLU").Count == 1, "Incolla sulle celle esistenti");
        string before = Rows("SLU")[0].Values.ToJsonString();
        Paste("-1\t2\t3\n-2\tnon numero\t4");
        Assert(Services.Messages.Shown.Count == warnings + 1 && Rows("SLU")[0].Values.ToJsonString() == before && Rows("SLU").Count == 1, "Incolla non valido atomico");
        // Casi della prova di ParsePaste sul percorso del prodotto: intestazione Nome–N–Mx–My saltata, 4 colonne con nome e virgola
        // decimale (stesso nome, valori diversi); poi la riga 0 torna ai dati di prima con le 3 colonne N–Mx–My, già provate sopra.
        // Lo stesso testo di ParsePaste, con 4 e 3 colonne insieme, è rifiutato senza toccare le righe.
        string PastedInputs(JsonRowViewModel row) => string.Join("\t", new[] { "nome", "N", "Mx", "My" }.Select(k => row.Values.S(k)));
        string inputsBefore = PastedInputs(Rows("SLU")[0]), rowName = Rows("SLU")[0].Values.S("nome");
        three.Table.Table.Selected = Rows("SLU")[0]; three.Table.CurrentColumn = "nome";
        Paste("Nome\tN\tMx\tMy\n" + rowName + "\t-100,25\t21\t31");
        var headerRow = Rows("SLU")[0].Values;
        Assert(headerRow.S("nome") == rowName && headerRow.D("N") == -100.25 && headerRow.D("Mx") == 21 && headerRow.D("My") == 31 && Rows("SLU").Count == 1, "Incolla con intestazione e nome");
        three.Table.CurrentColumn = "N";
        Paste("-100,5\t20\t30");
        Assert(PastedInputs(Rows("SLU")[0]) == inputsBefore && Rows("SLU").Count == 1, "Incolla riporta la riga 0 ai dati di prima");
        string beforeMixed = Rows("SLU")[0].Values.ToJsonString(); warnings = Services.Messages.Shown.Count;
        three.Table.CurrentColumn = "nome";
        Paste("Nome\tN\tMx\tMy\nA\t-100,5\t20\t30\n-200\t1\t2");
        Assert(Services.Messages.Shown.Count == warnings + 1 && Rows("SLU")[0].Values.ToJsonString() == beforeMixed && Rows("SLU").Count == 1, "Incolla con 4 e 3 colonne rifiutato");
        Paste("Aggiunta\t-200\t25\t10", true); await Automatic();
        Assert(Rows("SLU").Count == 2, "Aggiunta dagli appunti");
        Assert(Viewport3D.VisibleActionCount == 2, "Tutte le forze visibili");
        var forces = Find<Button>(View.Domain3DView, b => ConcreteSectionViewChecks.AutomationName(b).StartsWith("Forze", StringComparison.Ordinal))!;
        Click(forces); await Idle();
        Assert(Viewport3D.VisibleActionCount == 1 && Viewport3D.SelectedResistance is not null, "Sola selezionata e punto resistente");
        Click(forces);
        DisplayOption(three, "tutte_rd").IsChecked = true; await Idle();
        Assert(Viewport3D.Resistances?.Count == DomainResults("3D:SLU").Count(v => v.Value?["Resistance"] is JsonObject), "Tutti i punti resistenti visibili");
        DisplayOption(three, "tutte_rd").IsChecked = false; await Idle(); Assert(Viewport3D.Resistances is null, "Tutti i punti resistenti disattivabili");
        var export = Result;
        var transparency = Named<Slider>(View.Domain3DView, "Trasparenza");
        foreach (double opacity in new[] { 0d, .5, 1 }) { transparency.Value = 100 * (1 - opacity); await Idle(); Assert(Math.Abs(Viewport3D.SurfaceOpacity - opacity) < 1e-12, "Trasparenza " + opacity); }
        transparency.Value = 45;
        Assert(ReferenceEquals(export, Result) && !Queued, "Opzioni grafiche non ricalcolano");
        Assert(three.Detail.Contains("STATO AL PUNTO RESISTENTE") && three.Detail.Contains("εc min"), "Riepilogo dominio con stato nativo");
        await Capture("dominio_automatico");
        Tab(3); Family("SLE"); await Idle();
        var rare = Stress("SLE");
        var contourChoice = Named<ComboBox>(StressView("SLE"), "Contorno della mappa");
        var contourResult = Result;
        foreach (string contour in ConcreteSectionViewport.Contours)
        {
            contourChoice.SelectedItem = contour; await Capture("contour_" + ConcreteSectionViewport.Contours.IndexOf(contour));
            Assert(rare.Map.Contour == contour && Settings["sle"]!["SLE"].S("contour") == contour && ReferenceEquals(Result, contourResult), "Menu contour seleziona subito e non ricalcola " + contour);
            Assert(contour == "Solo geometria" || StressView("SLE").Map.ContourLegend.Length > 0, "Legenda contour " + contour);
        }
        contourChoice.SelectedItem = ConcreteSectionViewport.Contours[0];
        var selectedContour = contourChoice.SelectedItem;
        contourChoice.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
        Assert(Equals(contourChoice.SelectedItem, selectedContour), "Rotella sul menu chiuso non cambia contour");
        // NTC 2018 in XC1: the crack check is not required for the rare combination and its summary has no cracking section; the extended summary
        // with the cracking is the one of the quasi permanent combination.
        Assert(rare.Detail.Contains("TENSIONI") && !rare.Detail.Contains("\n\nFESSURAZIONE"), "Riepilogo SLE rara senza fessurazione non richiesta");
        Family("SLE_QP"); await Idle();
        var quasiPermanent = Stress("SLE_QP"); quasiPermanent.Table.Table.Selected = Rows("SLE_QP")[0]; await Idle();
        Assert(quasiPermanent.Detail.Contains("FESSURAZIONE") && quasiPermanent.Detail.Contains("As,eff"), "Riepilogo SLE quasi permanente esteso");
        Family("SLE"); await Idle();
        await Automatic();
        Tab(0); await Idle();
        var widthBox = FieldEditor<TextBox>(Inputs.Geometry, "width_mm", TabContent(0)); widthBox.Focus();
        string oldWidth = Input.S("width_mm"); var previousResult = Result;
        widthBox.Text = "620";
        Assert(widthBox.IsKeyboardFocusWithin && Input.S("width_mm") == oldWidth && ReferenceEquals(previousResult, Result), "Input geometria resta bozza durante digitazione");
        FieldEditor<TextBox>(Inputs.Geometry, "height_mm", TabContent(0)).Focus();
        pending = Pending;
        Assert(Input.S("width_mm") == "620" && Queued && Result is null, "Cambio focus conferma senza timer");
        statuses = await Statuses(Automatic);
        // The new section is prepared once for every domain and family of the batch (a single native section shared by the domains).
        Assert(ConcreteCalculation.AllCalculations.All(pending.Contains) && statuses.Count(s => s.Contains("preparazione unica della sezione")) == 1
            && domainNames.All(n => Result?["domini"]?[n] is JsonObject), "Unica sezione nativa condivisa fra i domini");
        Inputs.Geometry.Set("width_mm", oldWidth); await Automatic();
        rare.Form.Set("n_armature", "15"); await Automatic();
        double ec = ConcreteMaterials.Concrete(Input).E;
        Assert(Math.Abs(Settings["sle"]!["SLE"].D("phi") - (15 * ec / Input.D("steel_modulus_mpa") - 1)) < 1e-10, "n armature aggiorna phi senza arrotondare");
        rare.Form.Set("n_trefoli", "12"); await Automatic();
        Assert(Math.Abs(Settings["sle"]!["SLE"].D("phi_trefoli") - (12 * ec / 195000 - 1)) < 1e-10, "n trefoli aggiorna phi");
        Tab(3); Family("SLE"); await Idle();
        Assert(rare.Concrete.Rows.Count == Inputs.Drawing.Section!.Outline.Count && rare.Bars.Rows.All(r => r.Values.S("strain") != ""), "Dettagli CLS e deformazioni barre");
        Assert(!rare.Map.BarValues && !rare.Map.TendonValues && !rare.Map.ConcreteValues, "Etichette tensionali disattivate di default");
        Tab(2); two.Table.Table.Selected = Rows("SLU")[0]; await Capture("assi_centrati");
        Assert(Plot2D.CenteredAxes && Plot2D.AxisHalfRange.X % 10 == 0 && Plot2D.AxisHalfRange.Y % 10 == 0, "Assi simmetrici e arrotondati");
        Assert(two.Chart!.VerificationSegments.Count == 2, "Linea origine Ed Rd");
        foreach (var panel in new[] { three, two })
        {
            // the change of tab refreshes the detailing checks and rebuilds the export: the reference is taken after it, so that the check concerns
            // only the graphic switches
            Tab(panel.ThreeD ? 1 : 2); await Idle();
            var noRecompute = Result;
            foreach (string key in new[] { "mostra_ed", "mostra_rd", "mostra_linee" }) DisplayOption(panel, key).IsChecked = false;
            await Idle();
            Assert(panel.ThreeD ? Viewport3D.VisibleActionCount == 0 : Plot2D.Markers.Count == 0 && Plot2D.VerificationSegments.Count == 0, "Livelli grafici indipendenti " + panel.Prefix);
            foreach (string key in new[] { "mostra_ed", "mostra_rd", "mostra_linee", "colora_eta" }) DisplayOption(panel, key).IsChecked = true;
            await Idle();
            Assert(ReferenceEquals(noRecompute, Result) && !Queued, "Interruttori grafici non ricalcolano " + panel.Prefix);
        }
        var filter = three.Table.Table.Filter;
        filter.Column = three.Table.Table.Columns.First(c => c.Key == "nome").Header; filter.Query = "Aggiunta";
        Assert(three.Table.Table.Shown.Count == 1 && Rows("SLU").Count == 2, "Filtro testuale non elimina input");
        filter.Query = ""; filter.Column = three.Table.Table.Columns.First(c => c.Key == "N").Header; filter.Order = 1;
        Assert(three.Table.Table.Shown.First().Values.D("N") <= three.Table.Table.Shown.Last().Values.D("N"), "Ordinamento numerico e non lessicografico");
        // «Sfoglia…»: the file of the actions chosen once, shown by every table and saved.
        string excel = Path.Combine(directory, "sollecitazioni.xlsx");
        Services.OpenFileAnswer = _ => excel;
        try { three.Table.BrowseCommand.Execute(null); }
        finally { Services.OpenFileAnswer = null; }
        Assert(new[] { three.Table, two.Table, Sheet.Actions.Shear }.Concat(Sheet.Actions.Stress.Values).All(t => t.ExcelPath == Settings.S("file_sollecitazioni")) && Settings.S("file_sollecitazioni") == excel,
            "Percorso Excel condiviso da tutte le tabelle");
        string unchangedDomains = Part("domini"), oldShear = Part("taglio"), unchangedStress = Part("tensioni");
        rare.Form.Set("modello", "Non lineare");
        pending = Pending;
        await Automatic();
        Assert(Part("domini") == unchangedDomains && Part("taglio") == oldShear && !pending.Any(p => p.Contains("D:") || p == "Taglio"), "Cambio modello SLE conserva verifiche SLU/SLV e taglio senza ricalcolarle " + PendingText(pending));
        Assert(pending.SequenceEqual(["SLE", "SLE_FREQ", "SLE_QP"]) && new[] { "SLE", "SLE_FREQ", "SLE_QP" }.All(k => Settings["sle"]![k].S("modello") == "Non lineare") && rare.NonLinearNotice && Part("tensioni") != unchangedStress, "Modello non lineare applicato solo alle tre famiglie SLE " + PendingText(pending) + " avvisi " + string.Join(",", Sheet.StressPanels.Select(p => p.NonLinearNotice)) + " tensioni cambiate " + (Part("tensioni") != unchangedStress));
        rare.Form.Set("modello", "Lineare");
        Inputs.StirrupScheme.Set("rami_x", "4");
        pending = Pending;
        await Automatic();
        Assert(Part("domini") == unchangedDomains && Part("taglio") != oldShear && pending.Contains("Taglio") && pending.Contains("SLE") && !pending.Any(p => p.Contains("D:")),
            "Modifiche SLE e staffe accodate insieme aggiornano entrambe senza domini " + PendingText(pending));
        unchangedStress = Part("tensioni");
        string originalSpacing = Input.S("transverse_spacing_mm");
        Inputs.Stirrups.Set("transverse_spacing_mm", "125");
        pending = Pending;
        await Automatic();
        Assert(pending.SequenceEqual(["Taglio"]) && Part("domini") == unchangedDomains && Part("tensioni") == unchangedStress, "Passo staffe ricalcola solo taglio e conserva tutti i risultati flessionali/SLE " + PendingText(pending));
        Inputs.Stirrups.Set("transverse_spacing_mm", originalSpacing); await Automatic();
        Assert(new[] { Inputs.StirrupScheme, Sheet.Shear.Stirrups.Scheme, Sheet.Detailing.Stirrups.Scheme }.All(f => f.Get("rami_x") == "4")
            && ReferenceEquals(Inputs.Drawing.Stirrups, ShearOptions) && ReferenceEquals(Sheet.Shear.Drawing.Stirrups, ShearOptions), "Staffe sincronizzate e disegnate nelle preview");
        Inputs.Standard.Set("normativa", "DS EN 1992-1-1"); await Automatic();
        Assert(Math.Abs(Input.D("gamma_c") - 1.45) < 1e-10 && Math.Abs(Input.D("gamma_s") - 1.2) < 1e-10 && domainNames.All(n => Result?["domini"]?[n] is JsonObject),
            "Normativa nazionale collegata ai domini (DK NA: γc 1,45, γs 1,20)");
        Assert(StressResults("SLE").All(s => s.Value!["Cracking"]!.ToString().Contains("Non richiesta")) && ShearResults.Count > 0, "DS: taglio nazionale e fessurazione richiesta nella QP");
        Inputs.Standard.Set("normativa", "Model Code 2010"); await Automatic();
        Sheet.Shear.Form.Set("ancoraggio", "Confermato"); await Automatic();
        double Strain(JsonNode? check) => check!["Details"]!.AsArray().Single(d => d!["Symbol"]!.ToString() == "εx")!["Value"]!.GetValue<double>();
        var mcBefore = ShearResults.Single().Value![1];
        double strainBefore = Strain(mcBefore);
        ShearRows[0]["Mx"] = "1200"; await Automatic();
        Assert(Strain(ShearResults.Single().Value![1]) > strainBefore, "MC: modifica Mx aggiorna la deformazione usata per Vy");
        var aggregateForm = Sheet.Detailing.Forms.Single(f => f.Contains("aggregato"));
        aggregateForm.Set("aggregato", "12");
        pending = Pending;
        await Automatic();
        Assert(pending.Contains("Taglio") && ShearResults.Single().Value![1] is JsonObject, "Aggregato condiviso invalida il taglio " + PendingText(pending));
        aggregateForm.Set("aggregato", "20"); ShearRows[0]["Mx"] = "12";
        Inputs.Standard.Set("normativa", "NTC 2018"); await Automatic();
        Assert(!Inputs.Reinforcement.Contains("transverse_bar_diameter_mm") && !Inputs.Reinforcement.Contains("transverse_spacing_mm"), "Staffe assenti dal gruppo armature");
        Tab(1); await Idle();
        var stateChoice = FieldEditor<ComboBox>(three.Form, "stato", View.Domain3DView);
        Assert(SectionWorkspace.Label("SLU") == "Plastico" && SectionWorkspace.Label("SLV") == "Elastico" && (stateChoice.ItemTemplate is not null || stateChoice.ItemTemplateSelector is not null || stateChoice.DisplayMemberPath.Length > 0
            || three.Form["stato"].Label == "Tipo di dominio precedente"), "Etichette Plastico ed Elastico senza cambiare ID archivi");
        var oldMesh = three.View3D!.Mesh; string oldTwo = Part("domini", "2D:SLU"), oldStress = Part("tensioni", "SLE");
        three.Form.Set("criterio", "Eccentricità costante");
        pending = Pending;
        Assert(ReferenceEquals(three.View3D.Mesh, oldMesh) && Viewport3D.TriangleCount > 100 && Result is null, "Cambio criterio conserva subito la mesh e invalida solo esiti");
        await Automatic();
        Assert(pending.SequenceEqual(["3D:SLU", "3D:SLV"]) && three.View3D.Mesh is not null && three.View3D.VerificationCriterion == "Eccentricità costante" && Viewport3D.TriangleCount > 100,
            "Criterio aggiornato nella DLL senza ricostruire dominio " + PendingText(pending));
        Assert(Part("tensioni", "SLE") == oldStress && Part("domini", "2D:SLU") == oldTwo, "Cambio criterio non ricalcola SLE né altro dominio");
        three.Form.Set("criterio", "N costante"); await Automatic();
        two.Form.Set("proietta", "No");
        pending = Pending;
        await Automatic();
        Assert(pending.SequenceEqual(["2D:SLU", "2D:SLV"]) && Utilizations("2D:SLU").All(v => v is null), "Proiezione aggiorna solo azioni 2D " + PendingText(pending));
        two.Form.Set("proietta", "Sì"); await Automatic();
        Tab(2); await Idle();
        Assert(!Ui.Descendants<Expander>(View.Domain2DView).Any(e => Ui.Descendants<FrameworkElement>(e).Contains(FieldEditor<ComboBox>(two.Form, "proietta", View.Domain2DView))), "Proiezione fuori dalle opzioni avanzate");
        Assert(Sheet.StressPanels.All(p => !p.Form["n_trefoli"].IsVisible && !p.ValueOptions.Single(o => o.Key == "testi_trefoli").IsShown), "Impostazioni trefoli nascoste senza trefoli");
        static string States(JsonNode? stresses) => string.Join("|", (stresses as JsonObject ?? []).OrderBy(f => f.Key, StringComparer.Ordinal)
            .Select(f => f.Key + ":" + string.Join(",", (f.Value as JsonObject ?? []).OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => v.Key + "=" + v.Value?["State"]?.ToJsonString()))));
        string cachedStates = States(Result?["tensioni"]);
        rare.Form.Set("esposizione", "XC2"); await Automatic();
        if (States(Result?["tensioni"]) != cachedStates) { File.WriteAllText(Path.Combine(directory, "stati-prima.txt"), cachedStates); File.WriteAllText(Path.Combine(directory, "stati-dopo.txt"), States(Result?["tensioni"])); }
        Assert(States(Result?["tensioni"]) == cachedStates, "Ambiente riusa identiche tensioni native");
        rare.Form.Set("sensibilita", "Sensibile"); await Automatic();
        Assert(States(Result?["tensioni"]) == cachedStates, "Sensibilita armatura non ricalcola tensioni SLE");
        rare.Form.Set("sensibilita", "Poco sensibile"); await Automatic();
        Assert(new[] { "SLE", "SLE_FREQ", "SLE_QP" }.All(k => Settings["sle"]![k].S("esposizione") == "XC2") && Sheet.StressPanels.All(p => p.Form.Get("esposizione") == "XC2"), "Opzioni SLE comuni nelle tre viste");
        rare.Form.Set("esposizione", "XC1"); await Automatic();
        Assert(three.Summary.Text.Contains("Governa:") && rare.Summary.Text.Contains("Quasi permanente") && rare.Summary.Text.Contains("η =") && Sheet.Shear.Worst.Text.Contains("Governa:"), "Riepiloghi peggiori per dominio, SLE e taglio");
        double originalDepth = ShearOptions.D("d_y"); Inputs.Geometry.Set("height_mm", "820"); await Automatic();
        Assert(Math.Abs(ShearOptions.D("d_y") - originalDepth - 20) < 1e-8 && !Sheet.Shear.Form["d_y"].IsEnabled, "d automatico segue il wizard ed è protetto");
        Inputs.Geometry.Set("height_mm", "800"); await Automatic();
        Sheet.Shear.Form.Set("parametri", "Manuali"); Sheet.Shear.Form.Set("d_y", "400"); await Automatic();
        Assert(ShearOptions.D("d_y") == 400 && Sheet.Shear.Form["d_y"].IsEnabled, "Override manuale taglio disponibile");
        Sheet.Shear.Form.Set("parametri", "Automatici da sezione"); await Automatic();
        Sheet.Shear.Form.Set("modello", "Senza staffe"); await Automatic();
        Assert(ShearResults.Count == 0 && ShearRows.All(r => r.Values.S("esito").Contains("ancoraggio")), "Asl geometrica non implica ancoraggio verificato");
        Sheet.Shear.Form.Set("ancoraggio", "Confermato"); await Automatic();
        Assert(ShearResults.Count == ShearRows.Count, "Conferma ancoraggio abilita modello senza staffe");
        Inputs.Geometry.Set("height_mm", "810"); await Automatic();
        Assert(ShearOptions.S("ancoraggio") == "Da verificare" && ShearResults.Count == 0, "Modifica geometria richiede nuova conferma ancoraggio");
        Inputs.Geometry.Set("height_mm", "800"); Sheet.Shear.Form.Set("modello", "Con staffe"); await Automatic();
        Tab(2); await Idle();
        var plot = Plot2D;
        plot.Zoom = 1; _ = plot.Png(); double halfRange = plot.AxisHalfRange.X;
        plot.Zoom = 1.01; _ = plot.Png();
        Assert(plot.AxisHalfRange.X < halfRange, "Zoom 2D continuo senza scatti da arrotondamento"); plot.ResetView();
        var originalMarkers = plot.Markers.ToList(); plot.Markers.Add(new(1e7, 1e7, "outlier", Ui.Blue)); _ = plot.Png();
        Assert(plot.AxisHalfRange.X == halfRange, "Fit 2D sul dominio stabile con azioni esterne");
        plot.FitIncludesMarkers = true; _ = plot.Png();
        Assert(plot.AxisHalfRange.X >= 1e7, "Fit 2D può includere le azioni"); plot.FitIncludesMarkers = false; plot.Markers = originalMarkers;
        plot.ScaleX = 2; two.Options["scala_x"] = 2; plot.ResetView();
        Assert(plot.ScaleX == 1 && two.Options.D("scala_x") == 1, "Fit e doppio clic 2D ripristinano anche la scala salvata");
        var scaleResult = Result; // after the change of tab, which rebuilds the export (see the graphic switches)
        Tab(1); await Idle(); scaleResult = Result;
        var viewport = Viewport3D;
        viewport.SetAxisScale(1.4, .8, 1); viewport.FitView(); viewport.Zoom = 1.2;
        Assert(Math.Abs(viewport.Zoom - 1.2) < 1e-9 && ReferenceEquals(scaleResult, Result) && !Queued, "Scala e zoom 3D senza calcolo");
        viewport.SetAxisScale(1, 1, 1); viewport.FitView();
        bool scaleApplied = false;
        await WithDialogs((dialog, window) =>
        {
            var scale = (ConcreteDomainScaleDialogViewModel)dialog;
            scale.Form.Set("scala_n", "0.8"); scale.Form.Set("zoom", "125");
            window.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "ca_scala_dialog.png"), Ui.Snapshot(window));
            Click(Named<Button>(window, "Applica")); scaleApplied = true;
        }, () => { Click(Named<Button>(View.Domain3DView, "Scala…")); return Task.CompletedTask; });
        await Idle();
        Assert(scaleApplied && three.Options.D("scala_n") == .8 && Math.Abs(Viewport3D.Zoom - 1.25) < 1e-9 && ReferenceEquals(scaleResult, Result), "Dialog scala applica e persiste senza ricalcolo");
        Click(Find<Button>(View.Domain3DView, b => ReferenceEquals(b.Command, three.FitCommand))!); await Idle();
        Assert(three.Options.D("scala_n") == 1 && Math.Abs(Viewport3D.Zoom - 1) < 1e-9, "Adatta ripristina scala e zoom");
        Tab(0); owner.Width = 1000; owner.Height = 650; await Capture("finestra_ridotta");
        var scroll = View.Scroll;
        Assert(scroll.ScrollableHeight > 0 && scroll.ScrollableWidth > 0, "Finestra piccola ha entrambe le barre di scorrimento");
        scroll.ScrollToBottom(); scroll.ScrollToRightEnd(); await Capture("finestra_scorsa");
        Assert(scroll.VerticalOffset > 0 && scroll.HorizontalOffset > 0, "Barre finestra spostano realmente il contenuto");
        scroll.ScrollToTop(); scroll.ScrollToLeftEnd(); owner.Width = 1366; owner.Height = 850;
        Tab(1); await Idle();
        grid = Grid(View.Domain3DView, three.Table);
        var numericCell = grid.Columns.First(c => c.SortMemberPath == "N").GetCellContent(grid.Items[0]) as TextBlock;
        var nameCell = grid.Columns.First(c => c.SortMemberPath == "nome").GetCellContent(grid.Items[0]) as TextBlock;
        Assert(numericCell?.TextAlignment == TextAlignment.Center && nameCell?.TextAlignment == TextAlignment.Left, "Numeri centrati e nomi leggibili a sinistra");
        Editor.ExportReport(Path.Combine(directory, "relazione_ca.docx"), "Collaudo sezione rettangolare", ["geometria", "materiali", "coefficienti", "azioni", "dominio3d", "dominio2d", "SLE", "SLE_FREQ", "SLE_QP", "taglio", "grafici"]);
        Editor.ExportReport(Path.Combine(directory, "relazione_ca_completa.docx"), "SLE tutte le combinazioni", ["SLE", "SLE_FREQ", "SLE_QP", "sle_tutte", "dettagli"]);
        Editor.ExportReport(Path.Combine(directory, "relazione_ca_selettiva.docx"), "Solo SLE", ["SLE"]);
        using (var zip = ZipFile.OpenRead(Path.Combine(directory, "relazione_ca_selettiva.docx")))
        using (var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
        {
            string xml = reader.ReadToEnd();
            Assert(xml.Contains("Ambito e limiti") && !xml.Contains("Armature longitudinali") && xml.Contains("SLE Rara"), "Report sezioni selettive e limiti sempre inclusi");
            Assert(xml.Contains("Inviluppo tensioni e deformazioni") && xml.Contains("Combinazione di origine") && !xml.Contains("Stampa completa"), "Report SLE predefinito a inviluppo");
        }
        using (var zip = ZipFile.OpenRead(Path.Combine(directory, "relazione_ca.docx")))
        using (var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
        {
            var xml = System.Xml.Linq.XDocument.Parse(reader.ReadToEnd()); System.Xml.Linq.XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var blocks = xml.Root!.Element(w + "body")!.Elements().ToList();
            int firstImage = blocks.FindIndex(b => b.Descendants(w + "drawing").Any());
            int materialsHeading = blocks.FindIndex(b => b.Descendants(w + "t").Any(t => t.Value == "Materiali"));
            Assert(firstImage >= 0 && firstImage < materialsHeading && blocks.Count(b => b.Descendants(w + "drawing").Any()) >= 4, "Grafici inseriti nelle sezioni pertinenti");
            Assert(blocks.Any(b => b.Descendants(w + "t").Any(t => t.Value.Contains("governante tensioni"))), "Contouring report riferito alla combinazione governante");
        }
        Editor.Commit(); string archivePath = Path.Combine(directory, "ca_impostazioni.programma");
        var archive = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", "str_palo"), ("dati", Data)); DocumentService.Save(archivePath, archive);
        Assert(ConcretePersistenceComparison.Equivalent(DocumentService.Open(archivePath)["dati"], Data), "Salvataggio integrale opzioni, normativa, staffe, Excel e report");
        var reopened = Reopen((JsonObject)Data.DeepClone(), out var reopenedEditor);
        using (reopenedEditor)
        {
            reopened.Commit();
            Assert(reopened.Settings.S("file_sollecitazioni") == Settings.S("file_sollecitazioni") && reopened.Settings["taglio"].S("rami_x") == "4"
                && new[] { reopened.Domain3D, reopened.Domain2D }.All(p => p.Options.B("colora_eta")), "Riapertura delle nuove impostazioni");
        }
        owner.Width = 1366; owner.Height = 850;
        for (int i = 0; i < 5; i++) { Tab(i); await Capture("automatico_1366_tab_" + (i + 1)); }
        Tab(4); await Idle();
        Assert(Grid(View.ShearView, Sheet.Actions.Shear).ActualHeight >= 80, "Tabella taglio leggibile anche a 1366 px con Mx/My e comandi Excel");
        Tab(3); Family("SLE"); await Idle();
        var summaryTab = Find<TabControl>(StressView("SLE"), t => t.Items.OfType<TabItem>().Any(i => ConcreteSectionViewChecks.AutomationName(i) == "Riepilogo verifiche"))!;
        summaryTab.SelectedIndex = 1; await Capture("riepilogo_verifiche");
        Tab(0);
        File.WriteAllText(Path.Combine(directory, "ca_workspace_tests.txt"), $"{checks} controlli WPF Checker superati.");
    }

    /// <summary>The check box of a display option of a domain panel, in the bar of its drawing.</summary>
    internal CheckBox DisplayOption(ConcreteDomainPanelViewModel panel, string key)
    {
        var option = panel.DisplayOptions.Single(o => o.Key == key);
        return Find<CheckBox>(panel.ThreeD ? View.Domain3DView : View.Domain2DView, c => ReferenceEquals(c.DataContext, option))
            ?? throw new InvalidOperationException("Opzione del disegno non trovata: " + key);
    }

    /// <summary>«+ Riga» of a table of the actions, then the cells of the new row (CreateAction of the historic checks).</summary>
    internal JsonRowViewModel AddAction(string key, string name, string n, string mx, string my)
    {
        var table = key is "SLU" or "SLV" ? (Three.Key == key ? Three.Table : Two.Table) : Sheet.Actions.Stress[key];
        table.AddCommand.Execute(null);
        var row = Rows(key)[^1];
        row["nome"] = name; row["N"] = n; row["Mx"] = mx; row["My"] = my;
        return row;
    }

    /// <summary>«+ Combinazione» of the shear, then the cells of the new row (ShearRow of the historic checks).</summary>
    internal JsonRowViewModel AddShear(string name, string n, string vx, string vy, string? t = null)
    {
        Sheet.Actions.Shear.AddCommand.Execute(null);
        var row = ShearRows[^1];
        row["nome"] = name; row["N"] = n; row["Vx"] = vx; row["Vy"] = vy;
        if (t is not null) row["T"] = t;
        return row;
    }
}
