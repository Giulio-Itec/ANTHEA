using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private JsonRow EditableBar(JsonObject values) => new(values, _ =>
    {
        Input["barre_manuali"] = new JsonArray(barInventory.Rows.Select(r => r.Values.DeepClone()).ToArray());
        Invalidate(false);
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(RefreshPreview));
    });

    private void ShowSectionProperties()
    {
        var text = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Text = "Calcolo proprietà della sezione…" };
        Commit(); var input = (JsonObject)Input.DeepClone(); var workspace = (JsonObject)settings.DeepClone();
        if (settings["proprieta_sezione"] is not JsonObject) settings["proprieta_sezione"] = J.Obj(("metodo", "Da φ"), ("phi", "0"));
        var options = settings["proprieta_sezione"]!.AsObject();
        var modelTask = Task.Run(() => CheckerSection.PrepareModel(input, workspace));
        InputForm? form = null; int generation = 0; bool closed = false;
        bool ready = false;
        var export = Ui.Button("Esporta report TXT…", () =>
        {
            if (!ready) return;
            var file = new Microsoft.Win32.SaveFileDialog { Filter = "Report testo|*.txt", FileName = "ANTHEA_Proprieta_sezione.txt" };
            if (file.ShowDialog(Window.GetWindow(text)) == true)
                try { Archivio.ScriviAtomico(file.FileName, System.Text.Encoding.UTF8.GetBytes(text.Text)); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Esportazione proprietà"); }
        });
        async void Refresh()
        {
            int current = ++generation; ready = false;
            form?.Enable("phi", options.S("metodo") == "Da φ"); form?.Enable("n", options.S("metodo") == "Da n");
            try
            {
                var model = await modelTask; if (closed || current != generation) return;
                var snapshot = (JsonObject)options.DeepClone();
                var properties = await Task.Run(() => Anthea.Calculations.ConcreteSectionProperties.Calculate(model, input, workspace, snapshot));
                if (closed || current != generation) return;
                options["phi"] = Exact(properties.Phi); options["n"] = Exact(properties.N);
                form?.Set("phi", EngineeringFormat.Number(properties.Phi), true); form?.Set("n", EngineeringFormat.Number(properties.N), true); Modified?.Invoke();
                var b = new System.Text.StringBuilder();
                foreach (var group in properties.Values.GroupBy(v => v.Group))
                {
                    b.AppendLine(group.Key.ToUpperInvariant());
                    foreach (var value in group) b.AppendLine($"{value.Name} = {EngineeringFormat.Number(value.Value)} {value.Unit}");
                    b.AppendLine();
                }
                b.AppendLine("Sezione elastica non fessurata. n = Eacciaio(1 + φ)/Ecm, φ comune; Ep distinto per materiale. Queste opzioni non modificano le verifiche SLE del foglio.");
                string report = b.ToString();
                if (!closed && current == generation) { text.Text = report; ready = true; }
            }
            catch (Exception ex) { if (!closed && current == generation) text.Text = "Proprietà non disponibili: " + ex.Message; }
        }
        form = new InputForm(options, [new("metodo", "Omogeneizzazione", Choices: ["Da φ", "Da n"]), new("phi", "Coefficiente di viscosità φ"), new("n", "n armature = Es (1 + φ) / Ecm")], _ => Refresh(), true);
        form.SetValue(InputForm.CommitOnFocusLossProperty, true);
        var header = Ui.Stack(form, Ui.Bar(export, Ui.Button("Copia", () => { if (ready) Clipboard.SetText(text.Text); })));
        var dialog = Ui.Dialog(this, "Proprietà / report della sezione", Ui.Paper(Ui.Dock(text, top: header), 12), 720, 780);
        dialog.Closed += (_, _) => { closed = true; }; dialog.Show(); Refresh();
    }
}
