using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private void RefreshStandardMaterialChoices()
    {
        foreach (var (key, catalog) in new[] { ("classe_cls", ConcreteMaterialCatalog.Concrete(settings.S("normativa"))), ("classe_acciaio", ConcreteMaterialCatalog.Steel(false, settings.S("normativa"))) })
        {
            var combo = (ComboBox)materials.Editors[key]; string current = Input.S(key);
            if (current != "Personalizzato")
            {
                var standardMaterial = catalog.FirstOrDefault(m => m.S("nome") == current) ?? catalog.First(m => m.S("nome") == (key == "classe_cls" ? "C35/45" : "B450C"));
                ApplyStandardMaterial(standardMaterial); current = standardMaterial.S("nome");
            }
            bool previous = initializing; initializing = true;
            combo.ItemsSource = catalog.Select(m => m.S("nome")).Append(current).Append("Personalizzato").Distinct().ToArray();
            materials.Set(key, current, true); initializing = previous;
        }
    }
    private void ApplyStandardMaterial(JsonObject material)
    {
        foreach (var (key, value) in material.Where(p => p.Key != "nome" && p.Key is not ("cls_diagramma" or "steel_diagramma")))
        {
            Input[key] = value?.DeepClone();
            materials.Set(key, Input.S(key), true);
        }
    }

    private void BuildMaterialFields()
    {
        var concreteCatalog = ConcreteMaterialCatalog.Concrete(settings.S("normativa"));
        var steelCatalog = ConcreteMaterialCatalog.Steel(false, settings.S("normativa"));
        // Never replace saved numerical properties merely by opening an existing sheet.
        if (!Input.ContainsKey("classe_acciaio")) Input["classe_acciaio"] = "Personalizzato";
        if (!Input.ContainsKey("steel_fu_mpa")) Input["steel_fu_mpa"] = Input["fyk_mpa"]?.DeepClone();
        if (!Input.ContainsKey("steel_eps_u")) Input["steel_eps_u"] = "100";
        if (!Input.ContainsKey("steel_diagramma")) Input["steel_diagramma"] = "Elastoplastico";
        if (!Input.ContainsKey("cls_diagramma")) Input["cls_diagramma"] = ConcreteMaterials.ConcreteDiagrams[0];
        string[] Choices(JsonObject[] catalog, string key) => catalog.Select(m => m.S("nome"))
            .Append(Input.S(key)).Append("Personalizzato").Where(s => s.Length > 0).Distinct().ToArray();
        materials = new InputForm(Input, [
            new("classe_cls", "Calcestruzzo da normativa", Choices: Choices(concreteCatalog, "classe_cls")),
            new("fck_mpa", "fck", "MPa", ReadOnly: true), new("cls_diagramma", "Diagramma CLS", Choices: ConcreteMaterials.ConcreteDiagrams),
            new("gettato_sottile", "Piano gettato in opera < 50 mm", Choices: ["No", "Sì"]),
            new("__fcd", "fcd", "MPa", ReadOnly: true), new("__ecm", "Ecm", "MPa", ReadOnly: true),
            new("__ec2", "εc,y (diagramma)", "‰", ReadOnly: true), new("__ecu", "εc,u (diagramma)", "‰", ReadOnly: true),
            new("classe_acciaio", "Acciaio da normativa", Choices: Choices(steelCatalog, "classe_acciaio")),
            new("fyk_mpa", "fyk", "MPa", ReadOnly: true), new("steel_modulus_mpa", "Es", "MPa", ReadOnly: true),
            new("steel_fu_mpa", "fu", "MPa", ReadOnly: true), new("steel_eps_u", "εu", "‰", ReadOnly: true),
            new("steel_diagramma", "Diagramma acciaio", Choices: ["Elastoplastico", "Incrudente"]),
            new("__fyd", "fyd", "MPa", ReadOnly: true), new("n", "Es / Ecm (riferimento)", ReadOnly: true), new("__nmode", "Viscosità", ReadOnly: true)
        ], key =>
        {
            if (initializing) return;
            if (key is "classe_cls" or "classe_acciaio")
            {
                var selected = (key == "classe_cls" ? ConcreteMaterialCatalog.Concrete(settings.S("normativa")) : ConcreteMaterialCatalog.Steel(false, settings.S("normativa"))).FirstOrDefault(m => m.S("nome") == Input.S(key));
                if (selected is not null) ApplyStandardMaterial(selected);
            }
            else if (key is "fck_mpa")
            {
                Input["classe_cls"] = "Personalizzato"; Input["materiale_cls_nome"] = "CLS personalizzato";
                materials.Set("classe_cls", "Personalizzato", true);
            }
            else if (key is "fyk_mpa" or "steel_modulus_mpa" or "steel_fu_mpa" or "steel_eps_u" )
            {
                Input["classe_acciaio"] = "Personalizzato"; Input["materiale_acciaio_nome"] = "Acciaio personalizzato";
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
        void Reload() { list.ItemsSource = settings.Array("materiali_custom").Select(m => m.S("tipo") + " · " + m.S("nome")).ToArray(); if (list.Items.Count > 0) list.SelectedIndex = list.Items.Count - 1; }
        void Apply(JsonObject material)
        {
            string type = material.S("tipo");
            if (type == "Calcestruzzo")
            {
                Input["classe_cls"] = "Personalizzato"; Input["fck_mpa"] = material["fck_mpa"]?.DeepClone(); Input["cls_diagramma"] = material["cls_diagramma"]?.DeepClone(); Input["materiale_cls_nome"] = material.S("nome");
                materials.Set("classe_cls", "Personalizzato", true); materials.Set("fck_mpa", Input.S("fck_mpa"), true); materials.Set("cls_diagramma", Input.S("cls_diagramma"), true);
            }
            else if (type == "Acciaio")
            {
                foreach (var key in new[] { "steel_modulus_mpa", "fyk_mpa", "steel_fu_mpa", "steel_eps_u", "steel_diagramma" }) { Input[key] = material[key]?.DeepClone(); materials.Set(key, Input.S(key), true); }
                Input["materiale_acciaio_nome"] = material.S("nome");
                Input["classe_acciaio"] = "Personalizzato"; materials.Set("classe_acciaio", "Personalizzato", true);
            }
            else
            {
                settings["materiale_trefolo"] = material.DeepClone();
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
