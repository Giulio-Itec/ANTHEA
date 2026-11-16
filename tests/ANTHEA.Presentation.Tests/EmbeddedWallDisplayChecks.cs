using System.Globalization;
using System.Text.RegularExpressions;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Modules.EmbeddedWalls;
using Anthea.Presentation.Shell.Commands;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Cifre significative (16/11): the paratie module shows every number with the decimals of its unit, in the culture of the application (the
/// decimal separator of «Aspetto»), with 0 for the noise below the last decimal and never «-0» or an exponent; the terms written in English by
/// the results and the choices show an Italian label while the value stays. Display only: data and revision unchanged.
/// </summary>
internal static partial class EmbeddedWallChecks
{
    private static readonly Regex Exponent = new("[0-9][eE][+-]?[0-9]", RegexOptions.CultureInvariant);

    private static async Task DisplayChecks(Expect e)
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            foreach (var choice in new[] { DecimalSeparatorChoice.Comma, DecimalSeparatorChoice.Point })
            {
                CultureInfo.CurrentCulture = NumberSeparators.Culture(choice, CultureInfo.GetCultureInfo("it-IT"));
                string s = choice == DecimalSeparatorChoice.Comma ? "," : ".";
                (double? Value, string Unit, string Text)[] cases =
                [
                    (4.25419E-10, "kNm", "0"), (-5.55112E-17, "kN/m", "0"), (-3.13861E-05, "m", "0"), (-0.017358, "m", "-0" + s + "017"),
                    (111.089, "mm", "111" + s + "09"), (0.000903893, "kN", "0"), (0.0117187, "kN", "0" + s + "01"), (-153.58, "kNm", "-153" + s + "58"),
                    (1631.474, "kN", "1631" + s + "47"), (0.00123456, "rad", "0" + s + "00123"), (1.5E-07, "m/s", "1" + s + "5·10⁻⁷"),
                    (0.61534, "-", "0" + s + "615"), (0.61534, "", "0" + s + "615"), (1.25, "MPa", "1" + s + "25"), (0.01534, "L/s/m", "0" + s + "0153"),
                    (32.0, "°", "32"), (18.5, "kN/m³", "18" + s + "5"), (null, "m", EmbeddedWallDisplay.Missing), (double.NaN, "kN", EmbeddedWallDisplay.Missing),
                    (-0.004, "mm", "0"), (1E+21, "kN", "1000000000000000000000"),
                ];
                foreach (var (value, unit, text) in cases)
                {
                    string shown = EmbeddedWallDisplay.Number(value, unit);
                    e.That(shown == text && !Exponent.IsMatch(shown) && shown != "-0",
                        $"Cifre ({choice}): {value?.ToString("R", CultureInfo.InvariantCulture)} {unit} mostrato «{shown}» invece di «{text}».");
                }
                e.That(EmbeddedWallSketch.Format(-4.25, "m") == "-4" + s + "25" && EmbeddedWallSketch.Format(15, "kPa") == "15",
                    "Cifre: il disegno non usa la regola del modulo o il separatore scelto.");
                // Approved on 16/11: tables with fixed decimals and the final zeros; m/s as a power of ten (1,5·10⁻⁷) everywhere.
                (double? Value, string Unit, string Text)[] table =
                [
                    (110.9, "mm", "110" + s + "90"), (111.089, "mm", "111" + s + "09"), (0d, "kN", "0" + s + "00"), (-0.004, "mm", "0" + s + "00"),
                    (-4.25419E-10, "kNm", "0" + s + "00"), (0.6, "-", "0" + s + "600"), (-3.0, "m", "-3" + s + "000"), (null, "m", EmbeddedWallDisplay.Missing),
                    (1.5E-07, "m/s", "1" + s + "5·10⁻⁷"), (0d, "m/s", "0"),
                ];
                foreach (var (value, unit, text) in table)
                {
                    string shown = EmbeddedWallDisplay.Number(value, unit, table: true);
                    e.That(shown == text, $"Cifre in tabella ({choice}): {value?.ToString("R", CultureInfo.InvariantCulture)} {unit} mostrato «{shown}» invece di «{text}».");
                }
                (double? Value, string Text)[] powers =
                [
                    (1E-05, "1" + s + "0·10⁻⁵"), (-2.345E-06, "-2" + s + "3·10⁻⁶"), (9.96E-08, "1" + s + "0·10⁻⁷"), (3.2, "3" + s + "2"),
                    (1200d, "1" + s + "2·10³"), (0d, "0"), (null, EmbeddedWallDisplay.Missing), (double.PositiveInfinity, EmbeddedWallDisplay.Missing),
                ];
                foreach (var (value, text) in powers)
                    e.That(EmbeddedWallDisplay.Scientific(value) == text, $"Cifre ({choice}): potenza di dieci di {value} «{EmbeddedWallDisplay.Scientific(value)}» invece di «{text}».");
                var kxx = EmbeddedWallFields.Columns("hydraulic_left").Single(f => f.Key == "kxx");
                var permanent = EmbeddedWallFields.Columns("anchor_checks").Single(f => f.Key == "permanent");
                var kind = EmbeddedWallFields.Columns("supports").Single(f => f.Key == "kind");
                e.That(EmbeddedWallDisplay.Field(kxx, "1E-05") == "1" + s + "0·10⁻⁵" && EmbeddedWallDisplay.Field(kxx, "0,00002") == "2" + s + "0·10⁻⁵"
                    && EmbeddedWallDisplay.Field(kxx, "da definire") == "da definire" && EmbeddedWallDisplay.Field(permanent, "true") == "sì"
                    && EmbeddedWallDisplay.Field(kind, "Anchor") == "Tirante", "Cifre: valori dei campi nei riquadri e nella tabella dei dati errati.");
            }
            CultureInfo.CurrentCulture = NumberSeparators.Culture(DecimalSeparatorChoice.Comma, CultureInfo.GetCultureInfo("it-IT"));
            e.That(EmbeddedWallDisplay.UnitOf("Momento [kNm]") == "kNm" && EmbeddedWallDisplay.UnitOf("Rotazione [rad]") == "rad" && EmbeddedWallDisplay.UnitOf("Senza") == "",
                "Cifre: unità della grandezza dei diagrammi non letta.");
            e.That(EmbeddedWallDisplay.Term("side", "Below") == "sotto" && EmbeddedWallDisplay.Term("side", "Above") == "sopra" && EmbeddedWallDisplay.Term("side", "Interior") == "interno"
                && EmbeddedWallDisplay.Term("kind", "Strut") == "Puntone" && EmbeddedWallDisplay.Term("search", "NoSurface") == "nessuna superficie"
                && EmbeddedWallDisplay.Term("seepage", "Converged") == "Convergente" && EmbeddedWallDisplay.Term("side", "S1") == "S1" && EmbeddedWallDisplay.Term(null, "Below") == "Below",
                "Cifre: termini dei risultati non in italiano o valori sconosciuti cambiati.");
            // Every English choice of the forms has its label, and every label belongs to a choice of the forms (no stale labels).
            var fields = EmbeddedWallDocument.Tables.SelectMany(t => EmbeddedWallFields.Columns(t)).Concat(EmbeddedWallFields.General()).Concat(EmbeddedWallFields.Hydraulics())
                .Concat(EmbeddedWallFields.Embedment()).Where(f => f.Choices is not null).ToArray();
            foreach (var (key, labels) in EmbeddedWallDisplay.ChoiceLabels)
            {
                var choices = fields.Where(f => f.Key == key).SelectMany(f => f.Choices!).Distinct().ToArray();
                e.That(choices.Length > 0 && choices.All(labels.ContainsKey) && labels.Keys.All(choices.Contains),
                    $"Cifre: etichette della scelta «{key}» incomplete o superate: {string.Join(", ", choices.Where(c => !labels.ContainsKey(c)))}.");
            }
            e.That(EmbeddedWallDisplay.Choice("kind", "Anchor") == "Tirante" && EmbeddedWallDisplay.Choice("kind", "Altro") == "Altro"
                && EmbeddedWallDisplay.Choice("injection", "IRS") == "IRS", "Cifre: etichetta della scelta errata.");

            // A calculated sheet: texts of the module without exponents; the tooltip of a support says «Tirante», the row keeps «Anchor».
            var data = Example();
            var support = EmbeddedWallDocument.NewRow("supports"); support["id"] = "T1"; support["elevation"] = -1; support["stiffness"] = 10000;
            data["supports"]!.AsArray().Add(support);
            var present = EmbeddedWallDocument.NewRow("activations"); present["prestress"] = 0; data["activations"]!.AsArray().Add(present);
            using var sheet = Open(data);
            e.That(await Expect.Within(sheet.CalculateAsync()) && sheet.Current is not null, "Cifre: esempio non calcolato.");
            string before = sheet.Data.ToJsonString(); long revision = sheet.Revision;
            var texts = sheet.AnalysisEntries.SelectMany(a => new[] { a.Title, a.Detail }).Append(sheet.EnvelopeExtrema).Append(sheet.PhaseSummary)
                .Concat(sheet.Sketch!.Notices).Concat(sheet.Messages.All.Where(m => m.Origin != EmbeddedWallMessages.Calculation).Select(m => m.Text)).ToArray();
            e.That(texts.All(t => !Exponent.IsMatch(t)) && sheet.AnalysisEntries.Any(a => a.Detail.Contains("forza ")),
                "Cifre: notazione esponenziale nei testi del modulo: " + string.Join(" | ", texts.Where(t => Exponent.IsMatch(t))));
            var tie = sheet.Sketch.Supports.Single();
            e.That(tie.Detail.Contains("Tipo: Tirante") && tie.Row["kind"] == "Anchor", "Cifre: tipo del sostegno non in italiano o valore salvato cambiato: " + tie.Detail);
            var node = sheet.WorkTree.SelectMany(n => Flatten(n)).First(n => ReferenceEquals(n.Row, tie.Row));
            e.That(node.Detail.Contains("Tipo: Tirante") && sheet.ChoiceLabels["kind"]["Anchor"] == "Tirante", "Cifre: l'albero mostra il valore inglese del tipo.");
            e.That(sheet.Data.ToJsonString() == before && sheet.Revision == revision, "Cifre: la presentazione modifica dati o revisione.");
        }
        finally { CultureInfo.CurrentCulture = saved; }
    }

    private static IEnumerable<EmbeddedWallWorkNode> Flatten(EmbeddedWallWorkNode node) => new[] { node }.Concat(node.Children.SelectMany(n => Flatten(n)));
}
