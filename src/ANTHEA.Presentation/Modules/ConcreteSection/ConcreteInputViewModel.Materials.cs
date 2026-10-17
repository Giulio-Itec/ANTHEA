using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Materials;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>Materials, coefficients and saved materials of the inputs (ConcreteMaterials.cs, ConcreteMaterialEditor.cs and ConcreteParameters.cs of the historic view).</summary>
public sealed partial class ConcreteInputViewModel
{
    /// <summary>The materials saved in the sheet («Tipo · nome»); the last one selected after every change of the list.</summary>
    public ConcreteChoiceViewModel SavedMaterials { get; } = new();
    /// <summary>«Applica materiale salvato».</summary>
    public IRelayCommand ApplySavedMaterialCommand { get; }
    /// <summary>«Nuovo materiale CLS», «Nuovo materiale acciaio», «Nuovo materiale trefoli»: the window on the dialog service.</summary>
    public IAsyncRelayCommand NewConcreteMaterialCommand { get; }
    public IAsyncRelayCommand NewSteelMaterialCommand { get; }
    public IAsyncRelayCommand NewTendonMaterialCommand { get; }
    /// <summary>«Trefoli da standard EN 1992»: the default material of the new cables (the catalogue and the saved tendons).</summary>
    public ConcreteChoiceViewModel StandardTendon { get; } = new();

    private JsonObject[] tendonCatalog = [];
    private bool refreshingTendon;
    private readonly JsonObject tendonValues = new();

    private FormViewModel BuildMaterials()
    {
        StandardNote = ConcreteEditor.StandardNote(Settings.S("normativa"));
        var concreteCatalog = MaterialInputCatalog.Concrete(Settings.S("normativa"));
        var steelCatalog = MaterialInputCatalog.Steel(false, Settings.S("normativa"));
        ConcreteSheetPreparation.PrepareMaterials(Input);
        string[] Choices(JsonObject[] catalog, string key) => ConcreteInputPresentation.MaterialChoices(catalog, Input.S(key));
        var form = Form(Input, [
            new("classe_cls", "Calcestruzzo da normativa", Choices: Choices(concreteCatalog, "classe_cls")),
            new("fck_mpa", "fck", "MPa", ReadOnly: true), new("cls_diagramma", "Diagramma CLS", Choices: MaterialInputCatalog.ConcreteDiagrams, Note: FieldNotes.StressBlock),
            new("gettato_sottile", "Piano gettato in opera < 50 mm", Choices: ["No", "Sì"]),
            new("__fcd", "fcd", "MPa", ReadOnly: true), new("__ecm", "Ecm", "MPa", ReadOnly: true),
            new("__ec2", "εc,y (diagramma)", "‰", ReadOnly: true), new("__ecu", "εc,u (diagramma)", "‰", ReadOnly: true),
            new("classe_acciaio", "Acciaio da normativa", Choices: Choices(steelCatalog, "classe_acciaio"), Note: values => MaterialInputCatalog.SteelDifference(values, Settings.S("normativa"))),
            new("fyk_mpa", "fyk", "MPa", ReadOnly: true), new("steel_modulus_mpa", "Es", "MPa", ReadOnly: true),
            new("steel_fu_mpa", "fu", "MPa", ReadOnly: true), new("steel_eps_u", "εu", "‰", ReadOnly: true),
            new("steel_diagramma", "Diagramma acciaio", Choices: ["Elastoplastico", "Incrudente"]),
            new("__fyd", "fyd", "MPa", ReadOnly: true), new("n", "Es / Ecm (riferimento)", ReadOnly: true), new("__nmode", "Viscosità", ReadOnly: true)
        ], key =>
        {
            if (sheet.Initializing) return;
            if (key is "classe_cls" or "classe_acciaio")
            {
                var selected = (key == "classe_cls" ? MaterialInputCatalog.Concrete(Settings.S("normativa")) : MaterialInputCatalog.Steel(false, Settings.S("normativa")))
                    .FirstOrDefault(m => m.S("nome") == Input.S(key));
                if (selected is not null) ApplyStandardMaterial(selected);
            }
            else if (key is "fck_mpa")
            {
                ConcreteInputPresentation.CustomConcrete(Input);
                Materials.Set("classe_cls", "Personalizzato", true);
            }
            else if (ConcreteInputPresentation.SteelProperties.Contains(key))
            {
                ConcreteInputPresentation.CustomSteel(Input);
                Materials.Set("classe_acciaio", "Personalizzato", true);
            }
            sheet.Invalidate();
        }, wideChoices: true);
        form.GroupFields("Calcestruzzo", ["classe_cls", "fck_mpa", "cls_diagramma", "gettato_sottile", "__fcd", "__ecm", "__ec2", "__ecu"], true);
        form.GroupFields("Acciaio per armature", ["classe_acciaio", "fyk_mpa", "steel_modulus_mpa", "steel_fu_mpa", "steel_eps_u", "steel_diagramma", "__fyd", "n", "__nmode"], true);
        return form;
    }

    // After a change of the standard: the class of the catalogue of the new standard (unless «Personalizzato») and its choices.
    private void RefreshStandardMaterialChoices()
    {
        foreach (var (key, catalog) in new[] { ("classe_cls", MaterialInputCatalog.Concrete(Settings.S("normativa"))), ("classe_acciaio", MaterialInputCatalog.Steel(false, Settings.S("normativa"))) })
        {
            string current = Input.S(key);
            if (current != "Personalizzato")
            {
                var standardMaterial = ConcreteInputPresentation.StandardMaterial(catalog, current, key);
                ApplyStandardMaterial(standardMaterial); current = standardMaterial.S("nome");
            }
            bool previous = sheet.Initializing; sheet.Initializing = true;
            Materials[key].ReplaceChoices(ConcreteInputPresentation.StandardChoices(catalog, current));
            Materials.Set(key, current, true); sheet.Initializing = previous;
        }
    }

    // Each value is written and then shown, one at a time, as the sheet always did.
    private void ApplyStandardMaterial(JsonObject material)
    {
        foreach (var (key, value) in ConcreteInputPresentation.StandardMaterialValues(material))
        {
            Input[key] = value?.DeepClone();
            Materials.Set(key, Input.S(key), true);
        }
    }

    // ---------------------------------------------------------------- coefficients (ConcreteParameters.cs)

    private FormViewModel BuildCoefficients() => Form(Settings["coefficienti"]!.AsObject(), ConcreteEditor.Coefficients.Select(c => new FieldSpec(c.Key, c.Label)), key =>
    {
        foreach (var (input, _) in ConcreteEditor.CommonCoefficients.Where(p => p.Standard == key))
        {
            Input[input] = Settings["coefficienti"]![key]?.DeepClone();
            Materials.Set(input, Input.S(input), true);
        }
        sheet.Invalidate();
    });

    // A change of the standard (or «Ripristina coefficienti della normativa»): the coefficients of the standard and the common ones of the input.
    private void ResetCoefficients()
    {
        var values = Settings["coefficienti"]!.AsObject(); var defaults = ConcreteEditor.StandardDefaults(Settings.S("normativa"));
        foreach (var (key, value) in defaults) { values[key] = value?.DeepClone(); Coefficients.Set(key, value?.ToString() ?? "", true); }
        foreach (var (input, standard) in ConcreteEditor.CommonCoefficients) { Input[input] = defaults[standard]?.DeepClone(); Materials.Set(input, defaults.S(standard), true); }
        StandardNote = ConcreteEditor.StandardNote(Settings.S("normativa"));
    }

    // ---------------------------------------------------------------- saved materials (ConcreteMaterials.cs)

    private void BuildCustomMaterials()
    {
        if (Settings["materiali_custom"] is not JsonArray) Settings["materiali_custom"] = new JsonArray();
        ReloadSavedMaterials();
        tendonCatalog = TendonMaterials().ToArray();
        StandardTendon.SetItems(tendonCatalog.Select(m => m.S("nome")));
        StandardTendon.SelectedItem = Settings["materiale_trefolo"].S("nome");
        if (StandardTendon.SelectedIndex < 0) StandardTendon.SelectedIndex = 0;
        TendonProperties = Form(tendonValues, [new("Ep", "Ep", "MPa", ReadOnly: true), new("fpyk", "fpyk", "MPa", ReadOnly: true), new("fpk", "fpk", "MPa", ReadOnly: true),
            new("eps_u", "εpu", "‰", ReadOnly: true), new("diagramma", "Diagramma", Choices: ["Elastoplastico", "Incrudente"])], _ =>
        {
            // The law chosen for the material of the new cables: saved for its identifier; no recalculation, as the historic view.
            if (Settings["materiale_trefolo"] is not JsonObject current) return;
            if (Settings["legami_trefoli"] is not JsonObject) Settings["legami_trefoli"] = new JsonObject();
            Settings["legami_trefoli"]![current.S("id")] = tendonValues.S("diagramma");
            current["diagramma"] = tendonValues.S("diagramma");
            RefreshDefaultTendon(); ReloadTendonMaterials(); sheet.RaiseModified();
        });
        StandardTendon.SelectionChanged += (_, _) =>
        {
            if (refreshingTendon) return;
            DescribeTendon();
            if (StandardTendon.SelectedIndex >= 0) Apply(tendonCatalog[StandardTendon.SelectedIndex]);
        };
        RefreshDefaultTendon();
    }

    private void ReloadSavedMaterials()
    {
        SavedMaterials.SetItems(Settings.Array("materiali_custom").Select(ConcreteInputPresentation.SavedMaterialLabel));
        if (SavedMaterials.Items.Count > 0) SavedMaterials.SelectedIndex = SavedMaterials.Items.Count - 1;
    }

    private void DescribeTendon()
    {
        if (StandardTendon.SelectedIndex < 0) return;
        var material = tendonCatalog[StandardTendon.SelectedIndex];
        foreach (var field in TendonProperties.Fields) TendonProperties.Set(field.Key, material.S(field.Key), true);
    }

    private void RefreshDefaultTendon()
    {
        refreshingTendon = true;
        tendonCatalog = TendonMaterials().ToArray();
        if (Settings["materiale_trefolo"] is JsonObject current && !tendonCatalog.Any(m => m.S("id") == current.S("id"))) tendonCatalog = [.. tendonCatalog, current];
        StandardTendon.SetItems(tendonCatalog.Select(m => m.S("nome")));
        StandardTendon.SelectedItem = Settings["materiale_trefolo"].S("nome");
        DescribeTendon(); refreshingTendon = false;
    }

    // A saved material, or the tendon of the standard chosen: a concrete or a steel to the input, a tendon as the default of the new cables.
    private void Apply(JsonObject material)
    {
        string type = ConcreteInputPresentation.ApplySavedMaterial(Input, Settings, material);
        if (type == "Calcestruzzo")
        {
            Materials.Set("classe_cls", "Personalizzato", true); Materials.Set("fck_mpa", Input.S("fck_mpa"), true); Materials.Set("cls_diagramma", Input.S("cls_diagramma"), true);
        }
        else if (type == "Acciaio")
        {
            foreach (var key in ConcreteInputPresentation.SavedSteelKeys) Materials.Set(key, Input.S(key), true);
            Materials.Set("classe_acciaio", "Personalizzato", true);
        }
        else
        {
            RefreshDefaultTendon();
            // New material becomes the default for new cables; existing cables are unchanged.
            ReloadTendonMaterials();
            TendonsChanged();
        }
        sheet.Invalidate();
    }

    private void ApplySavedMaterial()
    {
        if (!sheet.CanEdit || SavedMaterials.SelectedIndex < 0) return;
        Apply((JsonObject)Settings.Array("materiali_custom")[SavedMaterials.SelectedIndex]!);
    }

    // «Nuovo materiale …»: the window on the dialog service; a saved material is added to the sheet, applied, and the list selects it.
    private async Task EditMaterialAsync(string type)
    {
        if (!sheet.CanEdit) return;
        var dialog = new ConcreteMaterialDialogViewModel(type, Input, Settings, sheet.Wiki);
        var value = await sheet.Dialogs.ShowDialogAsync(dialog);
        if (value is null || sheet.IsDisposed) return;
        Settings["materiali_custom"]!.AsArray().Add(value.DeepClone());
        Apply(value);
        ReloadSavedMaterials();
    }

    /// <summary>The dialog «Nuovo materiale» of <paramref name="type"/> as the commands open it (the checks drive it without the service).</summary>
    internal ConcreteMaterialDialogViewModel CreateMaterialDialog(string type) => new(type, Input, Settings, sheet.Wiki);
}
