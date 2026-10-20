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
        foreach (var (key, title, source) in Anthea.Application.Concrete.ConcreteInputPresentation.Layers)
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
            controls.Body.Visibility = !Anthea.ConcreteWorkspace.ConcreteTemplateAdapter.Uses(Input) &&
                Anthea.Application.Concrete.ConcreteInputPresentation.LayerShown(key, Input.S("shape")) ? Visibility.Visible : Visibility.Collapsed;
            bool enabled = Input.B("second_" + key + "_enabled");
            bool locked = Anthea.Application.Concrete.ConcreteInputPresentation.LayersLocked(Input);
            controls.Toggle.IsEnabled = controls.Form.IsEnabled = !locked;
            controls.Toggle.ToolTip = Anthea.Application.Concrete.ConcreteInputPresentation.LayerToolTip(locked);
            controls.Toggle.Content = Anthea.Application.Concrete.ConcreteInputPresentation.LayerToggleText(enabled, (string)controls.Toggle.Tag);
            controls.Form.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
