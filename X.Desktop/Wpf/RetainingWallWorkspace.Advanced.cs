using Anthea.Application.Walls;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.RetainingWall;
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
        var grid = GridFor(RetainingWallFields.ServiceLayerColumns, layers, height: 140);
        return Ui.Stack(Group("Portanza sismica · inerzia del terreno di fondazione", Ui.Stack(
            Ui.Text("EN 1998-5 allegato F · fondazione nastriforme su terreno granulare asciutto e base ruvida. L’accelerazione è quella del terreno, prima della riduzione β del muro. Non include liquefazione.", 11, color: Ui.Muted),
            Form("bearing_seismic", RetainingWallFields.BearingSeismic),
            Ui.Text("Da sito: ah/g=ag/g·Ss·St, |av|/g=0,5 ah/g. γRD: 1 per sabbia medio densa; 1,15 per sabbia sciolta asciutta. γR della combinazione applicato separatamente alle azioni normalizzate. Ricoprimento favorevole trascurato.", 11)), false),
            Group("Cedimenti e spostamenti in esercizio", Ui.Stack(
                Ui.Text("1. Attivare la verifica. 2. Inserire gli strati sotto il piano di posa e M da indagini geotecniche. 3. Assegnare il peso di terreno rimosso. Le righe non vengono riempite con moduli inventati.", 11),
                Form("serviceability", RetainingWallFields.Serviceability),
                grid, Ui.Bar(Ui.Button("+ Strato", () => AddRow(grid, layers, RetainingWallPresentation.NewServiceLayer())),
                    Ui.Button("− Strato", () => { if (grid.SelectedItem is JsonRow row) { layers.Remove(row.Values); grid.Rows.Remove(row); Changed(); } })),
                Ui.Text("Modello edometrico: s=∫Δσz/M dz; tensioni da fondazione nastriforme. Integrare fino a Δσz ≤10% del carico netto o substrato rigido. Rotazione stimata dal profilo libero; spostamento totale disaccoppiato = flessione + H/K − rotazione·(H + t), con la rotazione rigida della fondazione attorno alla base. Limiti modificabili dal progettista.", 11, color: Ui.Muted)), false));
    }

    private UIElement BuildGravityMaterial() => Group("Materiale del muro a gravità", Ui.Stack(
        Form("gravity_design", RetainingWallFields.GravityDesign),
        Ui.Text("CLS: proprietà dal materiale GPC e NTC 4.1.11. Muratura: blocco compresso 0,85 fk/(γM·FC), scorrimento dei giunti. Mensola libera: EI minimo e amplificazione 1/(1−N/Ncr); calcolo disponibile in assenza di trazione. Fessurazione o N≥0,8Ncr richiedono un modello non lineare. Fondazione dello stesso materiale.", 11, color: Ui.Muted)), false);

    private UIElement BuildDetailing()
    {
        if (designPreview.Parent is Panel parent) parent.Children.Remove(designPreview);
        designPreview.Content = null;
        DesignButton = Ui.Button("Calcola armature", async () => await DesignRebarAsync());
        return Ui.Stack(Group("Distinta ferri del tratto di muro", Ui.Stack(
            Form("bar_schedule", RetainingWallFields.BarSchedule),
            Ui.Text("La lunghezza serve al conteggio dei pezzi; il calcolo strutturale resta per metro. Per i collegamenti delle due zone assegnare lo sviluppo dopo aver definito sagoma e ganci.", 11),
            Ui.Bar(Ui.Button("Tavola armature e distinta ferri…", ShowBarSchedule, inspection: true))), false),
            Group("Ancoraggi, sovrapposizioni e predimensionamento", Ui.Stack(
            Form("detailing", RetainingWallFields.Detailing),
            Ui.Text("Il pulsante ricerca barre simmetriche per zona e le verifica con GPC in SLU e SLE. La proposta resta separata finché si preme Applica. Diametri, numero barre e facce restano modificabili. Controllare ingombro delle pieghe e giunzioni.", 11),
            Ui.Bar(DesignButton, Ui.Button("Interrompi ricerca", () => { recalculation.Invalidate(); status.Text = RetainingWallPresentation.DesignInterrupted; })), designPreview), false));
    }

    internal Task DesignRebarAsync() => scheduledCalculation = DesignRebarScheduledAsync();
    private async Task DesignRebarScheduledAsync()
    {
        if (Busy || disposed) return; Commit(); int request = revision;
        var snapshot = (JsonObject)Data.DeepClone();
        var completed = await recalculation.ScheduleAsync(async token =>
        {
            Busy = true; DesignButton.IsEnabled = false; status.Text = RetainingWallPresentation.DesignRunning;
            try { return await Task.Run(() => Anthea.Application.Modules.StructuralQueries.WallReinforcement(snapshot, token), token); }
            finally { Busy = false; if (!disposed) DesignButton.IsEnabled = true; }
        });
        completed.TryPublish(current =>
        {
            try
            {
                if (current.Error is not null) throw current.Error;
                var proposal = current.Value!;
                var table = CheckTable(Envelope(proposal.Checks)); table.Height = 200;
                var bars = Table([.. RetainingWallPresentation.ProposalColumns], RetainingWallPresentation.ProposalRows(proposal)); bars.Height = 140;
                designPreview.Content = Ui.Stack(Ui.Text(proposal.Message, 12, true), bars, table, Ui.Button("Applica proposta", () =>
                {
                    if (request != revision) { designPreview.Content = Ui.Text(RetainingWallPresentation.DesignStale, 12); return; }
                    Data["reinforcement"] = proposal.Input["reinforcement"]!.DeepClone(); Data["detailing"] = proposal.Input["detailing"]!.DeepClone();
                    BuildInputs(); UpdateFields(); Changed(); ViewMode.SelectedItem = "Armature"; CheckFilter.SelectedItem = "Dettagli armature";
                }));
                status.Text = proposal.Message;
            }
            catch (Exception ex) { status.Text = RetainingWallPresentation.DesignError(ex.Message); }
        });
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
                host.Children.Add(Group(record.S("name"), Ui.Stack(Form("history_" + index, RetainingWallFields.History, record),
                    Ui.Text(RetainingWallPresentation.HistorySamples(record), 11),
                    Ui.Bar(Ui.Button("Importa CSV t; a/g", () => ImportHistory(record, Rebuild)),
                        Ui.Button("Elimina accelerogramma", () => { histories.Remove(record); Rebuild(); Changed(); }))), false));
            }
        }
        Rebuild();
        return Group("Spostamenti permanenti · Newmark SLD / SLV", Ui.Stack(
            Ui.Text("Occorrono un accelerogramma e ky/g, cioè l’accelerazione alla quale inizia lo scorrimento, derivata da un’analisi di equilibrio del muro. Il solo ag/g non basta. Integrazione monodirezionale verso valle; modello non applicabile a Wood o a muri vincolati. La verifica della singola storia non sostituisce la selezione normativa dell’insieme di accelerogrammi.", 11),
            Ui.Button("+ Accelerogramma", () => { histories.Add(RetainingWallPresentation.NewHistory(histories.Count)); Rebuild(); Changed(); }), host), false);
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
    internal static JsonArray ParseHistory(string text) => Anthea.Application.Presentation.WallSheetPresentation.ParseHistory(text);

    private FrameworkElement AdvancedResults(string mode, WallModel.Result r, WallModel.LoadCase c)
    {
        if (mode == "Dettagli armature")
        {
            var detail = r.Detailing;
            if (detail is null) return Ui.Text(RetainingWallPresentation.DetailingUnavailable, 12);
            var bars = Table([.. RetainingWallPresentation.DetailingColumns], RetainingWallPresentation.DetailingRows(detail)); bars.Height = 220;
            var checksGrid = CheckTable(detail.Checks); checksGrid.Height = 220;
            return Ui.Stack(Ui.Text(detail.Note, 11), Ui.Text(RetainingWallPresentation.DetailingSteel(detail), 12, true), bars, checksGrid);
        }
        if (mode == "Portanza sismica")
        {
            if (c.State != "SISMA") return Ui.Text(RetainingWallPresentation.SeismicBearingSelect, 12);
            if (c.SeismicBearing is not { } b) return Ui.Text(c.SeismicBearingError ?? RetainingWallPresentation.SeismicBearingUnavailable, 12);
            return Ui.Stack(Ui.Text(RetainingWallPresentation.SeismicBearingMethod, 11),
                Table([.. RetainingWallPresentation.SeismicBearingColumns], [RetainingWallPresentation.SeismicBearingRow(b)]));
        }
        var service = r.Serviceability; var panel = Ui.Stack();
        if (service is null) return Ui.Text(RetainingWallPresentation.ServiceUnavailable, 12);
        var summaryGrid = Table([.. RetainingWallPresentation.ServiceColumns], RetainingWallPresentation.ServiceRows(service)); summaryGrid.Height = 180; panel.Children.Add(summaryGrid);
        var current = service.Cases.FirstOrDefault(s => s.Combination == c.Name);
        if (current is not null)
        {
            panel.Children.Add(Group(RetainingWallPresentation.SettlementTitle, Table([.. RetainingWallPresentation.SettlementColumns], RetainingWallPresentation.SettlementRows(current)), false));
            panel.Children.Add(Group(RetainingWallPresentation.ShapeTitle, Table([.. RetainingWallPresentation.ShapeColumns], RetainingWallPresentation.ShapeRows(current)), false));
        }
        foreach (var history in service.Earthquakes)
        {
            panel.Children.Add(Ui.Text(RetainingWallPresentation.EarthquakeText(history), 12));
            if (history.History is { } values) panel.Children.Add(Group(RetainingWallPresentation.EarthquakeTitle(history), Table([.. RetainingWallPresentation.EarthquakeColumns], RetainingWallPresentation.EarthquakeRows(values)), false));
        }
        return panel;
    }
}
