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
                double ec = model.Section.ConcreteMaterial.E, es = input.Required("steel_modulus_mpa", strict: true);
                double phi = options.S("metodo") == "Da n" ? SectionWorkspace.Number(options.S("n"), "n") * ec / es - 1 : SectionWorkspace.Number(options.S("phi", "0"), "φ");
                if (!double.IsFinite(phi) || phi < -1e-12) throw new ArgumentException("Inserire φ ≥ 0 oppure n ≥ Es/Ecm.");
                phi = Math.Max(0, phi); double n = es * (1 + phi) / ec;
                options["phi"] = Exact(phi); options["n"] = Exact(n);
                form?.Set("phi", EngineeringFormat.Number(phi), true); form?.Set("n", EngineeringFormat.Number(n), true); Modified?.Invoke();
                string report = await Task.Run(() =>
                {
                var section = model.Section;
                var b = new System.Text.StringBuilder(model.Geometry.Holes.Count>0?"SOLO CALCESTRUZZO · area al netto del foro, senza sottrarre le barre\n":"SOLO CALCESTRUZZO · sezione lorda\n");
                void Value(string name, double value, string unit) => b.AppendLine($"{name} = {EngineeringFormat.Number(value)} {unit}");
                // Width/Height are not implemented by the DLL's generic polygon section.
                // Use the same geometric bounds that generated the native shape.
                Value("Area", section.Area / 100, "cm²"); Value("Larghezza", model.Geometry.Width, "mm"); Value("Altezza", model.Geometry.Height, "mm");
                if(model.Geometry.Shape=="Circolare")Value("Lati per contorno circolare",model.Geometry.CircularSides,"");
                Value("Baricentro x", section.Centroid.X, "mm"); Value("Baricentro y", section.Centroid.Y, "mm");
                foreach (string key in new[] { "Jxx", "Jyy", "Jxy", "Jp", "J11", "J22" }) Value(key, (double)section.GetType().GetProperty(key)!.GetValue(section)! / 1e4, "cm⁴");
                foreach (string key in new[] { "WelXMin", "WelXMax", "WelYMin", "WelYMax" }) Value(key, (double)section.GetType().GetProperty(key)!.GetValue(section)! / 1e3, "cm³");
                Value("Raggio giratore x", section.Rxx, "mm"); Value("Raggio giratore y", section.Ryy, "mm");
                b.AppendLine($"\nOMOGENEIZZATA AL CLS · sezione integra, φ = {EngineeringFormat.Number(phi)} (DLL Checker)");
                Value("n armature", n, "");
                foreach (var ep in workspace.Array("trefoli").Select(t => t.D("Ep")).Distinct()) Value($"n trefoli (Ep = {EngineeringFormat.Number(ep)} MPa)", ep * (1 + phi) / ec, "");
                var h = section.GetHomogeneizedMechanicalProperties(phi);
                Value("Area omogeneizzata", h.areaH / 100, "cm²"); Value("Baricentro x", h.centroidH.X, "mm"); Value("Baricentro y", h.centroidH.Y, "mm");
                Value("Sx", h.SxH / 1e3, "cm³"); Value("Sy", h.SyH / 1e3, "cm³");
                Value("Jxx", h.JxxH / 1e4, "cm⁴"); Value("Jyy", h.JyyH / 1e4, "cm⁴"); Value("Jxy", h.JxyH / 1e4, "cm⁴");
                Value("J11", h.J11H / 1e4, "cm⁴"); Value("J22", h.J22H / 1e4, "cm⁴"); Value("Angolo principale", h.angleX * 180 / Math.PI, "°");
                b.AppendLine("\nARMATURE ORDINARIE"); Value("Numero barre", model.Geometry.Bars.Count, ""); Value("As totale", model.Geometry.AreaSteel / 100, "cm²"); Value("As / Ac", 100 * model.Geometry.AreaSteel / section.Area, "%");
                foreach (var group in model.Geometry.Bars.GroupBy(v => v.Diametro).OrderBy(g => g.Key)) b.AppendLine($"{group.Count()} Ø{EngineeringFormat.Number(group.Key)} · As = {EngineeringFormat.Number(group.Sum(v => v.Area) / 100)} cm²");
                b.AppendLine("\nTREFOLI / CAVI"); Value("Numero cavi", workspace.Array("trefoli").Count, ""); Value("Ap totale", workspace.Array("trefoli").Sum(t => t.D("area")) / 100, "cm²");
                b.AppendLine("\nSezione elastica non fessurata. n = Eacciaio(1 + φ)/Ecm, φ comune; Ep distinto per materiale. Queste opzioni non modificano le verifiche SLE del foglio.");
                return b.ToString();
                });
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
