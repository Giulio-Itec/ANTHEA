using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The section σ/ε and the moment–curvature curve of the RC section (F5.10 S7): the steps of the tab «Sezione σ / ε» of the domain panels and of
/// the tab 07, driven on <see cref="ConcreteSectionInspectionViewModel"/> and <see cref="ConcreteCurvatureViewModel"/> as the historic trace drives
/// their controls; the part «[ispezione]» of the state, in the format of the historic trace (tests/Desktop/ConcreteSectionHistoricState.cs,
/// TraceInspection: the map as the properties of its viewport, the strain plane as the hash of its diagram, the curve as the Plot of the tab); and
/// the checks beyond the traces.
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private sealed partial class Side
    {
        /// <summary>A step of the section σ/ε or of the curve; null when the step is not one.</summary>
        private (string? Message, bool Own)? ApplyInspectionStep(ConcreteSectionStep step)
        {
            if (step.Target is "curvatura" && step.Kind == ConcreteSectionStep.Button)
            {
                var curvature = Sheet.Curvature;
                var command = step.Key switch
                {
                    "Calcola curva" => (CommunityToolkit.Mvvm.Input.IRelayCommand)curvature.CalculateCommand,
                    "Interrompi" => curvature.StopCommand,
                    "Esporta CSV…" => curvature.ExportCommand,
                    _ => throw new InvalidOperationException("Pulsante non trovato: " + step.Key)
                };
                // «Calcola curva» starts the curve; the trace waits for it with the next «attendi».
                if (command.CanExecute(null)) command.Execute(null);
                return (null, true);
            }
            if (step.Target is not ("ispezione-3D" or "ispezione-2D")) return null;
            var panel = step.Target == "ispezione-3D" ? Sheet.Domain3D : Sheet.Domain2D;
            var inspection = panel.Inspection;
            switch (step.Kind)
            {
                case ConcreteSectionStep.InnerTab: panel.InspectionTab = step.Index; return (null, true);
                // The choice of the solution and of the contour stay usable in a read-only revision.
                case ConcreteSectionStep.Pick when step.Key == "stato":
                {
                    int index = inspection.States.ToList().IndexOf(step.Text!);
                    if (index >= 0) inspection.State = index;
                    return (null, true);
                }
                case ConcreteSectionStep.Pick when step.Key == "contorno": inspection.Contour = step.Text!; return (null, true);
                case ConcreteSectionStep.Toggle when step.Key == "Asse neutro": panel.NeutralAxis = !panel.NeutralAxis; return (null, true);
                case ConcreteSectionStep.Toggle when step.Key == "Valori σ/ε sulle barre": panel.BarValues = !panel.BarValues; return (null, true);
                case ConcreteSectionStep.Button when step.Key == "Proprietà…":
                    if (inspection.PropertiesCommand.CanExecute(null)) inspection.PropertiesCommand.Execute(null);
                    return (null, true);
                default: throw new InvalidOperationException("Passo della sezione σ/ε sconosciuto: " + step);
            }
        }
    }

    private static async Task InspectionAsync(string root, Expect expect)
    {
        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default")))
        {
            await IdleAsync(sheet);
            var panel = sheet.Domain3D; var inspection = panel.Inspection;
            expect.That(panel.InspectionTab == -1 && inspection.Map.Section is null && inspection.Message == ConcreteInspectionPresentation.NoSelection && inspection.Values == "",
                "Sezione σ/ε prima della prima vista: schede non ancora mostrate, nessuna sezione");
            sheet.SelectedTab = 1;
            expect.That(panel.InspectionTab == 0 && inspection.Map.Section is not null && inspection.Values == ConcreteInspectionPresentation.NoSolution && sheet.Domain2D.InspectionTab == -1,
                "Dominio 3D mostrato: scheda «Dominio», sezione del pannello di controllo, nessuna soluzione");
            panel.InspectionTab = 1;
            expect.That(inspection.Message == ConcreteInspectionPresentation.Pending(true) && inspection.IsRunning, "Punto limite: lettura del piano in corso");
            await IdleAsync(sheet);
            expect.That(inspection.Message == ConcreteInspectionPresentation.Completed(true) && inspection.Map.Stress is not null && inspection.Plane is not null
                && inspection.Values.StartsWith("PUNTO LIMITE · assi locali", StringComparison.Ordinal), "Punto limite: piano, mappa e valori");
            // A solution superseded before it ends is never published.
            inspection.State = 0;
            panel.InspectionTab = 0;
            await IdleAsync(sheet);
            expect.That(inspection.Message == ConcreteInspectionPresentation.Pending(false) && inspection.Map.Stress is null && inspection.Plane is null
                && inspection.Values == ConcreteInspectionPresentation.NoSolution, "Azione inserita abbandonata: nessuna soluzione pubblicata");
            panel.InspectionTab = 1;
            await IdleAsync(sheet);
            expect.That(inspection.Message == ConcreteInspectionPresentation.Completed(false) && inspection.Values.StartsWith("AZIONE INSERITA · assi ", StringComparison.Ordinal),
                "Azione inserita: analisi dell'azione");
            int modified = 0; sheet.Modified += (_, _) => modified++;
            panel.NeutralAxis = !panel.NeutralAxis; panel.BarValues = true; inspection.Contour = ConcreteSectionDrawingViewModel.Contours[2];
            expect.That(inspection.Map.ShowNeutralAxis == panel.NeutralAxis && inspection.Map.BarValues && inspection.Map.Contour == ConcreteSectionDrawingViewModel.Contours[2]
                && modified == 1, "Asse neutro (con Modified), valori sulle barre e contorno della mappa");
            // A change of the data clears the solution of both panels.
            sheet.Inputs.Geometry["width_mm"].EditText = "650";
            expect.That(inspection.Message == ConcreteInspectionPresentation.SelectCalculated && inspection.Map.Stress is null && sheet.Domain2D.Inspection.Values == ConcreteInspectionPresentation.NoSolution,
                "Dati cambiati: soluzioni dei due pannelli azzerate");
            await IdleAsync(sheet);
            expect.That(inspection.Message == ConcreteInspectionPresentation.Completed(false), "Dopo il calcolo: soluzione di nuovo calcolata sulla riga selezionata");
        }
        await CurvatureAsync(root, expect);
    }

    private static async Task CurvatureAsync(string root, Expect expect)
    {
        var files = new ScriptedFileDialogs();
        var services = new Anthea.Presentation.Services.PresentationServices(new ScriptedMessages(), new ScriptedConfirmations(), files, new ScriptedDialogs(), new RecordingClipboard(),
            new RecordingLinks(), new RecordingWiki());
        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"), services: services))
        {
            await IdleAsync(sheet);
            var curvature = sheet.Curvature;
            expect.That(curvature.Text == "" && curvature.Chart.Points.Count == 0 && curvature.Result is null,
                "Curva all'apertura: nessun testo, nessuna serie");
            await curvature.CalculateCommand.ExecuteAsync(null);
            await IdleAsync(sheet);
            var result = curvature.Result;
            expect.That(result is not null && curvature.Chart.Points.Count == result.Points.Count && curvature.Text.StartsWith(result.Status, StringComparison.Ordinal)
                && sheet.Result?["momento_curvatura"] is not null, "Curva calcolata: serie, testo, risultato esportato");
            // The same data: the curve is not calculated again.
            await curvature.CalculateCommand.ExecuteAsync(null);
            expect.That(ReferenceEquals(result, curvature.Result), "Stessi dati: curva non ricalcolata");
            string file = Path.Combine(Path.GetTempPath(), "anthea-f5-10-s7-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ".csv");
            files.Then(file);
            curvature.ExportCommand.Execute(null);
            string[] csv = File.ReadAllLines(file);
            File.Delete(file);
            expect.That(csv.Length == result!.Points.Count + 1 && csv[0].StartsWith("M_kNm;Mx_kNm;", StringComparison.Ordinal), "Esporta CSV: intestazione e un punto per riga");
            int modified = 0; sheet.Modified += (_, _) => modified++;
            curvature.Form["passi"].EditText = "30";
            expect.That(curvature.Result is null && curvature.Text == ConcreteCurvaturePresentation.Stale && curvature.Chart.Points.Count == 0 && modified == 1,
                "Form della curva: curva azzerata, Modified");
            // «Interrompi» abandons the curve in progress.
            var running = curvature.CalculateCommand.ExecuteAsync(null);
            curvature.StopCommand.Execute(null);
            await running;
            expect.That(curvature.Result is null && curvature.Text == ConcreteCurvaturePresentation.Interrupted, "Interrompi: nessuna curva parziale pubblicata");
            await IdleAsync(sheet);
        }
        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"), readOnly: true))
        {
            await IdleAsync(sheet);
            var curvature = sheet.Curvature;
            expect.That(curvature.Form.IsReadOnly && curvature.Form["N"].IsInputReadOnly && curvature.CalculateCommand.CanExecute(null) && sheet.Domain3D.Inspection.States.Count == 2
                && !sheet.Domain3D.Inspection.PropertiesCommand.CanExecute(null), "Sola lettura: form della curva protetto, «Calcola curva» disponibile, «Proprietà…» spento");
            await curvature.CalculateCommand.ExecuteAsync(null);
            await IdleAsync(sheet);
            expect.That(curvature.Result is not null, "Sola lettura: la curva si calcola (comportamento storico)");
        }
    }
}
