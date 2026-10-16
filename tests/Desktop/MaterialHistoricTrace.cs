using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.Presentation.Tests;

namespace X.Desktop;

/// <summary>
/// --check-materials-historic &lt;radice&gt; &lt;cartella&gt; and --check-materials-historic-capture &lt;radice&gt; &lt;cartella&gt;
/// (UiTests, F5.5, proposal F5 section 7.2): the scenarios of tests/Shared/MaterialTrace.cs on the historic views of the
/// materials, <see cref="Materiali.MaterialView"/> (mat_calcestruzzo) and <see cref="RebarMaterialView"/> (mat_acciaio_armatura),
/// opened as the window opens them (<see cref="SheetEditor"/>), off screen. After the opening and after every step the state seen by
/// the user is written as text: data and preview (<see cref="SheetEditor.PreviewData"/>) in memory order, Modified, inputs, texts,
/// tables and the drawing of the steel. The capture writes the traces in the folder; the check also compares them with the traces
/// frozen in tests/fixtures/f5-materiali/storica, which the ViewModel tests without WPF compare with the new sheets. The check stays
/// in the ui stage until the historic views are archived (Q11). Writes esito.txt («PASS n scenari …»).
/// </summary>
internal static class MaterialHistoricTrace
{
    internal static async Task Run(string root, string output, bool check)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("La cartella della cattura deve essere vuota.");
        Directory.CreateDirectory(output);
        var culture = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
        var scenarios = MaterialScenarios.All(root);
        string reference = MaterialScenarios.Reference(root);
        int lines = 0;
        var differences = new List<string>();
        foreach (var scenario in scenarios)
        {
            var trace = await Trace(root, scenario);
            MaterialTrace.Write(Path.Combine(output, scenario.File), trace);
            lines += trace.Count;
            if (!check) continue;
            string stored = Path.Combine(reference, scenario.File);
            string? difference = File.Exists(stored) ? MaterialTrace.FirstDifference(MaterialTrace.Read(stored), trace) : "traccia di riferimento assente";
            if (difference is not null) differences.Add(scenario.File + ": " + difference);
        }
        if (check)
        {
            var names = scenarios.Select(s => s.File).ToHashSet(StringComparer.Ordinal);
            foreach (string extra in Directory.Exists(reference) ? Directory.EnumerateFiles(reference, "*.txt").Select(n => Path.GetFileName(n)).Where(n => !names.Contains(n)) : [])
                differences.Add(extra + ": traccia di riferimento senza scenario");
            if (differences.Count > 0)
            {
                File.WriteAllLines(Path.Combine(output, "differenze.txt"), differences, new UTF8Encoding(false));
                throw new InvalidOperationException("Viste storiche dei materiali diverse dalle tracce congelate: " + differences[0]);
            }
        }
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {scenarios.Count} scenari, {lines} righe delle viste storiche dei materiali"
            + (check ? " uguali alle tracce congelate" : " catturate"), new UTF8Encoding(false));
    }

    static async Task<List<string>> Trace(string root, MaterialScenario scenario)
    {
        var lines = new List<string>();
        var side = new HistoricSide(scenario.Module, MaterialScenarios.FixtureData(root, scenario.Module, scenario.Fixture));
        try
        {
            lines.AddRange(side.Lines("apertura"));
            foreach (var step in scenario.Steps)
            {
                await side.Apply(step);
                lines.AddRange(side.Lines(step.ToString()));
            }
        }
        finally { side.Dispose(); }
        return lines;
    }

    /// <summary>The historic sheet driven through its controls, as the user does.</summary>
    private sealed class HistoricSide : IDisposable
    {
        private readonly string module;
        private SheetEditor editor = null!;
        private int modified;

        internal HistoricSide(string module, JsonObject data) { this.module = module; Open(data); }

        private void Open(JsonObject data)
        {
            editor = new SheetEditor(module, data);
            modified = 0;
            editor.Modified += () => modified++;
            editor.Measure(new Size(1400, 1000)); editor.Arrange(new Rect(0, 0, 1400, 1000)); editor.UpdateLayout();
        }

        private Materiali.MaterialView Concrete => editor.materials ?? throw new InvalidOperationException("Foglio senza la vista storica del calcestruzzo.");
        private RebarMaterialView Steel => editor.rebarMaterial ?? throw new InvalidOperationException("Foglio senza la vista storica dell'acciaio.");

        internal IEnumerable<string> Lines(string step) => module == MaterialScenarios.Concrete
            ? Concrete.TraceForChecks(editor.Data, editor.PreviewData, modified).Lines(step)
            : Steel.TraceForChecks(editor.Data, editor.PreviewData, modified).Lines(step);

        internal async Task Apply(MaterialStep step)
        {
            switch (step.Kind)
            {
                case MaterialStep.Commit: editor.Commit(); break;
                case MaterialStep.Calculate: await editor.CalculateAsync(); break;
                case MaterialStep.ReadOnly: editor.InspectRevision(); break;
                case MaterialStep.Reopen:
                    var data = editor.Data; editor.Dispose(); Open(data); break;
                case MaterialStep.Class: Concrete.ClassBoxForChecks.SelectedItem = step.Text; break;
                case MaterialStep.Number: Concrete.NumberBoxForChecks(step.Key!).Text = step.Text; break;
                case MaterialStep.Choice when module == MaterialScenarios.Concrete: Concrete.ChoiceBoxForChecks(step.Key!).SelectedItem = step.Text; break;
                case MaterialStep.Option:
                    var box = Concrete.OptionBoxForChecks(step.Key!);
                    box.IsChecked = box.IsChecked != true;
                    break;
                case MaterialStep.Exposure: Concrete.ExposureBoxForChecks.SelectedItem = step.Text; break;
                case MaterialStep.Steel: Steel.Selection.SelectedItem = step.Text; break;
                case MaterialStep.Field: Steel.TextBoxForChecks(step.Key!).Text = step.Text; break;
                case MaterialStep.Choice: Steel.DiagramBoxForChecks.SelectedItem = step.Text; break;
                case MaterialStep.Reference: Steel.ReferenceBoxForChecks.Text = step.Text; break;
                default: throw new ArgumentException("Passo sconosciuto: " + step.Kind);
            }
        }

        public void Dispose() => editor.Dispose();
    }

    /// <summary>The colour of a brush, to tell the two colours of a text apart.</summary>
    internal static Color ColorOf(Brush brush) => brush is SolidColorBrush solid ? solid.Color : throw new InvalidOperationException("Pennello non uniforme: " + brush);

    internal static List<T> Controls<T>(DependencyObject root) where T : DependencyObject
    {
        var found = new List<T>(); var pending = new Stack<object>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is T match) found.Add(match);
            if (node is DependencyObject element) foreach (var child in LogicalTreeHelper.GetChildren(element).Cast<object>().Reverse()) pending.Push(child);
        }
        return found;
    }
}

internal sealed partial class RebarMaterialView
{
    private static readonly string[] TextKeysForChecks = ["materiale_acciaio_nome", "fyk_mpa", "steel_fu_mpa", "steel_modulus_mpa", "steel_eps_u", "gamma_s"];

    /// <summary>UiTests: the text box of a field of the three forms (identity, properties, coefficient).</summary>
    internal TextBox TextBoxForChecks(string key) => (TextBox)(key == "materiale_acciaio_nome" ? identity : key == "gamma_s" ? coefficient : Properties).Editors[key];

    internal ComboBox DiagramBoxForChecks => (ComboBox)Properties.Editors["steel_diagramma"];

    internal TextBox ReferenceBoxForChecks => MaterialHistoricTrace.Controls<TextBox>(this)
        .Single(t => System.Windows.Automation.AutomationProperties.GetName(t) == "Certificato o riferimento");

    /// <summary>The state of the view for the trace of F5.5: what the controls show, read without changing them.</summary>
    internal SteelTraceState TraceForChecks(JsonObject sheetData, JsonObject preview, int modifiedCount) => new()
    {
        Data = sheetData, Preview = preview, Modified = modifiedCount, Editable = Selection.IsEnabled,
        Type = Selection.SelectedItem as string, Types = Selection.Items.Cast<object>().Select(i => i.ToString()!).ToList(),
        Fields = TextKeysForChecks.Select(k => (k, TextBoxForChecks(k).Text, TextBoxForChecks(k).IsReadOnly)).ToList(),
        Diagram = DiagramBoxForChecks.SelectedItem as string, Diagrams = DiagramBoxForChecks.Items.Cast<object>().Select(i => i.ToString()!).ToList(),
        DiagramEnabled = DiagramBoxForChecks.IsEnabled,
        Reference = ReferenceBoxForChecks.Text,
        Description = description.Text, Source = source.Text, Results = results.Text, Status = Status.Text,
        StatusCoherent = MaterialHistoricTrace.ColorOf(Status.Foreground) == MaterialHistoricTrace.ColorOf(Appearance.Foreground(Ui.Brush("#18794E"))),
        Series = diagram.Series.Select(s => (s.Name, (IReadOnlyList<double[]>)s.Points)).ToList(),
    };
}
