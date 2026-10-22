using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;

/// <summary>
/// Derivations of the shear and torsion tab of the RC section moved from the historic view in sub-step S5 of F5.10 (ConcreteShearPresentation:
/// options of the shear, note of the automatic parameters, outputs of the rows, summary, detail and torsion), with values written by hand: the
/// shear of a new row of the fixture «default» is that of its frozen historic trace (tests/fixtures/f5-sezione, taglio/taglio-torsione.txt,
/// the wait after «+ Combinazione»). The identity of the historic view with these functions is proved by ui/check-sezione-historic.
/// </summary>
internal static class ConcreteShearPresentationChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var options = J.Obj(("modello", "Con staffe"));
            Check(ConcreteShearPresentation.Automatic(options) && !ConcreteShearPresentation.Automatic(J.Obj(("parametri", "Manuali")))
                && ConcreteShearPresentation.WithLinks(options) && !ConcreteShearPresentation.WithLinks(J.Obj(("modello", "Senza staffe")))
                && ConcreteShearPresentation.ModelCode(J.Obj(("normativa", "Model Code 2010"))) && !ConcreteShearPresentation.ModelCode(J.Obj(("normativa", "NTC 2018"))),
                "Parametri automatici senza valore, modello con staffe, Model Code");
            Check(ConcreteShearPresentation.AutomaticNote("Circolare").StartsWith("Circolare: bw = D (piena)", StringComparison.Ordinal)
                && ConcreteShearPresentation.AutomaticNote("Rettangolare").StartsWith("Automatici: bw minima", StringComparison.Ordinal), "Nota dei parametri automatici per forma");
            Check(string.Join("|", ConcreteShearPresentation.FailedOutputs("rami_x: inserire un numero finito.").Select(o => o.Key + "=" + o.Value))
                == "VRdx=—|VRdy=—|eta_x=—|eta_y=—|eta_t=—|eta_vt=—|esito=rami_x: inserire un numero finito.", "Uscite di una riga fermata dall'errore");
            Check(ConcreteShearPresentation.Summary(0, [], []) == "Taglio: 0/0 combinazioni calcolate · 0 oltre resistenza · 0 senza esito · dettagli da verificare. Torsione: 0 calcolate, 0 non soddisfatte."
                && ConcreteShearPresentation.ShearSummary(2, []) == "Taglio: 0/2 combinazioni calcolate · 0 oltre resistenza · 0 senza esito · dettagli da verificare.", "Sintesi senza risultati");
            var waiting = J.Obj(("nome", "Taglio 1"), ("N", "-800"), ("Vx", "300"), ("Vy", "120"));
            Check(ConcreteShearPresentation.Detail(waiting, options, "Rettangolare", null, null) == "Taglio 1 · Con staffe\nN = -800,00 kN (compressione negativa)\nVx / Vy = 300,00 / 120,00 kN\n\nDa calcolare"
                && ConcreteShearPresentation.TorsionSummary(waiting, null) == "", "Dettaglio senza risultati: le azioni e l'esito; nessuna torsione");

            // A new row of the fixture «default», calculated.
            var data = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/str_palo--default.json")))!["data"]!.AsObject();
            ConcreteSheetPreparation.PrepareReport(data);
            var input = data["input"]!.AsObject(); var settings = data["workspace_ca"]!.AsObject(); var shear = settings["taglio"]!.AsObject();
            var row = J.Obj(("id", "t1"), ("nome", "Taglio 1"), ("N", "0"), ("Mx", "0"), ("My", "0"), ("Vx", "0"), ("Vy", "0"), ("T", "0"));
            ConcreteResults.ShearBatchResult? result = null;
            ConcreteShearBatch.Calculate(input, settings, [row], (_, calculated, _) => result = calculated);
            var outputs = ConcreteShearPresentation.Outputs(result!.Shear, result.Torsion);
            Check(string.Join("|", outputs.Select(o => o.Key + "=" + o.Value)) == "VRdx=352,60|eta_x=0,00|VRdy=489,58|eta_y=0,00|esito=x: Resistenza sufficiente · dettagli da verificare · y: Resistenza sufficiente · dettagli da verificare|eta_t=—|eta_vt=—",
                "Uscite della riga: VRd ed η per direzione, esito, torsione assente");
            string detail = ConcreteShearPresentation.Detail(row, shear, input.S("shape"), result.Shear, result.Torsion);
            Check(detail.StartsWith("Taglio 1 · Con staffe\nN = 0,00 kN (compressione negativa)\nVx / Vy = 0,00 / 0,00 kN\n\nDIREZIONE Vx\nbw = 800,00 mm · d = 509,91 mm\nVRsd (staffe) = 352,60 kN\nVRcd (puntone) = 1255,44 kN\ncot θ = 2,50\nGoverna: armatura trasversale\nVRd = 352,60 kN · η = 0,00\n", StringComparison.Ordinal)
                && detail.EndsWith("\n\nNTC 2018 §§4.1.2.3.5.1–2\n\n\n", StringComparison.Ordinal) && !detail.Contains("TORSIONE", StringComparison.Ordinal), "Dettaglio della riga calcolata");
            Check(ConcreteShearPresentation.Summary(1, [result.Shear], []).StartsWith("Taglio: 1/1 combinazioni calcolate · 0 oltre resistenza · 0 senza esito", StringComparison.Ordinal),
                "Sintesi con una riga calcolata");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        return checks;
    }
}
