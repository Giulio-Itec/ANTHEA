using X.Core;

namespace X.Desktop;

internal sealed partial class HorizontalWorkspace
{
    private static readonly Dictionary<string, double> PileConcreteClasses = ConcreteMaterialCatalog.MaterialSheetClasses().ToDictionary(m => m.Name, m => m.Fck);

    private static string ConcreteClass(double strength) => PileConcreteClasses.FirstOrDefault(p => p.Value == strength).Key ?? "Personalizzato";

    private void SectionMaterialChanged(string key)
    {
        var input = Data["sezione"]!;
        if(key is "classe_cls" or "classe_acciaio")
        {
            var catalog=key=="classe_cls"?ConcreteMaterialCatalog.Concrete():ConcreteMaterialCatalog.Steel(false,"NTC 2018");
            var material=catalog.FirstOrDefault(m=>m.S("nome")==input.S(key));
            if(material!=null)foreach(var (field,value) in material.Where(v=>v.Key!="nome")){input[field]=value?.DeepClone();sectionFields.Set(field,input.S(field),true);}
        }
        else if (key == "fck_mpa")
        {
            string name = ConcreteClass(input.D("fck_mpa"));
            input["classe_cls"] = name; sectionFields.Set("classe_cls", name, true);
        }
        else if (key is "fyk_mpa" or "steel_modulus_mpa" or "steel_fu_mpa" or "steel_eps_u" or "steel_diagramma")
        {
            input["classe_acciaio"] = "Personalizzato";
            input["materiale_acciaio_nome"] = "Acciaio personalizzato";
        }
        else if (key == "second_inner_enabled") ShowInnerRing(true);
        Changed();
    }

    /// <summary>Second ring of bars, as «Secondo anello interno» of the c.a. section: its fields appear only when it is active and
    /// start, like there, from the bars of the first ring and a clear distance of 30 mm.</summary>
    private void ShowInnerRing(bool fill)
    {
        var input = Data["sezione"]!.AsObject(); bool enabled = input.B("second_inner_enabled");
        if (fill && enabled)
            foreach (var (key, value) in new[] { ("second_inner_count", input.S("longitudinal_bar_count")), ("second_inner_diameter", input.S("longitudinal_bar_diameter_mm")), ("second_inner_gap", "30") })
                if (string.IsNullOrWhiteSpace(input.S(key))) { input[key] = value; sectionFields.Set(key, value, true); }
        foreach (string key in new[] { "second_inner_count", "second_inner_diameter", "second_inner_gap" }) sectionFields.ShowField(key, enabled);
    }

    private void UpdateSectionMaterialValues()
    {
        var input = Data["sezione"]!.AsObject();
        try
        {
            var strengths = ConcreteMaterials.DesignValues(input, ConcreteStandards.PileWorkspace(input));
            sectionFields.Set("__fcd", strengths.Fcd.ToString("F1"), true);
            sectionFields.Set("__fyd", strengths.Fyd.ToString("F1"), true);
        }
        catch (ArgumentException) { sectionFields.Set("__fcd", "—", true); sectionFields.Set("__fyd", "—", true); }
    }
}
