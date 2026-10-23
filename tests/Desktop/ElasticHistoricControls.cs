using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Anthea.Application.Modules;
using Anthea.Presentation.Tests;
using X.Core;

namespace X.Desktop;

// f5-pass-orizzontale: extracted unchanged from tests/Desktop/ElasticPileHistoricTrace.cs, archived with the historic view
// (supporto/SUPERATI/ui-storica/palo-elastico): the readers serve the traces through the XAML view (HorizontalPileViewChecks.Elastic.cs).
/// <summary>Readers of the controls of the historic view shared by the elastic response and the reinforcement editor (UiTests).</summary>
internal static class ElasticHistoricControls
{
    private static readonly FieldInfo InputFormRows = typeof(InputForm).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(InputForm), "rows");

    internal static bool Usable(UIElement element) => element.IsEnabled && element.Visibility == Visibility.Visible;

    internal static void Click(ButtonBase button) { if (Usable(button)) button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button)); }

    /// <summary>A check box clicked by the user: the state changes, then Click.</summary>
    internal static void Toggle(CheckBox box)
    {
        if (!Usable(box)) return;
        box.IsChecked = box.IsChecked != true;
        box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, box));
    }

    internal static ElasticCommandState Command(Button button) => new(Convert.ToString(button.Content, CultureInfo.InvariantCulture) ?? "", Usable(button));

    internal static IEnumerable<T> Children<T>(Panel panel) => panel.Children.OfType<T>();

    /// <summary>The descendants in the logical tree (also inside collapsed expanders and dialogs not laid out).</summary>
    internal static IEnumerable<T> Logical<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T found) yield return found;
            foreach (var inner in Logical<T>(child)) yield return inner;
        }
    }

    internal static Button Button(DependencyObject root, string label) => Logical<Button>(root).First(b => Equals(b.Content, label));

    /// <summary>The fields of an InputForm as the user sees them (the line of the F5.6 and F5.7 traces).</summary>
    internal static List<VerticalPileFieldState> Fields(InputForm form)
    {
        var rows = (Dictionary<string, List<FrameworkElement>>)InputFormRows.GetValue(form)!;
        return form.Editors.Select(pair =>
        {
            var label = rows[pair.Key][0];
            string name = AutomationProperties.GetName(pair.Value);
            bool enabled = label.IsEnabled, dimmed = label.Opacity < 1, visible = label.Visibility == Visibility.Visible;
            return pair.Value switch
            {
                TextBox => new VerticalPileFieldState(pair.Key, "campo", name, form.Get(pair.Key), null, enabled, dimmed, visible),
                ComboBox choice => new VerticalPileFieldState(pair.Key, "scelta", name, form.Get(pair.Key), ((IEnumerable<string>)choice.ItemsSource).ToArray(), enabled, dimmed, visible),
                CheckBox option => new VerticalPileFieldState(pair.Key, "opzione", name, VerticalPileTrace.YesNo(option.IsChecked == true), null, enabled, dimmed, visible),
                _ => throw new InvalidOperationException("Editor non previsto: " + pair.Value.GetType().Name)
            };
        }).ToList();
    }

    /// <summary>A field of a form typed, chosen or clicked by the user; false when the form has no such field.</summary>
    internal static bool Edit(InputForm form, string key, string? text)
    {
        if (!form.Editors.TryGetValue(key, out var editor)) return false;
        switch (editor)
        {
            case TextBox box when text is not null: if (Usable(box) && !box.IsReadOnly) box.Text = text; break;
            case ComboBox choice when text is not null: if (Usable(choice)) choice.SelectedItem = text; break;
            case CheckBox option when text is null: Toggle(option); break;
            default: throw new ArgumentException("Passo non adatto al campo " + key);
        }
        return true;
    }

    internal static string Tone(Brush? brush) => brush is SolidColorBrush solid ? ElasticPileTrace.Tone("#" + solid.Color.ToString(CultureInfo.InvariantCulture)[3..]) : "-";

    internal static string Section(SectionDrawing drawing) => ElasticPileTrace.Section(drawing.Outline, drawing.Bars.Select(b => (b.X, b.Y, b.Area, b.Diametro)),
        drawing.CircularLinkRadius, drawing.LinkDiameter, drawing.ShowAxes, drawing.Caption, drawing.LinkCaption);

    internal static string Progress(ProgressBar bar) => ElasticPileTrace.Progress(bar.Visibility == Visibility.Visible, bar.IsIndeterminate, bar.Value);

    /// <summary>A dialog of the view shown off screen, without activation, as the trace drives it.</summary>
    internal static void ShowOffScreen(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -10000; window.Top = -10000; window.ShowActivated = false; window.ShowInTaskbar = false;
        window.Show(); window.UpdateLayout();
    }
}
