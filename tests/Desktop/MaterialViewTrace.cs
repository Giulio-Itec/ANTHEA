using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Tests;
using X.Desktop;

namespace Materiali;

/// <summary>UiTests: the controls of the historic concrete sheet driven by the F5.5 traces (tests/Desktop/MaterialHistoricTrace.cs).</summary>
public sealed partial class MaterialView
{
    internal ComboBox ClassBoxForChecks => choice;
    internal TextBox NumberBoxForChecks(string key) => numbers[key];
    internal ComboBox ChoiceBoxForChecks(string key) => choices[key];
    internal CheckBox OptionBoxForChecks(string key) => Flags[key];
    internal ComboBox ExposureBoxForChecks => exposureSelector;

    /// <summary>The state of the view for the trace of F5.5: what the controls show, read without changing them.</summary>
    internal ConcreteTraceState TraceForChecks(JsonObject sheetData, JsonObject preview, int modifiedCount) => new()
    {
        Data = sheetData, Preview = preview, Modified = modifiedCount, Editable = choice.IsEnabled,
        ClassName = choice.SelectedItem as string, Classes = choice.Items.Count,
        Numbers = numbers.Select(p => (p.Key, p.Value.Text)).ToList(),
        Choices = choices.Select(p => (p.Key, p.Value.SelectedItem?.ToString(), p.Value.IsEnabled)).ToList(),
        DeviationValues = choices["deviationValue"].Items.Cast<object>().Select(i => i.ToString()!).ToList(),
        Options = Flags.Select(p => (p.Key, p.Value.IsChecked == true)).ToList(),
        Exposure = exposureSelector.SelectedItem?.ToString(), Exposures = exposureSelector.Items.Count,
        Active = exposureChecks.Where(p => p.Value.IsChecked == true).Select(p => p.Key).ToList(),
        ExposureDetails = exposureDetails.Text, ExposureExamples = exposureExamples.Text, Aggressiveness = exposureAggressiveness.Text,
        Combined = combinedExposure.Text, CombinedVisible = combinedExposure.Visibility == Visibility.Visible,
        Strength = strength.Text, Tension = tension.Text, Modulus = modulus.Text, Status = status.Text,
        Properties = (table.ItemsSource as IEnumerable<PropertyRow>)?.Select(r => new[] { r.Nome, r.Simbolo, r.Valore, r.Unita }).ToList() ?? [],
        BondValue = bondValue.Text, BondDetails = bondDetails.Text,
        MinimumClass = minimumConcreteClass.Text, MinimumNote = minimumConcreteNote.Text,
        CoverHeadline = coverHeadline.Text, CoverSteps = coverSteps.Text, CoverNotes = coverNotes.Text,
        CoverError = MaterialHistoricTrace.ColorOf(coverHeadline.Foreground) == MaterialHistoricTrace.ColorOf(Themed("foreground", "#9A4D0A")),
        NtcVisible = ntcOptions!.Visibility == Visibility.Visible, Ec2Visible = ec2Options!.Visibility == Visibility.Visible,
        DeviationNote = deviationNote.Text, DeviationToolTip = deviationNote.ToolTip as string ?? "",
        Composition = compositionValues.Select(p => (p.Key, p.Value.Text)).ToList(),
        CompositionSource = compositionSource.Text, AirNote = compositionAirNote.Text, ChlorideNote = compositionChlorideNote.Text,
        Requirements = compositionRequirements.Text, Consistency = consistencyInfo.Text,
    };
}
