using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;
using X.Desktop.Views.Common;

namespace X.Desktop;

/// <summary>
/// Step «componenti-campi» of --check-contrast (F5.2, proposal F5 section 7.3): two FieldForm in a dialog of the main window,
/// in each appearance. The audit captures the dialog (one PNG per appearance), opens the closed groups and measures the
/// contrast of every text. The forms show every element of the template: labels, units, symbols, the «?» label, text boxes,
/// calculated fields, choices, check boxes, a wide field, a notice, an error of a validator and the groups.
/// Run alone: ANTHEA.dll --check-contrast &lt;cartella&gt; --viste componenti.
/// </summary>
internal static class FieldFormContrast
{
    sealed class Required(string key, string message) : IFieldValidator
    {
        public IEnumerable<FieldError> Validate(JsonObject values) => values.S(key) == "" ? [new FieldError(key, message)] : [];
    }

    internal static async Task Audit(ContrastAudit audit, Window owner)
    {
        var options = new JsonObject
        {
            ["modello"] = "Lineare", ["n_armature"] = "15.123456", ["__ep_ref"] = "195000", ["cls_diagramma"] = "Stress block", ["trazione_cls"] = "No",
            ["presenza_falda"] = true, ["motivo"] = "Sezione fessurata", ["limite_fessure"] = "", ["origine_x"] = "0", ["rotazione"] = "0"
        };
        var concrete = new FormViewModel(options, [
            new("modello", "Analisi", Choices: ["Lineare", "Non lineare"], HelpTopic: WikiContextHelp.StressAnalysis.Uri), new("n_armature", "n armature"),
            new("__ep_ref", "Ep di riferimento", "MPa", ReadOnly: true), new("cls_diagramma", "Diagramma CLS", Choices: ["Parabola-rettangolo", "Stress block"], Note: FieldNotes.StressBlock),
            new("presenza_falda", "Presenza falda", Bool: true), new("motivo", "Motivazione", Wide: true), new("limite_fessure", "wlim di progetto", "mm"),
            new("trazione_cls", "CLS resistente a trazione", Choices: ["No", "Sì"]), new("origine_x", "Origine x", "mm"), new("rotazione", "Rotazione assi", "°")],
            wikiModule: "str_palo", validator: new Required("limite_fessure", "wlim richiesto dal Model Code: valore di progetto mancante"));
        concrete.GroupFields("Avanzate · CLS teso e assi", ["trazione_cls", "origine_x", "rotazione"]);
        var pile = new FormViewModel(Archivio.NuovoFoglio("geo_palo_verticale")["generali"]!.AsObject().DeepClone().AsObject(), [
            new("tipo_palo", "Tipo di palo", Choices: ["Trivellato", "Elica continua", "Battuto"]), new("diametro", "Diametro palo D", "m"),
            new("presenza_falda", "Presenza falda", Bool: true), new("profondita_falda", "Profondità falda zf", "m"), new("__xi3", "Correlazione ξ3", ReadOnly: true)],
            compact: true, symbolColumns: true, wikiModule: "geo_palo_verticale");
        var body = new StackPanel();
        body.Children.Add(new FieldForm { DataContext = concrete });
        body.Children.Add(new FieldForm { DataContext = pile, Margin = new Thickness(0, 16, 0, 0) });
        var dialog = Ui.Dialog(owner, "Componenti · campi del form (FieldForm)", new ScrollViewer { Content = body, Padding = new Thickness(12) }, 640, 900);
        await audit.Trigger("FieldForm", dialog.Show, 8);
    }
}
