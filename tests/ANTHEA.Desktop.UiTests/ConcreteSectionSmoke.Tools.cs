using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Presentation.Modules.ConcreteSection;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteSectionSmoke
{
    /// <summary>SetGeometry of the historic checks: the diameter written through the form of the geometry (InputForm.Set: the same write and callbacks).</summary>
    internal void SetGeometry(string value) => Inputs.Geometry.Set("diameter_mm", value);

    // ---------------------------------------------------------------- ui/smoke-section-actions

    /// <summary>
    /// ui/smoke-section-actions (VerifySectionActions of the historic SectionActionsSmokeChecks.cs): the unified actions in the tables and in the checks,
    /// the criterion of the domain separated from the limit state (its change calculates the domains again: CA-P1, on the checks queued and on the values),
    /// the window «Sollecitazioni della sezione» opened by «Sollecitazioni…» and shown from its DataTemplate (the historic SectionActionsView.Create),
    /// the report and the reopening.
    /// </summary>
    internal async Task VerifySectionActions(string directory)
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        Check(Sheet.Actions.Unified && Rows("SLU").Count == 1 && Rows("SLV").Count == 1 && Rows("SLE").Count == 1, "Instradamento nelle schede");
        Check(ShearRows.Count == 2 && Sheet.Actions.Shear.Table.IsReadOnly, "Taglio/torsione devono leggere SLU e SLV");
        Check(ShearRows.Any(r => r.Values.S("elemento") == "P02"), "Elemento perso in vista");
        Check(ShearResults.Count == 2 && DomainResults("3D:SLU").Count == 1 && DomainResults("3D:SLV").Count == 1, "Verifiche non alimentate");
        Editor.Commit(); Check(Settings["taglio"]!.Array("azioni").Count == 0 && Data["combinazioni"]!["SLU"]![0]!.Array("azioni").Count == 6, "Commit duplica/perde dati");
        var panel = Three; Tab(1); await Idle();
        Check(panel.Form["stato"].Label == "Stato limite" && panel.Form.Contains("modello_resistente"), "Criterio non separato dallo stato limite");
        string first = Part("domini", "3D:SLU");
        panel.Form.Set("modello_resistente", "Elastico");
        var pending = Pending;
        await Sheet.CalculateAsync(); await Settle();
        Check(pending.Contains("3D:SLU") && Part("domini", "3D:SLU") != first, "Cambio criterio non invalida dominio " + PendingText(pending));
        panel.Form.Set("modello_resistente", "Plastico"); await Sheet.CalculateAsync(); await Settle();
        Layout(); File.WriteAllBytes(Path.Combine(directory, "dominio-slu.png"), Ui.Snapshot(Window));
        Tab(4); Layout(); File.WriteAllBytes(Path.Combine(directory, "taglio-slu-slv.png"), Ui.Snapshot(Window));
        bool shown = false;
        await WithDialogs((dialog, window) =>
        {
            var vm = (SectionActionsViewModel)dialog; shown = true;
            var grid = Named<DataGrid>(window, "Sollecitazioni");
            Check(grid.Items.Count == 3 && grid.Columns[2] is DataGridComboBoxColumn, "Griglia Excel non collegata");
            Check(grid.Columns[^1].Visibility == Visibility.Collapsed, "Origine legacy mostrata per nuovi dati");
            vm.Selected = vm.Rows[0]; Click(Named<Button>(window, "Duplica")); window.UpdateLayout(); Check(grid.Items.Count == 4, "Binding comando/righe");
            File.WriteAllBytes(Path.Combine(directory, "editor-sollecitazioni.png"), Ui.Snapshot(window));
            Click(Named<Button>(window, "Annulla"));
        }, () => { Click(View.ActionsButton); return Task.CompletedTask; });
        Check(shown && Rows("SLU").Count == 1, "Finestra delle sollecitazioni aperta e annullata senza modifiche");
        File.WriteAllBytes(Path.Combine(directory, "report.docx"), Editor.BuildReport("Azioni unificate", ["azioni", "dominio3d", "taglio"]));
        Editor.Commit();
        var copy = (JsonObject)Data.DeepClone();
        var reopened = Reopen(copy, out var reopenedEditor);
        using (reopenedEditor) { reopened.Commit(); Check(JsonNode.DeepEquals(Data["combinazioni"], copy["combinazioni"]), "Riapertura perde componenti"); }
        File.WriteAllText(Path.Combine(directory, "section-actions-checks.txt"), $"PASS {checks} controlli WPF");
    }

    // ---------------------------------------------------------------- ui/smoke-neutral-axis (the c.a. section)

    /// <summary>
    /// The switch «Asse neutro» of the section σ/ε of the 3D domain (VerifyNeutralAxisControls of the historic checks, on NeutralAxisToggle): checked
    /// at first with the axis drawn, the click hides it and saves the choice, which the section opened again keeps.
    /// </summary>
    internal async Task VerifyNeutralAxisControls(Action<bool, string> check)
    {
        await Settle();
        Tab(1); View.Domain3DView.InnerTabs.SelectedIndex = 1; await Settle();
        var toggle = Named<CheckBox>(View.Domain3DView, "Asse neutro");
        check(toggle.IsChecked == true && View.Domain3DView.Map.ShowNeutralAxis, "CLS: opzione inizialmente attiva");
        Click(toggle); await Idle();
        Editor.Commit();
        var reopened = Reopen((JsonObject)Data.DeepClone(), out var editor);
        using (editor) check(!View.Domain3DView.Map.ShowNeutralAxis && !reopened.Domain3D.NeutralAxis && !reopened.Domain3D.Inspection.Map.ShowNeutralAxis, "CLS: preferenza persistita");
    }

    // ---------------------------------------------------------------- --check-concrete-design

    /// <summary>The options of the search of the reinforcement typed in the tab «Calcola armature» (PrepareDesignSmoke of the historic checks).</summary>
    internal async Task PrepareDesignSmoke()
    {
        await Settle();
        Tab(ConcreteSheetViewModel.DesignTab);
        foreach (var (k, v) in new[] { ("diametri", "16;20"), ("n_superiori", "3;4"), ("n_inferiori", "3;4"), ("n_laterali", "2"), ("diametri_staffe", "8"), ("passi_staffe", "100;150"), ("max_tentativi", "100") })
            Sheet.Design.Form.Set(k, v);
    }

    /// <summary>
    /// --check-concrete-design (RunDesignSmoke of the historic ConcreteDesignDesktopChecks.cs, moved from ConcreteDesign.DesktopChecks in F1.6): the
    /// search, the alternatives and their preview, «Applica», the persistence, the stale proposal refused, the full and the limited search, the
    /// parallelism and the timings, in a window off screen, large and compact. The result of the search is read from the tab (status, ranking,
    /// diagnostics: the result object is not exposed by the sheet).
    /// </summary>
    internal async Task RunDesignSmoke(string folder)
    {
        void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
        var design = Sheet.Design; var window = Window;
        async Task Run() { await design.RunCommand.ExecuteAsync(null); await Settle(); }
        int Admissible() => int.Parse(System.Text.RegularExpressions.Regex.Match(design.Status, @"([\d.]+) ammissibili").Groups[1].Value.Replace(".", ""), CultureInfo.InvariantCulture);
        await Run();
        File.WriteAllText(Path.Combine(folder, "ui-diagnostics.txt"), design.Status + "\n\n" + design.Diagnostics);
        Assert(Settings["calcola_armature"].B("ricerca_completa") && !design.Form["max_tentativi"].IsVisible, "Full search not default/limited budget still shown");
        Assert(View.Tabs.Items.Count == 9 && View.Tabs.SelectedIndex == ConcreteSheetViewModel.DesignTab, "Missing design tab");
        Assert(Admissible() > 1, "No UI alternatives: " + design.Status + " / " + design.Diagnostics);
        Assert(design.Form["parallelismo"].Kind == Anthea.Presentation.Forms.FieldKind.Choice && design.Diagnostics.Contains("Tempi di ricerca") && design.Diagnostics.Contains("riutilizzati"), "Parallel control or timing diagnostics missing");
        Assert(design.ApplyCommand.CanExecute(null), "Apply not enabled: " + design.Status);
        Assert(design.Ranking.Rows.Count == 10, "Top-N not applied");
        design.Shown.Set("mostra", "Tutte");
        Assert(design.Ranking.Rows.Count == Admissible(), "Show all failed");
        design.Ranking.SelectedIndex = 1;
        Assert(design.Ranking.Selected?.Values.S("rank") == "2" && design.Preview.Section is not null, "Alternative preview mismatch");
        await Idle();
        File.WriteAllBytes(Path.Combine(folder, "calcola-armature.png"), Ui.Snapshot(window));
        window.Width = 1140; window.Height = 800;
        await Idle();
        File.WriteAllBytes(Path.Combine(folder, "calcola-armature-compatto.png"), Ui.Snapshot(window));
        string before = Input.ToJsonString();
        design.ApplyCommand.Execute(null);
        Assert(Input.ToJsonString() != before, "Apply not transferred");
        Assert(Settings["proposta_armature_applicata"] is JsonObject, "Applied audit missing");
        Assert(!design.ApplyCommand.CanExecute(null), "Applied result still reusable");
        await Settle();
        Editor.Commit();
        var roundtrip = JsonNode.Parse(Data.ToJsonString())!.AsObject(); SectionWorkspace.Prepare(roundtrip);
        Assert(roundtrip["workspace_ca"]!["calcola_armature"].S("diametri") == "16;20", "Search settings not persisted");
        design.Form.Set("parallelismo", "2");
        await Run(); Assert(design.ApplyCommand.CanExecute(null), "Second search did not recover");
        Assert(design.Diagnostics.Contains($"fino a {Math.Min(2, Environment.ProcessorCount)} calcoli contemporanei"), "UI parallel setting not applied");
        before = Input.ToJsonString(); Rows("SLU")[0]["Mx"] = "9000";
        Assert(!design.ApplyCommand.CanExecute(null) && design.IsStale, "Stale proposal not disabled");
        design.ApplyCommand.Execute(null); Assert(Input.ToJsonString() == before, "Stale candidate applied");
        await Settle();
        await Run();
        Assert(design.Status.StartsWith("Ricerca completata", StringComparison.Ordinal) && Admissible() == 0, "Expected fully searched infeasible case");
        Assert(design.Scope.Contains("Primi motivi di scarto") && design.Diagnostics.Contains("obiettivo ≤"), "Failed search hides its actual check and target");
        Assert(!design.ApplyCommand.CanExecute(null) && design.Preview.Section is null, "Failed search retained old proposal");
        design.Form["ricerca_completa"].IsChecked = false;
        design.Form.Set("max_tentativi", "1");
        Assert(design.Form["max_tentativi"].IsVisible, "Limited budget not exposed");
        await Run();
        Assert(design.Status.StartsWith("Ricerca parziale", StringComparison.Ordinal) && Admissible() == 0 && design.Description.Contains("ricerca incompleta"), "Limited empty result looks exhaustive");
        window.Width = 1500; window.Height = 1000;
        await Idle();
        File.WriteAllBytes(Path.Combine(folder, "ricerca-incompleta.png"), Ui.Snapshot(window));
        Editor.Commit();
        File.WriteAllText(Path.Combine(folder, "ui-after-apply.json"), Data.ToJsonString());
    }

    // ---------------------------------------------------------------- --capture-validation

    /// <summary>
    /// --capture-validation, the c.a. section (ValidationCapture of the historic ValidationCaptureChecks.cs): the screen of the case through the controls
    /// and the calculation of the product, then the PNG and the result. The XAML view calculates every check of the opening (the historic tool cleared
    /// the queue but for the check of the case): the screens are those of the same data.
    /// </summary>
    internal async Task ValidationCapture(JsonObject c, string output, string id)
    {
        string mode = c.S("mode");
        await Settle();
        if (mode == "material")
        {
            ShowDialog(Inputs.CreateMaterialDialog(c.B("steel") ? "Acciaio" : "Calcestruzzo"), (dialog, window) =>
            {
                var form = ((ConcreteMaterialDialogViewModel)dialog).Form;
                foreach (var key in new[] { "fck_mpa", "cls_diagramma", "steel_modulus_mpa", "fyk_mpa", "steel_fu_mpa", "steel_eps_u", "steel_diagramma" })
                    if (form.Contains(key)) form.Set(key, Input.S(key));
                window.UpdateLayout();
                File.WriteAllBytes(Path.Combine(output, id + ".png"), Ui.Snapshot(window));
            });
            return;
        }
        int tab = (int)c.D("tab"); Tab(tab);
        if (mode == "domain")
        {
            var panel = tab == 1 ? Three : Two; string state = c.S("state", "SLU");
            panel.Form.Set("stato", state); panel.Table.Table.Selected = Rows(state).FirstOrDefault();
        }
        else if (mode == "stress")
        {
            string set = c.S("set", "SLE_QP"); Family(set);
            Stress(set).Table.Table.Selected = Rows(set).FirstOrDefault();
        }
        else if (mode == "shear") Sheet.Actions.Shear.Table.SelectedIndex = 0;
        else if (mode == "anchor")
        {
            await Idle();
            foreach (var scroller in Ui.Descendants<ScrollViewer>(TabContent(5)).Where(s => s.IsVisible).Take(1)) scroller.ScrollToBottom();
            View.Scroll.ScrollToTop();
        }
        else if (mode == "curve") await Sheet.Curvature.CalculateCommand.ExecuteAsync(null);
        await Settle();
        File.WriteAllBytes(Path.Combine(output, id + ".png"), Ui.Snapshot(Window));
        File.WriteAllText(Path.Combine(output, id + "-actual.json"), (Result ?? new JsonObject()).ToJsonString());
    }

    // ---------------------------------------------------------------- ui/f3-concrete-figures

    /// <summary>
    /// The 2D domain of the tab 03 as the XAML view draws it (the Plot of the view) for both limit states, the four filters and the resistant points with
    /// the colours of η, chosen through the options of the panel (FigureState of the historic F3ConcreteFigureChecks.cs, which wrote the options and
    /// drew the hidden panel).
    /// </summary>
    internal async Task<Dictionary<string, object>> FigureState()
    {
        var states = new Dictionary<string, object>();
        var panel = Two;
        Tab(2); await Idle();
        foreach (string key in new[] { "SLU", "SLV" })
            foreach (string filter in new[] { "Tutte", "Entro il dominio", "Fuori dominio", "Da controllare" })
                foreach (bool all in new[] { false, true })
                {
                    panel.Form.Set("stato", key); panel.Form.Set("filtro", filter);
                    panel.DisplayOptions.Single(o => o.Key == "tutte_rd").IsChecked = all; panel.DisplayOptions.Single(o => o.Key == "colora_eta").IsChecked = all;
                    await Idle();
                    var p = Plot2D;
                    states[key + "/" + filter + "/" + all] = new
                    {
                        p.Title, p.XLabel, p.YLabel, p.Note, p.InvertY, p.CenteredAxes, p.ScaleX, p.ScaleY, p.FitIncludesMarkers, p.EmptyMessage,
                        Segments = p.Segments.Select(s => new { s.A, s.B }).ToArray(),
                        Lines = p.VerificationSegments.Select(s => new { s.A, s.B }).ToArray(),
                        Markers = p.Markers.Select(m => new { m.X, m.Y, m.Label, Color = m.Color.ToString(), m.Radius }).ToArray()
                    };
                }
        return states;
    }

    /// <summary>
    /// --check-f3-concrete-figures: for each F3 fixture of the c.a. section, the figure states of the 2D domain of the XAML view equal to those frozen in
    /// tests/fixtures/f3-concrete-figures (captured on the historic view), as CheckF3ConcreteFigures of the historic view.
    /// </summary>
    internal static async Task CheckF3ConcreteFigures(string root, string output, bool capture)
    {
        Directory.CreateDirectory(output); CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        // it-IT also for the operations that the dispatcher runs outside this flow (the calculation queued at the opening draws the title of the chart).
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT"));
        int count = 0;
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "tests/fixtures/f3/inputs"), "str_palo--*.json").Order(StringComparer.Ordinal))
        {
            var fixture = JsonNode.Parse(File.ReadAllText(file))!.AsObject(); var data = fixture["data"]!.AsObject();
            SectionWorkspace.Prepare(data);
            foreach (string key in SectionWorkspace.Sets)
                foreach (var (row, index) in data["combinazioni"]!.Array(key).OfType<JsonObject>().Select((r, i) => (r, i))) row["id"] = key + "-" + index;
            using var view = Open(data);
            await view.Settle(); view.Editor.Commit(); await view.Sheet.CalculateAsync(); await view.Settle(); view.Editor.Commit();
            string actual = JsonSerializer.Serialize(await view.FigureState(), new JsonSerializerOptions { WriteIndented = true });
            string name = Path.GetFileName(file); File.WriteAllText(Path.Combine(output, name), actual);
            if (!capture)
            {
                string expected = Path.Combine(root, "tests/fixtures/f3-concrete-figures", name);
                if (!File.Exists(expected) || File.ReadAllText(expected).Replace("\r\n", "\n") != actual.Replace("\r\n", "\n"))
                    throw new InvalidOperationException("Dati grafici CA diversi: " + name);
            }
            count++;
        }
        // 4 fixtures before F5.10; 11 with the seven added in F5.10 (section fixture_aggiunte_f5_10 of the F3 manifest).
        if (count != 11) throw new InvalidOperationException("Corpus grafico CA incompleto");
        File.WriteAllText(Path.Combine(output, "esito.txt"), (capture ? "CAPTURE " : "PASS ") + count + " figure CA, filtri e punti resistenti esatti.");
    }

    // ---------------------------------------------------------------- ui/check-concrete-template-wizard

    /// <summary>
    /// The section reopened after the wizard (CheckTemplateEditor of the historic ConcreteTemplateViewChecks.cs): geometry and bars in the inputs, the
    /// controls of other layouts hidden for a template section, the paths of the links kept.
    /// </summary>
    internal static void CheckTemplateEditor(ConcreteSheetViewModel sheet, Action<bool, string> check, int bars)
    {
        var inputs = sheet.Inputs; var input = sheet.Input;
        check(inputs.Drawing.Section is not null && inputs.Drawing.Message.Length == 0 && inputs.Bars.Rows.Count == bars,
            input.S("shape") + ": riapertura nell'editor senza geometria o armature");
        if (!Anthea.ConcreteWorkspace.ConcreteTemplateAdapter.Uses(input)) return;
        check(!inputs.GeometryShown && !inputs.ReinforcementShown && inputs.Layers.All(layer => !layer.IsShown),
            input.S("shape") + ": controlli di disposizioni estranee ancora visibili");
        check(inputs.Drawing.Section!.TemplateLinks.Count > 0, input.S("shape") + ": percorsi staffe persi nell'editor");
    }

    // ---------------------------------------------------------------- --check-report-appearance-offscreen

    /// <summary>
    /// The 3D domain of the tab 02 as the view shows it (ShowDomain3DForChecks of the historic checks): the panel drawn, the view at a fixed size at the
    /// origin of its frame (the PNG then compares only the colours).
    /// </summary>
    internal async Task<DomainViewport3D> ShowDomain3DForChecks()
    {
        Tab(1); await Settle();
        var view = Viewport3D;
        view.Width = 620; view.Height = 360;
        view.HorizontalAlignment = HorizontalAlignment.Left; view.VerticalAlignment = VerticalAlignment.Top;
        Layout();
        return view;
    }
}

/// <summary>
/// F5.10, passaggio della Release: the probe of ui/f3-scheduling on the RC section with the XAML view, in place of ConcreteWorkspace.CheckF3Scheduling
/// (archived with the historic view): a calculation held at its start (the batches of the analysis session), the geometry changed meanwhile, the curve
/// M–χ and the quick resistances started, the sheet closed; nothing is published after the closing. Same expectations as the historic probe.
/// </summary>
internal static class F3ConcreteSectionMvvmSchedulingChecks
{
    internal static async Task<int> CheckF3Scheduling()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        var editor = (MvvmSheetEditor)ConcreteSectionSmoke.Editors().Factory.Open(ConcreteSectionSheetModule.Id, ModuleCatalog.CreateData(ConcreteSectionSheetModule.Id));
        var sheet = (ConcreteSheetViewModel)editor.Sheet;
        try
        {
            var held = await ConcreteSectionSmoke.HoldCalculations(sheet);
            Task work;
            try
            {
                work = sheet.CalculateAsync();
                for (int i = 0; i < 300 && !sheet.IsBusy; i++) await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                Check(sheet.IsBusy, "CA non segnala il calcolo in corso");
                sheet.Inputs.Geometry.Set("diameter_mm", "1350");
                Check(sheet.Result is null, "CA conserva risultati dopo modifica");
                var curve = sheet.Curvature.CalculateCommand.ExecuteAsync(null);
                sheet.Inputs.QuickResistance.Set("N", "-100");
                editor.Dispose();
                string status = sheet.Status, curveStatus = sheet.Curvature.Text, quickStatus = sheet.Inputs.QuickResistanceText;
                int quickCards = sheet.Inputs.QuickResistanceCards.Count;
                held.Dispose();
                await Task.WhenAll(work, curve).WaitAsync(TimeSpan.FromSeconds(20));
                for (int i = 0; i < 400 && sheet.Inputs.IsQuickResistanceRunning; i++) await Task.Delay(50);
                Check(!sheet.IsBusy && !sheet.IsCalculationQueued && sheet.Result is null, "Chiusura CA lascia calcoli o risultati in coda");
                Check(sheet.Status == status, "CA aggiorna lo stato dopo la chiusura");
                Check(sheet.Curvature.Result is null && !sheet.Curvature.IsRunning && sheet.Curvature.Text == curveStatus, "Curva M–χ pubblicata nella vista chiusa");
                Check(sheet.Inputs.QuickResistanceCards.Count == quickCards && sheet.Inputs.QuickResistanceText == quickStatus, "Resistenze rapide aggiornate nella vista chiusa");
            }
            finally { held.Dispose(); }
        }
        finally { editor.Dispose(); }
        return checks;
    }
}
