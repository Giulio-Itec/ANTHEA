using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private readonly Dictionary<string, (StackPanel Body, Button Toggle, InputForm Form)> additionalLayers = new();
    private UIElement BuildAdditionalLayers()
    {
        var stack = new StackPanel();
        foreach (var (key, title, source) in new[] { ("top", "Seconda fila superiore", "top"), ("bottom", "Seconda fila inferiore", "bottom"), ("inner", "Secondo anello interno", "longitudinal") })
        {
            string prefix = "second_" + key;
            Anthea.Application.Concrete.ConcreteSheetPreparation.PrepareLayer(Input, prefix, source);
            var form = new InputForm(Input, [new(prefix + "_count", "Numero barre"), new(prefix + "_diameter", "Diametro", "mm"), new(prefix + "_gap", "Distanza libera dalla prima fila", "mm")], _ => Invalidate(), true);
            var toggle = Ui.Button("", () => { Input[prefix + "_enabled"] = !Input.B(prefix + "_enabled"); Invalidate(); });
            toggle.Tag = title;
            var body = Ui.Stack(toggle, form); stack.Children.Add(body); additionalLayers[key] = (body, toggle, form);
        }
        RefreshAdditionalLayers(); return stack;
    }
    private void RefreshAdditionalLayers()
    {
        foreach (var (key, controls) in additionalLayers)
        {
            controls.Body.Visibility = Input.S("shape") == (key == "inner" ? "Circolare" : "Rettangolare") ? Visibility.Visible : Visibility.Collapsed;
            bool enabled = Input.B("second_" + key + "_enabled");
            controls.Toggle.IsEnabled = controls.Form.IsEnabled = !Input.ContainsKey("barre_manuali");
            controls.Toggle.ToolTip = Input.ContainsKey("barre_manuali") ? "Ripristina le barre da wizard per modificare gli strati automatici." : "Distanza libera tra le superfici delle barre dei due strati.";
            controls.Toggle.Content = (enabled ? "− Disattiva · " : "+ Attiva · ") + controls.Toggle.Tag;
            controls.Form.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
