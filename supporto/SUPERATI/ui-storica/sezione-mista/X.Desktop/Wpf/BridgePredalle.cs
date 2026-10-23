using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    private readonly TextBlock slabLayoutInfo = Ui.Text("", 11, color: Ui.Muted);
    private TextBlock? steelHeightInfo;
    private InputForm? predalleForm;

    private UIElement BuildPredalleInputs()
    {
        predalleForm = Form(Data, Anthea.Presentation.Modules.CompositeSection.CompositeSectionFields.Predalle);
        RefreshSlabLayout();
        return Ui.Stack(predalleForm, slabLayoutInfo, Ui.Text(BridgeEditor.PredalleScope, 11, color: Ui.Muted));
    }

    private void RefreshSlabLayout()
    {
        ShowFields(predalleForm, "predalle");
        try
        {
            slabLayoutInfo.Text = Anthea.Application.Presentation.BridgePreview.SlabDescription(Data);
        }
        catch (ArgumentException ex) { slabLayoutInfo.Text = ex.Message; }
    }

    private void RefreshSteelHeight()
    {
        if (steelHeightInfo is null) return;
        try
        {
            steelHeightInfo.Text = Anthea.Application.Presentation.BridgePreview.UpdateSteelHeightDescription(Data);
        }
        catch (ArgumentException ex) { steelHeightInfo.Text = ex.Message; }
    }
}
