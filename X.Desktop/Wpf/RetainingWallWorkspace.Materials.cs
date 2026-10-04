using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    private readonly TextBlock wallCoverStatus = Ui.Text("", 11, color: Ui.Muted);
    private UIElement BuildWallMaterials()
    {
        if (wallCoverStatus.Parent is Panel parent) parent.Children.Remove(wallCoverStatus);
        var m = Data["materials"]!.AsObject();
        string[] Choices(JsonObject[] catalog, string key) => catalog.Select(x => x.S("nome")).Append(m.S(key)).Append("Personalizzato").Distinct().ToArray();
        var form = Form("materials", new Field[] {
            new("gamma", "Peso specifico muro", "kN/m³"),
            new("classe_cls", "Calcestruzzo da normativa", Choices: Choices(ConcreteMaterialCatalog.Concrete("NTC 2018"), "classe_cls")),
            new("materiale_cls_nome", "Nome CLS personalizzato"), new("fck", "fck", "MPa"),
            new("cls_diagramma", "Diagramma CLS", Choices: ConcreteMaterials.ConcreteDiagrams),
            new("__fcd", "fcd", "MPa", ReadOnly: true), new("__ecm", "Ecm", "MPa", ReadOnly: true),
            new("classe_acciaio", "Acciaio da normativa", Choices: Choices(ConcreteMaterialCatalog.Steel(false, "NTC 2018"), "classe_acciaio")),
            new("materiale_acciaio_nome", "Nome acciaio personalizzato"), new("fyk", "fyk", "MPa"), new("steel_modulus_mpa", "Es", "MPa"),
            new("steel_fu_mpa", "fu", "MPa"), new("steel_eps_u", "εu", "‰"), new("steel_diagramma", "Diagramma acciaio", Choices: ["Elastoplastico", "Incrudente"]),
            new("__fyd", "fyd", "MPa", ReadOnly: true), new("alpha_cc", "αcc"), new("gamma_c", "γc"), new("gamma_s", "γs"),
            new("cover", "Copriferro netto adottato", "mm"), new("exposure", "Esposizione", Choices: Ntc2018Checks.Exposures.Skip(1).ToArray()),
            new("creep", "Viscosità SLE", "−")
        }.Concat(Fields(RetainingWall.MaterialFields.Where(f => f.Key.EndsWith("_rd")))), handler: key =>
        {
            if (key is "classe_cls" or "classe_acciaio")
            {
                var catalog = key == "classe_cls" ? ConcreteMaterialCatalog.Concrete("NTC 2018") : ConcreteMaterialCatalog.Steel(false, "NTC 2018");
                var preset = catalog.FirstOrDefault(x => x.S("nome") == m.S(key));
                if (preset is not null) RetainingWall.ApplyMaterialPreset(Data, preset);
                else m[key == "classe_cls" ? "materiale_cls_nome" : "materiale_acciaio_nome"] = key == "classe_cls" ? "CLS personalizzato" : "Acciaio personalizzato";
                foreach (string field in Forms["materials"].Editors.Keys.Where(k => !k.StartsWith("__"))) Forms["materials"].Set(field, m.S(field), true);
            }
        });
        form.GroupFields("Calcestruzzo · verificatore sezioni", ["classe_cls", "materiale_cls_nome", "fck", "cls_diagramma", "__fcd", "__ecm"], true);
        form.GroupFields("Acciaio per armature · verificatore sezioni", ["classe_acciaio", "materiale_acciaio_nome", "fyk", "steel_modulus_mpa", "steel_fu_mpa", "steel_eps_u", "steel_diagramma", "__fyd"], true);
        form.GroupFields("Coefficienti materiali NTC 2018", ["alpha_cc", "gamma_c", "gamma_s"], false);
        var cover = Form("material_cover", [new("aggregate", "Diametro massimo aggregato", "mm"), new("life", "Vita utile", Choices: ["50", "100"]),
            new("cover_deviation", "Tolleranza copriferro", "mm"), new("cover_method", "Metodo copriferro", Choices: ["NTC + Circ. 2019", "EC2 2004"]),
            new("cover_element", "Elemento NTC", Choices: ["Trave / pilastro", "Piastra / soletta / parete"]), new("cover_quality", "Controllo qualità NTC", Bool: true),
            new("cover_ground", "Superficie di getto", Choices: ["Casseratura", "Terreno preparato", "Direttamente su terra"]),
            new("cover_abrasion", "Abrasione", Choices: ["Nessuno", "XM1", "XM2", "XM3"]),
            new("cover_high_strength", "Riduzione EC2 per resistenza", Bool: true), new("cover_slab", "Riduzione EC2 per piastra", Bool: true),
            new("cover_ec_quality", "Controllo qualità EC2", Bool: true), new("cover_rough", "Superficie irregolare", Bool: true)], Data["detailing"]!.AsObject());
        return Ui.Stack(Ui.Text("Stessi cataloghi GPC, legami e proprietà del verificatore delle sezioni in calcestruzzo. Nei progetti i materiali e la durabilità seguono i dati comuni del ramo; le differenze sono segnalate nel confronto.", 11, color: Ui.Muted),
            form, Group("Durabilità e copriferro · scheda Materiali", Ui.Stack(cover, wallCoverStatus,
                Ui.Button("Adotta copriferro minimo", () => { try { Commit(); m["cover"] = RetainingWall.RequiredCover(Data); form.Set("cover", m.S("cover"), true); UpdateFields(); Changed(); } catch (ArgumentException ex) { wallCoverStatus.Text = ex.Message; } })), false),
            BuildGravityMaterial());
    }
    private void UpdateWallMaterials()
    {
        var form = Forms["materials"]; var m = Data["materials"]!;
        bool concrete = RetainingWall.UsesConcrete(Data), rc = Data.S("family") == "cantilever";
        foreach (string key in new[] { "classe_cls", "fck", "cls_diagramma", "alpha_cc", "gamma_c", "__fcd", "__ecm" }) form.ShowField(key, concrete);
        foreach (string key in new[] { "classe_acciaio", "fyk", "steel_modulus_mpa", "steel_fu_mpa", "steel_eps_u", "steel_diagramma", "gamma_s", "__fyd", "cover", "exposure" }) form.ShowField(key, rc);
        form.ShowField("materiale_cls_nome", concrete && m.S("classe_cls") == "Personalizzato");
        form.ShowField("materiale_acciaio_nome", rc && m.S("classe_acciaio") == "Personalizzato");
        form.Enable("fck", m.S("classe_cls") == "Personalizzato", true);
        foreach (string key in new[] { "fyk", "steel_modulus_mpa", "steel_fu_mpa", "steel_eps_u" }) form.Enable(key, m.S("classe_acciaio") == "Personalizzato", true);
        if (Forms.TryGetValue("material_cover", out var cover))
        {
            cover.Visibility = rc ? Visibility.Visible : Visibility.Collapsed;
            bool ec = Data["detailing"].S("cover_method") == "EC2 2004";
            foreach (string key in new[] { "cover_high_strength", "cover_slab", "cover_ec_quality" }) cover.ShowField(key, ec);
            cover.ShowField("cover_element", !ec); cover.ShowField("cover_quality", !ec);
        }
        try
        {
            var input = RetainingWall.MaterialSectionInput(Data); var values = ConcreteMaterials.DesignValues(input, J.Obj(("normativa", "NTC 2018")));
            form.Set("__fcd", F(values.Fcd), true); form.Set("__fyd", F(values.Fyd), true); form.Set("__ecm", F(ConcreteMaterials.Concrete(input).Ecm), true);
            double required = RetainingWall.RequiredCover(Data);
            wallCoverStatus.Text = $"Minimo nominale {required:0.##} mm · adottato {m.D("cover"):0.##} mm · Ø massimo {RetainingWall.MaximumBarDiameter(Data):0.##} mm. " + (m.D("cover") < required ? "Copriferro insufficiente." : "Minimo rispettato.");
        }
        catch (ArgumentException ex) { foreach (string key in new[] { "__fcd", "__fyd", "__ecm" }) form.Set(key, "—", true); wallCoverStatus.Text = ex.Message; }
    }
}
