using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    internal readonly ContentControl designPreview = new();
    internal Button DesignButton = null!;
    private UIElement BuildAdvancedSoil()
    {
        var layers = Data["serviceability"]!.Array("layers");
        var grid = GridFor([new("name", "Strato sotto posa"), new("thickness", "Spessore [m]"), new("modulus", "M edometrico [kPa]")], layers, height: 140);
        return Ui.Stack(Group("Portanza sismica · inerzia del terreno di fondazione", Ui.Stack(
            Ui.Text("EN 1998-5 allegato F · fondazione nastriforme su terreno granulare asciutto e base ruvida. L’accelerazione è quella del terreno, prima della riduzione β del muro. Non include liquefazione.", 11, color: Ui.Muted),
            Form("bearing_seismic", [new("source", "Accelerazione fondazione", Choices: ["Da sito", "Assegnata"]), new("ground_kh", "Accelerazione orizzontale / g", "−"), new("ground_kv", "Accelerazione verticale / g (modulo)", "−"), new("model_factor", "Fattore modello γRD", "−")]),
            Ui.Text("Da sito: ah/g=ag/g·Ss·St, |av|/g=0,5 ah/g. γRD: 1 per sabbia medio densa; 1,15 per sabbia sciolta asciutta. γR della combinazione applicato separatamente alle azioni normalizzate. Ricoprimento favorevole trascurato.", 11)), false),
            Group("Cedimenti e spostamenti in esercizio", Ui.Stack(
                Ui.Text("1. Attivare la verifica. 2. Inserire gli strati sotto il piano di posa e M da indagini geotecniche. 3. Assegnare il peso di terreno rimosso. Le righe non vengono riempite con moduli inventati.", 11),
                Form("serviceability", [new("settlement", "Calcola cedimenti finali", Bool: true), new("displacement", "Calcola spostamenti in testa", Bool: true),
                    new("removed_pressure", "Pressione del terreno rimosso", "kPa"), new("rigid_base", "Substrato rigido documentato al fondo degli strati", Bool: true),
                    new("settlement_limit", "Limite cedimento", "mm"), new("rotation_limit", "Limite rotazione", "rad"),
                    new("head_limit", "Limite spostamento in testa", "mm"), new("horizontal_stiffness", "Rigidezza orizzontale fondazione per metro", "kN/m²")]),
                grid, Ui.Bar(Ui.Button("+ Strato", () => AddRow(grid, layers, J.Obj(("name", "Strato deformabile"), ("thickness", 2), ("modulus", "")))),
                    Ui.Button("− Strato", () => { if (grid.SelectedItem is JsonRow row) { layers.Remove(row.Values); grid.Rows.Remove(row); Changed(); } })),
                Ui.Text("Modello edometrico: s=∫Δσz/M dz; tensioni da fondazione nastriforme. Integrare fino a Δσz ≤10% del carico netto o substrato rigido. Rotazione stimata dal profilo libero; spostamento totale disaccoppiato = flessione + H/K − rotazione·H. Limiti modificabili dal progettista.", 11, color: Ui.Muted)), false));
    }

    private UIElement BuildGravityMaterial() => Group("Materiale del muro a gravità", Ui.Stack(
        Form("gravity_design", [new("type", "Modello materiale", Choices: ["Resistenze assegnate", "Calcestruzzo non armato", "Muratura"]),
            new("fk", "Resistenza caratteristica muratura", "MPa"), new("fvk0", "Resistenza a taglio senza compressione", "MPa"),
            new("fvk_limit", "Limite caratteristico a taglio", "MPa"), new("gamma_m", "Coefficiente γM", "−"),
            new("confidence", "Fattore di confidenza", "−"), new("elastic_modulus", "Modulo elastico muratura / assegnato", "MPa"),
            new("eccentricity", "Imperfezione aggiuntiva (minimo H/200)", "mm"), new("effective_height", "Lunghezza efficace (vuoto = 2H)", "m")]),
        Ui.Text("CLS: proprietà dal materiale GPC e NTC 4.1.11. Muratura: blocco compresso 0,85 fk/(γM·FC), scorrimento dei giunti. Mensola libera: EI minimo e amplificazione 1/(1−N/Ncr); calcolo disponibile in assenza di trazione. Fessurazione o N≥0,8Ncr richiedono un modello non lineare. Fondazione dello stesso materiale.", 11, color: Ui.Muted)), false);

    private static Field[] RebarFields => [
        new("diameter", "Ø monte (fusto) / inferiore (soletta)", "mm"), new("count", "Barre principali per metro", "−"),
        new("symmetric", "Stesse barre sulla faccia opposta", Bool: true),
        new("opposite_diameter", "Ø valle (fusto) / superiore (soletta)", "mm"), new("opposite_count", "Barre opposte per metro", "−"),
        new("secondary_diameter", "Ø rete secondaria su entrambe le facce", "mm"), new("secondary_spacing", "Passo rete secondaria", "mm"),
        new("anchor_length", "Ancoraggio (0 = automatico)", "mm"), new("lap_length", "Sovrapposizione (0 = automatica)", "mm"),
        new("bend_diameter", "Diametro mandrino (0 = automatico)", "mm")];

    private UIElement BuildDetailing()
    {
        if (designPreview.Parent is Panel parent) parent.Children.Remove(designPreview);
        designPreview.Content = null;
        DesignButton = Ui.Button("Calcola armature", async () => await DesignRebarAsync());
        return Ui.Stack(Group("Distinta ferri del tratto di muro", Ui.Stack(
            Form("bar_schedule", [new("panel_length", "Lunghezza del tratto da armare", "m"),
                new("end_cover", "Copriferro alle estremità del tratto", "mm"), new("stock_length", "Lunghezza barra commerciale", "m"),
                new("ties_per_m", "Collegamenti per fila e per metro di muro", "−"), new("tie_cut_length", "Sviluppo collegamento (0 = da definire)", "mm")]),
            Ui.Text("La lunghezza serve al conteggio dei pezzi; il calcolo strutturale resta per metro. Per i collegamenti delle due zone assegnare lo sviluppo dopo aver definito sagoma e ganci.", 11),
            Ui.Bar(Ui.Button("Tavola armature e distinta ferri…", ShowBarSchedule, inspection: true))), false),
            Group("Ancoraggi, sovrapposizioni e predimensionamento", Ui.Stack(
            Form("detailing", [new("enabled", "Verifica i dettagli delle armature", Bool: true),
                new("good_bond", "Condizioni di buona aderenza documentate", Bool: true), new("lap_percent", "Percentuale barre giuntate (schema: 100%)", "%"),
                new("lap_clear", "Distanza libera fra barre giuntate", "mm"), new("tie_diameter", "Ø collegamenti della giunzione", "mm"),
                new("tie_spacing", "Passo collegamenti della giunzione", "mm"), new("max_diameter", "Ø massimo nel predimensionamento", "mm"),
                new("max_count", "Numero massimo barre per metro", "−"), new("target_ratio", "Tasso massimo ricercato", "−")]),
            Ui.Text("Il pulsante ricerca barre simmetriche per zona e le verifica con GPC in SLU e SLE. La proposta resta separata finché si preme Applica. Diametri, numero barre e facce restano modificabili. Controllare ingombro delle pieghe e giunzioni.", 11),
            Ui.Bar(DesignButton, Ui.Button("Interrompi ricerca", () => cancellation?.Cancel())), designPreview), false));
    }

    internal async Task DesignRebarAsync()
    {
        if (Busy || disposed) return; Commit(); timer.Stop(); int request = revision;
        var snapshot = (JsonObject)Data.DeepClone(); cancellation?.Dispose(); cancellation = new(); var token = cancellation.Token;
        Busy = true; DesignButton.IsEnabled = false; status.Text = "Ricerca armature: controlli N–M GPC, taglio, SLE e dettagli…";
        running = Run(); await running;
        async Task Run()
        {
            try
            {
                var proposal = await Task.Run(() => RetainingWall.DesignReinforcement(snapshot, token));
                if (disposed || request != revision) return;
                var table = CheckTable(Envelope(proposal.Checks)); table.Height = 200;
                var bars = Table(["Zona", "Proposta per faccia", "Secondarie Ø / passo"], new[] { "stem", "stem_upper", "toe", "heel" }.Where(k => k != "stem_upper" || proposal.Input["reinforcement"].B("two_zones")).Select(k =>
                {
                    var a = proposal.Input["reinforcement"]![k]!;
                    return new[] { RetainingWall.RebarZoneName(k), $"{a.D("count")} Ø{a.D("diameter")}/m", $"{a.D("secondary_diameter")} / {a.D("secondary_spacing")} mm" };
                })); bars.Height = 140;
                designPreview.Content = Ui.Stack(Ui.Text(proposal.Message, 12, true), bars, table, Ui.Button("Applica proposta", () =>
                {
                    if (request != revision) { designPreview.Content = Ui.Text("Dati modificati: ricalcolare la proposta.", 12); return; }
                    Data["reinforcement"] = proposal.Input["reinforcement"]!.DeepClone(); Data["detailing"] = proposal.Input["detailing"]!.DeepClone();
                    BuildInputs(); UpdateFields(); Changed(); ViewMode.SelectedItem = "Armature"; CheckFilter.SelectedItem = "Dettagli armature";
                }));
                status.Text = proposal.Message;
            }
            catch (OperationCanceledException) { status.Text = "Ricerca interrotta; armature inserite conservate."; }
            catch (Exception ex) { status.Text = "Predimensionamento: " + ex.Message; }
            finally { Busy = false; DesignButton.IsEnabled = true; if (request != revision) timer.Start(); }
        }
    }

    private UIElement BuildHistories()
    {
        var host = Ui.Stack(); var histories = Data["serviceability"]!.Array("histories");
        void Rebuild()
        {
            foreach (string key in Forms.Keys.Where(k => k.StartsWith("history_")).ToArray()) Forms.Remove(key);
            host.Children.Clear();
            foreach (var (record, index) in histories.OfType<JsonObject>().Select((r, i) => (r, i)))
            {
                host.Children.Add(Group(record.S("name"), Ui.Stack(Form("history_" + index, [
                    new("enabled", "Calcola questo accelerogramma", Bool: true), new("name", "Nome"), new("state", "Stato limite", Choices: ["SLD", "SLV"]),
                    new("yield_g", "Accelerazione critica di scorrimento ky/g", "−"), new("scale", "Fattore di scala", "−"), new("limit_mm", "Limite spostamento permanente", "mm"),
                    new("compatible", "Accelerogramma e scala compatibili con il sito e lo stato limite", Bool: true)], record),
                    Ui.Text($"{record.Array("samples").Count} campioni salvati · tempo [s], accelerazione verso valle [g]", 11),
                    Ui.Bar(Ui.Button("Importa CSV t; a/g", () => ImportHistory(record, Rebuild)),
                        Ui.Button("Elimina accelerogramma", () => { histories.Remove(record); Rebuild(); Changed(); }))), false));
            }
        }
        Rebuild();
        return Group("Spostamenti permanenti · Newmark SLD / SLV", Ui.Stack(
            Ui.Text("Occorrono un accelerogramma e ky/g, cioè l’accelerazione alla quale inizia lo scorrimento, derivata da un’analisi di equilibrio del muro. Il solo ag/g non basta. Integrazione monodirezionale verso valle; modello non applicabile a Wood o a muri vincolati. La verifica della singola storia non sostituisce la selezione normativa dell’insieme di accelerogrammi.", 11),
            Ui.Button("+ Accelerogramma", () => { histories.Add(J.Obj(("enabled", true), ("name", "Storia " + (histories.Count + 1)), ("state", "SLD"), ("yield_g", ""), ("scale", 1), ("limit_mm", 20), ("compatible", false), ("samples", new JsonArray()))); Rebuild(); Changed(); }), host), false);
    }
    private void ImportHistory(JsonObject record, Action refresh)
    {
        var dialog = new OpenFileDialog { Filter = "Accelerogramma CSV|*.csv;*.txt" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            var samples = ParseHistory(File.ReadAllText(dialog.FileName)); record["samples"] = samples; refresh(); Changed();
        }
        catch (Exception ex) { status.Text = "Accelerogramma non importato: " + ex.Message; }
    }
    internal static JsonArray ParseHistory(string text)
    {
        var samples = new JsonArray(); int line = 0;
        foreach (var raw in text.Split('\n'))
        {
            line++; string row = raw.Trim(); if (row == "" || row.StartsWith('#')) continue;
            var fields = row.Split([';', '\t'], StringSplitOptions.TrimEntries);
            if (fields.Length != 2 || !double.TryParse(fields[0].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double t)
                || !double.TryParse(fields[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double a))
            { if (samples.Count == 0 && row.StartsWith("t", StringComparison.OrdinalIgnoreCase)) continue; throw new ArgumentException($"Riga {line}: usare due colonne separate da ; (secondi; g)."); }
            if (!double.IsFinite(t + a) || t < 0 || Math.Abs(a) > 10 || samples.Count > 0 && t <= samples[^1].D("t")) throw new ArgumentException($"Riga {line}: tempi crescenti e accelerazioni finite richiesti.");
            if (samples.Count >= 200000) throw new ArgumentException("Massimo 200000 campioni.");
            samples.Add(J.Obj(("t", t), ("a_g", a)));
        }
        if (samples.Count < 2) throw new ArgumentException("Inserire almeno due campioni.");
        return samples;
    }

    private FrameworkElement AdvancedResults(string mode, RetainingWall.Result r, RetainingWall.LoadCase c)
    {
        if (mode == "Dettagli armature")
        {
            var detail = r.Detailing;
            if (detail is null) return Ui.Text("Armature disponibili per muri a mensola in c.a.", 12);
            var bars = Table(["Marca / zona", "Faccia", "n Ø", "As [mm²/m]", "L barra [m]", "lbd richiesto/usato [mm]", "l0 richiesto/usato [mm]", "Mandrino [mm]", "fbd [MPa]"], detail.Bars.Select(b => new[] { b.Mark + " " + RetainingWall.RebarZoneName(b.Zone), b.Face, $"{b.Count} Ø{b.Diameter}", F(b.Area), F(b.Length), F(b.RequiredAnchor) + " / " + F(b.Anchor), F(b.RequiredLap) + " / " + F(b.Lap), F(b.Mandrel), F(b.Fbd) })); bars.Height = 220;
            var checksGrid = CheckTable(detail.Checks); checksGrid.Height = 220;
            return Ui.Stack(Ui.Text(detail.Note, 11), Ui.Text($"Acciaio stimato incluse sovrapposizioni, ancoraggi e secondarie: {detail.SteelKg:0.0} kg/m", 12, true), bars, checksGrid);
        }
        if (mode == "Portanza sismica")
        {
            if (c.State != "SISMA") return Ui.Text("Selezionare una combinazione SISMA.", 12);
            if (c.SeismicBearing is not { } b) return Ui.Text(c.SeismicBearingError ?? "Portanza sismica non disponibile.", 12);
            return Ui.Stack(Ui.Text("EN 1998-5 allegato F · coefficienti: a=c=0,92; b=d=1,25; e=0,41; f=0,32; m=0,96; k=1; k′=0,39; cT=1,14; cM=c′M=1,01; β=2,90; γ=2,80. N,V,M normalizzati con γRD·γR; F=γRD·ah/(g tanφ). Ricerca della capacità lungo il raggio N,V,M.", 11),
                Table(["Nmax [kN/m]", "F", "N̄", "V̄", "M̄", "N̄ limite", "Interazione ≤1", "η proporzionale", "Esito"],
                [new[] { F(b.NMax), F(b.SoilInertia), F(b.NBar), F(b.VBar), F(b.MBar), F(b.VerticalLimit), b.Interaction?.ToString("0.000") ?? "Fuori dominio", b.Ratio?.ToString("0.000") ?? "—", b.Status }]));
        }
        var service = r.Serviceability; var panel = Ui.Stack();
        if (service is null) return Ui.Text("Attivare le verifiche in Terreno.", 12);
        var summaryGrid = Table(["Caso", "s valle/centro/monte [mm]", "Rotazione [rad]", "u fusto/totale [mm]", "Stato"], service.Cases.Select(s => new[] { s.Combination, $"{s.ToeSettlement:0.###} / {s.CentreSettlement:0.###} / {s.HeelSettlement:0.###}", $"{s.FoundationRotation:0.000000}", $"{s.StemDisplacement:0.###} / {s.HeadDisplacement:0.###}", s.Status })); summaryGrid.Height = 180; panel.Children.Add(summaryGrid);
        var current = service.Cases.FirstOrDefault(s => s.Combination == c.Name);
        if (current is not null)
        {
            panel.Children.Add(Group("Integrazione cedimento · verticale centrale", Table(["Strato", "z₀–z₁ [m]", "Δσ [kPa]", "M [kPa]", "Δs [mm]"], current.Slices.Select(s => new[] { s.Soil, $"{s.Top:0.###}–{s.Bottom:0.###}", F(s.Stress), F(s.Modulus), F(s.SettlementMm) })), false));
            panel.Children.Add(Group("Curvature e spostamenti del fusto", Table(["y dal piede [m]", "κ [1/m]", "θ [rad]", "u [mm]"], current.Shape.Select(s => new[] { F(s.Y), $"{s.Curvature:0.000000}", $"{s.Rotation:0.000000}", F(s.DisplacementMm) })), false));
        }
        foreach (var history in service.Earthquakes)
        {
            panel.Children.Add(Ui.Text($"{history.Name} · {history.State} · ky/g={history.YieldG:0.####}; scala={history.Scale:0.###}; d={history.History?.DisplacementMm:0.###} mm / limite={history.LimitMm} mm · {history.Status}", 12));
            if (history.History is { } values) panel.Children.Add(Group("Storia integrata " + history.Name, Table(["t [s]", "a/g", "v [m/s]", "d [mm]"], values.Points.Select(p => new[] { F(p.Time), F(p.AccelerationG), F(p.Velocity), F(p.DisplacementMm) })), false));
        }
        return panel;
    }
}
