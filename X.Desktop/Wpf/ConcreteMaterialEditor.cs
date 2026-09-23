using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    internal Window CreateMaterialDialog(string type, Action<JsonObject> save)
    {
        var catalog = type == "Calcestruzzo" ? ConcreteMaterialCatalog.Concrete(settings.S("normativa")) : ConcreteMaterialCatalog.Steel(type == "Trefoli", settings.S("normativa"));
        string current = type == "Calcestruzzo" ? Input.S("classe_cls") : type == "Acciaio" ? Input.S("classe_acciaio") : settings["materiale_trefolo"].S("nome");
        var preset = catalog.FirstOrDefault(m => m.S("nome") == current) ?? catalog[0];
        var value = (JsonObject)preset.DeepClone();
        value["nome"] = type + " personalizzato"; value["tipo"] = type; value["id"] = Guid.NewGuid().ToString("N");
        value["origine"] = "Personalizzato"; value["normativa_origine"] = settings.S("normativa");
        Field[] parameters = type == "Calcestruzzo" ? [new("fck_mpa", "fck", "MPa"), new("cls_diagramma", "Diagramma", Choices: ConcreteMaterials.ConcreteDiagrams)]
            : type == "Acciaio" ? [new("steel_modulus_mpa", "Es", "MPa"), new("fyk_mpa", "fyk", "MPa"), new("steel_fu_mpa", "fu", "MPa"), new("steel_eps_u", "εu", "‰"), new("steel_diagramma", "Diagramma", Choices: ["Elastoplastico", "Incrudente"])]
            : [new("Ep", "Ep", "MPa"), new("fpyk", "fpyk", "MPa"), new("fpk", "fpk", "MPa"), new("eps_u", "εpu", "‰"), new("diagramma", "Diagramma", Choices: ["Elastoplastico", "Incrudente"])];
        var plot = new Plot { Title = "Diagramma σ–ε", XLabel = "ε [‰]", YLabel = "σ [MPa]", InvertY = false, NegateAxisLabels = type == "Calcestruzzo", VerticalLegend = true, FitPadding = .06, Note = type == "Calcestruzzo" ? "Compressione (−ε, −σ) nel primo quadrante · sola rappresentazione" : "Compressione negativa · curve native della DLL" };
        var error = Ui.Text("", 12); var derived = Ui.Text("", 12);
        InputForm? form = null;
        bool valid = false;
        void Refresh(string key)
        {
            try
            {
                var curves = MaterialDiagram.Create(type, value, Input, settings);
                List<double[]> Display(double[][] points) => points.Select(p => type == "Calcestruzzo" ? new[] { -p[0], -p[1] } : p).ToList();
                plot.Series = [new("Caratteristica", Display(curves.Characteristic), Ui.Blue), new("Progetto · " + settings.S("normativa"), Display(curves.Design), Ui.Brush("#CE4F44"), true)];
                if (type == "Calcestruzzo")
                {
                    var native = ConcreteMaterials.Concrete(value);
                    derived.Text = $"Ecm = {EngineeringFormat.Number(native.E)} MPa\nεc,y = {EngineeringFormat.Number(Math.Abs(native.StrainYCompression) * 1000)} ‰\nεc,u = {EngineeringFormat.Number(Math.Abs(native.StrainUCompression) * 1000)} ‰";
                }
                else derived.Text = "Proprietà caratteristiche dal materiale; valori di progetto con i coefficienti del foglio.";
                error.Text = ""; valid = true;
            }
            catch (ArgumentException ex) { plot.Series = []; error.Text = ex.Message; valid = false; derived.Text = ""; }
            plot.ResetView();
        }
        form = new InputForm(value, new Field[] { new("nome", "Nome", Wide: true) }.Concat(parameters), Refresh, wideChoices: true);
        ((TextBox)form.Editors["nome"]).TextAlignment = TextAlignment.Left;
        Refresh("origine");
        Window? dialog = null;
        var apply = Ui.Button("Salva materiale e applica", () =>
        {
            form.Commit(); Refresh("");
            if (!valid) return;
            if (string.IsNullOrWhiteSpace(value.S("nome"))) { error.Text = "Inserire il nome del materiale."; return; }
            if (catalog.Any(m => m.S("nome") == value.S("nome")) || settings.Array("materiali_custom").Any(m => m.S("tipo") == type && m.S("nome") == value.S("nome")))
            { error.Text = "Usare un nome distinto dai materiali esistenti."; return; }
            save((JsonObject)value.DeepClone()); dialog!.Close();
        });
        var help = Notice(settings.S("normativa") + " · inserire le proprietà caratteristiche del nuovo materiale. Le leggi della DLL ricavano il diagramma di progetto con i coefficienti del foglio. Nessuna modifica finché non si salva.");
        dialog = Ui.Dialog(this, "Nuovo materiale · " + type, Ui.Paper(Ui.Dock(Columns((Scroller(Ui.Stack(form, derived)), 4, 280), (plot, 6, 380)), top: help, bottom: Ui.Stack(error, apply)), 12), 1050, 700);
        return dialog;
    }
}
