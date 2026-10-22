using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;

/// <summary>
/// Derivations of the detailing tab of the RC section moved from the historic view in sub-step S6 of F5.10 (ConcreteDetailingPresentation: the
/// fields of the lap, the fields that follow the element, the text of cmin,dur), with values written by hand from RefreshDetailing of the
/// historic view. The identity of the historic view with these functions is proved by ui/check-sezione-historic.
/// </summary>
internal static class ConcreteDetailingPresentationChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            static JsonObject Workspace(string element, string anchorage) =>
                J.Obj(("dettagli_costruttivi", J.Obj(("elemento", element))), ("ancoraggi", J.Obj(("tipo", anchorage))));
            Check(string.Join("|", ConcreteDetailingPresentation.LapFields) == "percentuale|interferro|posizione", "Campi della sovrapposizione");
            Check(ConcreteDetailingPresentation.Lap(Workspace("Trave", "Sovrapposizione rettilinea")) && !ConcreteDetailingPresentation.Lap(Workspace("Trave", "Ancoraggio rettilineo"))
                && !ConcreteDetailingPresentation.Lap(J.Obj(("ancoraggi", J.Obj()))), "Sovrapposizione solo con il tipo «Sovrapposizione…»");
            string Shown(string element) => string.Join("|", new[] { "as_secondaria", "passo_secondaria", "zona_critica", "ancoraggio_appoggi", "aggregato" }
                .Select(k => ConcreteDetailingPresentation.ElementFieldShown(Workspace(element, ""), k) switch { true => "1", false => "0", null => "-" }));
            Check(Shown("Da scegliere") == "0|0|0|0|-" && Shown("Trave") == "0|0|0|1|-" && Shown("Pilastro") == "0|0|0|0|-" && Shown("Soletta piena") == "1|1|1|0|-"
                && Shown("Parete") == "1|1|0|0|-", "Campi mostrati per elemento: barre ortogonali per solette e pareti, zona critica per le solette, appoggi per le travi");
            Check(ConcreteDetailingPresentation.DurabilityCoverText(JsonValue.Create(27.5)) == "27,5" && ConcreteDetailingPresentation.DurabilityCoverText(JsonValue.Create(35.12345)) == "35,123"
                && ConcreteDetailingPresentation.DurabilityCoverText(JsonValue.Create("")) == "" && ConcreteDetailingPresentation.DurabilityCoverText(null) == "",
                "cmin,dur: al massimo tre decimali nella cultura corrente, vuoto senza numero");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        return checks;
    }
}
