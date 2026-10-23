using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

// Check of the CHS section of the horizontal micropile (--smoke-horizontal), moved out of HorizontalChs.cs.
internal sealed partial class HorizontalWorkspace
{
    internal async Task VerifyChs()
    {
        await WaitForAutomatic();
        if (Result?["sezione"].S("tipo") != "CHS" || chsDrawing.Properties is null) throw new Exception("Calcolo CHS iniziale fallito");
        double original = Result.D("momento_resistente_knm");
        ((ComboBox)sectionFields.Editors["modo_chs"]).SelectedItem = "Manuale";
        await WaitForAutomatic();
        if (Result is null || Math.Abs(Result.D("momento_resistente_knm") - original) > 1e-8) throw new Exception("CHS manuale discordante dal catalogo");
        sectionFields.Set("spessore_chs_mm", "10"); await WaitForAutomatic();
        if (Result is null || Result.D("momento_resistente_knm") <= original || chsDrawing.Properties?.D("spessore_mm") != 10) throw new Exception("CHS manuale non aggiornato");
        sectionFields.Set("spessore_chs_mm", "0"); await WaitForAutomatic();
        if (Result is not null || chsDrawing.Properties is not null) throw new Exception("CHS risultati obsoleti");
        sectionFields.Set("spessore_chs_mm", "8");
        ((ComboBox)sectionFields.Editors["modo_chs"]).SelectedItem = "Catalogo";
        ((ComboBox)sectionFields.Editors["profilo_chs"]).SelectedItem = "CHS 114.3 × 6.3";
        await WaitForAutomatic();
        if (Result is null || chsDrawing.Properties?.D("diametro_mm") != 114.3 || sectionFields.Editors["diametro_chs_mm"].IsEnabled)
            throw new Exception("Catalogo CHS non applicato");
    }
}
