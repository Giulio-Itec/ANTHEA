using Anthea.Application.Concrete;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private readonly Dictionary<string, ConcreteResults.ShearResult[]> shearResults = new();
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
        bool automatic = ConcreteShearPresentation.Automatic(ShearOptions);
        bool stirrups = ConcreteShearPresentation.WithLinks(ShearOptions);
        bool mc = ConcreteShearPresentation.ModelCode(settings);
        foreach (string axis in new[] { "x", "y" })
            foreach (string field in new[] { "bw_", "d_", "asl_" }) shearForm.Enable(field + axis, !automatic && (field != "asl_" || !stirrups || mc));
        shearForm.ShowField("ancoraggio", automatic && (!stirrups || mc));
        foreach (string axis in new[] { "x", "y" }) shearForm.ShowField("eccentricita_mc_" + axis, mc);
        if (!automatic) { shearAutomaticNote.Text = Anthea.Application.Concrete.ConcreteInputPresentation.ManualShearNote(ShearOptions); return; }
        try
        {
            var geometry = Anthea.Application.Concrete.ConcreteSheetPreparation.UpdateAutomaticShear(Input, ShearOptions);
            foreach (string axis in new[] { "x", "y" })
                foreach (string field in new[] { "bw_", "d_", "asl_" })
                    shearForm.Set(field + axis, EngineeringFormat.Number(J.Number(ShearOptions[field + axis])), true);
            shearForm.Set("ancoraggio", ShearOptions.S("ancoraggio"), true);
            shearAutomaticNote.Text = ConcreteShearPresentation.AutomaticNote(geometry.Shape);
        }
        catch (ArgumentException ex)
        {
            foreach (string axis in new[] { "x", "y" }) foreach (string field in new[] { "bw_", "d_", "asl_" }) { ShearOptions[field + axis] = ""; shearForm.Set(field + axis, "", true); }
            shearAutomaticNote.Text = ex.Message;
        }
    }
    private UIElement BuildShearPanel()
    {
        ConcreteEditor.PrepareParameters(Input, settings);
        var options = ShearOptions;
        void EnableFields()
        {
            bool stirrups = ConcreteShearPresentation.WithLinks(options);
            foreach (var axis in new[] { "x", "y" })
            {
                shearForm.Enable("asl_"+axis, !stirrups || ConcreteShearPresentation.ModelCode(settings));
                foreach (var field in new[] { "rami_", "alpha_", "cot_" }) shearForm.Enable(field+axis, stirrups);
            }
        }
        var fields = new List<Field> { new("modello", "Modello", Choices: ["Con staffe", "Senza staffe"]), new("parametri", "Parametri geometrici", Choices: ["Automatici da sezione", "Manuali"]), new("ancoraggio", "Asl automatica efficacemente ancorata", Choices: ["Da verificare", "Confermato"]) };
        foreach (var axis in new[] {"x","y"})
            fields.AddRange([new("bw_"+axis,"bw · "+axis,"mm"),new("d_"+axis,"d utile · "+axis,"mm"),new("asl_"+axis,"Asl ancorata · "+axis,"mm²"),new("alpha_"+axis,"α staffa · "+axis,"°"),new("cot_"+axis,"cot θ · "+axis+" (vuoto: auto)")]);
        fields.AddRange([new("eccentricita_mc_x", "Δe MC · Vx", "mm"), new("eccentricita_mc_y", "Δe MC · Vy", "mm")]);
        shearForm = new InputForm(options, fields, key => { if(key=="modello"&&options.S("modello")=="Con staffe"&&Input.S("staffe_presenti")=="No"){Input["staffe_presenti"]="Sì";Invalidate();} EnableFields(); RefreshAutomaticShear(); SynchronizeStirrups(); InvalidateActions("Taglio"); }, true, true);
        foreach (var axis in new[] { "x", "y" }) shearForm.GroupFields("Direzione V" + axis, new[] { "bw_", "d_", "asl_", "rami_", "alpha_", "cot_" }.Select(f => f + axis).ToArray(), true);
        EnableFields(); RefreshAutomaticShear();
        shearGrid = new JsonGrid([new("nome","Combinazione"),new("N","N [kN]"),new("Mx","Mx [kNm]"),new("My","My [kNm]"),new("Vx","Vx [kN]"),new("Vy","Vy [kN]"),new("T","T [kNm]"),new("VRdx","VRd,x [kN]",ReadOnly:true),new("VRdy","VRd,y [kN]",ReadOnly:true),new("eta_x","ηx",ReadOnly:true),new("eta_y","ηy",ReadOnly:true),new("eta_t","ηT",ReadOnly:true),new("eta_vt","ηV+T",ReadOnly:true),new("esito","Esito",ReadOnly:true)], true);
        grids.Add(shearGrid);
        shearGrid.Columns[0].MinWidth = 120;
        shearGrid.RowHeight = double.NaN;
        shearGrid.Columns[^1].Width = new DataGridLength(3, DataGridLengthUnitType.Star);
        shearGrid.Columns[^1].MinWidth = 220;
        var outputStyle = new Style(typeof(TextBlock));
        outputStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        ((DataGridTextColumn)shearGrid.Columns[^1]).ElementStyle = outputStyle;
        foreach (var row in SectionActionInputs.ShearRows(Data)) shearGrid.Rows.Add(ShearRow((JsonObject)row.DeepClone()));
        void Store() { options["azioni"] = ConcreteSheetPreparation.StoreShearRows(shearGrid.Rows.Select(r => r.Values)); }
        var buttons = Ui.Bar(Ui.Button("+ Combinazione", () => { var row = ShearRow(ConcreteActionPresentation.NewShearRow(ConcreteActionPresentation.ShearName(shearGrid.Rows.Count))); shearGrid.Rows.Add(row); shearGrid.SelectedItem = row; Store(); InvalidateActions("Taglio"); }),
            Ui.Button("−", () => { shearGrid.Commit(); if (shearGrid.SelectedItem is JsonRow row) shearGrid.Rows.Remove(row); Store(); InvalidateActions("Taglio"); }));
        if (UnifiedActions)
        {
            shearGrid.IsReadOnly = true;
            shearGrid.Columns.Insert(0, new DataGridTextColumn { Header = "Elemento", SortMemberPath = "elemento", Binding = new System.Windows.Data.Binding("[elemento]"), IsReadOnly = true, MinWidth = 75 });
            shearGrid.Columns.Insert(1, new DataGridTextColumn { Header = "Stato limite", SortMemberPath = "stato", Binding = new System.Windows.Data.Binding("[stato]"), IsReadOnly = true, MinWidth = 80 });
            buttons.Children.Clear(); buttons.Children.Add(Ui.Button("Gestisci sollecitazioni…", OpenActionEditor));
        }
        else AttachClipboard(shearGrid, () => "Taglio", buttons);
        shearGrid.SelectionChanged += (_, _) => UpdateShearSelection();
        shearGrid.IsVisibleChanged += (_, _) => { if (shearGrid.IsVisible) UpdateShearSelection(); };
        var detailTabs = new TabControl { SelectedIndex = 1 }; Ui.Tab(detailTabs, "Dettagli combinazione", Scroller(shearDetail)); Ui.Tab(detailTabs, "Riepilogo verifiche", Scroller(shearWorst));
        return AnalysisLayout(Panel("Taglio e torsione", Scroller(Ui.Stack(Group("Staffe · dati comuni", BuildStirrups(readOnly: true)), shearForm,Group("Torsione / modello circolare",BuildTorsionOptions(),true),shearAutomaticNote, Notice("Model Code 2010: Mx è associato a Vy, My a Vx. N negativo a compressione. Il diametro massimo dell’aggregato è condiviso con Dettagli → Copriferro e durabilità; modificarlo aggiorna anche il taglio. Le azioni sono già coefficientate; il modulo applica i coefficienti dei materiali alle resistenze."))), "Azioni di progetto già combinate · assi locali"),
            new ViewportFrame("Sezione · riferimenti geometrici", shearView, shearView.ResetView), detailTabs,
            Panel("Combinazioni e resistenze",Ui.Dock(WithFilters(shearGrid),bottom:Ui.Stack(buttons,shearSummary))), tableMinimumHeight: 280);
    }
    private JsonRow ShearRow(JsonObject values)
    {
        return new(ConcreteActionPresentation.ShearRow(values), _ =>
        {
        if (shearGrid is null || synchronizing || UnifiedActions) return;
        ShearOptions["azioni"] = ConcreteSheetPreparation.StoreShearRows(shearGrid.Rows.Select(r => r.Values));
        InvalidateActions("Taglio");
        });
    }
    private void InvalidateShear()
    {
        using var notifications = JsonRow.DeferNotifications(shearGrid?.Rows.AsEnumerable() ?? Enumerable.Empty<JsonRow>());
        shearResults.Clear(); torsionResults.Clear(); shearSummary.Text = shearDashboard.Text = shearWorst.Text = ConcreteShearPresentation.Pending;
        shearDetail.Text = ConcreteShearPresentation.DetailPending;
        if (shearGrid is not null) foreach (var row in shearGrid.Rows) foreach (var key in new[] {"VRdx","VRdy","eta_x","eta_y","eta_t","eta_vt","esito"}) row.Output(key,"—");
    }
    private void CalculateShear()
    {
        if (shearGrid is null) return; shearGrid.Commit(); RefreshAutomaticShear();
        using var notifications = JsonRow.DeferNotifications(shearGrid.Rows);
        InvalidateShear();
        Anthea.Application.Concrete.ConcreteShearBatch.Calculate(Input, settings, shearGrid.Rows.Select(r => r.Values).ToArray(), (index, calculated, error) =>
        {
            var row = shearGrid.Rows[index]; string id = row.Values.S("id");
            if (calculated is not null)
            {
                shearResults[id] = calculated.Shear;
                if (calculated.Torsion is not null) torsionResults[id] = calculated.Torsion;
                foreach (var (key, value) in ConcreteShearPresentation.Outputs(calculated.Shear, calculated.Torsion)) row.Output(key, value);
            }
            else { shearResults.Remove(id); torsionResults.Remove(id); foreach (var (key, value) in ConcreteShearPresentation.FailedOutputs(error)) row.Output(key, value); }
        });
        shearDashboard.Text = ConcreteShearPresentation.ShearSummary(shearGrid.Rows.Count, shearResults.Values);
        shearSummary.Text = ConcreteShearPresentation.Summary(shearGrid.Rows.Count, shearResults.Values, torsionResults.Values);
        shearWorst.Text = string.Join("\n\n", new[] { 0, 1 }.Select(i => WorstSummary(i == 0 ? "Taglio Vx" : "Taglio Vy", shearGrid.Rows.Count,
            shearGrid.Rows.Select(row => { var check = shearResults.GetValueOrDefault(row.Values.S("id"))?.ElementAtOrDefault(i); return (row.Values.S("nome"), check?.Ratio, check?.Status ?? row.Values.S("esito")); }))));
        if (shearGrid.SelectedItem is null && shearGrid.Rows.Count > 0) shearGrid.SelectedIndex = 0;
        UpdateShearSelection();
        RefreshVerificationSummaries();
    }
    private void UpdateShearSelection()
    {
        if (synchronizing || shearGrid?.IsVisible != true) return;
        try { shearView.Section = Anthea.Application.Concrete.ConcretePreview.PrepareGeometry(Input); } catch (ArgumentException) { shearView.Section = null; }
        shearView.InvalidateVisual();
        if (shearGrid?.SelectedItem is not JsonRow row) { shearDetail.Text = ConcreteShearPresentation.NoSelection; return; }
        string id = row.Values.S("id");
        shearDetail.Text = ConcreteShearPresentation.Detail(row.Values, ShearOptions, Input.S("shape"), shearResults.GetValueOrDefault(id), torsionResults.GetValueOrDefault(id));
    }
}
