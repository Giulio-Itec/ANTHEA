using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private InputForm? coefficients;
    private readonly TextBlock standardNote = Ui.Text("", 11, color: Ui.Muted);
    private readonly List<InputForm> stirrupForms = [];
    private static readonly (string Input, string Standard)[] CommonCoefficients = ConcreteCalculationSettings.CommonCoefficients;
    private void PrepareCoefficients()
    {
        if (settings["coefficienti"] is not JsonObject) settings["coefficienti"] = ConcreteStandards.Defaults(settings.S("normativa", "NTC 2018"));
        SyncCoefficientsFromInput(); standardNote.Text = ConcreteStandards.Note(settings.S("normativa"));
    }
    private void SyncCoefficientsFromInput()
    {
        foreach (var (input, standard) in CommonCoefficients)
        { settings["coefficienti"]![standard] = Input[input]?.DeepClone(); coefficients?.Set(standard, Input.S(input), true); }
    }
    private void ResetCoefficients()
    {
        var values = settings["coefficienti"]!.AsObject(); var defaults = ConcreteStandards.Defaults(settings.S("normativa"));
        foreach (var (key, value) in defaults) { values[key] = value?.DeepClone(); coefficients?.Set(key, value?.ToString() ?? "", true); }
        foreach (var (input, standard) in CommonCoefficients) { Input[input] = defaults[standard]?.DeepClone(); materials?.Set(input, defaults.S(standard), true); }
        standardNote.Text = ConcreteStandards.Note(settings.S("normativa"));
    }
    private UIElement BuildCoefficients()
    {
        coefficients = new InputForm(settings["coefficienti"]!.AsObject(), ConcreteStandards.Coefficients.Select(c => new Field(c.Key, c.Label)), key =>
        {
            foreach (var pair in CommonCoefficients.Where(p => p.Standard == key)) { Input[pair.Input] = settings["coefficienti"]![key]?.DeepClone(); materials.Set(pair.Input, Input.S(pair.Input), true); }
            Invalidate();
        }, true);
        return Ui.Stack(Notice("Valori iniziali dalla classe della DLL. Modifiche salvate nel foglio. Cambiare normativa ripristina i coefficienti della nuova classe. Coefficienti accidentali/FRC esposti per predisposizione, non equivalgono a verifiche implementate."), coefficients, Ui.Button("Ripristina coefficienti della normativa", () => { ResetCoefficients(); Invalidate(); }));
    }
    private void PrepareStirrups() => ConcreteCalculationSettings.Prepare(Input, settings);
    private UIElement BuildStirrups(bool readOnly = false)
    {
        if(!Input.ContainsKey("staffe_presenti"))Input["staffe_presenti"]="Sì";
        if (readOnly)
        {
            var values = new InputForm(Input, [new("staffe_presenti", "Staffe presenti", ReadOnly: true), new("transverse_bar_diameter_mm", "Ø staffe", "mm", ReadOnly: true), new("transverse_spacing_mm", "Passo", "mm", ReadOnly: true)], _ => { }, true);
            var scheme = new InputForm(ShearOptions, [new("tipo_staffa", "Circolare", ReadOnly: true), new("rami_x", "Braccia resistenti a Vx", ReadOnly: true), new("rami_y", "Braccia resistenti a Vy", ReadOnly: true), new("schema_interno", "Schema interno", ReadOnly: true), new("rami_interni", "Bracci aggiunti / staffe interne", ReadOnly: true), new("rotazione_staffa", "Rotazione schema", "°", ReadOnly: true)], _ => { }, true);
            stirrupForms.Add(values); stirrupForms.Add(scheme);
            return Ui.Stack(values, scheme, Ui.Button("Modifica staffe nel pannello di controllo →", () => tabs.SelectedIndex = 0));
        }
        var dimensions = new InputForm(Input, [new("staffe_presenti", "Staffe presenti", Choices:["Sì","No"]),new("transverse_bar_diameter_mm", "Ø staffe", "mm"), new("transverse_spacing_mm", "Passo", "mm")], key => { if(Input.S("staffe_presenti")=="No") {ShearOptions["modello"]="Senza staffe";shearForm?.Set("modello","Senza staffe",true);} SynchronizeStirrups(); RefreshDetailing(); if (key is "transverse_bar_diameter_mm" or "staffe_presenti") Invalidate(); else InvalidateActions("Taglio"); }, true);
        var form = new InputForm(ShearOptions, [new("tipo_staffa", "Circolare", Choices: ["Staffa chiusa", "Spirale"]), new("rami_x", "Braccia resistenti a Vx"), new("rami_y", "Braccia resistenti a Vy"), new("schema_interno", "Schema interno", Choices: ["Bracci paralleli", "Staffe chiuse sovrapposte"]), new("rami_interni", "Bracci aggiunti / staffe interne"), new("rotazione_staffa", "Rotazione schema", "°")], _ => { SynchronizeStirrups(); RefreshDetailing(); InvalidateActions("Taglio"); }, true, true);
        stirrupForms.Add(dimensions); stirrupForms.Add(form);
        return Ui.Stack(dimensions, form, Ui.Text("Schema indicativo, non esecutivo. Rettangolare / T: staffe a più braccia. Circolare: bracci e staffe interne sono rappresentati secondo lo schema scelto; assegnare i rami effettivamente resistenti a Vx e Vy. Il modello di calcolo circolare si sceglie nella scheda Taglio e torsione.", 11, color: Ui.Muted));
    }
    private void SynchronizeStirrups()
    {
        foreach (var form in stirrupForms)
        {
            foreach (var key in form.Editors.Keys) form.Set(key, key.StartsWith("transverse_") || key=="staffe_presenti" ? Input.S(key) : ShearOptions.S(key), true);
            foreach(var key in form.Editors.Keys.Where(k=>k!="staffe_presenti"))form.Enable(key,Input.S("staffe_presenti","Sì")=="Sì");
            form.ShowField("tipo_staffa", Input.S("shape") == "Circolare"); form.ShowField("rami_interni", Input.S("shape") == "Circolare"); form.ShowField("schema_interno", Input.S("shape") == "Circolare"); form.ShowField("rotazione_staffa", Input.S("shape") == "Circolare");
            foreach (var key in new[] { "rami_x", "rami_y" }) form.ShowField(key, true);
        }
        foreach (var key in new[] { "transverse_bar_diameter_mm", "transverse_spacing_mm" }) reinforcement?.Set(key, Input.S(key), true);
        foreach (var key in new[] { "rami_x", "rami_y" }) shearForm?.Set(key, ShearOptions.S(key), true);
        foreach (var view in new[] { preview, shearView }.Concat(stressPanels.Values.Select(p => p.View))) { view.Stirrups = ShearOptions; view.InvalidateVisual(); }
    }
    private void ValidateStirrups() => ConcreteCalculationSettings.ValidateStirrups(Input, ShearOptions);
}
