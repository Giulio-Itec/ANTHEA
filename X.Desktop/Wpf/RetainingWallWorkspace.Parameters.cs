using Anthea.Application.Walls;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    private readonly List<Anthea.Application.Scheduling.RecalculationScheduler> parameterSessions = [];
    private Task parameterApplyTask = Task.CompletedTask;
    internal Window CalculationParametersWindow()
    {
        Commit(); int openedRevision = revision; bool closed = false;
        var scheduler = new Anthea.Application.Scheduling.RecalculationScheduler(); parameterSessions.Add(scheduler);
        var window = new Window { Owner = Window.GetWindow(this), Title = "Muro · valori utilizzati nel calcolo", Width = 1150, Height = 780, MinWidth = 780, MinHeight = 530, Background = Appearance.Surface, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Appearance.Watch(window);
        window.Closed += (_, _) => { closed = true; scheduler.Dispose(); parameterSessions.Remove(scheduler); };
        var rows = new JsonGrid([new("group", "Gruppo", ReadOnly: true), new("meaning", "Parametro", ReadOnly: true), new("value", "Valore assegnato"), new("unit", "Unità", ReadOnly: true)]) { Height = 230 };
        rows.Columns[0].Width = 270; rows.Columns[1].Width = 340; rows.Columns[2].Width = 190;
        var parameters = new Anthea.Application.Presentation.WallParameterTable(Data);
        foreach (var row in parameters.Rows) rows.Rows.Add(new JsonRow(row));
        var combo = Ui.Choice(Calculation?.Cases.Select(c => c.Name) ?? [], Combination.SelectedItem as string ?? Calculation?.Cases.FirstOrDefault()?.Name ?? "");
        var derived = new ContentControl();
        void Display()
        {
            if (Calculation?.Cases.FirstOrDefault(c => c.Name == combo.SelectedItem as string) is not { } c) { derived.Content = Ui.Text("Ricalcolare per interrogare i valori derivati.", 12); return; }
            var values = new List<string[]>();
            void Add(string key, string value, string source) => values.Add([key, value, source]);
            foreach (var p in c.SoilAudit) { var meaning = WallModel.AuditMeaning(p.Key); Add(meaning.Label, (J.Number(p.Value) is double n ? n.ToString("0.#####", It) : p.Value?.ToString() ?? "") + " " + meaning.Unit, meaning.Origin); }
            foreach (var p in c.Factors ?? new()) if (p.Value is JsonValue) Add(p.Key, p.Value.ToString(), "Coefficiente della combinazione selezionata");
            foreach (var p in c.PressureDetails) Add($"Monte z={F(p.Z0)}–{F(p.Z1)} m", $"φd={F(p.PhiDesign)}°; K={p.K:0.00000}; Ke={p.Ke:0.00000}; σ′={F(p.Sigma0)}–{F(p.Sigma1)}; p={F(p.Total0)}–{F(p.Total1)} kPa", "Piano esterno di equilibrio; acqua e incremento inclusi");
            foreach (var p in c.StemPressureDetails) Add($"Coefficienti fusto z={F(p.Z0)}–{F(p.Z1)} m", $"φd={F(p.PhiDesign)}°; K={p.K:0.00000}; Ke={p.Ke:0.00000}; Δp={F(p.Dynamic)} kPa", "Coulomb/MO con δ muro, oppure K₀ Wood");
            foreach (var p in c.StemPressures) Add($"Fusto z={F(p.Z0)}–{F(p.Z1)} m", $"p={F(p.P0)}–{F(p.P1)} kPa", "Coulomb/MO al paramento + acqua + azioni − passiva");
            foreach (var p in c.ValleyPressures) Add($"Valle z={F(p.Z0)}–{F(p.Z1)} m", $"p={F(p.P0)}–{F(p.P1)} kPa", "Passiva effettivamente utilizzata · z dalla sommità muro");
            Add("Equilibrio", $"H={F(c.Horizontal)}; V′={F(c.Vertical)}; U={F(c.Uplift)} kN/m; Mstab={F(c.Stabilizing)}; Mrib={F(c.Overturning)} kNm/m", "Somma forze e momenti");
            Add("Fondazione", $"e={F(c.Eccentricity)} m; B′={F(c.EffectiveWidth)} m; Rsc={F(c.SlidingResistance)} kN/m; Rport={c.BearingResistance?.ToString("0.###") ?? "non disponibile"} kN/m", "Contatto e resistenze");
            foreach (var p in c.Sections) Add($"{p.Name} z/l={F(p.Position)} m", $"N={F(p.N)} kN/m; M={F(p.M)} kNm/m; V={F(p.V)} kN/m", "Equilibrio della sezione · modificare le cause negli input");
            var table = Table(["Valore", "Risultato utilizzato", "Origine / significato"], values); table.Height = 230; derived.Content = table;
        }
        combo.SelectionChanged += (_, _) => Display(); Display();
        var message = Ui.Text("Modificare gli input nella colonna Valore. Per cambiare un coefficiente derivato selezionare la relativa modalità Assegnato. Le forze risultanti sono consultabili; si aggiornano ricalcolando le loro cause.", 11);
        async Task Apply()
        {
            if (closed || !window.IsEnabled) return;
            rows.Commit();
            try
            {
                if (disposed || revision != openedRevision) throw new ArgumentException("Il foglio è cambiato: chiudere e riaprire il riepilogo.");
                window.IsEnabled = false; message.Text = "Controllo dati e ricalcolo…";
                var completed = await scheduler.ScheduleAsync(token => parameters.ValidateAsync(calculationService, token));
                bool applied = false;
                completed.TryPublish(current =>
                {
                    if (disposed || closed) return;
                    if (revision != openedRevision) throw new ArgumentException("Il foglio è cambiato: chiudere e riaprire il riepilogo.");
                    if (current.Error is not null) throw current.Error;
                    Data.Clear(); foreach (var p in current.Value!) Data[p.Key] = p.Value?.DeepClone();
                    BuildInputs(); BuildMatrix(); UpdateFields(); Changed(); applied = true;
                });
                if (applied) { window.Close(); await CalculateAsync(); }
            }
            catch (Exception ex) { if (!closed && !disposed) { message.Text = ex.Message; message.Foreground = Appearance.Foreground(Brushes.Firebrick); } }
            finally { if (!closed) window.IsEnabled = true; }
        }
        var apply = Ui.Button("Applica e ricalcola", () => { parameterApplyTask = Apply(); });
        var body = Ui.Stack(Ui.Text("Input modificabili e valori effettivamente utilizzati", 18, true), message, rows, Ui.Bar(Ui.Text("Combinazione", 12), combo), derived, Ui.Bar(apply, Ui.Button("Chiudi", window.Close)));
        body.Background = Appearance.Surface; body.Margin = new Thickness(15); window.Content = new Border { Background = Appearance.Surface, Child = Scroll(body) }; return window;
    }
    private async Task ShowCalculationParameters() { await CalculateAsync(); if (!disposed) CalculationParametersWindow().ShowDialog(); }
}
