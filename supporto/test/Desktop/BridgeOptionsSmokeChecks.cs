using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private async Task CheckBridgeOptions(BridgeWorkspace bridge, Func<Task> wait, string directory)
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        void Near(double a, double b, string message) => Check(Math.Abs(a - b) < 1e-10 * Math.Max(1, Math.Abs(b)), message);
        void Edit(InputForm form, string key, double value)
        {
            // Bringing a newly rebuilt phase into a ScrollViewer queues layout. Complete it
            // before requesting keyboard focus, as a user click would do on the visible field.
            Activate(); UpdateLayout(); var text = (TextBox)form.Editors[key]; text.BringIntoView(); UpdateLayout(); text.Focus();
            if (!text.IsKeyboardFocused) File.WriteAllBytes(Path.Combine(directory, "focus-campo.png"), Ui.Snapshot(this));
            Check(text.IsKeyboardFocused, $"{key}: campo non modificabile (visibile={text.IsVisible}, abilitato={text.IsEnabled}, finestra attiva={IsActive}).");
            text.Text = value.ToString("R", CultureInfo.InvariantCulture); bridge.Drawing.Focus();
        }
        void Cleared() => Check(bridge.Calculation is null && bridge.ResultsAreStale && bridge.DisplayedCalculation is not null &&
            (bridge.Pages.SelectedIndex == 0 || bridge.Drawing.Stage is not null && bridge.Drawing.IsStale), "Cambio opzioni perde l'ultimo risultato o lo presenta come corrente.");
        void Current()
        {
            var result = bridge.Calculation!;
            Check(bridge.Pages.SelectedIndex == 0 ? bridge.Drawing.Stage is null : ReferenceEquals(bridge.Drawing.Stage, result.Stages[bridge.StageChoice.SelectedIndex]), "Grafico non aggiornato alla situazione corrente.");
            foreach (string key in new[] { "stato", "classe4", "y_ref", "classe_cls" })
                Check(JsonNode.DeepEquals(result.Input[key], bridge.Data[key]), key + ": risultato di una revisione precedente.");
            Check(JsonNode.DeepEquals(result.Input["fasi"], bridge.Data["fasi"]), "Calcolo non allineato alle fasi correnti.");
            Check(bridge.Pages.SelectedIndex == 0 || bridge.Drawing.LoadMarker is not null, "Punto N assente dalla viewport.");
        }
        var phases = bridge.Data.Array("fasi").OfType<JsonObject>().ToArray();
        var original = phases.Select(p => (JsonObject)p.DeepClone()).ToArray();
        var optionForm = bridge.InputForms.Single(f => f.Editors.ContainsKey("stato"));
        var state = (ComboBox)optionForm.Editors["stato"];
        var class4 = (CheckBox)optionForm.Editors["classe4"];
        InputForm PhaseForm(int i) => bridge.InputForms.Where(f => f.Editors.ContainsKey("N")).ElementAt(i);
        InputForm Homo(int i) => bridge.InputForms.Where(f => f.Editors.ContainsKey("n")).ElementAt(i);
        bridge.Pages.SelectedIndex = 1; bridge.DisplayChoice.SelectedIndex = 0; bridge.StageChoice.SelectedIndex = 2;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        foreach (int i in new[] { 0, 1, 2 }) ((ComboBox)PhaseForm(i).Editors["riferimento_N"]).SelectedItem = BridgeSection.CommonLoadReference;
        await wait();
        Check(optionForm.Editors["y_ref"].IsEnabled, "Quota comune non riattivata quando una fase la seleziona.");
        Check(!bridge.InputForms.Any(f => f.Editors.ContainsKey("modo")), "Selettore del parametro di ingresso ancora presente.");
        foreach (var form in bridge.InputForms.Where(f => f.Editors.ContainsKey("n")))
            foreach (string key in new[] { "n", "phi", "psi" })
                Check(form.Editors[key] is TextBox { IsReadOnly: false, IsEnabled: true, IsVisible: true }, key + ": parametro nascosto o non modificabile.");
        double n0 = BridgeSection.Homogenization(bridge.Data, phases[1]).N0;
        Near(phases[1].D("n"), n0 * (1 + 1.1 * 2), "n iniziale non corrisponde al calcolo.");
        Edit(Homo(0), "phi", 2.34567890123); Cleared();
        Near(phases[1].D("n"), n0 * (1 + 1.1 * 2.34567890123), "φ non aggiorna n.");
        await wait(); Current();
        Near(bridge.Calculation!.Stages.Last().Contributions[1].HomogenizationN, phases[1].D("n"), "n visualizzato diverso da quello usato.");
        Edit(Homo(0), "n", 24.123456789); Cleared();
        double phi = (24.123456789 / n0 - 1) / 1.1;
        Near(phases[1].D("phi"), phi, "n non aggiorna φ.");
        Near(phases[2].D("phi"), original[2].D("phi"), "Una fase altera φ dell'altra.");
        Edit(Homo(0), "psi", 1.4);
        Near(phases[1].D("phi"), phi, "Modifica ψL altera φ.");
        Near(phases[1].D("n"), n0 * (1 + 1.4 * phi), "ψL non aggiorna n.");
        Edit(Homo(1), "n", n0); await wait();
        Near(phases[2].D("phi"), 0, "n₀ non produce φ nullo.");
        Current();
        Edit(Homo(0), "n", 1); Cleared(); await Task.Delay(800);
        Check(bridge.Calculation is null && Homo(0).Get("phi") == "—", "n < n₀ conserva un φ o risultato apparentemente valido.");
        Edit(Homo(0), "n", 24); await wait();
        Edit(Homo(0), "psi", 0); Cleared(); await Task.Delay(800);
        Check(bridge.Calculation is null && Homo(0).Get("n") == "—", "ψL nullo non segnalato.");
        Edit(Homo(0), "psi", original[1].D("psi")); Edit(Homo(0), "phi", original[1].D("phi"));
        Edit(Homo(1), "n", 12.345678901); await wait();
        bridge.Pages.SelectedIndex = 0;
        var material = (ComboBox)bridge.InputForms.Single(f => f.Editors.ContainsKey("classe_cls")).Editors["classe_cls"];
        string oldMaterial = bridge.Data.S("classe_cls"); material.SelectedItem = "C30/37"; Cleared(); await wait();
        Near(phases[1].D("phi"), original[1].D("phi"), "Cambio materiale non conserva φ assegnato.");
        Near(phases[2].D("n"), 12.345678901, "Cambio materiale non conserva n assegnato.");
        Near(phases[2].D("phi"), (phases[2].D("n") / BridgeSection.Homogenization(bridge.Data, phases[2]).N0 - 1), "Cambio materiale non aggiorna φ da n.");
        Current(); material.SelectedItem = oldMaterial; await wait(); bridge.Pages.SelectedIndex = 1;
        Edit(Homo(1), "phi", original[2].D("phi")); await wait();

        Edit(PhaseForm(2), "N", -500); Edit(optionForm, "y_ref", -600); await wait(); Current();
        Check(bridge.Drawing.LoadPoints.All(p => p.Y == -600), "Quota dei marcatori diversa dal calcolo.");
        var lowMarker = bridge.Drawing.LoadMarker!.Value;
        File.WriteAllBytes(Path.Combine(directory, "ponte_N_eccentrico.png"), Ui.Snapshot(bridge.Drawing));
        Edit(optionForm, "y_ref", 600); await wait();
        Check(bridge.Drawing.LoadMarker is { } highMarker && highMarker.Y < lowMarker.Y && highMarker.Y > 0 && highMarker.Y < bridge.Drawing.ActualHeight,
            "Marcatore N esterno alla sezione non inquadrato o non aggiornato.");
        File.WriteAllBytes(Path.Combine(directory, "ponte_N_esterno.png"), Ui.Snapshot(bridge.Drawing));
        ((TextBox)optionForm.Editors["y_ref"]).Text = "invalido"; Cleared(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        Check(bridge.Drawing.IsStale && bridge.Drawing.LoadMarker is not null && bridge.Calculation is null, "Quota N invalida perde il grafico precedente o lo presenta come valido.");
        Edit(optionForm, "y_ref", 0); Edit(PhaseForm(2), "N", original[2].D("N")); await wait();

        var effective = bridge.Calculation!.Stages.Last();
        class4.IsChecked = false; Cleared(); await wait(); Current();
        var gross = bridge.Calculation!.Stages.Last();
        Near(gross.Effective.WebTop + gross.Effective.WebBottom, bridge.Calculation.Geometry.WebHeight, "Classe 4 disattivata lascia anima inefficace.");
        Check(effective.EffectiveSteel.Area < gross.EffectiveSteel.Area, "Cambio classe 4 non aggiorna l'area.");
        Check(!effective.Points.Select(p => p.Stress).SequenceEqual(gross.Points.Select(p => p.Stress)), "Cambio classe 4 non aggiorna le tensioni.");
        foreach (string limit in new[] { "SLE rara", "SLE quasi permanente", "SLU" })
        {
            bridge.Results.SelectedIndex = 4; var oldTables = Ui.Descendants<DataGrid>(bridge).ToArray();
            state.SelectedItem = limit; Cleared(); await wait(); Current();
            var stage = bridge.Calculation!.Stages.Last();
            double expected = limit == "SLU" ? .85 * 35 / 1.5 : limit == "SLE rara" ? .6 * 35 : .45 * 35;
            Near(stage.Points.First(p => p.Material == "CLS").Limit, expected, "Limite CLS non aggiornato: " + limit);
            Check(stage.Points.Zip(gross.Points).All(p => Math.Abs(p.First.Stress - p.Second.Stress) < 1e-7), "Scelta limiti altera il campo elastico.");
            Check(Ui.Descendants<DataGrid>(bridge).All(t => !oldTables.Contains(t)), "Tabelle non ricreate dopo cambio limiti.");
        }
        bridge.StageChoice.SelectedIndex = 1;
        ((CheckBox)PhaseForm(0).Editors["attiva"]).IsChecked = false; Cleared(); await wait();
        Check(bridge.StageChoice.SelectedIndex == 0 && bridge.Drawing.Stage?.Name == phases[1].S("nome"), "Disattivare una fase precedente cambia la situazione visualizzata.");
        ((CheckBox)PhaseForm(0).Editors["attiva"]).IsChecked = true; await wait();
        Check(bridge.StageChoice.SelectedIndex == 1 && bridge.Drawing.Stage?.Name == phases[1].S("nome"), "Riattivare una fase precedente cambia la situazione visualizzata.");
        bridge.StageChoice.SelectedIndex = 2;
        ((ComboBox)PhaseForm(2).Editors["tipo"]).SelectedItem = "Soletta esclusa"; Cleared(); await wait(); Current();
        Check(bridge.Calculation!.Stages.Last().Contributions.Last().Kind == "Soletta esclusa" &&
            bridge.Calculation.Stages.Last().Points.Where(p => p.Material == "CLS").All(p => p.Contributions.Last() == 0), "Cambio sezione reagente non aggiornato.");
        ((ComboBox)PhaseForm(2).Editors["tipo"]).SelectedItem = "Solo acciaio"; Cleared(); await Task.Delay(800);
        Check(bridge.Calculation is null && bridge.Drawing.Stage is not null && bridge.Drawing.IsStale, "Sequenza di fasi invalida perde il precedente grafico o non lo segnala.");
        ((ComboBox)PhaseForm(2).Editors["tipo"]).SelectedItem = "Composta"; await wait();

        // Change options after the worker has captured its input, before it can publish the result.
        var pending = bridge.CalculateAsync(false);
        class4.IsChecked = true; state.SelectedItem = "SLE rara"; state.SelectedItem = "SLE quasi permanente";
        Cleared(); await pending; await wait(); Current();
        Check(bridge.Calculation!.Input.B("classe4") && bridge.Calculation.Input.S("stato") == "SLE quasi permanente", "Un calcolo superato ripopola la vista.");
        state.SelectedItem = "SLU"; await wait(); Current();
        Edit(Homo(0), "phi", original[1].D("phi")); Edit(Homo(1), "phi", original[2].D("phi")); await wait();
        for (int i = 0; i < phases.Length; i++) ((ComboBox)PhaseForm(i).Editors["riferimento_N"]).SelectedItem = original[i].S("riferimento_N");
        await wait();
        Homo(0).Editors["n"].BringIntoView(); bridge.Results.SelectedIndex = 1;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "ponte_omogeneizzazione.png"), Ui.Snapshot(this));
        File.WriteAllText(Path.Combine(directory, "opzioni-smoke.txt"), $"OK: {checks} controlli su n/φ/ψL, materiali, dati invalidi, punto N interno/esterno, classe 4, SLU/SLE, tabelle, fase selezionata, sezione reagente e revisioni concorrenti.");
    }
}
