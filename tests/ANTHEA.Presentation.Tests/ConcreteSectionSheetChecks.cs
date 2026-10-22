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
/// wizard; S3: domains; S4: serviceability; S5: shear and torsion; S6: detailing) against the traces of the historic view frozen in
/// tests/fixtures/f5-sezione/storica, without WPF. Each scenario of the sub-steps (ingressi/*, azioni/*, domini/*, sle/*, taglio/*, dettagli/*, percorso-f3/*,
/// calcolo/*, muri/*, esempi/*) is driven on the ViewModel as the user drives the historic controls; the common part of every state and the parts
/// «[ingressi]», «[azioni]», «[domini]», «[sle]», «[taglio]», «[dettagli]» and «[wizard]» are compared line by
/// line, the parts of the sub-steps not migrated yet are left out by their label (section 4.0
/// of docs/refactoring/f5-10-sezione.md). A step that belongs to a sub-step not migrated (the options of the domains, the stresses, the M–χ…)
/// does nothing here: when the historic trace shows its effect, the comparison of that scenario stops there, at the place listed in
/// <see cref="ExpectedStops"/> (any other stop is a failure).
/// One "UI" thread (<see cref="SingleThreadContext"/>): the batches of the calculation publish there, as on the dispatcher.
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private static readonly string[] OwnParts = ["ingressi", "azioni", "domini", "sle", "taglio", "dettagli", ConcreteSectionScenarios.WizardSection];
    private static readonly string[] OtherParts = ConcreteSectionScenarios.AllSections.Except(OwnParts).ToArray();

    /// <summary>
    /// The scenarios whose comparison stops at a step of a sub-step not migrated yet, with that step and its occurrence in the trace (a step such
    /// as «attendi» comes back more times). Since S4: in the read-only revision the M–χ curve (S7) is calculated and enters the exported result,
    /// visible at the wait after «Calcola curva», the second «attendi» of the scenario.
    /// </summary>
    private static readonly Dictionary<string, (string Step, int Occurrence)> ExpectedStops = new(StringComparer.Ordinal)
    {
        ["calcolo/sola-lettura.txt"] = ("attendi", 2),
    };

    /// <summary>The scenarios of the fast stage (decision C7: the long traces run in the ui stage, with --sezione-tracce).</summary>
    private static readonly string[] FastScenarios = ["calcolo/ricalcolo-revisione.txt", "ingressi/resistenze-rapide.txt", "percorso-f3/default.txt", "azioni/incolla.txt",
        "ingressi/wizard-tipologie.txt", "sle/fessurazione.txt", "taglio/circolare-senza-staffe.txt", "dettagli/soletta.txt"];

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
            }
            return expect.Count;
        }
        finally { CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    // ---------------------------------------------------------------- traces

    private static async Task TracesAsync(string root, string output, bool full, Expect expect)
    {
        string folder = Path.Combine(output, "sezione-ca");
        var scenarios = ConcreteSectionScenarios.All(root).Where(s => s.SubStep is "ingressi" or "azioni" or "domini" or "sle" or "taglio" or "dettagli" or "percorso-f3" or "calcolo" or "muri" or "esempi")
            .Where(s => full || FastScenarios.Contains(s.File.Replace('\\', '/'))).ToList();
        expect.That(scenarios.Count == (full ? 51 : FastScenarios.Length), "Scenari S1, S2, S1b, S3, S4, S5 e S6 della sezione c.a.: " + scenarios.Count);
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
        // WPF does not pass the disabled state of a form to its fields until the form is laid out once (the historic trace runs off screen):
        // a form in a closed expander, in a tab not shown or hidden itself keeps its fields enabled. The forms laid out so far: «geometria»
        // and «foro» (group «Geometria», open), «armatura» and the second layers (group «Armature», closed until the user opens it).
        private readonly HashSet<string> laidOut = new(StringComparer.Ordinal);

        internal Side(JsonObject data) => Open(data);

        internal ConcreteSheetViewModel Sheet { get; private set; } = null!;
        private ConcreteInputViewModel Inputs => Sheet.Inputs;

        private void Open(JsonObject data)
        {
            session = new ConcreteAnalysisSession();
            var services = new PresentationServices(messages, new ScriptedConfirmations(), new ScriptedFileDialogs(), dialogs, clipboard,
                new RecordingLinks(), new RecordingWiki());
            var context = new SheetContext((JsonObject)data.DeepClone(), false, new Host(), services,
                ModuleServices.Get(ConcreteSectionSheetModule.Id), new CalculationLimiter(1));
            Sheet = new ConcreteSheetViewModel(context, session);
            modified = 0;
            Sheet.Modified += (_, _) => modified++;
            laidOut.Clear();
            Observe();
        }

        private void Close() => Sheet.Dispose();

        internal async Task DisposeAsync()
        {
            held?.Release(); held = null;
            if (started is not null) try { await started.WaitAsync(TimeSpan.FromMinutes(1)); } catch (Exception) { }
            Close();
        }

        // What the layout of the historic window lays out after a step: the forms shown in the open groups of the control panel.
        private void Observe()
        {
            if (Sheet.SelectedTab != 0) return;
            if (Inputs.Groups.First(g => g.Title == "Geometria").IsExpanded)
            {
                if (Inputs.GeometryShown) laidOut.Add("geometria");
                if (Inputs.HoleFieldsVisible) laidOut.Add("foro");
            }
            if (!Inputs.Groups.First(g => g.Title == "Armature").IsExpanded) return;
            if (Inputs.ReinforcementShown) laidOut.Add("armatura");
            foreach (var layer in Inputs.Layers) if (layer.IsShown && layer.FormVisible) laidOut.Add("strato-" + layer.Key);
        }

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
                quiet = !Sheet.IsBusy && !Sheet.IsCalculationQueued && !Inputs.IsQuickResistanceRunning
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

        private FormViewModel? Form(string target, string? key = null) => target switch
        {
            "normativa" => Inputs.Standard,
            "coefficienti" => Inputs.Coefficients,
            "materiali" => Inputs.Materials,
            "geometria" => Inputs.Geometry,
            "foro" => Inputs.Hole,
            "armatura" => Inputs.Reinforcement,
            "strato-top" or "strato-bottom" or "strato-inner" => Inputs.Layers.Single(l => l.Key == target[7..]).Form,
            "staffe" => Inputs.Stirrups,
            "schema-staffe" => Inputs.StirrupScheme,
            "cavo" => Inputs.Cable,
            "trefolo" => Inputs.TendonProperties,
            "rapide" => Inputs.QuickResistance,
            "3D" => Sheet.Domain3D.Form,
            "2D" => Sheet.Domain2D.Form,
            "SLE" or "SLE_FREQ" or "SLE_QP" => Sheet.StressPanel(target).Form,
            "taglio" => Sheet.Shear.Form,
            "torsione" => Sheet.Shear.Torsion,
            "dettagli" => Sheet.Detailing.Forms.Single(f => f.Contains(key!)),
            "ancoraggi" => Sheet.Detailing.Anchorage,
            _ => null
        };

        /// <summary>Whether the form is shown (its own visibility and that of its containers).</summary>
        private bool FormShown(FormViewModel form)
        {
            if (ReferenceEquals(form, Inputs.Hole)) return Inputs.HoleFieldsVisible;
            if (ReferenceEquals(form, Inputs.Geometry)) return Inputs.GeometryShown;
            if (ReferenceEquals(form, Inputs.Reinforcement)) return Inputs.ReinforcementShown;
            var layer = Inputs.Layers.FirstOrDefault(l => ReferenceEquals(l.Form, form));
            return layer is null || layer.IsShown && layer.FormVisible;
        }

        /// <summary>
        /// The fields of a form disabled as a whole, once laid out: the geometry and the hole of a section of the wizard, the reinforcement
        /// locked by the coordinates of the bars or by the wizard, the locked layers.
        /// </summary>
        private bool FormDisabled(FormViewModel form)
        {
            if (ReferenceEquals(form, Inputs.Geometry)) return !Inputs.GeometryEnabled && laidOut.Contains("geometria");
            if (ReferenceEquals(form, Inputs.Hole)) return !Inputs.HoleFormEnabled && laidOut.Contains("foro");
            if (ReferenceEquals(form, Inputs.Reinforcement)) return !Inputs.ReinforcementEnabled && laidOut.Contains("armatura");
            var layer = Inputs.Layers.FirstOrDefault(l => ReferenceEquals(l.Form, form));
            return layer is not null && !layer.FormEnabled && laidOut.Contains("strato-" + layer.Key);
        }

        /// <summary>A field as the user sees it: enabled (choices and check boxes disabled in a read-only revision), visible.</summary>
        private bool FieldEnabled(FormViewModel form, FieldViewModel field) => field.IsEnabled && !FormDisabled(form) && (field.Kind == FieldKind.Text || field.CanChange);

        private bool Usable(FormViewModel form, FieldViewModel field) => FieldEnabled(form, field) && field.IsVisible && FormShown(form);

        private async Task<(string? Message, bool Own)> ApplyControlAsync(ConcreteSectionStep step)
        {
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
                    log.Add("aperta · " + DialogState(material));
                    foreach (var inner in step.Dialog ?? [])
                    {
                        if (closed) { log.Add("chiusa: passo " + inner + " non eseguito"); continue; }
                        DialogStep(material, inner);
                        log.Add(inner + " · " + (closed ? "chiusa" : DialogState(material)));
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

        private static string DialogState(ConcreteMaterialDialogViewModel dialog)
        {
            var texts = (dialog.Error.Length > 0 ? new[] { dialog.Error } : []).Append(dialog.Derived);
            return string.Join(" · ", dialog.Form.Fields.Select(f => f.Key + "=" + ConcreteSectionTrace.Quote(dialog.Form.Get(f.Key))))
                + " · testi " + ConcreteSectionTrace.Quote(string.Join(" ⏎ ", texts))
                + " · curve " + string.Join(" ", dialog.Curves.Select(c => ConcreteSectionTrace.Quote(c.Name) + " " + c.Points.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(c.Points)));
        }

        // ---------------------------------------------------------------- state

        internal ConcreteSectionTraceState State(IReadOnlyList<string> sections, string? message)
        {
            Observe();
            var state = new ConcreteSectionTraceState
            {
                Data = Sheet.Data, Modified = modified, Busy = Sheet.IsBusy, Queued = Sheet.IsCalculationQueued, HasResults = Sheet.HasResults, Result = Sheet.Result,
                Status = Sheet.Status, Progress = Sheet.IsProgressVisible, Tab = Sheet.SelectedTab,
                Editable = !Inputs.Geometry["width_mm"].IsInputReadOnly, Message = message,
            };
            foreach (string section in sections.Where(OwnParts.Contains))
            {
                var block = new ConcreteSectionBlock(section);
                if (section == "azioni") TraceActions(block);
                else if (section == "domini") TraceDomains(block);
                else if (section == "sle") TraceServiceability(block);
                else if (section == "taglio") TraceShear(block);
                else if (section == "dettagli") TraceDetailing(block);
                else if (section == ConcreteSectionScenarios.WizardSection) TraceWizard(block);
                else TraceInputs(block);
                state.Blocks.Add(block);
            }
            return state;
        }

        private ConcreteFormState FormState(string name, FormViewModel form)
        {
            var fields = form.Fields.Select(field =>
            {
                bool enabled = FieldEnabled(form, field);
                string? note = field.IsNoteVisible ? field.Note : null;
                return field.Kind switch
                {
                    FieldKind.Text => new ConcreteFieldState(field.Key, "campo", field.Label, form.Get(field.Key), null, enabled, field.IsDimmed, field.IsVisible, field.IsInputReadOnly, note),
                    FieldKind.Choice => new ConcreteFieldState(field.Key, "scelta", field.Label, form.Get(field.Key), field.Choices, enabled, field.IsDimmed, field.IsVisible, false, note),
                    _ => new ConcreteFieldState(field.Key, "opzione", field.Label, ConcreteSectionTrace.YesNo(field.IsChecked), null, enabled, field.IsDimmed, field.IsVisible, false, note)
                };
            }).ToList();
            var groups = form.Groups.Select(g => (g.Title, (IReadOnlyList<string>)g.Fields.Select(f => f.Key).ToList(), g.IsExpanded)).ToList();
            return new ConcreteFormState(name, fields, groups);
        }

        private static ConcreteTableState TableState(string name, ConcreteTableViewModel table) =>
            new(name, table.Columns.Where(c => c.IsVisible).Select(c => c.Header + "=" + c.Key).ToList(), table.Shown.Select(r => r.Values).ToList(), table.SelectedIndex,
                !table.IsReadOnly, table.Filter.Description, table.Rows.Count);

        private static string Colour(ConcreteCard card) => "#FF" + (card.Tone switch
        {
            ConcreteCardTone.Passed => "#39A879",
            ConcreteCardTone.Failed => UtilizationColors.Color(2),
            ConcreteCardTone.Utilization => UtilizationColors.Color(card.Ratio),
            ConcreteCardTone.Information => "#0B5CAD",
            _ => UtilizationColors.Color(null)
        })[1..].ToUpperInvariant();

        private static ConcreteCardsState Cards(string name, ConcreteCardsViewModel cards) => new(name, cards.Text, cards.Cards.Select(Colour).ToList());

        private static string Section(ConcreteGeometry? section) => section is null ? "-" :
            section.Shape + " " + ConcreteSectionTrace.Number(section.Width) + "x" + ConcreteSectionTrace.Number(section.Height)
            + " · Ac " + ConcreteSectionTrace.Number(section.AreaCls) + " · As " + ConcreteSectionTrace.Number(section.AreaSteel)
            + " · contorno " + section.Outline.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(section.Outline)
            + " · fori " + section.Holes.Count.ToString(CultureInfo.InvariantCulture)
            + " · barre " + section.Bars.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(section.Bars.Select(b => new[] { b.X, b.Y, b.Diametro }).ToArray());

        private static string Viewport(ConcreteSectionDrawingViewModel view) =>
            "sezione " + Section(view.Section) + " · trefoli " + ConcreteSectionTrace.Hash(view.Tendons.Select(t => new object[] { t.Id, t.X, t.Y, t.Area }).ToArray())
            + " · messaggio " + ConcreteSectionTrace.Quote(view.Message) + " · tensioni " + StressState(view.Stress) + " · contorno " + ConcreteSectionTrace.Quote(view.Contour)
            + " · opzioni " + string.Concat(new[] { view.Labels, view.Axes, view.ShowNeutralAxis, view.BarValues, view.TendonValues, view.ConcreteValues, view.Dimensions, view.CoverDimensions,
                view.SpacingDimensions, view.Stirrups is not null }.Select(b => b ? "1" : "0"))
            + " · barra " + ConcreteSectionTrace.Quote(view.SelectedBar) + " · zona " + ConcreteSectionTrace.Quote(view.EffectiveRegion?.Name);

        // The part «[wizard]» (S1b): the command, the editor hidden or disabled with the notes of the wizard, the hole, the note of the shear.
        private void TraceWizard(ConcreteSectionBlock block)
        {
            block.Add("comando " + ConcreteSectionTrace.Quote(Inputs.WizardCommandText) + (Inputs.OpenWizardCommand.CanExecute(null) ? "" : " · spento"));
            block.Add("geometria" + (Inputs.GeometryShown ? "" : " · nascosta") + (Inputs.GeometryEnabled ? "" : " · spenta") + " · nota "
                + ConcreteSectionTrace.Quote(Inputs.TemplateGeometryNote) + (Inputs.TemplateNotesShown ? "" : " · nascosta"));
            block.Add("armatura" + (Inputs.ReinforcementShown ? "" : " · nascosta") + " · nota " + ConcreteSectionTrace.Quote(Inputs.TemplateReinforcementNote)
                + (Inputs.TemplateNotesShown ? "" : " · nascosta"));
            block.Add("foro campi" + (Inputs.HoleFormEnabled ? "" : " · spenti"));
            block.Text("nota-taglio", Sheet.ShearAutomaticNote);
        }

        private void TraceInputs(ConcreteSectionBlock block)
        {
            block.Text("nota-normativa", Inputs.StandardNote);
            block.Text("valori-integrati", Inputs.MigratedInput);
            foreach (var (name, form) in new[] { ("normativa", Inputs.Standard), ("coefficienti", Inputs.Coefficients), ("materiali", Inputs.Materials), ("geometria", Inputs.Geometry) })
                block.Form(FormState(name, form));
            block.Add("foro " + ConcreteSectionTrace.YesNo(Inputs.HoleChecked) + (Inputs.ToggleHoleCommand.CanExecute(null) ? "" : " · spento") + (Inputs.HoleFieldsVisible ? "" : " · campi nascosti"));
            block.Form(FormState("foro", Inputs.Hole));
            block.Add("armatura " + (Inputs.ReinforcementEnabled ? "attiva" : "spenta") + " · nota " + ConcreteSectionTrace.Quote(Inputs.ReinforcementToolTip));
            block.Form(FormState("armatura", Inputs.Reinforcement));
            foreach (var layer in Inputs.Layers)
            {
                block.Add("strato " + layer.Key + (layer.IsShown ? "" : " · nascosto") + " · " + ConcreteSectionTrace.Quote(layer.ToggleText)
                    + (layer.ToggleCommand.CanExecute(null) ? "" : " · spento") + " · nota " + ConcreteSectionTrace.Quote(layer.ToggleToolTip) + (layer.FormVisible ? "" : " · campi nascosti"));
                block.Form(FormState("strato-" + layer.Key, layer.Form));
            }
            block.Form(FormState("staffe", Inputs.Stirrups)); block.Form(FormState("schema-staffe", Inputs.StirrupScheme));
            block.Form(FormState("cavo", Inputs.Cable));
            block.Add("cavo materiale " + ConcreteSectionTrace.Quote(Inputs.CableMaterial.SelectedItem) + " di " + ConcreteSectionTrace.Quote(string.Join("|", Inputs.CableMaterial.Items))
                + " · messaggio " + ConcreteSectionTrace.Quote(Inputs.CableMessage));
            foreach (string line in TableState("trefoli", Inputs.Tendons).Lines()) block.Add(line);
            block.Add("trefoli materiali " + ConcreteSectionTrace.Quote(string.Join("|", Inputs.Tendons.Columns.First(c => c.Key == "materiale").Choices)));
            block.Add("trefolo standard " + ConcreteSectionTrace.Quote(Inputs.StandardTendon.SelectedItem) + " di " + ConcreteSectionTrace.Quote(string.Join("|", Inputs.StandardTendon.Items)));
            block.Form(FormState("trefolo", Inputs.TendonProperties));
            block.Add("materiali salvati " + Inputs.SavedMaterials.SelectedIndex.ToString(CultureInfo.InvariantCulture) + " di " + ConcreteSectionTrace.Quote(string.Join("|", Inputs.SavedMaterials.Items)));
            block.Form(FormState("rapide", Inputs.QuickResistance));
            block.Add("rapide " + ConcreteSectionTrace.Quote(Inputs.QuickResistanceText) + (Inputs.QuickResistanceTextVisible ? "" : " · nascosto") + " · riquadri "
                + ConcreteSectionTrace.Quote(string.Join(" | ", Inputs.QuickResistanceCards.Select(c => c.Direction + " " + c.Value))));
            block.Add("anteprima " + Viewport(Inputs.Drawing));
            foreach (string line in TableState("barre", Inputs.Bars).Lines()) block.Add(line);
            foreach (var (key, cards) in Sheet.Summaries) block.Add(Cards("riepilogo-" + key, cards).Line());
            block.Add(Cards("riepilogo-taglio", Sheet.ShearDashboard).Line());
        }
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
