using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
using Anthea.Calculations;

/// <summary>
/// Presentation of the pile group sheet moved from the historic view in F5.4 (PileGroupPresentation): readings of the rows of the
/// comparison, rules of the texts and arithmetic of the cap and of the reductions, with values written by hand. The identity of
/// the historic view with these functions is proved by ui/check-pile-group-historic (traces frozen before the move).
/// </summary>
internal static class PileGroupPresentationChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            Check(PileGroupPresentation.Format(null) == "—" && PileGroupPresentation.Format(2) == "2" && PileGroupPresentation.Format(0.123456) == "0,123"
                && PileGroupPresentation.Format(-1.5) == "-1,5", "Formato dei numeri della scheda");
            var row = J.Obj(("Method", 4), ("DirectionId", "Y-"), ("Angle", 270), ("Factor", 0.5));
            Check(PileGroupPresentation.Method(row) == 4 && PileGroupPresentation.Angle(row) == 270 && PileGroupPresentation.Available(row), "Lettura di una riga del confronto");
            Check(!PileGroupPresentation.Available(new JsonObject { ["Factor"] = null }) && !PileGroupPresentation.Available(new JsonObject()), "Metodo non applicabile senza fattore medio");
            Check(PileGroupPresentation.IsSelected(row, J.Obj(("metodo", 4), ("direzione_selezionata", "Y-"))), "Riga selezionata non riconosciuta");
            Check(!PileGroupPresentation.IsSelected(row, J.Obj(("metodo", 4), ("direzione_selezionata", "Y")))
                && !PileGroupPresentation.IsSelected(row, J.Obj(("metodo", 4.5), ("direzione_selezionata", "Y-"))), "Riga selezionata con metodo o direzione diversi");
            JsonObject Warnings(string error, params string[] warnings) => J.Obj(("Error", error), ("Warnings", new JsonArray(warnings.Select(w => (JsonNode)JsonValue.Create(w)!).ToArray())));
            Check(PileGroupPresentation.Applicability(Warnings("Interasse fuori dal campo del metodo.", "Davisson: estrapolazione esplicita sotto 3D.")) == "Interasse fuori dal campo del metodo.",
                "L'errore del metodo prevale sugli avvisi");
            Check(PileGroupPresentation.Applicability(Warnings("", "Davisson: estrapolazione esplicita sotto 3D; risultato limitato a [0,1].")) == "Approssimazione esplicita"
                && PileGroupPresentation.Applicability(Warnings("", "Estrapolazione esplicita oltre il campo tabellato.")) == "Approssimazione esplicita"
                && PileGroupPresentation.Applicability(Warnings("", "File proiettate: estensione geometrica del metodo originale.")) == "Approssimazione esplicita",
                "Avvisi di estrapolazione o di estensione geometrica");
            Check(PileGroupPresentation.Applicability(Warnings("", "Metodo per file: verificare l'applicabilità.")) == "Disponibile"
                && PileGroupPresentation.Applicability(new JsonObject()) == "Disponibile", "Metodo disponibile senza approssimazioni");
            Check(PileGroupPresentation.FactorDescription(0) == "coefficiente Rg di kh/nh" && PileGroupPresentation.FactorDescription(3) == "coefficiente Pm di reazione p",
                "Grandezza ridotta dal metodo");
            Check(!PileGroupPresentation.ShowsBeta(3) && PileGroupPresentation.ShowsBeta(4) && PileGroupPresentation.ShowsBeta(5)
                && !PileGroupPresentation.ShowsAlpha(4) && PileGroupPresentation.ShowsAlpha(5), "β e α mostrati per i metodi giusti");
            Check(PileGroupPresentation.CapDistances(J.Obj(("diametro", 2), ("basamento", J.Obj(("bordo_d", 1.5))))) == (3, 2), "Asse–bordo e margine del basamento");
            Check(PileGroupPresentation.CapDistances(J.Obj(("diametro", 1), ("basamento", J.Obj(("bordo_d", "1,25"))))) == (1.25, 0.75), "Asse–bordo scritto come testo con la virgola");
            Check(PileGroupPresentation.CapDistances(J.Obj(("diametro", 2), ("basamento", J.Obj(("bordo_d", ""))))) == (null, null)
                && PileGroupPresentation.CapDistances(J.Obj(("diametro", 2))) == (null, null), "Basamento senza distanza");
            Check(PileGroupPresentation.Row(J.Obj(("Row", 3))) == 3 && PileGroupPresentation.Reduction(J.Obj(("Factor", 0.25))) == 75
                && PileGroupPresentation.Reduction(new JsonObject { ["Factor"] = null }) is null, "Fila e riduzione del palo");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        return checks;
    }
}
