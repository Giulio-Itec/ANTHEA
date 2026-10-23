using Anthea.Application.Concrete;
using Anthea.Application.Materials;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private void RefreshStandardMaterialChoices()
    {
        foreach (var (key, catalog) in new[] { ("classe_cls", MaterialInputCatalog.Concrete(settings.S("normativa"))), ("classe_acciaio", MaterialInputCatalog.Steel(false, settings.S("normativa"))) })
        {
            var combo = (ComboBox)materials.Editors[key]; string current = Input.S(key);
            if (current != "Personalizzato")
            {
                var standardMaterial = ConcreteInputPresentation.StandardMaterial(catalog, current, key);
                ApplyStandardMaterial(standardMaterial); current = standardMaterial.S("nome");
            }
            bool previous = initializing; initializing = true;
            combo.ItemsSource = ConcreteInputPresentation.StandardChoices(catalog, current);
            materials.Set(key, current, true); initializing = previous;
        }
    }
    private void ApplyStandardMaterial(JsonObject material)
    {
        // Each value is written and then shown, one at a time, as the sheet always did.
        foreach (var (key, value) in ConcreteInputPresentation.StandardMaterialValues(material))
        {
            Input[key] = value?.DeepClone();
            materials.Set(key, Input.S(key), true);
        }
    }

    private void BuildMaterialFields()
    {
        var concreteCatalog = MaterialInputCatalog.Concrete(settings.S("normativa"));
        var steelCatalog = MaterialInputCatalog.Steel(false, settings.S("normativa"));
        Anthea.Application.Concrete.ConcreteSheetPreparation.PrepareMaterials(Input);
        string[] Choices(JsonObject[] catalog, string key) => ConcreteInputPresentation.MaterialChoices(catalog, Input.S(key));
        materials = new InputForm(Input, [
            new("classe_cls", "Calcestruzzo da normativa", Choices: Choices(concreteCatalog, "classe_cls")),
            new("fck_mpa", "fck", "MPa", ReadOnly: true), new("cls_diagramma", "Diagramma CLS", Choices: MaterialInputCatalog.ConcreteDiagrams, Note: FieldNotes.StressBlock),
            new("gettato_sottile", "Piano gettato in opera < 50 mm", Choices: ["No", "Sì"]),
            new("__fcd", "fcd", "MPa", ReadOnly: true), new("__ecm", "Ecm", "MPa", ReadOnly: true),
            new("__ec2", "εc,y (diagramma)", "‰", ReadOnly: true), new("__ecu", "εc,u (diagramma)", "‰", ReadOnly: true),
            new("classe_acciaio", "Acciaio da normativa", Choices: Choices(steelCatalog, "classe_acciaio"), Note: i => MaterialInputCatalog.SteelDifference(i, settings.S("normativa"))),
            new("fyk_mpa", "fyk", "MPa", ReadOnly: true), new("steel_modulus_mpa", "Es", "MPa", ReadOnly: true),
            new("steel_fu_mpa", "fu", "MPa", ReadOnly: true), new("steel_eps_u", "εu", "‰", ReadOnly: true),
            new("steel_diagramma", "Diagramma acciaio", Choices: ["Elastoplastico", "Incrudente"]),
            new("__fyd", "fyd", "MPa", ReadOnly: true), new("n", "Es / Ecm (riferimento)", ReadOnly: true), new("__nmode", "Viscosità", ReadOnly: true)
        ], key =>
        {
            if (initializing) return;
            if (key is "classe_cls" or "classe_acciaio")
            {
                var selected = (key == "classe_cls" ? MaterialInputCatalog.Concrete(settings.S("normativa")) : MaterialInputCatalog.Steel(false, settings.S("normativa"))).FirstOrDefault(m => m.S("nome") == Input.S(key));
                if (selected is not null) ApplyStandardMaterial(selected);
            }
            else if (key is "fck_mpa")
            {
                ConcreteInputPresentation.CustomConcrete(Input);
                materials.Set("classe_cls", "Personalizzato", true);
            }
            else if (ConcreteInputPresentation.SteelProperties.Contains(key))
            {
                ConcreteInputPresentation.CustomSteel(Input);
                materials.Set("classe_acciaio", "Personalizzato", true);
            }
            Invalidate();
        }, true, wideChoices: true);

        materials.GroupFields("Calcestruzzo", ["classe_cls", "fck_mpa", "cls_diagramma", "gettato_sottile", "__fcd", "__ecm", "__ec2", "__ecu"], true);
        materials.GroupFields("Acciaio per armature", ["classe_acciaio", "fyk_mpa", "steel_modulus_mpa", "steel_fu_mpa", "steel_eps_u", "steel_diagramma", "__fyd", "n", "__nmode"], true);
    }

    private UIElement BuildCustomMaterials()
    {
        if (settings["materiali_custom"] is not JsonArray) settings["materiali_custom"] = new JsonArray();
        var list = new ComboBox { MinWidth = 175, DisplayMemberPath = "" };
        Action? refreshDefaultTendon = null;
        void Reload() { list.ItemsSource = settings.Array("materiali_custom").Select(ConcreteInputPresentation.SavedMaterialLabel).ToArray(); if (list.Items.Count > 0) list.SelectedIndex = list.Items.Count - 1; }
        void Apply(JsonObject material)
        {
            string type = ConcreteInputPresentation.ApplySavedMaterial(Input, settings, material);
            if (type == "Calcestruzzo")
            {
                materials.Set("classe_cls", "Personalizzato", true); materials.Set("fck_mpa", Input.S("fck_mpa"), true); materials.Set("cls_diagramma", Input.S("cls_diagramma"), true);
            }
            else if (type == "Acciaio")
            {
                foreach (var key in ConcreteInputPresentation.SavedSteelKeys) materials.Set(key, Input.S(key), true);
                materials.Set("classe_acciaio", "Personalizzato", true);
            }
            else
            {
                refreshDefaultTendon?.Invoke();
                // New material becomes the default for new cables; existing cables are unchanged.
                reloadTendonMaterials?.Invoke();
                TendonsChanged();
            }
            Invalidate();
        }
        void EditMaterial(string type) => CreateMaterialDialog(type, value => { settings["materiali_custom"]!.AsArray().Add(value.DeepClone()); Apply(value); Reload(); }).ShowDialog();
        Reload();
        var tendonMaterial = Ui.Button("Nuovo materiale trefoli", () => EditMaterial("Trefoli"));
        var tendonCatalog = AvailableTendonMaterials().ToArray();
        var standardTendon = new ComboBox { ItemsSource = tendonCatalog.Select(m => m.S("nome")).ToArray(), MinWidth = 175 };
        standardTendon.SelectedItem = settings["materiale_trefolo"].S("nome");
        if (standardTendon.SelectedIndex < 0) standardTendon.SelectedIndex = 0;
        var tendonValues = new JsonObject();
        var tendonProperties = new InputForm(tendonValues, [new("Ep", "Ep", "MPa", ReadOnly: true), new("fpyk", "fpyk", "MPa", ReadOnly: true), new("fpk", "fpk", "MPa", ReadOnly: true), new("eps_u", "εpu", "‰", ReadOnly: true), new("diagramma", "Diagramma", Choices: ["Elastoplastico","Incrudente"])], _ =>
        {
            if(settings["materiale_trefolo"] is not JsonObject current)return;
            if(settings["legami_trefoli"] is not JsonObject)settings["legami_trefoli"]=new JsonObject();
            settings["legami_trefoli"]![current.S("id")]=tendonValues.S("diagramma");
            current["diagramma"]=tendonValues.S("diagramma");
            refreshDefaultTendon?.Invoke();reloadTendonMaterials?.Invoke();Modified?.Invoke();
        }, true);
        void DescribeTendon()
        {
            if (standardTendon.SelectedIndex < 0) return;
            var m = tendonCatalog[standardTendon.SelectedIndex];
            foreach (string key in tendonProperties.Editors.Keys) tendonProperties.Set(key, m.S(key), true);
        }
        bool refreshingTendon = false;
        refreshDefaultTendon = () =>
        {
            refreshingTendon = true;
            tendonCatalog = AvailableTendonMaterials().ToArray();
            if (settings["materiale_trefolo"] is JsonObject current && !tendonCatalog.Any(m => m.S("id") == current.S("id"))) tendonCatalog = [.. tendonCatalog, current];
            standardTendon.ItemsSource = tendonCatalog.Select(m => m.S("nome")).ToArray();
            standardTendon.SelectedItem = settings["materiale_trefolo"].S("nome");
            DescribeTendon(); refreshingTendon = false;
        };
        standardTendon.SelectionChanged += (_, _) => { if (refreshingTendon) return; DescribeTendon(); if (standardTendon.SelectedIndex >= 0) Apply(tendonCatalog[standardTendon.SelectedIndex]); };
        refreshDefaultTendon();
        return Ui.Stack(Group("Trefoli", Ui.Stack(Ui.Text("Trefoli da standard EN 1992", 12), standardTendon, tendonProperties,
            Ui.Text("Materiale predefinito dei nuovi cavi. Per un cavo esistente scegli il materiale nella sua riga.", 11, color: Ui.Muted)), true),
            Ui.Bar(Ui.Button("Nuovo materiale CLS", () => EditMaterial("Calcestruzzo")), Ui.Button("Nuovo materiale acciaio", () => EditMaterial("Acciaio")), tendonMaterial), list,
            Ui.Button("Applica materiale salvato", () => { if (list.SelectedIndex >= 0) Apply((JsonObject)settings.Array("materiali_custom")[list.SelectedIndex]!); }));
    }
}
