using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Tests;

/// <summary>Pieces of the fields moved from X.Desktop Ui.cs and NumericPresentation (F5.2): write rules, symbols, notes, numbers.</summary>
internal static class FieldSpecChecks
{
    internal static int Run()
    {
        var expect = new Expect();
        WriteRules(expect);
        Symbols(expect);
        Presentation(expect);
        return expect.Count;
    }

    static string Text(JsonObject values) => values.ToJsonString(J.Options);

    static void WriteRules(Expect expect)
    {
        // Raw text: InputForm.Store, values[key] = J.Node(text); nothing is interpreted.
        var raw = new JsonObject { ["a"] = "x", ["b"] = 1 };
        FieldWrite.Apply(raw, "a", "1,50", FieldWriteRule.RawText);
        FieldWrite.Apply(raw, "b", "", FieldWriteRule.RawText);
        FieldWrite.Apply(raw, "c", " 2 ", FieldWriteRule.RawText);
        var rawExpected = new JsonObject { ["a"] = J.Node("1,50"), ["b"] = J.Node(""), ["c"] = J.Node(" 2 ") };
        expect.That(Text(raw) == Text(rawExpected), "Testo grezzo: stesso nodo di InputForm.Store, chiavi nell'ordine di prima");
        expect.That(raw["a"]!.GetValueKind() == JsonValueKind.String && raw["b"]!.GetValueKind() == JsonValueKind.String, "Testo grezzo: numeri e vuoti restano testo");

        // Number when the text is a finite number, otherwise the text: ModuleSheetPreparation.NumericText (pile group).
        var numeric = new JsonObject();
        foreach (var (key, text) in new[] { ("virgola", "1,5"), ("punto", "2.25"), ("esponente", "1e-3"), ("testo", "abc"), ("infinito", "1e400"), ("vuoto", ""), ("spazi", "  ") })
            FieldWrite.Apply(numeric, key, text, FieldWriteRule.NumberOrText);
        var numericExpected = new JsonObject();
        foreach (var (key, text) in new[] { ("virgola", "1,5"), ("punto", "2.25"), ("esponente", "1e-3"), ("testo", "abc"), ("infinito", "1e400"), ("vuoto", ""), ("spazi", "  ") })
            numericExpected[key] = ModuleSheetPreparation.NumericText(text);
        expect.That(Text(numeric) == Text(numericExpected), "Numero o testo: stessi byte di ModuleSheetPreparation.NumericText");
        expect.That(numeric["virgola"]!.GetValueKind() == JsonValueKind.Number && numeric["virgola"]!.GetValue<double>() == 1.5, "Numero o testo: la virgola decimale diventa numero");
        expect.That(numeric["testo"]!.GetValue<string>() == "abc" && numeric["infinito"]!.GetValue<string>() == "1e400" && numeric["vuoto"]!.GetValue<string>() == "",
            "Numero o testo: testo, numero non finito e vuoto restano testo");

        // Optional field: key removed when empty or blank, otherwise number or text.
        var optional = new JsonObject { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
        FieldWrite.Apply(optional, "a", "", FieldWriteRule.NumberOrTextRemoveEmpty);
        FieldWrite.Apply(optional, "b", "   ", FieldWriteRule.NumberOrTextRemoveEmpty);
        FieldWrite.Apply(optional, "c", "4,5", FieldWriteRule.NumberOrTextRemoveEmpty);
        FieldWrite.Apply(optional, "a", "7", FieldWriteRule.NumberOrTextRemoveEmpty);
        expect.That(Text(optional) == Text(new JsonObject { ["c"] = 4.5, ["a"] = 7.0 }), "Campo opzionale: vuoto toglie la chiave, poi torna in coda");
        FieldWrite.Apply(optional, "assente", "", FieldWriteRule.NumberOrTextRemoveEmpty);
        expect.That(!optional.ContainsKey("assente") && optional.Count == 2, "Campo opzionale vuoto e assente: nessuna chiave");

        // Booleans are JSON booleans with every rule.
        foreach (var rule in Enum.GetValues<FieldWriteRule>())
        {
            var flags = new JsonObject();
            FieldWrite.Apply(flags, "v", true, rule); FieldWrite.Apply(flags, "f", false, rule);
            expect.That(Text(flags) == Text(new JsonObject { ["v"] = J.Node(true), ["f"] = J.Node(false) }), "Booleano JSON con la regola " + rule);
        }
        expect.That(new FieldSpec("k", "K").Write == FieldWriteRule.RawText, "Regola predefinita: testo grezzo, come InputForm");
    }

    static void Symbols(Expect expect)
    {
        expect.That(FieldSymbols.For("diametro", "Diametro palo D", "") == ("Diametro palo", "D"), "Tabella dei simboli: diametro");
        expect.That(FieldSymbols.For("percentuale_punta", "Punta", "") == ("Punta rispetto alla laterale", ""), "Tabella dei simboli: etichetta senza simbolo");
        expect.That(FieldSymbols.For("diametro", "Diametro perforazione", "Db") == ("Diametro perforazione", "Db"), "Simbolo proprio del campo prevale sulla tabella");
        expect.That(FieldSymbols.For("altro", "Etichetta", "") == ("Etichetta", ""), "Chiave fuori tabella: etichetta invariata");
        var spec = new FieldSpec("__xi3", "Correlazione ξ3", "", null, false, true, Note: _ => "n", HelpTopic: "t", Write: FieldWriteRule.NumberOrText);
        var applied = FieldSymbols.Apply(spec);
        expect.That(applied.Label == "Correlazione" && applied.Symbol == "ξ3" && applied.ReadOnly && applied.HelpTopic == "t" && applied.Note == spec.Note
            && applied.Write == FieldWriteRule.NumberOrText, "Apply cambia solo etichetta e simbolo");
        var own = new FieldSpec("diametro", "D", Symbol: "Db");
        expect.That(ReferenceEquals(FieldSymbols.Apply(own), own), "Apply restituisce il campo con simbolo proprio");
    }

    static void Presentation(Expect expect)
    {
        string Rounded(double n) => n.ToString("0.##", CultureInfo.CurrentCulture);
        expect.That(NumericDisplay.Format("123.456789", "x") == Rounded(123.46), "Due decimali in presentazione");
        expect.That(NumericDisplay.Format("1,5", "x") == Rounded(1.5), "Virgola decimale riconosciuta");
        expect.That(NumericDisplay.Format("-0.000000001", "x") == "0", "Posizione quasi nulla senza zero negativo");
        expect.That(NumericDisplay.Format("0.000123456", "strain").Contains('E') && NumericDisplay.Format("0.000123456", "eps_u").Contains('E'), "Deformazioni piccole leggibili");
        expect.That(NumericDisplay.Format("001", "nome") == "001" && NumericDisplay.Format("12.345", "id") == "12.345", "Identificativi numerici non arrotondati");
        expect.That(NumericDisplay.Format("abc", "x") == "abc" && NumericDisplay.Format("", "x") == "" && NumericDisplay.Format("1e400", "x") == "1e400", "Testi e numeri non finiti invariati");
        expect.That(FieldNotes.StressBlock(new JsonObject { ["cls_diagramma"] = "Stress block" })?.StartsWith("Stress block: risultati SLU meno precisi.") == true
            && FieldNotes.StressBlock(new JsonObject { ["cls_diagramma"] = "Parabola-rettangolo" }) is null && FieldNotes.StressBlock(new JsonObject()) is null,
            "Nota dello stress block solo con il legame stress block");
    }
}
