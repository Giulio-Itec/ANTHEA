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
    private static readonly (string Input, string Standard)[] CommonCoefficients = [("alpha_cc", "AlphaCC"), ("gamma_c", "GammaC"), ("gamma_s", "GammaS")];
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
    private void PrepareStirrups()
    {
        if (settings["taglio"] is not JsonObject) settings["taglio"] = new JsonObject();
        foreach (var (key, value) in new[] { ("tipo_staffa", "Staffa chiusa"), ("rami_x", "2"), ("rami_y", "2"), ("rami_interni", "0") })
            if (!ShearOptions.ContainsKey(key)) ShearOptions[key] = value;
    }
    private UIElement BuildStirrups()
    {
        var dimensions = new InputForm(Input, [new("transverse_bar_diameter_mm", "Ø staffe", "mm"), new("transverse_spacing_mm", "Passo", "mm")], _ => { SynchronizeStirrups(); Invalidate(); }, true);
        var form = new InputForm(ShearOptions, [new("tipo_staffa", "Circolare", Choices: ["Staffa chiusa", "Spirale"]), new("rami_x", "Braccia resistenti a Vx"), new("rami_y", "Braccia resistenti a Vy"), new("rami_interni", "Ferri interni circolari")], _ => { SynchronizeStirrups(); Invalidate(); }, true, true);
        stirrupForms.Add(dimensions); stirrupForms.Add(form);
        return Ui.Stack(dimensions, form, Ui.Text("Schema indicativo, non esecutivo. Rettangolare / T: staffe a più braccia. Circolare: staffa o spirale e ferri interni; modello resistente circolare ancora da validare.", 11, color: Ui.Muted));
    }
    private void SynchronizeStirrups()
    {
        foreach (var form in stirrupForms)
        {
            foreach (var key in form.Editors.Keys) form.Set(key, key.StartsWith("transverse_") ? Input.S(key) : ShearOptions.S(key), true);
            form.ShowField("tipo_staffa", Input.S("shape") == "Circolare"); form.ShowField("rami_interni", Input.S("shape") == "Circolare");
            foreach (var key in new[] { "rami_x", "rami_y" }) form.ShowField(key, Input.S("shape") != "Circolare");
        }
        foreach (var key in new[] { "transverse_bar_diameter_mm", "transverse_spacing_mm" }) reinforcement?.Set(key, Input.S(key), true);
        foreach (var key in new[] { "rami_x", "rami_y" }) shearForm?.Set(key, ShearOptions.S(key), true);
        foreach (var view in new[] { preview, shearView }.Concat(stressPanels.Values.Select(p => p.View))) { view.Stirrups = ShearOptions; view.InvalidateVisual(); }
    }
    private void ValidateStirrups()
    {
        foreach (var key in Input.S("shape") == "Circolare" ? new[] { "rami_interni" } : new[] { "rami_x", "rami_y" })
        {
            // Legacy blank values are allowed as an unfinished shear model, not as a fabricated check.
            if (string.IsNullOrWhiteSpace(ShearOptions.S(key))) continue;
            double n = SectionWorkspace.Number(ShearOptions.S(key), key); int min = key == "rami_interni" ? 0 : 2;
            if (n < min || n > 100 || n != Math.Truncate(n)) throw new ArgumentException($"{key}: inserire un numero intero fra {min} e 100.");
        }
    }
}
