using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

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

    private InputForm CreateChsForm(JsonObject input)
    {
        var form = new InputForm(input, [
            new("modo_chs", "Inserimento CHS", Choices: ["Catalogo", "Manuale"]),
            new("profilo_chs", "Catalogo ANTHEA", Choices: Chs.Catalogo.Keys.ToArray()),
            new("diametro_chs_mm", "Diametro esterno manuale", "mm", Symbol: "De"),
            new("spessore_chs_mm", "Spessore manuale", "mm", Symbol: "t"),
            new("fy_chs_mpa", "Snervamento acciaio", "MPa", Symbol: "fy"),
            new("gamma_m0", "Sicurezza resistenza", Symbol: "γM0"),
            new("__fyd", "Resistenza di progetto", "MPa", Symbol: "fyd", ReadOnly: true),
            new("__diametro_mm", "Diametro adottato", "mm", Symbol: "De", ReadOnly: true),
            new("__spessore_mm", "Spessore adottato", "mm", Symbol: "t", ReadOnly: true),
            new("__area_mm2", "Area acciaio", "mm²", Symbol: "A", ReadOnly: true),
            new("__inerzia_mm4", "Momento d’inerzia", "mm⁴", Symbol: "I", ReadOnly: true),
            new("__wel_mm3", "Modulo elastico", "mm³", Symbol: "Wel", ReadOnly: true),
            new("__wpl_mm3", "Modulo plastico", "mm³", Symbol: "Wpl", ReadOnly: true),
            new("__massa_kg_m", "Massa lineare acciaio", "kg/m", ReadOnly: true),
            new("__classe", "Classe sezione", ReadOnly: true),
            new("__npl_kn", "Resistenza assiale sezione", "kN", Symbol: "Npl", ReadOnly: true),
            new("__mpl_knm", "Momento plastico N=0", "kNm", Symbol: "Mpl", ReadOnly: true)
        ], _ => Changed(), compact: true, symbolColumns: true);
        form.GroupFields("Tubolare CHS", ["modo_chs", "profilo_chs", "diametro_chs_mm", "spessore_chs_mm"], true);
        form.GroupFields("Acciaio", ["fy_chs_mpa", "gamma_m0", "__fyd"], true);
        form.GroupFields("Proprietà della sezione", ["__diametro_mm", "__spessore_mm", "__area_mm2", "__inerzia_mm4", "__wel_mm3", "__wpl_mm3", "__massa_kg_m", "__classe", "__npl_kn", "__mpl_knm"], true);
        return form;
    }
    private void UpdateChsPreview()
    {
        bool catalog = Data["sezione"].S("modo_chs") == "Catalogo";
        sectionFields.Enable("profilo_chs", catalog, true);
        sectionFields.Enable("diametro_chs_mm", !catalog, true); sectionFields.Enable("spessore_chs_mm", !catalog, true);
        string[] keys = ["diametro_mm", "spessore_mm", "area_mm2", "inerzia_mm4", "wel_mm3", "wpl_mm3", "massa_kg_m", "classe", "npl_kn", "mpl_knm"];
        try {
            var p = MicropaloOrizzontale.Properties(Data);
            foreach (string key in keys) sectionFields.Set("__" + key, p.D(key).ToString(key == "classe" ? "0" : "N1"), true);
            sectionFields.Set("__fyd", p.D("fyd_mpa").ToString("N1"), true);
            chsDrawing.Properties = p;
        }
        catch (ArgumentException) {
            foreach (string key in keys) sectionFields.Set("__" + key, "—", true);
            sectionFields.Set("__fyd", "—", true); chsDrawing.Properties = null;
        }
        chsDrawing.InvalidateVisual();
    }
}

internal sealed class ChsDrawing : DrawingView
{
    internal JsonObject? Properties { get; set; }
    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(size));
        if (Properties is not { } p) { Text(dc, "Completare i dati CHS", 20, 30); return; }
        double r = Math.Max(1, Math.Min(size.Width - 60, size.Height - 100) / 2);
        Point center = new(size.Width / 2, (size.Height - 65) / 2);
        dc.DrawEllipse(Ui.Brush("#96999D"), new Pen(Ui.Navy, 1.5), center, r, r);
        double ri = r * p.D("diametro_interno_mm") / p.D("diametro_mm");
        dc.DrawEllipse(Brushes.White, new Pen(Ui.Navy, 1), center, ri, ri);
        Text(dc, $"CHS {p.D("diametro_mm"):0.0} × {p.D("spessore_mm"):0.0} mm\nSolo acciaio · riempimento non resistente", 15, size.Height - 55, 12, Ui.Navy, size.Width - 30);
    }
}
