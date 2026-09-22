using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private UIElement BuildCustomMaterials()
    {
        if (settings["materiali_custom"] is not JsonArray) settings["materiali_custom"] = new JsonArray();
        var list = new ComboBox { MinWidth = 175, DisplayMemberPath = "" };
        void Reload() { list.ItemsSource = settings.Array("materiali_custom").Select(m => m.S("tipo") + " · " + m.S("nome")).ToArray(); if (list.Items.Count > 0) list.SelectedIndex = list.Items.Count - 1; }
        void Apply(JsonObject material)
        {
            string type = material.S("tipo");
            if (type == "Calcestruzzo")
            {
                Input["classe_cls"] = "Personalizzato"; Input["fck_mpa"] = material["fck_mpa"]?.DeepClone(); Input["cls_diagramma"] = material["cls_diagramma"]?.DeepClone(); Input["materiale_cls_nome"] = material.S("nome");
                materials.Set("classe_cls", "Personalizzato", true); materials.Set("fck_mpa", Input.S("fck_mpa"), true);
            }
            else if (type == "Acciaio")
            {
                foreach (var key in new[] { "steel_modulus_mpa", "fyk_mpa", "steel_fu_mpa", "steel_eps_u", "steel_diagramma" }) { Input[key] = material[key]?.DeepClone(); materials.Set(key, Input.S(key), true); }
                Input["materiale_acciaio_nome"] = material.S("nome");
            }
            else
            {
                settings["materiale_trefolo"] = material.DeepClone();
                if (tendons.SelectedItem is JsonRow selected) ApplyTendonMaterial(selected, material);
                reloadTendonMaterials?.Invoke();
                TendonsChanged();
            }
            Invalidate();
        }
        void EditMaterial(string type)
        {
            var value = type == "Calcestruzzo" ? J.Obj(("nome", "CLS personalizzato"), ("fck_mpa", Input.S("fck_mpa")), ("cls_diagramma", Input.S("cls_diagramma", ConcreteMaterials.ConcreteDiagrams[0])))
                : type == "Acciaio" ? J.Obj(("nome", "Acciaio personalizzato"), ("fyk_mpa", Input.S("fyk_mpa")), ("steel_modulus_mpa", Input.S("steel_modulus_mpa")), ("steel_fu_mpa", Input.S("fyk_mpa")), ("steel_eps_u", "100"), ("steel_diagramma", "Elastoplastico"))
                : J.Obj(("nome", "Trefolo personalizzato"), ("Ep", "195000"), ("fpyk", "1670"), ("fpk", "1860"), ("eps_u", "35"), ("diagramma", "Incrudente"));
            value["tipo"] = type; value["id"] = Guid.NewGuid().ToString("N");
            Field[] fields = type == "Calcestruzzo" ? [new("nome", "Nome"), new("fck_mpa", "fck", "MPa"), new("cls_diagramma", "Diagramma", Choices: ConcreteMaterials.ConcreteDiagrams)]
                : type == "Acciaio" ? [new("nome", "Nome"), new("steel_modulus_mpa", "Es", "MPa"), new("fyk_mpa", "fyk", "MPa"), new("steel_fu_mpa", "fu", "MPa"), new("steel_eps_u", "εu", "‰"), new("steel_diagramma", "Diagramma", Choices: ["Elastoplastico", "Incrudente"])]
                : [new("nome", "Nome"), new("Ep", "Ep", "MPa"), new("fpyk", "fpyk", "MPa"), new("fpk", "fpk", "MPa"), new("eps_u", "εpu", "‰"), new("diagramma", "Diagramma", Choices: ["Elastoplastico", "Incrudente"])];
            var form = new InputForm(value, fields, _ => { }, wideChoices: true); var error = Ui.Text("", 12);
            Window? dialog = null;
            var apply = Ui.Button("Salva materiale e applica", () =>
            {
                try
                {
                    form.Commit(); if (string.IsNullOrWhiteSpace(value.S("nome"))) throw new ArgumentException("Inserire il nome del materiale.");
                    if (type == "Calcestruzzo") _ = ConcreteMaterials.Concrete(value);
                    else if (type == "Acciaio") _ = ConcreteMaterials.Rebar(value);
                    else { double e = value.Required("Ep", strict: true), fy = value.Required("fpyk", strict: true), fu = value.Required("fpk", strict: true), eps = value.Required("eps_u", strict: true) / 1000; if (fu < fy || eps <= fy / e) throw new ArgumentException("Trefoli: controllare fpk ≥ fpyk e εpu > fpyk / Ep."); }
                    settings["materiali_custom"]!.AsArray().Add(value.DeepClone()); Apply(value); Reload(); dialog!.Close();
                }
                catch (ArgumentException ex) { error.Text = ex.Message; }
            });
            string note = type == "Calcestruzzo" ? "Moduli, deformazioni limite e resistenza a trazione sono derivati dal materiale EN1992 della DLL. Curve tabellari generiche e materiali FRC restano da implementare."
                : type == "Trefoli" ? "Applicazione al cavo selezionato; gli altri cavi conservano il proprio materiale. Il materiale diventa anche il predefinito dei nuovi trefoli." : "Parametri e legge costitutiva passati direttamente al materiale della DLL.";
            dialog = Ui.Dialog(this, "Nuovo materiale · " + type, Ui.Paper(Ui.Stack(form, Notice(note), error, apply), 18), 560, 520); dialog.ShowDialog();
        }
        Reload();
        var tendonMaterial = Ui.Button("+ Trefoli", () => EditMaterial("Trefoli"));
        return Ui.Stack(Ui.Bar(Ui.Button("+ CLS", () => EditMaterial("Calcestruzzo")), Ui.Button("+ Acciaio", () => EditMaterial("Acciaio")), tendonMaterial), list,
            Ui.Button("Applica materiale salvato", () => { if (list.SelectedIndex >= 0) Apply((JsonObject)settings.Array("materiali_custom")[list.SelectedIndex]!); }));
    }
}
