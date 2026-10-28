using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Presentation.Services;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The ViewModel of the RC section (F5.10, sub-steps S1: shell, calculation, inputs and materials; S2: actions; S1b: sections made by the
/// wizard; S3: domains; S4: serviceability; S5: shear and torsion; S6: detailing; S7: section σ/ε and M–χ; S8: reinforcement design; S9: reports) against the traces of the historic view frozen in
/// tests/fixtures/f5-sezione/storica, without WPF. Each scenario of the sub-steps (ingressi/*, azioni/*, domini/*, sle/*, taglio/*, dettagli/*, ispezione/*, armature/*, report/*, percorso-f3/*,
/// calcolo/*, muri/*, esempi/*) is driven on the ViewModel as the user drives the historic controls; the common part of every state and the parts
/// «[ingressi]», «[azioni]», «[domini]», «[sle]», «[taglio]», «[dettagli]», «[ispezione]», «[armature]» and «[wizard]» are compared line by
/// line, the parts of the sub-steps not migrated yet are left out by their label (section 4.0
/// of docs/refactoring/f5-10-sezione.md). A step that belongs to a sub-step not migrated (the options of the domains, the stresses, the M–χ…)
/// does nothing here: when the historic trace shows its effect, the comparison of that scenario stops there, at the place listed in
/// <see cref="ExpectedStops"/> (any other stop is a failure).
/// One "UI" thread (<see cref="SingleThreadContext"/>): the batches of the calculation publish there, as on the dispatcher.
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private static readonly string[] OwnParts = ["ingressi", "azioni", "domini", "sle", "taglio", "dettagli", "ispezione", "armature", ConcreteSectionScenarios.WizardSection];
    private static readonly string[] OtherParts = ConcreteSectionScenarios.AllSections.Except(OwnParts).ToArray();

    /// <summary>
    /// The scenarios whose comparison stops at a step of a sub-step not migrated yet, with that step and its occurrence in the trace (a step such
    /// as «attendi» comes back more times). From S4 to S6 «calcolo/sola-lettura» stopped at its second «attendi» (the M–χ curve calculated in the
    /// read-only revision); since S7 no scenario stops. Since S8 every part is compared; since S9 also in the scenarios of the reports (report/*).
    /// </summary>
    private static readonly Dictionary<string, (string Step, int Occurrence)> ExpectedStops = new(StringComparer.Ordinal);

    /// <summary>The scenarios of the fast stage (decision C7: the long traces run in the ui stage, with --sezione-tracce).</summary>
    private static readonly string[] FastScenarios = ["calcolo/ricalcolo-revisione.txt", "ingressi/resistenze-rapide.txt", "percorso-f3/default.txt", "azioni/incolla.txt",
        "ingressi/wizard-tipologie.txt", "sle/fessurazione.txt", "taglio/circolare-senza-staffe.txt", "dettagli/soletta.txt", "ispezione/sezione-sigma-epsilon.txt", "armature/opzioni-ricerca.txt",
        "report/disponibilita.txt"];

    internal static int Run(string root, string output, bool full) => SingleThreadContext.Run(_ => RunAsync(root, output, full));

    private static async Task<int> RunAsync(string root, string output, bool full)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var expect = new Expect();
            if (!full) StadiumWizardChecks(expect);
            if (full)
            {
                await WallSectionsAsync(root, output, expect);
                await DataFilesAsync(root, output, expect);
                await TracesAsync(root, output, full, expect);
                await NativePersistenceAsync(root, expect);
                await DomainDataAsync(root, output, expect);
            }
            else
            {
                await TracesAsync(root, output, full, expect);
                Contract(expect);
                await WithoutWindowAsync(root, expect);
                await ReadOnlyAsync(root, expect);
                await ClosingAsync(root, expect);
                await ErrorOrderAsync(root, expect);
                await HelpAsync(root, expect);
                await WindowsAsync(root, expect);
                await ReloadAsync(root, expect);
                await ActionsAsync(root, expect);
                await WizardAsync(root, expect);
                await DomainsAsync(root, expect);
                await ServiceabilityAsync(root, expect);
                await ShearAsync(root, expect);
                await DetailingAsync(root, expect);
                await InspectionAsync(root, expect);
                await DesignAsync(root, expect);
                // 1.15.0: the tab «Colonna modello» (GPCChecker.Concrete 0.27), its tab code and its options.
                await ModelColumnAsync(root, expect);
                await ReportAsync(root, expect);
                await ViewSupportAsync(root, expect);
            }
            return expect.Count;
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    // ---------------------------------------------------------------- traces

    private static async Task TracesAsync(string root, string output, bool full, Expect expect)
    {
        string folder = Path.Combine(output, "sezione-ca");
        // Since S9 every scenario of every sub-step, the reports included.
        var scenarios = ConcreteSectionScenarios.All(root).Where(s => full || FastScenarios.Contains(s.File.Replace('\\', '/'))).ToList();
        expect.That(scenarios.Count == (full ? 57 : FastScenarios.Length), "Scenari S1–S9 della sezione c.a.: " + scenarios.Count);
        var failures = new List<string>();
        var report = new List<string>();
        foreach (var scenario in scenarios)
        {
            string name = scenario.File.Replace('\\', '/');
            var watch = Stopwatch.StartNew();
            var (lines, foreign) = await TraceAsync(root, scenario);
            ConcreteSectionTrace.Write(Path.Combine(folder, scenario.File), lines);
            var expected = Filter(ConcreteSectionTrace.Read(Path.Combine(ConcreteSectionScenarios.Reference(root), scenario.File)));
            var actual = Filter(lines);
            var (difference, step, occurrence) = Difference(expected, actual);
            bool stops = ExpectedStops.TryGetValue(name, out var stop);
            string outcome;
            if (difference is null) outcome = !stops ? "uguale" : "uguale, ma era atteso l'arresto al passo «" + stop.Step + "» (" + stop.Occurrence.ToString(CultureInfo.InvariantCulture) + ")";
            else if (stops && step is not null && foreign.Contains((step, occurrence)) && (step, occurrence) == stop)
                outcome = "uguale fino al passo «" + step + "» (" + occurrence.ToString(CultureInfo.InvariantCulture) + ", sottopasso non migrato)";
            else outcome = "diverso: " + difference;
            report.Add(name + " · " + lines.Count.ToString(CultureInfo.InvariantCulture) + " righe · " + watch.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s · " + outcome);
            if (!outcome.StartsWith("uguale", StringComparison.Ordinal) || outcome.Contains("atteso", StringComparison.Ordinal)) failures.Add(name + ": " + outcome);
            else expect.That(true, name);
        }
        Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, "confronto.txt"), report, new UTF8Encoding(false));
        expect.That(failures.Count == 0, "Sezione c.a. MVVM diversa dalla vista storica: " + string.Join(" | ", failures));
    }

    /// <summary>The lines of the common part and of the parts of S1 and S2: the parts of the other sub-steps are left out by their label.</summary>
    private static List<string> Filter(IEnumerable<string> lines) =>
        lines.Where(l => !OtherParts.Any(p => l.StartsWith("[" + p + "] ", StringComparison.Ordinal))).ToList();

    /// <summary>The first different line (null when equal), the step whose state holds it and the occurrence of that step in the trace (1 for
    /// the first time it appears).</summary>
    private static (string? Difference, string? Step, int Occurrence) Difference(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        string? step = null;
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        int count = Math.Min(expected.Count, actual.Count);
        for (int i = 0; i < count; i++)
        {
            if (expected[i].StartsWith("== ", StringComparison.Ordinal)) { step = expected[i][3..]; seen[step] = seen.GetValueOrDefault(step) + 1; }
            if (expected[i] != actual[i])
                return ("riga " + (i + 1).ToString(CultureInfo.InvariantCulture) + " (" + step + "): attesa «" + expected[i] + "», ottenuta «" + actual[i] + "»", step, Seen(step));
        }
        if (expected.Count == actual.Count) return (null, null, 0);
        return ("righe " + expected.Count.ToString(CultureInfo.InvariantCulture) + " attese, " + actual.Count.ToString(CultureInfo.InvariantCulture) + " ottenute (" + step + ")", step, Seen(step));
        int Seen(string? name) => name is null ? 0 : seen.GetValueOrDefault(name);
    }

    private static async Task<(List<string> Lines, HashSet<(string Step, int Occurrence)> Foreign)> TraceAsync(string root, ConcreteSectionScenario scenario)
    {
        var lines = new List<string>();
        var data = ConcreteSectionScenarios.FixtureData(root, scenario.Fixture);
        var guids = new ConcreteGuids(data);
        var memory = new ConcreteDataMemory();
        var side = new Side(data);
        // A foreign step and every step after it can show the effects of a sub-step not migrated: the comparison may stop from there. The steps
        // are counted by their occurrence («attendi» comes back), as Difference counts them.
        var foreign = new HashSet<(string, int)>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal) { ["apertura"] = 1 };
        bool afterForeign = false;
        try
        {
            lines.AddRange(side.State(scenario.Sections, null).Lines("apertura", guids, memory));
            foreach (var step in scenario.Steps)
            {
                string label = step.ToString();
                seen[label] = seen.GetValueOrDefault(label) + 1;
                var (message, own) = await side.ApplyAsync(step);
                if (!own) afterForeign = true;
                if (afterForeign) foreign.Add((label, seen[label]));
                lines.AddRange(side.State(scenario.Sections, message).Lines(label, guids, memory));
            }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or TimeoutException)
        { lines.Add("errore " + error.GetType().Name + ": " + error.Message); }
        finally { await side.DisposeAsync(); }
        return (lines, foreign);
    }

    /// <summary>
    /// The ViewModel driven as its view would be driven: a control that the user cannot use (disabled, hidden, protected by the read-only
    /// revision) does nothing, as in the historic trace. The state is read from the ViewModel in the format of the historic trace.
    /// </summary>
    private sealed partial class Side
    {
        private static readonly FieldInfo Batches = typeof(ConcreteAnalysisSession).GetField("batches", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(ConcreteAnalysisSession), "batches");
        private readonly DriverDialogs dialogs = new();
        // The messages and the clipboard of the sheet: the paste reads the text of the step, a refused paste shows its warning (S2).
        private readonly ScriptedMessages messages = new();
        private readonly RecordingClipboard clipboard = new();
        private int modified;
        private SemaphoreSlim? held;
        private Task? started;
        private ConcreteAnalysisSession session = null!;
        // The state in the format of the historic trace, with the model of the layout of the historic window (tests/Shared, shared with the
        // traces driven through the XAML view in UiTests).
        private readonly ConcreteSectionSheetReader reader;

        internal Side(JsonObject data) { reader = new(() => Sheet); Open(data); }

        internal ConcreteSheetViewModel Sheet { get; private set; } = null!;
        private ConcreteInputViewModel Inputs => Sheet.Inputs;

        private void Open(JsonObject data)
        {
            session = new ConcreteAnalysisSession();
            var services = new PresentationServices(messages, new ScriptedConfirmations(), new ScriptedFileDialogs(), dialogs, clipboard,
                new RecordingLinks(), new RecordingWiki());
            var context = new SheetContext((JsonObject)data.DeepClone(), false, new Host(), services,
                ModuleServices.Get(ConcreteSectionSheetModule.Id), new CalculationLimiter(1));
            Sheet = new ConcreteSheetViewModel(context, session, Figures);
            modified = 0;
            Sheet.Modified += (_, _) => modified++;
            reader.Reset();
        }

        private void Close() => Sheet.Dispose();

        internal async Task DisposeAsync()
        {
            held?.Release(); held = null;
            if (started is not null) try { await started.WaitAsync(TimeSpan.FromMinutes(1)); } catch (Exception) { }
            Close();
        }

        // What the layout of the historic window lays out after a step: the forms shown in the open groups of the control panel.
        private void Observe() => reader.Observe();

        /// <summary>Applies a step; returns the message the view shows and false for a step of a sub-step not migrated (it does nothing).</summary>
        internal async Task<(string? Message, bool Own)> ApplyAsync(ConcreteSectionStep step)
        {
            try
            {
                switch (step.Kind)
                {
                    case ConcreteSectionStep.Commit: Sheet.Commit(); return (null, true);
                    case ConcreteSectionStep.Calculate: await Sheet.CalculateAsync(); return (null, true);
                    case ConcreteSectionStep.Start: started = Sheet.CalculateAsync(); return (null, true);
                    case ConcreteSectionStep.Wait:
                        if (started is not null) { await started.WaitAsync(TimeSpan.FromMinutes(3)); started = null; }
                        await SettleAsync(); return (null, true);
                    case ConcreteSectionStep.ReadOnly: Sheet.IsReadOnly = true; return (null, true);
                    case ConcreteSectionStep.Reopen: { var data = Sheet.Data; Close(); Open(data); return (null, true); }
                    case ConcreteSectionStep.Hold: held = (SemaphoreSlim)Batches.GetValue(session)!; await held.WaitAsync(); return (null, true);
                    case ConcreteSectionStep.Entered: await EnteredAsync(); return (null, true);
                    case ConcreteSectionStep.Release: held?.Release(); held = null; return (null, true);
                    case ConcreteSectionStep.Visit: Visit(); return (null, true);
                    case ConcreteSectionStep.Report: return (Report(step.Text!), true);
                    case ConcreteSectionStep.Tab: Sheet.SelectedTab = step.Index; return (null, true);
                    default: return await ApplyControlAsync(step);
                }
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return (error.GetType().Name + ": " + error.Message, true); }
            finally { Observe(); }
        }

        private async Task SettleAsync()
        {
            var watch = Stopwatch.StartNew(); int quiet = 0;
            while (quiet < 4)
            {
                await Task.Yield();
                await Task.Delay(20);
                await Task.Yield();
                // The mesh shown alone (S3) runs on a worker thread, as the historic view (its status says so).
                // The section σ/ε of the panels and the M–χ curve run on worker threads with their own schedulers (S7).
                quiet = !Sheet.IsBusy && !Sheet.IsCalculationQueued && !Inputs.IsQuickResistanceRunning && !Sheet.Curvature.IsRunning
                    && !Sheet.Domain3D.Inspection.IsRunning && !Sheet.Domain2D.Inspection.IsRunning && !Sheet.Design.IsRunning
                    && !Sheet.Status.StartsWith("Aggiornamento della sola mesh", StringComparison.Ordinal) ? quiet + 1 : 0;
                if (watch.Elapsed > TimeSpan.FromMinutes(3)) throw new TimeoutException("La sezione c.a. non si ferma: " + Sheet.Status);
            }
        }

        private async Task EnteredAsync()
        {
            var watch = Stopwatch.StartNew();
            while (!Sheet.IsBusy || Sheet.IsCalculationQueued)
            {
                await Task.Yield(); await Task.Delay(10);
                if (watch.Elapsed > TimeSpan.FromMinutes(1)) throw new TimeoutException("Nessun calcolo avviato.");
            }
        }

        // The visit of the F3 capture: every expander opened, then every tab of every tab control selected and restored (the inner tabs
        // belong to the sub-steps not migrated and write nothing).
        private void Visit()
        {
            foreach (var group in Inputs.Groups) group.IsExpanded = true;
            Inputs.TendonGroup.IsExpanded = true;
            foreach (var form in Inputs.AllForms.Concat([Sheet.Domain3D.Form, Sheet.Domain2D.Form]).Concat(Sheet.StressPanels.Select(p => p.Form)).Append(Sheet.Shear.Form))
                foreach (var group in form.Groups) group.IsExpanded = true;
            int selected = Sheet.SelectedTab;
            for (int i = 0; i < ConcreteSheetViewModel.TabTitles.Length; i++) { Sheet.SelectedTab = i; Observe(); }
            Sheet.SelectedTab = selected;
            // The inner tabs of the domain panels («Dominio», «Sezione σ / ε»), selected and restored after the main tabs.
            foreach (var panel in new[] { Sheet.Domain3D, Sheet.Domain2D })
            {
                int inner = panel.InspectionTab;
                for (int i = 0; i < 2; i++) panel.InspectionTab = i;
                panel.InspectionTab = inner;
            }
            // The tab 04 (S4): the families, the columns of the results and the tabs of the details.
            VisitServiceability();
            // The tab 05 (S5): the column of the results, selected and restored.
            int shearTab = Sheet.Shear.DetailTab;
            for (int i = 0; i < 2; i++) Sheet.Shear.DetailTab = i;
            Sheet.Shear.DetailTab = shearTab;
        }

        // ---------------------------------------------------------------- controls of the inputs

        private FormViewModel? Form(string target, string? key = null) => reader.Form(target, key);

        private bool Usable(FormViewModel form, FieldViewModel field) => reader.Usable(form, field);

        private async Task<(string? Message, bool Own)> ApplyControlAsync(ConcreteSectionStep step)
        {
            if (ApplyInspectionStep(step) is { } inspection) return inspection;
            if (ApplyDesignStep(step) is { } design) return design;
            if (ApplyDomainStep(step) is { } domain) return domain;
            if (ApplyServiceabilityStep(step) is { } serviceability) return serviceability;
            if (IsActionStep(step)) return await ApplyActionAsync(step);
            switch (step.Kind)
            {
                case ConcreteSectionStep.Field when Form(step.Target!, step.Key) is { } form:
                {
                    if (form.Contains(step.Key!) && form[step.Key!] is { Kind: FieldKind.Text } field && Usable(form, field) && !field.IsInputReadOnly) field.EditText = step.Text!;
                    return (null, true);
                }
                case ConcreteSectionStep.Choice when Form(step.Target!, step.Key) is { } form:
                {
                    if (form.Contains(step.Key!) && form[step.Key!] is { Kind: FieldKind.Choice } field && Usable(form, field)) field.SelectedChoice = step.Text;
                    return (null, true);
                }
                case ConcreteSectionStep.Option when Form(step.Target!, step.Key) is { } form:
                {
                    if (form.Contains(step.Key!) && form[step.Key!] is { Kind: FieldKind.Bool } field && Usable(form, field)) field.IsChecked = !field.IsChecked;
                    return (null, true);
                }
                case ConcreteSectionStep.Toggle when step.Target == "foro":
                    if (step.Key == "Foro centrale" && Inputs.ToggleHoleCommand.CanExecute(null)) Inputs.ToggleHoleCommand.Execute(null);
                    return (null, true);
                case ConcreteSectionStep.Toggle when step.Target == "anteprima":
                    switch (step.Key)
                    {
                        case "Assi": Inputs.ShowAxes = !Inputs.ShowAxes; break;
                        case "ID": Inputs.ShowLabels = !Inputs.ShowLabels; break;
                        default:
                            string key = step.Key switch { "Dimensioni" => "quote_dimensioni", "Copriferro" => "quote_copriferro", "Interferro min." => "quote_interferro", _ => throw new ArgumentException("Casella sconosciuta: " + step.Key) };
                            Inputs.SetDimension(key, !Inputs.Dimension(key)); break;
                    }
                    return (null, true);
                case ConcreteSectionStep.Button when step.Target == "strati":
                {
                    var layer = Inputs.Layers.Single(l => l.Key == step.Key);
                    if (layer.IsShown && layer.ToggleCommand.CanExecute(null)) layer.ToggleCommand.Execute(null);
                    return (null, true);
                }
                case ConcreteSectionStep.Button when step.Target == "ancoraggi" && step.Key == "Aggiorna verifiche ancoraggi":
                    if (Sheet.Detailing.UpdateAnchoragesCommand.CanExecute(null)) Sheet.Detailing.UpdateAnchoragesCommand.Execute(null);
                    return (null, true);
                case ConcreteSectionStep.Button when step.Target is "armatura" or "coefficienti" or "cavo" or "materiali-utente":
                {
                    var command = (step.Target, step.Key) switch
                    {
                        ("armatura", "Ripristina barre da wizard") => Inputs.RestoreBarsCommand,
                        ("coefficienti", "Ripristina coefficienti della normativa") => Inputs.ResetCoefficientsCommand,
                        ("cavo", "+ Cavo / trefolo") => Inputs.AddCableCommand,
                        ("cavo", "−") => Inputs.RemoveCableCommand,
                        ("materiali-utente", "Applica materiale salvato") => Inputs.ApplySavedMaterialCommand,
                        _ => throw new ArgumentException("Pulsante sconosciuto: " + step.Target + " " + step.Key)
                    };
                    if (command.CanExecute(null)) command.Execute(null);
                    return (null, true);
                }
                case ConcreteSectionStep.Pick when step.Target is "materiali-utente" or "trefoli":
                {
                    var choice = step.Target == "trefoli" ? Inputs.StandardTendon : Inputs.SavedMaterials;
                    // The combo boxes are disabled in a read-only revision.
                    if (!Sheet.CanEdit) return (null, true);
                    if (choice.Items.Contains(step.Text!)) choice.SelectedItem = step.Text;
                    return (null, true);
                }
                case ConcreteSectionStep.Cell when step.Target is "barre" or "trefoli":
                {
                    var table = step.Target == "barre" ? Inputs.Bars : Inputs.Tendons;
                    if (step.Row >= table.Shown.Count) return (null, true);
                    var row = table.Shown[step.Row];
                    var column = table.Columns.FirstOrDefault(c => c.Key == step.Key);
                    if (table.IsReadOnly || column is null || column.IsReadOnly) return (null, true);
                    row[step.Key!] = column.Kind == ConcreteColumnKind.Bool ? step.Text == "true" : step.Text;
                    // The refresh of the collection view that the historic trace queues after the edit of a cell.
                    SynchronizationContext.Current!.Post(_ => table.Refresh(), null);
                    return (null, true);
                }
                case ConcreteSectionStep.Select when step.Target is "barre" or "trefoli":
                {
                    var table = step.Target == "barre" ? Inputs.Bars : Inputs.Tendons;
                    if (step.Index < table.Shown.Count) table.SelectedIndex = step.Index;
                    return (null, true);
                }
                case ConcreteSectionStep.Open when step.Target is "materiale" or "materiale-acciaio" or "materiale-trefoli":
                    return (await OpenMaterialAsync(step), true);
                case ConcreteSectionStep.Open when step.Target == "wizard":
                    return (await OpenWizardAsync(step), true);
                default:
                    // A control of a sub-step not migrated (domains, stresses, shear, detailing, design, inspection, reports).
                    return (null, false);
            }
        }

        // «Nuovo materiale …»: the dialog driven inside its window, as the historic trace drives the modal window; its state after every step.
        private async Task<string?> OpenMaterialAsync(ConcreteSectionStep step)
        {
            var command = step.Target switch { "materiale" => Inputs.NewConcreteMaterialCommand, "materiale-acciaio" => Inputs.NewSteelMaterialCommand, _ => Inputs.NewTendonMaterialCommand };
            if (!command.CanExecute(null)) return "finestra non aperta: comando non disponibile";
            var log = new List<string>();
            bool opened = false;
            dialogs.Next = dialog =>
            {
                opened = true;
                var material = (ConcreteMaterialDialogViewModel)dialog;
                bool closed = false;
                void Closed(object? sender, EventArgs e) => closed = true;
                material.CloseRequested += Closed;
                try
                {
                    log.Add("aperta · " + ConcreteSectionSheetReader.MaterialState(material));
                    foreach (var inner in step.Dialog ?? [])
                    {
                        if (closed) { log.Add("chiusa: passo " + inner + " non eseguito"); continue; }
                        DialogStep(material, inner);
                        log.Add(inner + " · " + (closed ? "chiusa" : ConcreteSectionSheetReader.MaterialState(material)));
                    }
                    if (!closed) log.Add("chiusa dalla traccia");
                }
                finally { material.CloseRequested -= Closed; }
                return closed;
            };
            await command.ExecuteAsync(null);
            dialogs.Next = null;
            if (!opened) throw new InvalidOperationException("Finestra " + step.Target + " non aperta.");
            return string.Join("\n", log);
        }

        // «Wizard tipologie / modifica disposizione…» (S1b): the wizard driven inside its window, its fields by the step shared with the historic
        // trace, its buttons by its commands; its state after every step.
        private async Task<string?> OpenWizardAsync(ConcreteSectionStep step)
        {
            if (!Inputs.OpenWizardCommand.CanExecute(null)) return "finestra non aperta: comando non disponibile";
            var log = new List<string>();
            bool opened = false;
            dialogs.Next = dialog =>
            {
                opened = true;
                var wizard = (ConcreteTemplateWizardViewModel)dialog;
                bool closed = false;
                void Closed(object? sender, EventArgs e) => closed = true;
                wizard.CloseRequested += Closed;
                try
                {
                    log.Add("aperta · " + ConcreteSectionTrace.WizardState(wizard));
                    foreach (var inner in step.Dialog ?? [])
                    {
                        if (closed) { log.Add("chiusa: passo " + inner + " non eseguito"); continue; }
                        if (!ConcreteSectionTrace.WizardStep(wizard, inner))
                        {
                            var command = (inner.Kind, inner.Key) switch
                            {
                                (ConcreteSectionStep.Button, "Applica sezione") => wizard.ApplyCommand,
                                (ConcreteSectionStep.Button, "Annulla") => wizard.CancelCommand,
                                _ => throw new ArgumentException("Passo del wizard sconosciuto: " + inner)
                            };
                            if (command.CanExecute(null)) command.Execute(null);
                        }
                        log.Add(inner + " · " + (closed ? "chiusa" : ConcreteSectionTrace.WizardState(wizard)));
                    }
                    if (!closed) log.Add("chiusa dalla traccia");
                }
                finally { wizard.CloseRequested -= Closed; }
                return closed;
            };
            await Inputs.OpenWizardCommand.ExecuteAsync(null);
            dialogs.Next = null;
            if (!opened) throw new InvalidOperationException("Finestra del wizard non aperta.");
            return string.Join("\n", log);
        }

        private static void DialogStep(ConcreteMaterialDialogViewModel dialog, ConcreteSectionStep step)
        {
            switch (step.Kind)
            {
                case ConcreteSectionStep.Button when step.Key == "Salva materiale e applica": dialog.SaveCommand.Execute(null); break;
                case ConcreteSectionStep.Field:
                    if (dialog.Form[step.Key!] is { Kind: FieldKind.Text, IsInputReadOnly: false, IsVisible: true, IsEnabled: true } field) field.EditText = step.Text!;
                    break;
                case ConcreteSectionStep.Choice:
                    if (dialog.Form[step.Key!] is { Kind: FieldKind.Choice, IsVisible: true, IsEnabled: true } choice) choice.SelectedChoice = step.Text;
                    break;
                default: throw new ArgumentException("Passo della finestra sconosciuto: " + step.Kind);
            }
        }

        // ---------------------------------------------------------------- state

        internal ConcreteSectionTraceState State(IReadOnlyList<string> sections, string? message) => reader.Read(sections, message, modified);
    }

    /// <summary>The dialog service of the traces: the step of the scenario drives the dialog; the result when the dialog closed itself.</summary>
    private sealed class DriverDialogs : IDialogService
    {
        internal Func<object, bool>? Next { get; set; }

        public Task<TResult?> ShowDialogAsync<TResult>(IDialogViewModel<TResult> dialog)
        {
            var next = Next ?? throw new InvalidOperationException("Dialogo non previsto: " + dialog.Title);
            bool closed = next(dialog);
            return Task.FromResult(closed ? dialog.Result : default);
        }
    }

    private sealed class Host : ISheetHost
    {
        internal List<(string Module, JsonObject Data, string Name)> Copies { get; } = [];
        public void OpenModuleCopy(string module, JsonObject data, string name) => Copies.Add((module, data, name));
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException("Terreno non previsto.");
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() => throw new InvalidOperationException("Report non previsto.");
    }
}
