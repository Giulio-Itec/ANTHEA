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
        predalleForm = Form(Data, [new("predalle", "Predalle presente · solo geometria", Bool: true),
            new("h_predalle", "Spessore predalle", "mm"), new("rif_ferri_inf", "Riferimento ferri inferiori", Choices: BridgeSection.BottomRebarReferences)]);
        predalleForm.Editors["predalle"].ToolTip = BridgeSection.PredalleScope;
        predalleForm.Editors["h_predalle"].ToolTip = "Quota dell'estradosso predalle rispetto a y=0. Compresa nello spessore totale della soletta, non aggiunta.";
        predalleForm.Editors["rif_ferri_inf"].ToolTip = "Estradosso predalle: y dei ferri = spessore predalle + distanza all'asse. Intradosso soletta: y = distanza all'asse. Cambiare il riferimento conserva la distanza inserita e sposta la fila.";
        RefreshSlabLayout();
        return Ui.Stack(predalleForm, slabLayoutInfo, Ui.Text(BridgeSection.PredalleScope, 11, color: Ui.Muted));
    }

    private void RefreshSlabLayout()
    {
        predalleForm?.ShowField("h_predalle", Data.B("predalle"));
        predalleForm?.ShowField("rif_ferri_inf", Data.B("predalle") && Data.B("rebars_bottom"));
        try
        {
            var slab = BridgeSection.SlabLayout(Data);
            string lower = Data.B("rebars_bottom") ? $"Ferri inferiori: {slab.BottomReference} + {F(Data.D("cover_bottom"))} mm → y = {F(slab.BottomAxisY)} mm." : "Ferri inferiori assenti.";
            slabLayoutInfo.Text = (slab.HasPredalle ? $"Predalle: y = 0…{F(slab.PredalleThickness)} mm; getto sovrastante: {F(slab.Height - slab.PredalleThickness)} mm.\n" : "y = 0 all'intradosso della soletta.\n") + lower;
        }
        catch (ArgumentException ex) { slabLayoutInfo.Text = ex.Message; }
    }

    private void RefreshSteelHeight()
    {
        if (steelHeightInfo is null) return;
        try
        {
            double clear = BridgeSection.ClearWebHeight(Data);
            Data["h_web"] = clear.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            double second = Data.B("plate2") && Data.S("sezione", BridgeSection.SectionTypes[0]) == BridgeSection.SectionTypes[0] ? Data.D("t_bottom2") : 0;
            steelHeightInfo.Text = $"H = {F(Data.D("h_trave"))} mm · h anima = H − t sup − t inf{(second > 0 ? " − t inf.2" : "")} = {F(clear)} mm.";
        }
        catch (ArgumentException ex) { steelHeightInfo.Text = ex.Message; }
    }
}
