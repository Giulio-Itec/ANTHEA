using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private readonly Dictionary<string, Ntc2018Checks.ShearResult[]> shearResults = new();
    private readonly TextBlock shearSummary = Ui.Text("Taglio · da calcolare", 12);
    private readonly TextBlock shearDashboard = Ui.Text("Taglio · da calcolare", 12);
    private readonly TextBlock shearDetail = Ui.Text("Selezionare una combinazione", 13);
    private readonly ConcreteSectionViewport shearView = new();
    private JsonGrid? shearGrid;
    private InputForm shearForm = null!;
    private JsonObject ShearOptions => settings["taglio"]!.AsObject();
    private UIElement BuildShearPanel()
    {
        if (settings["taglio"] is not JsonObject) settings["taglio"] = new JsonObject();
        var options = ShearOptions;
        foreach (var (key, value) in new[] { ("modello","Con staffe"), ("bw_x",""), ("d_x",""), ("asl_x",""), ("rami_x",""), ("alpha_x","90"), ("cot_x",""),
            ("bw_y",""), ("d_y",""), ("asl_y",""), ("rami_y",""), ("alpha_y","90"), ("cot_y","") })
            if (!options.ContainsKey(key)) options[key] = value;
        if (options["azioni"] is not JsonArray) options["azioni"] = new JsonArray();
        void EnableFields()
        {
            bool stirrups = options.S("modello") == "Con staffe";
            foreach (var axis in new[] { "x", "y" })
            {
                shearForm.Enable("asl_"+axis, !stirrups);
                foreach (var field in new[] { "rami_", "alpha_", "cot_" }) shearForm.Enable(field+axis, stirrups);
            }
        }
        var fields = new List<Field> { new("modello", "Modello", Choices: ["Con staffe", "Senza staffe"]) };
        foreach (var axis in new[] {"x","y"})
            fields.AddRange([new("bw_"+axis,"bw · "+axis,"mm"),new("d_"+axis,"d utile · "+axis,"mm"),new("asl_"+axis,"Asl ancorata · "+axis,"mm²"),new("rami_"+axis,"Rami staffa · "+axis),new("alpha_"+axis,"α staffa · "+axis,"°"),new("cot_"+axis,"cot θ · "+axis+" (vuoto: auto)")]);
        shearForm = new InputForm(options, fields, _ => { EnableFields(); InvalidateShear(); InvalidateChecks(); }, true, true);
        foreach (var axis in new[] { "x", "y" }) shearForm.GroupFields("Direzione V" + axis, new[] { "bw_", "d_", "asl_", "rami_", "alpha_", "cot_" }.Select(f => f + axis).ToArray(), true);
        EnableFields();
        shearGrid = new JsonGrid([new("nome","Combinazione"),new("N","N [kN]"),new("Vx","Vx [kN]"),new("Vy","Vy [kN]"),new("VRdx","VRd,x [kN]",ReadOnly:true),new("VRdy","VRd,y [kN]",ReadOnly:true),new("eta_x","ηx",ReadOnly:true),new("eta_y","ηy",ReadOnly:true),new("esito","Esito",ReadOnly:true)], true);
        grids.Add(shearGrid);
        shearGrid.Columns[0].MinWidth = 120;
        shearGrid.RowHeight = double.NaN;
        shearGrid.Columns[^1].Width = new DataGridLength(3, DataGridLengthUnitType.Star);
        shearGrid.Columns[^1].MinWidth = 220;
        var outputStyle = new Style(typeof(TextBlock));
        outputStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        ((DataGridTextColumn)shearGrid.Columns[^1]).ElementStyle = outputStyle;
        foreach (var row in options.Array("azioni").OfType<JsonObject>()) shearGrid.Rows.Add(ShearRow((JsonObject)row.DeepClone()));
        void Store() { options["azioni"] = new JsonArray(shearGrid.Rows.Select(r => (JsonNode)J.Obj(("id",r.Values.S("id")),("nome",r.Values.S("nome")),("N",r.Values.S("N")),("Vx",r.Values.S("Vx")),("Vy",r.Values.S("Vy")))).ToArray()); }
        var buttons = Ui.Bar(Ui.Button("+ Combinazione", () => { var row = ShearRow(J.Obj(("id",Guid.NewGuid().ToString("N")),("nome","Taglio "+(shearGrid.Rows.Count+1)),("N","0"),("Vx","0"),("Vy","0"))); shearGrid.Rows.Add(row); shearGrid.SelectedItem = row; Store(); InvalidateShear(); InvalidateChecks(); }),
            Ui.Button("−", () => { shearGrid.Commit(); if (shearGrid.SelectedItem is JsonRow row) shearGrid.Rows.Remove(row); Store(); InvalidateShear(); InvalidateChecks(); }));
        AttachClipboard(shearGrid, () => "Taglio", buttons);
        shearGrid.SelectionChanged += (_, _) => UpdateShearSelection();
        var notice = Notice("NTC 2018 §4.1.2.3.5 · N negativo a compressione. Inserire bw minima, d e Asl efficacemente ancorata per ogni direzione. Ø e passo staffe nei dati comuni. Esiti x/y separati: torsione, interazione biassiale, dettagli e gerarchia sismica non verificati. Per sezioni circolari il fattore 0,75 della vecchia routine richiede una schematizzazione specifica: nessun esito automatico.");
        var upper = Columns((new ViewportFrame("Sezione · riferimenti geometrici", shearView, shearView.ResetView), 4, 260), (Panel("Riepilogo della verifica selezionata", Scroller(shearDetail)), 6, 330));
        return Columns((Panel("Dati del modello a taglio", Scroller(Ui.Stack(shearForm,notice)), "Azioni di progetto già combinate · assi locali"),3,280),
            (Rows(upper, Panel("Combinazioni e resistenze",Ui.Dock(shearGrid,buttons,bottom:shearSummary)), 3, 2),7,680));
    }
    private JsonRow ShearRow(JsonObject values) => new(values, _ =>
    {
        if (shearGrid is null || synchronizing) return;
        ShearOptions["azioni"] = new JsonArray(shearGrid.Rows.Select(r => (JsonNode)J.Obj(("id",r.Values.S("id")),("nome",r.Values.S("nome")),("N",r.Values.S("N")),("Vx",r.Values.S("Vx")),("Vy",r.Values.S("Vy")))).ToArray());
        InvalidateShear(); InvalidateChecks();
    });
    private void InvalidateShear()
    {
        shearResults.Clear(); shearSummary.Text = shearDashboard.Text = "Taglio · da calcolare";
        shearDetail.Text = "Dati modificati · aggiornamento automatico in attesa";
        if (shearGrid is not null) foreach (var row in shearGrid.Rows) foreach (var key in new[] {"VRdx","VRdy","eta_x","eta_y","esito"}) row.Output(key,"—");
    }
    private void CalculateShear()
    {
        if (shearGrid is null) return; shearGrid.Commit(); InvalidateShear();
        foreach (var row in shearGrid.Rows)
        {
            try
            {
                if (settings.S("normativa") != "NTC 2018") throw new ArgumentException("Selezionare NTC 2018 nel pannello di controllo");
                if (Input.S("shape") == "Circolare") throw new ArgumentException("Sezione circolare: modello resistente specifico da validare");
                if (tendons.Rows.Count > 0) throw new ArgumentException("Taglio CAP: includere le componenti di precompressione; modello da definire");
                var geometry = new SezioneCA(Input); double fcd=geometry.Fcd*(Input.S("gettato_sottile")=="Sì"?.8:1);
                bool stirrups=ShearOptions.S("modello")=="Con staffe";
                double n = SectionWorkspace.Number(row.Values.S("N"),"N"), phi = stirrups?Input.Required("transverse_bar_diameter_mm",strict:true):0, spacing=stirrups?Input.Required("transverse_spacing_mm",strict:true):1;
                var results = new List<Ntc2018Checks.ShearResult>();
                foreach (var axis in new[] {"x","y"})
                {
                    double Value(string key) => SectionWorkspace.Number(ShearOptions.S(key+"_"+axis),key+" "+axis);
                    double v=SectionWorkspace.Number(row.Values.S("V"+axis),"V"+axis);
                    double legs=stirrups?Value("rami"):0;
                    if (legs<0 || legs!=Math.Truncate(legs) || stirrups&&legs==0) throw new ArgumentException("Numero rami staffa non valido");
                    double? cot=!stirrups||string.IsNullOrWhiteSpace(ShearOptions.S("cot_"+axis))?null:Value("cot");
                    double bw=Value("bw"), d=Value("d"), asl=stirrups?0:Value("asl");
                    if (bw > (axis=="x"?geometry.Height:geometry.Width) || d >= (axis=="x"?geometry.Width:geometry.Height) || asl > geometry.AreaSteel)
                        throw new ArgumentException("Taglio "+axis+": bw, d o Asl superano la geometria/armatura della sezione");
                    var check=Ntc2018Checks.Shear(n,v,geometry.AreaCls,bw,d,asl,Input.Required("fck_mpa"),fcd,geometry.Fyd,Input.Required("gamma_c"),legs*Math.PI*phi*phi/4,spacing,stirrups?Value("alpha"):90,cot);
                    results.Add(check); row.Output("VRd"+axis,check.VRd.ToString("0.00")); row.Output("eta_"+axis,check.Ratio?.ToString("0.000")??"—");
                }
                shearResults[row.Values.S("id")] = results.ToArray();
                row.Output("esito", string.Join(" · ",results.Select((r,i)=>(i==0?"x: ":"y: ")+r.Status)));
            }
            catch (ArgumentException ex) { foreach (var key in new[] {"VRdx","VRdy","eta_x","eta_y"}) row.Output(key,"—"); row.Output("esito",ex.Message); }
        }
        shearSummary.Text = shearDashboard.Text = $"Taglio: {shearResults.Count}/{shearGrid.Rows.Count} combinazioni calcolate · {shearResults.Values.Count(r=>r.Any(v=>v.Ratio>1))} oltre resistenza · {shearResults.Values.Count(r=>r.Any(v=>v.Ratio is null))} senza esito · dettagli da verificare.";
        if (shearGrid.SelectedItem is null && shearGrid.Rows.Count > 0) shearGrid.SelectedIndex = 0;
        UpdateShearSelection();
    }
    private void UpdateShearSelection()
    {
        try { shearView.Section = new SezioneCA(Input); } catch (ArgumentException) { shearView.Section = null; }
        shearView.InvalidateVisual();
        if (shearGrid?.SelectedItem is not JsonRow row) { shearDetail.Text = "Inserire o selezionare una combinazione N–Vx–Vy."; return; }
        shearDetail.Text = $"{row.Values.S("nome")} · {ShearOptions.S("modello")}\nN = {row.Values.S("N")} kN (compressione negativa)\nVx / Vy = {row.Values.S("Vx")} / {row.Values.S("Vy")} kN\n\n";
        if (!shearResults.TryGetValue(row.Values.S("id"), out var checks)) { shearDetail.Text += row.Values.S("esito", "Da calcolare"); return; }
        for (int i = 0; i < checks.Length; i++)
        {
            var check = checks[i]; string axis = i == 0 ? "x" : "y";
            shearDetail.Text += $"DIREZIONE V{axis}\nbw = {ShearOptions.S("bw_" + axis)} mm · d = {ShearOptions.S("d_" + axis)} mm\n";
            if (ShearOptions.S("modello") == "Con staffe") shearDetail.Text += $"VRsd (staffe) = {check.VRsd:0.##} kN\nVRcd (puntone) = {check.VRcd:0.##} kN\ncot θ = {check.CotTheta:0.###}\nGoverna: {(check.VRsd <= check.VRcd ? "armatura trasversale" : "calcestruzzo compresso")}\n";
            shearDetail.Text += $"VRd = {check.VRd:0.##} kN · η = {check.Ratio?.ToString("0.000") ?? "—"}\n{check.Status}\n\n";
        }
        shearDetail.Text += "Verifiche indipendenti nelle due direzioni; non comprende torsione, interazione biassiale e dettagli costruttivi.";
    }
}
