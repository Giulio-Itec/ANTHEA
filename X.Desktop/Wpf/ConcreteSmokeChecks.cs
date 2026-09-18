using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteWorkspace
{
    internal async Task VerifyWorkspace(string directory)
    {
        int checks = 0;
        void Assert(bool value, string message) { if (!value) throw new Exception("Workspace CA: " + message); checks++; }
        async Task Capture(string name)
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "ca_" + name + ".png"), Ui.Snapshot(this));
        }
        Assert(tabs.Items.Count == 4 && sleTabs.Items.Count == 3, "Quattro schede e tre SLE");
        Assert(actions["SLE"].Count == 1 && actions["SLE_FREQ"].Count == 0 && actions["SLE_QP"].Count == 0, "Migrazione SLE senza combinazioni inventate");
        Assert(meshes.Count == 2 && domainResults.ContainsKey("3D:SLU"), "Due domini calcolati");
        Assert(preview.Section?.Bars.Count == 16, "Preview geometrica");
        tabs.SelectedIndex = 0; await Capture("01_controllo");
        var three = domainPanels[0]; var two = domainPanels[1];
        foreach (string mode in new[] { "SLU", "SLV" })
        {
            tabs.SelectedIndex = 1; three.Mode.SelectedItem = mode; three.Grid.SelectedItem = actions[mode][0];
            Assert(three.View3D!.TriangleCount > 500 && three.View3D.SelectedAction is not null && three.View3D.SelectedResistance is not null, "Selezione mesh " + mode);
            var point = ReadAction(actions[mode][0]); var mesh = meshes[mode]; var check = mesh.Check(point);
            Assert(check.Utilization is > 0 && check.Resistance is not null, "Intersezione raggio " + mode);
            Assert(Math.Abs(mesh.Check(check.Resistance!.Value).Utilization!.Value - 1) < 1e-7, "Punto resistente sulla mesh " + mode);
            Assert(Math.Abs(mesh.Check(point * 2).Utilization!.Value - 2 * check.Utilization!.Value) < 1e-7, "Omogeneità azione " + mode);
            var axialCheck = mesh.Check(point, true);
            Assert(axialCheck.Resistance is ActionPoint resistance && Math.Abs(resistance.N - point.N) < 1e-7, "Ricerca N costante " + mode);
            var segments = mesh.Cut(true, .37);
            Assert(segments.Count > 5 && segments.All(s => Math.Abs(-s.A.Mx * Math.Sin(.37) + s.A.My * Math.Cos(.37)) < 1e-5), "Sezione effettiva N-M " + mode);
            Assert(mesh.Cut(false, point.N).Count > 5 && mesh.Cut(false, mesh.Scale.N * 5).Count == 0, "Piano Mx-My " + mode);
            await Capture("02_dominio3d_" + mode);
        }
        three.Mode.SelectedItem = "SLU"; three.View3D!.ToggleWireframe(); three.View3D.StandardView("N–Mx"); await Capture("02_mesh_frontale"); three.View3D.ToggleWireframe(); three.View3D.ResetView();
        three.Form.Set("filtro", "Fuori dominio"); Assert(three.Grid.Items.Count == 0, "Filtro esiti"); three.Form.Set("filtro", "Tutte");
        Assert(ReferenceEquals(three.Grid.Items[0], two.Grid.Items[0]), "Stesse azioni tra 2D e 3D");
        three.Grid.Focus(); three.Grid.CurrentCell = new DataGridCellInfo(actions["SLU"][0], three.Grid.Columns[2]);
        Assert(three.Grid.BeginEdit(), "Modifica reale cella WPF"); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var edit = Ui.Descendants<TextBox>(three.Grid).First(t => t.IsVisible && !t.IsReadOnly); edit.Text = "2600";
        Assert(edit.IsVisible && actions["SLU"][0].Values.S("N") == "2600", "Digitazione non interrompe EditItem");
        three.Grid.Commit(); Assert(Data["combinazioni"]!["SLU"]![0]!["azioni"]![0]!.ToString() == "2600", "Commit cella su archivio");
        actions["SLU"][0]["N"] = "2500"; await CalculateAllAsync();
        tabs.SelectedIndex = 2; two.Form.Set("theta", "26.5650511771");
        await RunAnalysis(token => CalculateDomain(two, token));
        Assert(domainResults["2D:SLU"].Values.First().Utilization is > 0 && two.Plot.Segments.Length > 5, "Verifica 2D nel piano");
        await Capture("03_dominio2d_NM");
        two.Form.Set("tipo", "Mx–My"); two.Form.Set("N", "2500"); await RunAnalysis(token => CalculateDomain(two, token));
        Assert(two.Plot.Markers.Any(m => m.Label == "Rd"), "Punto resistente 2D"); await Capture("03_dominio2d_MM");
        two.Form.Set("N", "2000"); await RunAnalysis(token => CalculateDomain(two, token));
        Assert(domainResults["2D:SLU"].Values.First().Utilization is null, "N fuori piano non verificato");
        tabs.SelectedIndex = 3;
        foreach (string key in new[] { "SLE_FREQ", "SLE_QP" }) { actions[key].Add(CreateAction(key, "SLE " + SectionWorkspace.Label(key), "1500", "120", "50")); SyncActions(key); }
        await CalculateAllAsync();
        foreach (string key in SectionWorkspace.Sets.Skip(2))
        {
            sleTabs.SelectedIndex = Array.IndexOf(SectionWorkspace.Sets, key) - 2; var panel = stressPanels[key]; panel.Grid.SelectedItem = actions[key][0];
            var outcome = stressResults[key].Values.First(); var expected = SezioneElastica.Tensioni(new SezioneCA(Input), new SezioneCA(Input).NAutomatico, ReadAction(actions[key][0]).N, ReadAction(actions[key][0]).Mx, ReadAction(actions[key][0]).My);
            Assert(outcome.State is not null && Math.Abs(outcome.State.sigma_acciaio - expected.sigma_acciaio) < 1e-10, "Tensioni uguali al Core " + key);
            Assert(outcome.Ratio is null && outcome.Cracking == "Da collegare", "Nessun falso esito normativo " + key);
            Assert(panel.View.Stress is not null && panel.Bars.Rows.Count == 16, "Viewport tensionale " + key);
            await Capture("04_tensioni_" + key);
        }
        var rare = stressPanels["SLE"]; rare.Options.Set("modello", "Non lineare · da collegare"); await RunAnalysis(token => CalculateStress("SLE", token));
        Assert(stressResults["SLE"].Values.All(r => r.State is null) && rare.View.Stress is null, "Non lineare non ripiega sul lineare");
        rare.Options.Set("modello", "Lineare · sezione fessurata"); rare.Options.Set("sigma_c_lim", "1"); rare.Options.Set("sigma_s_lim", "1"); await RunAnalysis(token => CalculateStress("SLE", token));
        Assert(stressResults["SLE"].Values.First().Ratio is > 1, "Limiti utente applicati");
        rare.Options.Set("sigma_c_lim", ""); rare.Options.Set("sigma_s_lim", "");
        var before = Data["combinazioni"]!["SLE_FREQ"]!.ToJsonString(); actions["SLE"][0]["N"] = "2510";
        Assert(Result is null && meshes.Count == 0 && stressResults.Count == 0 && preview.Stress is null && two.Plot.Segments.Length == 0 && actions["SLU"][0].Values.S("eta3d") == "—", "Invalidazione completa dopo edit");
        Assert(Data["combinazioni"]!["SLE_FREQ"]!.ToJsonString() == before, "Combinazioni SLE indipendenti");
        actions["SLE"][0]["N"] = "2500";
        Assert(ParsePaste("Nome\tN\tMx\tMy\nA\t100,5\t20\t30\n200\t1\t2").Count == 2, "Incolla tre/quattro colonne e virgola decimale");
        bool invalidPaste = false; try { ParsePaste("A\tabc\t1\t2"); } catch (ArgumentException) { invalidPaste = true; } Assert(invalidPaste, "Incolla invalido atomico");
        tendons.Rows.Add(new JsonRow(J.Obj(("id", "T1"), ("x", "0"), ("y", "-100"), ("area", "150"), ("sigma0", "1000")), _ => TendonsChanged())); TendonsChanged();
        await CalculateAllAsync(); Assert(Result is null && preview.Tendons.Count == 1 && status.Text.Contains("Trefoli"), "Precompressione non ignorata");
        tabs.SelectedIndex = 0; await Capture("01_trefoli_predisposti"); tendons.Rows.Clear(); TendonsChanged();
        foreach (string shape in new[] { "Rettangolare", "A T", "Circolare" }) { geometry.Set("shape", shape); Assert(preview.Section is not null && barInventory.Rows.Count > 0, "Geometria " + shape); await Capture("01_" + shape.Replace(' ', '_')); }
        Commit(); var archive = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", "str_palo"), ("dati", Data)); string filename = Path.Combine(directory, "ca_workspace.programma"); Archivio.Scrivi(filename, archive);
        using (var restored = new ConcreteWorkspace(Archivio.Leggi(filename)["dati"]!.AsObject()))
        { Assert(restored.actions["SLE_FREQ"].Count == 1 && restored.actions["SLE_QP"].Count == 1, "Riapertura SLE"); Assert(JsonNode.DeepEquals(restored.settings, settings), "Riapertura opzioni"); }
        await CalculateAllAsync(); Assert(Result is not null && !Result.B("verifica_normativa_completa", true), "Export esplicito dei limiti");
        var owner = Window.GetWindow(this); double width = owner.Width, height = owner.Height; owner.Width = 1366; owner.Height = 850;
        for (int i = 0; i < 4; i++) { tabs.SelectedIndex = i; await Capture("tab_" + (i + 1) + "_1366"); }
        owner.Width = width; owner.Height = height; tabs.SelectedIndex = 0; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var frame = Ui.Descendants<ViewportFrame>(this).First(f => ReferenceEquals(f.Host.Content, preview));
        bool expanded = false;
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => ReferenceEquals(w.Content, preview));
            expanded = window is not null; window?.Close();
        }), DispatcherPriority.ApplicationIdle);
        Ui.Descendants<Button>(frame).First(b => b.Content?.ToString() == "Espandi").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert(expanded && ReferenceEquals(frame.Host.Content, preview), "Espansione viewport e rientro");
        tabs.SelectedIndex = 0; File.WriteAllText(Path.Combine(directory, "ca_workspace_tests.txt"), $"Controlli workspace CA superati: {checks}\nQuattro tab, viewport, mesh/raggi/tagli, filtri, SLE, input, invalidazione, archivi e predisposizioni.");
    }
}
