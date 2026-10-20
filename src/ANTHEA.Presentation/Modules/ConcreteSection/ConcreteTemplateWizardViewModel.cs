using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.ConcreteWorkspace;
using Anthea.Presentation.Forms;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>Detached draft: selecting, previewing and cancelling never changes the document.</summary>
public sealed class ConcreteTemplateWizardViewModel : ObservableObject
{
    private readonly JsonObject data;
    private bool selecting;
    private ConcreteGeometry? geometry;
    private string error = "", note = "";
    public ConcreteTemplateWizardViewModel(JsonObject data)
    {
        this.data = data;
        Draft = ConcreteTemplateWizard.Draft(data["input"]!.AsObject());
        Form = new(Draft, [
            new("wizard_type", "Tipologia", Choices: ConcreteTemplateWizard.Choices.ToArray(), Wide: true),
            new("width_mm","Larghezza / base superiore / D circoscritto poligono","mm"), new("height_mm","Altezza totale","mm"),
            new("diameter_mm","Diametro esterno","mm"), new("inner_diameter_mm","Diametro interno","mm"),
            new("inner_width_mm","Larghezza foro","mm"), new("inner_height_mm","Altezza foro","mm"),
            new("flange_width_mm","Larghezza ala T","mm"), new("web_width_mm","Spessore anima","mm"),
            new("flange_thickness_mm","Spessore ala superiore / ala L","mm"),
            new("bottom_flange_width_mm","Larghezza ala / base inferiore","mm"), new("bottom_flange_thickness_mm","Spessore ala inferiore","mm"),
            new("cover_mm","Copriferro netto alle staffe","mm"), new("wizard_bar_diameter_mm","Diametro barre perimetrali","mm"),
            new("wizard_bar_spacing_mm","Passo massimo lungo il contorno","mm"),
            new("wizard_radial_count","Barre radiali / anello esterno"), new("wizard_inner_count","Barre anello interno"),
            new("wizard_stadium_layout","Armature della capsula", Choices: [
                ConcreteTemplateAdapter.StadiumSeparate, ConcreteTemplateAdapter.StadiumLegacy], Wide: true),
            new("wizard_stadium_curve_count","Barre per arco (2 tangenti incluse)"),
            new("wizard_stadium_curve_diameter_mm","Ø barre dei tratti curvi","mm"),
            new("wizard_stadium_straight_count","Barre interne per rettilineo"),
            new("wizard_stadium_straight_diameter_mm","Ø barre dei rettilinei","mm"),
            new("wizard_polygon_sides","Numero lati poligono (3–32)"),
            new("top_bar_count","Barre prima fila superiore"), new("top_bar_diameter_mm","Ø prima fila superiore","mm"),
            new("bottom_bar_count","Barre prima fila inferiore"), new("bottom_bar_diameter_mm","Ø prima fila inferiore","mm"),
            new("side_bar_count_per_side","Barre intermedie per lato"), new("side_bar_diameter_mm","Ø laterali","mm"),
            new("second_top_count","Barre seconda fila superiore"), new("second_top_diameter","Ø seconda fila superiore","mm"), new("second_top_gap","Interferro verticale superiore","mm"),
            new("second_bottom_count","Barre seconda fila inferiore"), new("second_bottom_diameter","Ø seconda fila inferiore","mm"), new("second_bottom_gap","Interferro verticale inferiore","mm"),
            new("longitudinal_bar_count","Barre anello esterno"), new("longitudinal_bar_diameter_mm","Ø anello esterno","mm"),
            new("second_inner_count","Barre secondo anello"), new("second_inner_diameter","Ø secondo anello","mm"), new("second_inner_gap","Interferro radiale","mm"),
            new("transverse_bar_diameter_mm","Diametro staffe","mm"), new("transverse_spacing_mm","Passo staffe","mm")
        ], Changed, compact: true) { CommitOnFocusLoss = true };
        Refresh();
    }
    public JsonObject Draft { get; }
    public FormViewModel Form { get; }
    public ConcreteGeometry? Geometry { get => geometry; private set => SetProperty(ref geometry, value); }
    public string Error { get => error; private set { SetProperty(ref error, value); OnPropertyChanged(nameof(CanApply)); } }
    public string Note { get => note; private set => SetProperty(ref note, value); }
    public bool CanApply => Geometry is not null && Error.Length == 0;
    public event EventHandler? PreviewChanged;
    private void Changed(string key)
    {
        if (selecting) return;
        if (key == "wizard_type")
        {
            selecting = true;
            try
            {
                ConcreteTemplateWizard.Select(Draft, Draft.S(key));
                foreach (var field in Form.Fields) Form.Set(field.Key, Draft.S(field.Key), true);
            }
            finally { selecting = false; }
        }
        Refresh();
    }
    public void Refresh()
    {
        string shape = Draft.S("shape");
        bool circle = shape == "Circolare", hole = Draft.B("foro_presente");
        bool extended = ConcreteTemplateAdapter.Uses(Draft), profile = shape is "A I" or "A L" or "A C" or "A T";
        bool separateStadium = ConcreteTemplateAdapter.SeparateStadium(Draft);
        foreach (var field in Form.Fields)
        {
            string k = field.Key;
            bool shown = k switch {
                "wizard_type" or "cover_mm" or "transverse_bar_diameter_mm" or "transverse_spacing_mm" => true,
                "width_mm" => !circle && shape != "A T", "height_mm" => !circle && shape != "Poligonale regolare", "diameter_mm" => circle,
                "inner_diameter_mm" => circle && hole, "inner_width_mm" or "inner_height_mm" => !circle && hole,
                "flange_width_mm" => shape == "A T", "web_width_mm" or "flange_thickness_mm" => profile,
                "bottom_flange_width_mm" => shape is "A I" or "A C" or "Trapezoidale",
                "bottom_flange_thickness_mm" => shape is "A I" or "A C",
                "wizard_polygon_sides" => shape == "Poligonale regolare",
                "wizard_stadium_layout" => shape == "Capsula",
                _ when k.StartsWith("wizard_stadium_") => separateStadium,
                "wizard_bar_diameter_mm" => extended && !separateStadium,
                "wizard_bar_spacing_mm" => extended && shape is not ("Circolare" or "Ellittica" or "Capsula"),
                "wizard_radial_count" => extended && !separateStadium && shape is "Circolare" or "Ellittica" or "Capsula",
                "wizard_inner_count" => circle && hole,
                _ when k.StartsWith("second_top") || k.StartsWith("second_bottom") => shape == "Rettangolare" && !hole,
                _ when k.StartsWith("second_inner") || k.StartsWith("longitudinal") => circle && !hole,
                _ => !extended && !circle
            };
            Form.ShowField(k, shown);
        }
        Note = ConcreteTemplateWizard.Note(Draft);
        try { Geometry = ConcreteTemplateWizard.Preview(Draft); Error = ""; }
        catch (ArgumentException exception) { Geometry = null; Error = exception.Message; }
        OnPropertyChanged(nameof(CanApply));
        PreviewChanged?.Invoke(this, EventArgs.Empty);
    }
    public bool Apply()
    {
        Form.Commit(); Refresh();
        if (!CanApply) return false;
        try { ConcreteTemplateWizard.Apply(data, Draft); return true; }
        catch (ArgumentException exception) { Error = exception.Message; return false; }
    }
}
