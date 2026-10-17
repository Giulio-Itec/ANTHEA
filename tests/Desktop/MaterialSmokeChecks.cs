using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Anthea.Application.Materials;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.Materials;
using Materiali;
using X.Core;

namespace X.Desktop;

/// <summary>
/// f5-suite-ui: the concrete sheet with the XAML view (ConcreteMaterialSheetView hosted by <see cref="MvvmSheetEditor"/>) as the material
/// suites drive it (ui/smoke-materials, ui/smoke-hierarchy, ui/smoke-sharing): the controls of the view set as the user sets them (class,
/// fields, choices, options, exposure of the menu, deviation value), read back from the view and, for what the historic view computed without
/// showing it, from the ViewModel (as MaterialSheetViewChecks). Elements are found also in the closed parts of the view (expanders, options of
/// the other criterion), as the historic controls existed there.
/// </summary>
internal sealed class ConcreteMaterialSmoke
{
    private readonly MvvmSheetEditor editor;

    internal ConcreteMaterialSmoke(MvvmSheetEditor editor) { this.editor = editor; Sheet = (ConcreteMaterialSheetViewModel)editor.Sheet; }

    /// <summary>The open sheet is the concrete with the XAML view.</summary>
    internal static bool Handles(ISheetEditor? editor) => editor is MvvmSheetEditor { Sheet: ConcreteMaterialSheetViewModel };

    internal ConcreteMaterialSheetViewModel Sheet { get; }

    private List<DependencyObject> Elements => MaterialSheetViewChecks.All(editor.View);

    private static string? BoundPath(FrameworkElement element, DependencyProperty property) => BindingOperations.GetBinding(element, property)?.Path?.Path;

    private T Field<T>(string key) where T : FrameworkElement =>
        Elements.OfType<T>().Single(e => e.DataContext is FieldViewModel f && f.Key == key && (e is not TextBox t || t.TemplatedParent is not Control));

    internal TextBox Box(string key) => Field<TextBox>(key);

    private ComboBox Choice(string key) => Field<ComboBox>(key);

    private CheckBox Option(string key) => Field<CheckBox>(key);

    /// <summary>A choice of the view by the list it shows: «Classes», «Exposures», «DeviationValues».</summary>
    private ComboBox Bound(string itemsPath) => Elements.OfType<ComboBox>().Single(c => BoundPath(c, ItemsControl.ItemsSourceProperty) == itemsPath);

    private TextBlock Text(string path) => Elements.OfType<TextBlock>().Single(t => ReferenceEquals(t.DataContext, Sheet) && BoundPath(t, TextBlock.TextProperty) == path);

    private TextBox Value(string path) => Elements.OfType<TextBox>().Single(t => ReferenceEquals(t.DataContext, Sheet) && BoundPath(t, TextBox.TextProperty) == path);

    private string Cover => Elements.OfType<TextBlock>().Single(t => t.Name == "CoverHeadline").Text;

    private void Exposure(string code) => Bound("Exposures").SelectedItem = code;

    /// <summary>
    /// What the sheet would show with the exposures <paramref name="codes"/> active together, on its current state: the presentation that the
    /// two views show (MaterialSheetPresentation.Evaluate, as the ViewModel and the historic view call it). Only for the combinations that the
    /// XAML view cannot choose (M10).
    /// </summary>
    private MaterialSheetPresentation Combined(params string[] codes) => MaterialSheetPresentation.Evaluate(Sheet.PreviewData, codes);

    /// <summary>
    /// RestoreState of the historic view, through the controls: class, fields, choices (the deviation value after its control), options and
    /// the exposure of the menu. The XAML view has only the exposure of the menu (decision M10): a state with other active exposures fails.
    /// </summary>
    internal void Apply(JsonObject state)
    {
        var codes = (state["esposizioni"] as JsonArray)?.Select(v => v!.GetValue<string>()).ToArray() ?? [];
        string exposure = state["esposizione_principale"]?.ToString() ?? codes.FirstOrDefault() ?? Sheet.SelectedExposure ?? "";
        if (codes.Length > 1 || codes.Length == 1 && codes[0] != exposure)
            throw new InvalidOperationException("Stato con più esposizioni attive: la vista XAML sceglie solo l'esposizione del menu (M10).");
        if (state["classe"] is JsonValue name && Sheet.SelectedClass != name.GetValue<string>()) Bound("Classes").SelectedItem = name.GetValue<string>();
        if (state["numeri"] is JsonObject numbers)
            foreach (var (key, value) in numbers)
                if (value is JsonValue text && Sheet.Inputs.Get(key) != text.GetValue<string>()) Box(key).Text = text.GetValue<string>();
        if (state["scelte"] is JsonObject choices)
        {
            foreach (var (key, value) in choices.Where(p => p.Key != ConcreteMaterialSheetViewModel.DeviationValueKey))
                if (value is JsonValue item && Sheet.Inputs.Get(key) != item.GetValue<string>()) Choice(key).SelectedItem = item.GetValue<string>();
            if (choices[ConcreteMaterialSheetViewModel.DeviationValueKey] is JsonValue deviation && Sheet.SelectedDeviationValue != deviation.GetValue<string>())
                Bound("DeviationValues").SelectedItem = deviation.GetValue<string>();
        }
        if (state["opzioni"] is JsonObject flags)
            foreach (var (key, value) in flags)
                if (value is JsonValue flag && Option(key).IsChecked != flag.GetValue<bool>()) Option(key).IsChecked = flag.GetValue<bool>();
        if (Sheet.SelectedExposure != exposure) Exposure(exposure);
    }

    /// <summary>The user types <paramref name="text"/> in a field of the concrete.</summary>
    internal void Type(string key, string text) => Box(key).Text = text;

    /// <summary>
    /// MaterialView.Check of the historic view (supporto/test/Materiali/MaterialViewChecks.cs) on the XAML view: the same benchmarks of the
    /// libraries, the same edits through the controls and the same expected texts. A combination of exposures (the hidden check boxes of the
    /// historic view: XS3+XF4, XF3+XS3, X0+XC4, XC1+XS3) cannot be chosen in the XAML view, which has only the exposure of the menu (decision
    /// M10): its expected texts are checked on the functions that both views show (<see cref="Combined"/>, docs/refactoring/f5-suite-ui.md S4).
    /// The exposure XF2, checked by hand in the historic view, is chosen from the menu.
    /// </summary>
    internal void SelfCheck()
    {
        static string Number(double value) => Math.Abs(value).ToString("N2", CultureInfo.CurrentCulture);
        foreach (var item in MaterialInputCatalog.MaterialSheetClasses())
        {
            var m = ConcreteMaterialCatalog.Material(item.Fck);
            if (!double.IsFinite(m.Ecm) || m.Ecm <= 0 || Math.Abs(Math.Abs(m.Fck) - item.Fck) > 1e-9)
                throw new Exception("Proprietà non valide: " + item.Name);
        }
        var baseline = ConcreteMaterialCatalog.Material(30);
        if (Math.Abs(Math.Abs(baseline.Fcm) - 38) > 1e-6 || Math.Abs(baseline.Ecm - 32836.568) > 1 ||
            Math.Abs(Math.Abs(baseline.Fctm) - 2.896468) > 1e-5 ||
            Math.Abs(Math.Abs(baseline.StrainUCompression) - .0035) > 1e-8)
            throw new Exception("Valori C30/37 inattesi.");
        Bound("Classes").SelectedItem = "C50/60";
        // The card of fck was built by the historic view and never added to it: from the ViewModel, as MaterialSheetViewChecks.
        if (Sheet.Strength != Number(50)) throw new Exception("Aggiornamento classe non riuscito.");
        Bound("Classes").SelectedItem = "C30/37";
        Materiali.DurabilityReferenceChecks.CheckDurability();
        CheckBond();
        Materiali.DurabilityReferenceChecks.CheckNtcCover();
        CheckAutomaticMix();
        CheckExposureSelector();
        Exposure("XF2");
        if (!Cover.Contains("45")) throw new Exception("XF2 NTC trave errato.");
        Choice("deviationControl").SelectedIndex = 1;
        Bound("DeviationValues").SelectedItem = "5 mm";
        if (!Cover.Contains("40")) throw new Exception("Tolleranza ridotta errata.");
        Choice("deviationControl").SelectedIndex = 2;
        Bound("DeviationValues").SelectedItem = "0 mm";
        if (!Cover.Contains("35")) throw new Exception("Tolleranza zero errata.");
        Choice("deviationControl").SelectedIndex = 0;
        var deviation = Bound("DeviationValues");
        if (deviation.SelectedItem as string != "10 mm" || deviation.Items.Count != 1 || !Cover.Contains("45"))
            throw new Exception("Ripristino tolleranza ordinaria errato.");
        Choice("ntcElement").SelectedIndex = 1;
        if (!Cover.Contains("40")) throw new Exception("XF2 NTC piastra errato.");
        Choice("coverMethod").SelectedIndex = 1;
        if (Cover != "Da completare") throw new Exception("XF2 EC2 non invalidato.");
        Choice("coverMethod").SelectedIndex = 0; Choice("ntcElement").SelectedIndex = 0;
        if (!Combined("XS3", "XF4").CoverHeadline.Contains("60")) throw new Exception("Combinazione esposizioni non aggiornata.");
        Box("diameter").Text = "NaN";
        if (Cover != "Da completare") throw new Exception("Copriferro obsoleto con input non valido.");
        Box("diameter").Text = "16";
        Choice("consistency").SelectedIndex = 4;
        // The menu of the historic view stays on XC1 at the end of its check (the exposure of the state it captures).
        Exposure("XC1");
    }

    private void CheckBond()
    {
        var good = ConcreteBond.Calculate(30, 16, 1, 1, 1.5);
        if (Math.Abs(good.Fbd - 3.041291) > 1e-5) throw new Exception("Benchmark aderenza C30/37 errato.");
        if (Math.Abs(ConcreteBond.Calculate(30, 40, .7, 1, 1.5).Fbd - good.Fbd * .7 * .92) > 1e-10) throw new Exception("Coefficienti aderenza errati.");
        if (ConcreteBond.Calculate(90, 16, 1, 1, 1.5).Fbd != ConcreteBond.Calculate(60, 16, 1, 1, 1.5).Fbd) throw new Exception("Limite C60/75 mancante.");
        Choice("bondCondition").SelectedIndex = 0;
        var bond = Text("BondValue");
        if (!bond.Text.Contains(good.Fbd.ToString("0.00"))) throw new Exception("Aggiornamento aderenza errato: " + bond.Text + " · " + Text("BondDetails").Text);
        Box("bondGamma").Text = "0";
        if (bond.Text != "Da completare") throw new Exception("Risultato aderenza obsoleto.");
        Box("bondGamma").Text = "1,5";
    }

    private void CheckAutomaticMix()
    {
        string[] keys = ["chloride", "ratio", "cement", "air", "dmax"];
        void Expect(string key, string expected)
        {
            string shown = Value("Composition[" + key + "]").Text;
            if (shown != expected) throw new Exception($"ATECAP {key}: {shown}, atteso {expected}");
        }
        Box("aggregate").Text = "32";
        foreach (var (code, ratio, cement) in new[] { ("X0", "Non prescritto", "Non prescritto"), ("XC1", "0,60", "300"), ("XC2", "0,60", "300"), ("XC3", "0,55", "320"), ("XC4", "0,50", "340"), ("XS1", "0,50", "340"), ("XS2", "0,45", "360"), ("XS3", "0,45", "360"), ("XD1", "0,55", "320"), ("XD2", "0,50", "340"), ("XD3", "0,45", "360"), ("XA1", "0,55", "320"), ("XA2", "0,50", "340"), ("XA3", "0,45", "360"), ("XF1", "0,50", "320"), ("XF2", "0,50", "340"), ("XF4", "0,45", "360"), ("XF3", "0,50", "340") })
        { Exposure(code); Expect("ratio", ratio); Expect("cement", cement); }
        Expect("air", "4,0"); Box("aggregate").Text = "16"; Expect("air", "5,0");
        foreach (var d in new[] { "20", "18", "8" }) { Box("aggregate").Text = d; Expect("air", "Da definire"); }
        Box("aggregate").Text = "NaN"; Expect("dmax", "Da definire"); Expect("air", "Da definire");
        Box("aggregate").Text = "32";
        void ExpectCombined(string[] codes, string key, string expected)
        {
            string shown = Combined(codes).Composition[key];
            if (shown != expected) throw new Exception($"ATECAP {key} ({string.Join("+", codes)}): {shown}, atteso {expected}");
        }
        ExpectCombined(["XF3", "XS3"], "ratio", "0,45"); ExpectCombined(["XF3", "XS3"], "cement", "360"); ExpectCombined(["XF3", "XS3"], "air", "4,0");
        Exposure("X0"); Expect("ratio", "Non prescritto"); Expect("cement", "Non prescritto"); Expect("air", "Non prescritto");
        ExpectCombined(["X0", "XC4"], "ratio", "Da definire"); ExpectCombined(["X0", "XC4"], "cement", "Da definire");
        Exposure("XC4"); Choice("life").SelectedIndex = 1;
        if (!Text("CompositionSource").Text.Contains("100 anni")) throw new Exception("Nota vita utile assente.");
        Choice("cement").SelectedIndex = 1; Expect("cement", "340"); Expect("chloride", "Cl 0,40");
        if (keys.Any(k => !Value("Composition[" + k + "]").IsReadOnly)) throw new Exception("Campo automatico modificabile.");
        Choice("cement").SelectedIndex = 0; Choice("life").SelectedIndex = 0; Box("aggregate").Text = "20"; Exposure("XC1");
    }

    private void CheckExposureSelector()
    {
        foreach (var (codes, expected) in new[] { ("X0 XC1 XC2 XC3 XF1", "ordinaria"), ("XC4 XD1 XS1 XA1 XA2 XF2 XF3", "aggressiva"), ("XD2 XD3 XS2 XS3 XA3 XF4", "molto aggressiva") })
            foreach (var code in codes.Split(' '))
            {
                Exposure(code);
                var minimum = Value("MinimumConcreteClass");
                if (!minimum.IsReadOnly || minimum.Text != Materiali.MinimumConcrete.Label(Materiali.MinimumConcrete.Fck(code))) throw new Exception("Classe minima non aggiornata: " + code);
                var details = Text("ExposureDetails").Text; var examples = Text("ExposureExamples").Text;
                if (Sheet.ActiveExposures.Count != 1 || Sheet.ActiveExposures[0] != code || details != MaterialExposurePresentation.Descriptions[code] || examples != MaterialExposurePresentation.Examples[code]
                    || string.IsNullOrWhiteSpace(examples) || Text("Aggressiveness").Text != "Aggressività: " + expected)
                    throw new Exception("Menu/descrizione/aggressività non aggiornati: " + code);
            }
        var both = MaterialSheetInput.ExposureChoices.Where(e => e.Code is "XC1" or "XS3").ToArray();
        if (!MaterialExposurePresentation.Texts("XC1", both, null).Combined.Contains("molto aggressiva") || Combined("XC1", "XS3").MinimumConcreteClass != "C35/45")
            throw new Exception("Combinazione non aggiornata.");
        Exposure("XC4");
        if (Sheet.ActiveExposures.Count != 1 || Value("MinimumConcreteClass").Text != "C32/40") throw new Exception("Cambio menu non propagato al calcolo.");
        Exposure("XC1");
    }
}

/// <summary>
/// f5-suite-ui: the reinforcing steel sheet as ui/smoke-steel and ui/smoke-hierarchy drive it, with either view: the historic RebarMaterialView
/// (Selection, Properties, Input, Status) or the XAML view (RebarMaterialSheetView: the type, the fields of its FieldForm, the status line).
/// </summary>
internal sealed class SteelSmoke(ISheetEditor editor)
{
    private readonly RebarMaterialView? historic = (editor as SheetEditor)?.rebarMaterial;

    private List<DependencyObject> Elements => MaterialSheetViewChecks.All(editor.View);

    private T Field<T>(string key) where T : FrameworkElement =>
        Elements.OfType<T>().Single(e => e.DataContext is FieldViewModel f && f.Key == key && (e is not TextBox t || t.TemplatedParent is not Control));

    private ComboBox TypeChoice => Elements.OfType<ComboBox>().Single(c => c.Name == "SteelType");

    /// <summary>The type of steel chosen (a preset of the catalogue or «Personalizzato»).</summary>
    internal string? Type
    {
        get => historic is not null ? historic.Selection.SelectedItem?.ToString() : TypeChoice.SelectedItem as string;
        set { if (historic is not null) historic.Selection.SelectedItem = value; else TypeChoice.SelectedItem = value; }
    }

    /// <summary>The text box of a property does not accept typing (a property fixed by the preset).</summary>
    internal bool IsReadOnly(string key) => historic is not null ? ((TextBox)historic.Properties.Editors[key]).IsReadOnly : Field<TextBox>(key).IsReadOnly;

    /// <summary>The input of the steel, as the sheet holds it.</summary>
    internal JsonObject Input => historic?.Input ?? editor.Data["input"]!.AsObject();

    /// <summary>The status line under the properties.</summary>
    internal string Status => historic is not null ? historic.Status.Text : Elements.OfType<TextBlock>().Single(t => t.Name == "StatusText").Text;

    /// <summary>The user types (or chooses) <paramref name="value"/> for a property, as InputForm.Set of the historic view.</summary>
    internal void Set(string key, string value)
    {
        if (historic is not null) { historic.Properties.Set(key, value); return; }
        if (Elements.OfType<ComboBox>().SingleOrDefault(c => c.DataContext is FieldViewModel f && f.Key == key) is { } choice) choice.SelectedItem = value;
        else Field<TextBox>(key).Text = value;
    }
}
