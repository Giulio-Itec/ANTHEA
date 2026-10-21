using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.CompositeSection;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    private readonly TextBlock methodNotice = Ui.Text("", 12, color: Ui.Muted);
    private InputForm? methodForm;
    private UIElement? meshControls;
    private UIElement BuildCalculationOptions()
    {
        BridgeSheetPresentation.PrepareCalculationOptions(Data);
        methodForm = Form(Data, CompositeSectionFields.Method, _ => { RefreshCalculationOptions(); Dispatcher.BeginInvoke(BuildPhases); });
        meshControls = Group("Discretizzazione e sottopassi", Ui.Stack(Form(Data, CompositeSectionFields.Mesh), Ui.Text("Due fibre per striscia. Aumentare i valori per verificare la sensibilità dei risultati.", 11, color: Ui.Muted)));
        RefreshCalculationOptions();
        return Ui.Stack(methodForm, methodNotice, meshControls);
    }
    private void RefreshCalculationOptions()
    {
        bool nonlinear = BridgeEditor.IsNonlinear(Data);
        ShowFields(methodForm, "metodo");
        if (meshControls is not null) meshControls.Visibility = BridgeEditor.IsHistory(Data) ? Visibility.Visible : Visibility.Collapsed;
        foreach (var form in inputForms.Where(f => f.Editors.ContainsKey("classe4")))
            foreach (string key in BridgeInputPresentation.StabilityKeys) form.Enable(key, !nonlinear, dim: true);
        methodNotice.Text = BridgeInputPresentation.MethodNotice(Data);
    }
}
