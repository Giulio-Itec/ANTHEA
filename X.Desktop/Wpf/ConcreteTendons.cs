using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private Action? reloadTendonMaterials;
    private IEnumerable<JsonObject> AvailableTendonMaterials() => ConcreteMaterialCatalog.Steel(true, settings.S("normativa"))
        .Concat(settings.Array("materiali_custom").OfType<JsonObject>().Where(m => m.S("tipo") == "Trefoli"));
    private static string Exact(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private JsonRow TendonRow(JsonObject values)
    {
        if (!values.ContainsKey("diametro")) values["diametro"] = Exact(Math.Sqrt(values.D("area") * 4 / Math.PI));
        JsonRow? row = null;
        row = new JsonRow(values, field =>
        {
            if (field is "area" or "diametro")
            {
                if (double.TryParse(row!.Values.S(field).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number) && number > 0)
                    row.Output(field == "area" ? "diametro" : "area", Exact(field == "area" ? Math.Sqrt(number * 4 / Math.PI) : Math.PI * number * number / 4));
                else row.Output(field == "area" ? "diametro" : "area", "");
            }
            if (field == "materiale")
            {
                var selected = AvailableTendonMaterials().FirstOrDefault(m => m.S("nome") == values.S("materiale"));
                if (selected is not null) ApplyTendonMaterial(row!, selected);
            }
            TendonsChanged();
        });
        return row;
    }
    private void ApplyTendonMaterial(JsonRow row, JsonObject material)
    {
        foreach (string key in new[] { "Ep", "fpyk", "fpk", "eps_u", "diagramma" }) row.Output(key, material.S(key, key == "diagramma" ? "Incrudente" : ""));
        row.Output("materiale", material.S("nome")); row.Output("materiale_id", material.S("id"));
    }
    private UIElement BuildTendonInput()
    {
        if (settings["materiale_trefolo"] is not JsonObject) settings["materiale_trefolo"] = AvailableTendonMaterials().First(m => m.S("nome") == "Y1860").DeepClone();
        var choice = new ComboBox { MinWidth = 160 };
        var materials = new List<JsonObject>();
        reloadTendonMaterials = () =>
        {
            materials = AvailableTendonMaterials().ToList();
            if (settings["materiale_trefolo"] is JsonObject current && !materials.Any(m => m.S("id") == current.S("id"))) materials.Add(current);
            choice.ItemsSource = materials.Select(m => m.S("nome", "Materiale trefolo")).ToArray();
            int currentIndex = materials.FindIndex(m => m.S("id") == settings["materiale_trefolo"].S("id"));
            choice.SelectedIndex = currentIndex >= 0 ? currentIndex : 0;
            ((DataGridComboBoxColumn)tendons.Columns.First(c => c.Header?.ToString() == "Materiale")).ItemsSource = materials.Select(m => m.S("nome")).Concat(tendons.Rows.Select(r => r.Values.S("materiale"))).Distinct().ToArray();
        };
        reloadTendonMaterials();
        var values = J.Obj(("numero", "1"), ("diametro", Exact(Math.Sqrt(150 * 4 / Math.PI))), ("x", "0"), ("y", "0"), ("sigma0", "1000"));
        var form = new InputForm(values, [new("numero", "Numero trefoli nel cavo"), new("diametro", "Ø equivalente singolo trefolo", "mm"), new("x", "x cavo", "mm"), new("y", "y cavo", "mm"), new("sigma0", "Tensione iniziale σp0", "MPa")], _ => { }, true);
        var message = Ui.Text("", 11);
        return Ui.Stack(Notice("Come CheckerUI: un cavo di n trefoli è modellato con Øeq = Ø√n e area totale n·πØ²/4. Usare il diametro equivalente all’area metallica, non quello nominale esterno. Materiale e σp0 sono distinti per ogni cavo."),
            form, choice, Ui.Bar(Ui.Button("+ Cavo / trefolo", () =>
            {
                try
                {
                    form.Commit();
                    if (choice.SelectedIndex < 0) throw new ArgumentException("Creare e selezionare un materiale trefolo nel gruppo Materiali.");
                    double n = values.Required("numero", strict: true), d = values.Required("diametro", strict: true);
                    if (n != Math.Truncate(n) || n > 1000) throw new ArgumentException("Numero trefoli: intero fra 1 e 1000.");
                    double x = SectionWorkspace.Number(values.S("x"), "x"), y = SectionWorkspace.Number(values.S("y"), "y"), sigma = values.Required("sigma0");
                    var material = materials[choice.SelectedIndex];
                    if (sigma >= material.Required("fpk", strict: true)) throw new ArgumentException("σp0 deve essere inferiore a fpk.");
                    int index = 1; while (tendons.Rows.Any(r => r.Values.S("id") == "T" + index.ToString("D2"))) index++;
                    var row = TendonRow(J.Obj(("id", "T" + index.ToString("D2")), ("x", Exact(x)), ("y", Exact(y)), ("area", Exact(n * Math.PI * d * d / 4)), ("sigma0", Exact(sigma))));
                    ApplyTendonMaterial(row, material); tendons.Rows.Add(row); tendons.SelectedItem = row; TendonsChanged(); message.Text = "";
                }
                catch (ArgumentException ex) { message.Text = ex.Message; }
            }), Ui.Button("−", () => { tendons.Commit(); if (tendons.SelectedItem is JsonRow row) { tendons.Rows.Remove(row); TendonsChanged(); } })),
            message);
    }
}
