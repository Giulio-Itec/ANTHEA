using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private async Task CheckBridgeGeometryView(BridgeWorkspace bridge, Func<Task> wait, string directory)
    {
        int count = 0;
        void Check(bool value, string message) { count++; if (!value) throw new Exception(message); }
        async Task Layout() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        int originalPage = bridge.Pages.SelectedIndex, originalView = bridge.DisplayChoice.SelectedIndex, originalResult = bridge.Results.SelectedIndex;
        var originalCalculation = bridge.Calculation;
        bridge.Pages.SelectedIndex = 0; await Layout();
        Check(bridge.Drawing.Stage is null && bridge.Drawing.Mode == 2 && !bridge.StageChoice.IsVisible && !bridge.DisplayChoice.IsVisible && !bridge.Results.IsVisible,
            "Pannello di controllo non dedicato alla geometria.");
        Check(ReferenceEquals(bridge.Calculation, originalCalculation), "Cambio pagina ricalcola.");
        Check(bridge.Drawing.VisibleTags.Count == 7, "Devono comparire soletta, tre piastre, anima e due file.");
        Check(bridge.Drawing.VisibleTags.Any(t => t.Contains("Anima") && t.Contains("1800 × 14")), "Tag anima non corrisponde all'ingresso.");
        Check(bridge.Drawing.VisibleTags.Any(t => t.Contains("inferiore 2") && t.Contains("500 × 20")), "Tag seconda piastra non corrisponde all'ingresso.");
        Check(bridge.Drawing.VisibleTags.Count(t => t.Contains("Ø16 / 150") && t.Contains("20 barre")) == 2, "Informazioni delle file incomplete.");
        foreach (var size in new[] { (1600d, 990d), (1366d, 768d) })
        {
            Width = size.Item1; Height = size.Item2; await Layout();
            var boxes = bridge.Drawing.TagBounds;
            Check(boxes.All(b => b.Left >= 0 && b.Top >= 0 && b.Right <= bridge.Drawing.ActualWidth && b.Bottom < bridge.Drawing.ActualHeight), "Tag fuori dal disegno.");
            for (int i = 0; i < boxes.Count; i++) for (int j = 0; j < i; j++) Check(!boxes[i].IntersectsWith(boxes[j]), "Tag sovrapposti.");
            File.WriteAllBytes(Path.Combine(directory, $"geometria_tag_{size.Item1:0}.png"), Ui.Snapshot(this));
        }
        Width = 1600; Height = 990; await Layout();
        bridge.GeometryLabels.IsChecked = false; await Layout(); Check(bridge.Drawing.VisibleTags.Count == 2, "Disattivare quote nasconde anche le info armature.");
        bridge.RebarLabels.IsChecked = false; await Layout(); Check(bridge.Drawing.VisibleTags.Count == 0 && bridge.Drawing.Geometry!.Bars.Length == 40, "Disattivare info armature altera la geometria.");
        File.WriteAllBytes(Path.Combine(directory, "geometria_senza_tag.png"), Ui.Snapshot(this));
        bridge.GeometryLabels.IsChecked = true; await Layout(); Check(bridge.Drawing.VisibleTags.Count == 5, "Quote non riattivate indipendentemente dalle armature.");
        bridge.RebarLabels.IsChecked = true; await Layout();
        bridge.Pages.SelectedIndex = 1; bridge.DisplayChoice.SelectedIndex = 0; bridge.Results.SelectedIndex = 3; await Layout();
        var second = bridge.InputForms.Where(f => f.Editors.ContainsKey("N")).ElementAt(1);
        var reference = (ComboBox)second.Editors["riferimento_N"];
        Check(reference.SelectedItem?.ToString() == BridgeSection.GrossLoadReference, "Nuova fase senza default al baricentro lordo.");
        Check(!bridge.InputForms.Single(f => f.Editors.ContainsKey("y_ref")).Editors["y_ref"].IsEnabled, "Quota comune modificabile quando nessuna fase la usa.");
        second.Set("N", "-350"); await wait();
        var fixedStage = bridge.Calculation!.Stages.Last(); var fixedPoint = fixedStage.Contributions[1].LoadY;
        reference.SelectedItem = BridgeSection.EffectiveLoadReference; await wait();
        var iterative = bridge.Calculation!.Stages.Last().Contributions[1];
        Check(Math.Abs(iterative.LoadY - iterative.Centroid) < 1e-9 && Math.Abs(iterative.LoadY - fixedPoint) > .01, "Riferimento iterativo non segue il baricentro efficace.");
        Check(bridge.Drawing.LoadPoints.Length == 3 && bridge.Drawing.LoadPoints[1].Y == iterative.LoadY, "Punti N della vista non allineati ai contributi.");
        File.WriteAllBytes(Path.Combine(directory, "riferimenti_N_per_fase.png"), Ui.Snapshot(this));
        var retained = bridge.Calculation; var retainedStage = bridge.Drawing.Stage; var retainedGeometry = bridge.Drawing.Geometry;
        var oldTables = Ui.Descendants<DataGrid>(bridge).ToArray();
        second.Set("N", "incompleto");
        Check(bridge.Calculation is null && bridge.ResultsAreStale && bridge.Drawing.IsStale, "Modifica non segnala l'ultimo risultato come precedente.");
        Check(ReferenceEquals(bridge.Drawing.Stage, retainedStage) && ReferenceEquals(bridge.Drawing.Geometry, retainedGeometry) && ReferenceEquals(bridge.DisplayedCalculation, retained), "Modifica cancella o mescola geometria e tensioni.");
        Check(oldTables.SequenceEqual(Ui.Descendants<DataGrid>(bridge)), "Modifica cancella le tabelle delle verifiche.");
        await Task.Delay(850); await Layout();
        Check(bridge.Calculation is null && ReferenceEquals(bridge.Drawing.Stage, retainedStage), "Errore numerico svuota il grafico precedente.");
        bool rejected = false; try { bridge.BuildReport("Non aggiornato", ReportBridge.DefaultSections()); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && bridge.Result is null, "Risultato precedente esportabile come corrente.");
        File.WriteAllBytes(Path.Combine(directory, "risultati_conservati_durante_modifica.png"), Ui.Snapshot(this));
        second.Set("N", "0"); reference.SelectedItem = BridgeSection.GrossLoadReference; await wait();
        Check(!bridge.ResultsAreStale && !bridge.Drawing.IsStale && !ReferenceEquals(bridge.Drawing.Stage, retainedStage), "Ricalcolo non sostituisce l'ultimo risultato.");
        bridge.Pages.SelectedIndex = 0; await Layout();
        Check(bridge.GeometryLabels.IsChecked == true && bridge.RebarLabels.IsChecked == true && bridge.Drawing.VisibleTags.Count == 7, "Preferenze delle annotazioni perse al cambio pagina.");
        bridge.Pages.SelectedIndex = originalPage; bridge.DisplayChoice.SelectedIndex = originalView; bridge.Results.SelectedIndex = originalResult; await Layout();
        File.WriteAllText(Path.Combine(directory, "geometria-riferimenti-smoke.txt"), $"OK: {count} controlli su tag e leggibilità, pannello geometrico, riferimenti N per fase, persistenza degli ultimi risultati e blocco degli export non aggiornati.");
    }
}
