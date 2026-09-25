using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    internal readonly TextBox ConcreteScaleEditor = new() { Width = 66, TextAlignment = TextAlignment.Right, Margin = new Thickness(4, 2, 4, 2), Padding = new Thickness(4, 2, 4, 2) };
    internal readonly CheckBox ConcreteScaleAuto = new() { Content = "Auto n", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 2, 0) };
    private UIElement concreteScaleControls = null!;
    private bool updatingStressScale;
    private const string ScaleHelp = "Amplifica solo la larghezza del diagramma CLS; valori, verifiche e armature non cambiano. Auto n segue l’ultima fase composta attiva (Q nello schema standard). Modificare il numero passa alla scala manuale. Intervallo: 0,01–1000.";

    private void BuildStressScaleControls()
    {
        ConcreteScaleEditor.ToolTip = ConcreteScaleAuto.ToolTip = ScaleHelp;
        ConcreteScaleEditor.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, "Amplificazione grafica delle tensioni CLS");
        concreteScaleControls = Ui.Bar(Ui.Text("CLS ×", 12, true), ConcreteScaleEditor, ConcreteScaleAuto);
        ((FrameworkElement)concreteScaleControls).Margin = new Thickness(8, 3, 0, 3);
        Viewport.Toolbar.Children.Add(concreteScaleControls);
        ConcreteScaleAuto.Checked += (_, _) => ChangeScaleMode(true);
        ConcreteScaleAuto.Unchecked += (_, _) => ChangeScaleMode(false);
        ConcreteScaleEditor.LostKeyboardFocus += (_, _) => CommitStressScale();
        ConcreteScaleEditor.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { CommitStressScale(); Drawing.Focus(); e.Handled = true; }
            if (e.Key == Key.Escape) { ConcreteScaleEditor.Text = F(Drawing.ConcreteAmplification); ClearScaleError(); Drawing.Focus(); e.Handled = true; }
        };
    }

    private void ChangeScaleMode(bool automatic)
    {
        if (updatingStressScale) return;
        viewSettings["amplificazione_cls_auto"] = automatic;
        if (!automatic) viewSettings["amplificazione_cls"] = Drawing.ConcreteAmplification;
        RefreshStressScale(); Drawing.InvalidateVisual(); Modified?.Invoke();
    }

    private void CommitStressScale()
    {
        if (updatingStressScale || viewSettings is null) return;
        if (!double.TryParse(ConcreteScaleEditor.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            || !double.IsFinite(value) || value < .01 || value > 1000)
        {
            ConcreteScaleEditor.BorderBrush = Ui.Brush("#B33A40");
            ConcreteScaleEditor.ToolTip = "Inserire un fattore fra 0,01 e 1000. Il diagramma conserva l’ultima scala valida. Esc ripristina il valore.";
            return;
        }
        // A focus change without an edit must not turn a rounded automatic value into an override.
        if (ConcreteScaleEditor.Text == F(Drawing.ConcreteAmplification)) { ClearScaleError(); return; }
        viewSettings["amplificazione_cls_auto"] = false; viewSettings["amplificazione_cls"] = value;
        RefreshStressScale(); Drawing.InvalidateVisual(); Modified?.Invoke();
    }

    private void ClearScaleError()
    {
        ConcreteScaleEditor.ClearValue(Control.BorderBrushProperty); ConcreteScaleEditor.ToolTip = ScaleHelp;
    }

    internal static (double Factor, string Source) StressScale(BridgeResult? result, JsonObject settings)
    {
        var all = result?.Stages.LastOrDefault()?.Contributions;
        var phase = all?.LastOrDefault(c => c.Kind == "Composta") ?? all?.LastOrDefault(c => c.HasConcrete);
        if (settings.B("amplificazione_cls_auto", true))
            return phase is not null ? (phase.HomogenizationN, "n · " + phase.Name) : (1, "nessuna fase composta");
        double manual = settings.D("amplificazione_cls", 1);
        return (double.IsFinite(manual) && manual is >= .01 and <= 1000 ? manual : 1, "manuale");
    }

    private void RefreshStressScale()
    {
        if (concreteScaleControls is null) return;
        var scale = StressScale(DisplayedCalculation, viewSettings);
        Drawing.ConcreteAmplification = scale.Factor;
        concreteScaleControls.Visibility = currentPage == 1 && Drawing.Mode != 2 ? Visibility.Visible : Visibility.Collapsed;
        updatingStressScale = true;
        ConcreteScaleAuto.IsChecked = viewSettings.B("amplificazione_cls_auto", true);
        if (!ConcreteScaleEditor.IsKeyboardFocusWithin) { ConcreteScaleEditor.Text = F(scale.Factor); ClearScaleError(); }
        ConcreteScaleAuto.ToolTip = ScaleHelp + "\nRiferimento attuale: " + scale.Source + " · ×" + F(scale.Factor);
        updatingStressScale = false;
    }
}
