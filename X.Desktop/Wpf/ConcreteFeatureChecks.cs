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
        async Task Update()
        {
            await CalculateAllAsync();
            var until = DateTime.UtcNow.AddSeconds(120);
            while ((Busy || calculationQueued) && DateTime.UtcNow < until) await Task.Delay(20);
            Check(!Busy && !calculationQueued && Result is not null, "Aggiornamento completo");
        }
        var panel = stressPanels["SLE"];
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
        geometry.Set("shape", "Circolare"); await Update();
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
        tendonA["area"] = "300"; Check(Math.Abs(tendonA.Values.D("diametro") - Math.Sqrt(1200 / Math.PI)) < 1e-10, "Area aggiorna diametro equivalente");
        tendonA["diametro"] = "20"; Check(Math.Abs(tendonA.Values.D("area") - 100 * Math.PI) < 1e-10, "Diametro aggiorna area");
        Check(tendonA.Values.S("materiale") == "A" && tendonB.Values.S("materiale") == "B", "Materiali distinti per cavo");
        await Update(); Check(stressResults["SLE"].Values.All(v => v.State is not null), "SLE con due materiali trefoli");
        Commit(); var copy = JsonNode.Parse(Data.ToJsonString())!.AsObject();
        using var restored = new ConcreteWorkspace(copy);
        Check(restored.tendons.Rows[0].Values.S("materiale") == "A" && restored.tendons.Rows[1].Values.S("diagramma") == "Elastoplastico" && restored.Input.S("flange_bottom_count") == "4", "Round trip nuovi input");
        File.WriteAllText(Path.Combine(directory, "features.txt"), count + " controlli superati: parallelo/seriale, cache SLE, geometria, staffe, materiali trefoli, salvataggio.");
    }
}
