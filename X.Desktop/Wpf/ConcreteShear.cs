using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private readonly Dictionary<string, Ntc2018Checks.ShearResult[]> shearResults = new();
    private readonly TextBlock shearSummary = Ui.Text("Taglio · da calcolare", 12);
    private readonly VerificationCards shearDashboard = new();
    private readonly TextBlock shearDetail = Ui.Text("Selezionare una combinazione", 13);
    private readonly VerificationCards shearWorst = new();
    private readonly ConcreteSectionViewport shearView = new();
    private JsonGrid? shearGrid;
    private InputForm shearForm = null!;
    private JsonObject ShearOptions => settings["taglio"]!.AsObject();
    private readonly TextBlock shearAutomaticNote = Ui.Text("", 11);
    private void RefreshAutomaticShear()
    {
        if (shearForm is null) return;
        bool automatic = ShearOptions.S("parametri", "Automatici da sezione") == "Automatici da sezione";
        bool stirrups = ShearOptions.S("modello") == "Con staffe";
        foreach (string axis in new[] { "x", "y" })
            foreach (string field in new[] { "bw_", "d_", "asl_" }) shearForm.Enable(field + axis, !automatic && (field != "asl_" || !stirrups));
        shearForm.ShowField("ancoraggio", automatic && !stirrups);
        if (!automatic) { shearAutomaticNote.Text = "Parametri manuali: verificare geometria, armatura tesa e ancoraggio."; return; }
        try
        {
            var geometry = ConcreteCalculationSettings.UpdateAutomaticShear(Input, ShearOptions);
            foreach (string axis in new[] { "x", "y" })
                foreach (string field in new[] { "bw_", "d_", "asl_" })
                    shearForm.Set(field + axis, EngineeringFormat.Number(J.Number(ShearOptions[field + axis])), true);
            shearForm.Set("ancoraggio", ShearOptions.S("ancoraggio"), true);
            shearAutomaticNote.Text = geometry.Shape=="Circolare"?"Circolare: bw = D (piena) o D−Di (cava); d dal baricentro delle barre di ciascun semicerchio, minimo fra i due versi. Asl minima dei due semicerchi. Suggerimenti geometrici: scegliere il modello e confermare la schematizzazione, oppure impostare parametri manuali.":"Automatici: bw minima (somma delle pareti per sezione cava); d dal baricentro delle barre di lembo, minimo fra i due versi; Asl minima dei due lembi. Per T: Vx usa lo spessore ala, Vy l’anima. Ancoraggio e disposizione resistente restano da verificare.";
        }
        catch (ArgumentException ex)
        {
            foreach (string axis in new[] { "x", "y" }) foreach (string field in new[] { "bw_", "d_", "asl_" }) { ShearOptions[field + axis] = ""; shearForm.Set(field + axis, "", true); }
            shearAutomaticNote.Text = ex.Message;
        }
    }
    private UIElement BuildShearPanel()
    {
        ConcreteCalculationSettings.Prepare(Input, settings);
        var options = ShearOptions;
        void EnableFields()
        {
            bool stirrups = options.S("modello") == "Con staffe";
            foreach (var axis in new[] { "x", "y" })
            {
                shearForm.Enable("asl_"+axis, !stirrups);
                foreach (var field in new[] { "rami_", "alpha_", "cot_" }) shearForm.Enable(field+axis, stirrups);
            }
        }
        var fields = new List<Field> { new("modello", "Modello", Choices: ["Con staffe", "Senza staffe"]), new("parametri", "Parametri geometrici", Choices: ["Automatici da sezione", "Manuali"]), new("ancoraggio", "Asl automatica efficacemente ancorata", Choices: ["Da verificare", "Confermato"]) };
        foreach (var axis in new[] {"x","y"})
            fields.AddRange([new("bw_"+axis,"bw · "+axis,"mm"),new("d_"+axis,"d utile · "+axis,"mm"),new("asl_"+axis,"Asl ancorata · "+axis,"mm²"),new("alpha_"+axis,"α staffa · "+axis,"°"),new("cot_"+axis,"cot θ · "+axis+" (vuoto: auto)")]);
        shearForm = new InputForm(options, fields, key => { if(key=="modello"&&options.S("modello")=="Con staffe"&&Input.S("staffe_presenti")=="No"){Input["staffe_presenti"]="Sì";Invalidate();} EnableFields(); RefreshAutomaticShear(); SynchronizeStirrups(); InvalidateActions("Taglio"); }, true, true);
        foreach (var axis in new[] { "x", "y" }) shearForm.GroupFields("Direzione V" + axis, new[] { "bw_", "d_", "asl_", "rami_", "alpha_", "cot_" }.Select(f => f + axis).ToArray(), true);
        EnableFields(); RefreshAutomaticShear();
        shearGrid = new JsonGrid([new("nome","Combinazione"),new("N","N [kN]"),new("Vx","Vx [kN]"),new("Vy","Vy [kN]"),new("T","T [kNm]"),new("VRdx","VRd,x [kN]",ReadOnly:true),new("VRdy","VRd,y [kN]",ReadOnly:true),new("eta_x","ηx",ReadOnly:true),new("eta_y","ηy",ReadOnly:true),new("eta_t","ηT",ReadOnly:true),new("eta_vt","ηV+T",ReadOnly:true),new("esito","Esito",ReadOnly:true)], true);
        grids.Add(shearGrid);
        shearGrid.Columns[0].MinWidth = 120;
        shearGrid.RowHeight = double.NaN;
        shearGrid.Columns[^1].Width = new DataGridLength(3, DataGridLengthUnitType.Star);
        shearGrid.Columns[^1].MinWidth = 220;
        var outputStyle = new Style(typeof(TextBlock));
        outputStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        ((DataGridTextColumn)shearGrid.Columns[^1]).ElementStyle = outputStyle;
        foreach (var row in options.Array("azioni").OfType<JsonObject>()) shearGrid.Rows.Add(ShearRow((JsonObject)row.DeepClone()));
        void Store() { options["azioni"] = new JsonArray(shearGrid.Rows.Select(r => (JsonNode)J.Obj(("id",r.Values.S("id")),("nome",r.Values.S("nome")),("N",r.Values.S("N")),("Vx",r.Values.S("Vx")),("Vy",r.Values.S("Vy")),("T",r.Values.S("T","0")))).ToArray()); }
        var buttons = Ui.Bar(Ui.Button("+ Combinazione", () => { var row = ShearRow(J.Obj(("id",Guid.NewGuid().ToString("N")),("nome","Taglio "+(shearGrid.Rows.Count+1)),("N","0"),("Vx","0"),("Vy","0"))); shearGrid.Rows.Add(row); shearGrid.SelectedItem = row; Store(); InvalidateActions("Taglio"); }),
            Ui.Button("−", () => { shearGrid.Commit(); if (shearGrid.SelectedItem is JsonRow row) shearGrid.Rows.Remove(row); Store(); InvalidateActions("Taglio"); }));
        AttachClipboard(shearGrid, () => "Taglio", buttons);
        shearGrid.SelectionChanged += (_, _) => UpdateShearSelection();
        shearGrid.IsVisibleChanged += (_, _) => { if (shearGrid.IsVisible) UpdateShearSelection(); };
        var detailTabs = new TabControl { SelectedIndex = 1 }; Ui.Tab(detailTabs, "Dettagli combinazione", Scroller(shearDetail)); Ui.Tab(detailTabs, "Riepilogo verifiche", Scroller(shearWorst));
        return AnalysisLayout(Panel("Taglio e torsione", Scroller(Ui.Stack(Group("Staffe · dati comuni", BuildStirrups(readOnly: true)), shearForm,Group("Torsione / modello circolare",BuildTorsionOptions(),true),shearAutomaticNote)), "Azioni di progetto già combinate · assi locali"),
            new ViewportFrame("Sezione · riferimenti geometrici", shearView, shearView.ResetView), detailTabs,
            Panel("Combinazioni e resistenze",Ui.Dock(WithFilters(shearGrid),bottom:Ui.Stack(buttons,shearSummary))));
    }
    private JsonRow ShearRow(JsonObject values)
    {
        if(!values.ContainsKey("T"))values["T"]="0";
        return new(values, _ =>
        {
        if (shearGrid is null || synchronizing) return;
        ShearOptions["azioni"] = new JsonArray(shearGrid.Rows.Select(r => (JsonNode)J.Obj(("id",r.Values.S("id")),("nome",r.Values.S("nome")),("N",r.Values.S("N")),("Vx",r.Values.S("Vx")),("Vy",r.Values.S("Vy")),("T",r.Values.S("T","0")))).ToArray());
        InvalidateActions("Taglio");
        });
    }
    private void InvalidateShear()
    {
        using var notifications = JsonRow.DeferNotifications(shearGrid?.Rows.AsEnumerable() ?? Enumerable.Empty<JsonRow>());
        shearResults.Clear(); torsionResults.Clear(); shearSummary.Text = shearDashboard.Text = shearWorst.Text = "Taglio e torsione · da calcolare";
        shearDetail.Text = "Dati modificati · aggiornamento automatico in attesa";
        if (shearGrid is not null) foreach (var row in shearGrid.Rows) foreach (var key in new[] {"VRdx","VRdy","eta_x","eta_y","eta_t","eta_vt","esito"}) row.Output(key,"—");
    }
    private void CalculateShear()
    {
        if (shearGrid is null) return; shearGrid.Commit(); RefreshAutomaticShear();
        using var notifications = JsonRow.DeferNotifications(shearGrid.Rows);
        InvalidateShear();
        ValidateStirrups();
        foreach (var row in shearGrid.Rows)
        {
            try
            {
                var calculated = ConcreteShearAnalysis.Calculate(Input, settings, ShearOptions, row.Values);
                var results = calculated.Shear;
                shearResults[row.Values.S("id")] = results;
                for (int i = 0; i < results.Length; i++)
                {
                    string axis = i == 0 ? "x" : "y";
                    row.Output("VRd" + axis, results[i].VRd.ToString("0.00"));
                    row.Output("eta_" + axis, results[i].Ratio?.ToString("0.00") ?? "—");
                }
                row.Output("esito", string.Join(" · ", results.Select((r, i) => (i == 0 ? "x: " : "y: ") + r.Status)));
                ShowTorsion(row, calculated.Torsion);
            }
            catch (ArgumentException ex) { shearResults.Remove(row.Values.S("id"));torsionResults.Remove(row.Values.S("id"));foreach (var key in new[] {"VRdx","VRdy","eta_x","eta_y","eta_t","eta_vt"}) row.Output(key,"—"); row.Output("esito",ex.Message); }
        }
        shearSummary.Text = shearDashboard.Text = $"Taglio: {shearResults.Count}/{shearGrid.Rows.Count} combinazioni calcolate · {shearResults.Values.Count(r=>r.Any(v=>v.Ratio>1))} oltre resistenza · {shearResults.Values.Count(r=>r.Any(v=>v.Ratio is null))} senza esito · dettagli da verificare.";
        shearSummary.Text += $" Torsione: {torsionResults.Count} calcolate, {torsionResults.Values.Count(r=>!r.Passed)} non soddisfatte.";
        shearWorst.Text = string.Join("\n\n", new[] { 0, 1 }.Select(i => WorstSummary(i == 0 ? "Taglio Vx" : "Taglio Vy", shearGrid.Rows.Count,
            shearGrid.Rows.Select(row => { var check = shearResults.GetValueOrDefault(row.Values.S("id"))?.ElementAtOrDefault(i); return (row.Values.S("nome"), check?.Ratio, check?.Status ?? row.Values.S("esito")); }))));
        if (shearGrid.SelectedItem is null && shearGrid.Rows.Count > 0) shearGrid.SelectedIndex = 0;
        UpdateShearSelection();
        RefreshVerificationSummaries();
    }
    private void UpdateShearSelection()
    {
        if (synchronizing || shearGrid?.IsVisible != true) return;
        try { shearView.Section = new SezioneCA(Input); } catch (ArgumentException) { shearView.Section = null; }
        shearView.InvalidateVisual();
        if (shearGrid?.SelectedItem is not JsonRow row) { shearDetail.Text = "Inserire o selezionare una combinazione N–Vx–Vy."; return; }
        shearDetail.Text = $"{row.Values.S("nome")} · {ShearOptions.S("modello")}\nN = {EngineeringFormat.Number(J.Number(row.Values["N"]))} kN (compressione negativa)\nVx / Vy = {EngineeringFormat.Number(J.Number(row.Values["Vx"]))} / {EngineeringFormat.Number(J.Number(row.Values["Vy"]))} kN\n\n";
        if (!shearResults.TryGetValue(row.Values.S("id"), out var checks)) { shearDetail.Text += row.Values.S("esito", "Da calcolare"); return; }
        for (int i = 0; i < checks.Length; i++)
        {
            var check = checks[i]; string axis = i == 0 ? "x" : "y";
            shearDetail.Text += $"DIREZIONE V{axis}\nbw = {EngineeringFormat.Number(J.Number(ShearOptions["bw_" + axis]))} mm · d = {EngineeringFormat.Number(J.Number(ShearOptions["d_" + axis]))} mm\n";
            if (ShearOptions.S("modello") == "Con staffe") shearDetail.Text += $"VRsd (staffe) = {check.VRsd:0.00} kN\nVRcd (puntone) = {check.VRcd:0.00} kN\ncot θ = {check.CotTheta:0.00}\nGoverna: {(check.VRsd <= check.VRcd ? "armatura trasversale" : "calcestruzzo compresso")}\n";
            shearDetail.Text += $"VRd = {check.VRd:0.00} kN · η = {check.Ratio?.ToString("0.00") ?? "—"}\n{check.Status}\n\n";
        }
        shearDetail.Text += TorsionSummary(row);
        if(Input.S("shape")=="Circolare")shearDetail.Text+="\nTaglio circolare · "+ShearOptions.S("modello_circolare")+" · verificare la schematizzazione assegnata.";
    }
}
