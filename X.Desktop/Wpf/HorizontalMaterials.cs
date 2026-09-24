using X.Core;

namespace X.Desktop;

internal sealed partial class HorizontalWorkspace
{
    private static readonly Dictionary<string, double> PileConcreteClasses = new()
    {
        ["C12/15"] = 12, ["C16/20"] = 16, ["C20/25"] = 20, ["C25/30"] = 25,
        ["C28/35"] = 28, ["C30/37"] = 30, ["C32/40"] = 32, ["C35/45"] = 35,
        ["C40/50"] = 40, ["C45/55"] = 45, ["C50/60"] = 50,
        ["C55/67"] = 55, ["C60/75"] = 60, ["C70/85"] = 70, ["C80/95"] = 80, ["C90/105"] = 90
    };

    private static string ConcreteClass(double strength) => PileConcreteClasses.FirstOrDefault(p => p.Value == strength).Key ?? "Personalizzato";

    private void SectionMaterialChanged(string key)
    {
        var input = Data["sezione"]!;
        if (key == "classe_cls" && PileConcreteClasses.TryGetValue(input.S(key), out double strength))
        {
            input["fck_mpa"] = strength.ToString(System.Globalization.CultureInfo.InvariantCulture);
            sectionFields.Set("fck_mpa", input.S("fck_mpa"), true);
        }
        else if (key == "fck_mpa")
        {
            string name = ConcreteClass(input.D("fck_mpa"));
            input["classe_cls"] = name; sectionFields.Set("classe_cls", name, true);
        }
        else if (key is "fyk_mpa" or "steel_modulus_mpa")
        {
            input["classe_acciaio"] = "Personalizzato";
            input["materiale_acciaio_nome"] = "Acciaio personalizzato";
        }
        Changed();
    }

    private void UpdateSectionMaterialValues()
    {
        var input = Data["sezione"]!;
        try
        {
            double fcd = input.Required("alpha_cc", strict: true) * input.Required("fck_mpa", strict: true) / input.Required("gamma_c", strict: true);
            sectionFields.Set("__fcd", fcd.ToString("F1"), true);
        }
        catch (ArgumentException) { sectionFields.Set("__fcd", "—", true); }
        try
        {
            double fyd = input.Required("fyk_mpa", strict: true) / input.Required("gamma_s", strict: true);
            sectionFields.Set("__fyd", fyd.ToString("F1"), true);
        }
        catch (ArgumentException) { sectionFields.Set("__fyd", "—", true); }
    }
}
