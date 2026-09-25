using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private readonly TextBlock sharedStatus = Ui.Text("", 12);
    private sealed record CoverStatus(string Text, bool? Passed);

    private static List<CoverStatus> CoverChecks(JsonObject section, JsonObject? only = null)
    {
        var sheets = ProjectSharedData.SubtreeSheets(section).ToArray();
        var result = new List<CoverStatus>();
        foreach (var sheet in sheets.Where(s => (only is null || ReferenceEquals(s, only)) && s.S("modulo_id") is "str_palo" or PaloOrizzontale.Module))
        {
            string name = sheet.S("nome"); var fields = ProjectSharedData.Fields(sheet);
            var owner = sheet.Parent?.Parent as JsonObject ?? section;
            var materials = ProjectSharedData.Ancestors(owner).Reverse().Append(owner)
                .Select(s => s.Array("fogli").OfType<JsonObject>().Where(f => f.S("modulo_id") == "mat_calcestruzzo").ToArray())
                .FirstOrDefault(m => m.Length > 0) ?? [];
            if (materials.Length == 0) result.Add(new(name + ": minimo del copriferro non verificabile; aggiungere una scheda Materiali alla sezione o a un livello superiore.", null));
            foreach (var material in materials)
            {
                try
                {
                    var mf = ProjectSharedData.Fields(material);
                    if (!ProjectSharedData.Equal(fields["esposizione"].Value, mf["esposizione"].Value) || !ProjectSharedData.Equal(fields["CLS · fck [MPa]"].Value, mf["CLS · fck [MPa]"].Value))
                        throw new ArgumentException("classe CLS o esposizione diversa da " + material.S("nome") + "; riallineare i dati oppure verificare separatamente");
                    var durability = ProjectSharedData.ComparableFields(sheet, material).Where(p => p.Source.Key.StartsWith("Durabilità · ") && !ProjectSharedData.Equal(p.Source.Value, p.Target.Value)).ToArray();
                    if (durability.Length > 0) throw new ArgumentException("parametri diversi da " + material.S("nome") + ": " + string.Join(", ", durability.Select(p => ProjectReportPlan.Label(p.Source.Key))));
                    var state = material["dati"]!.AsObject();
                    var input = (JsonObject)sheet["dati"]![sheet.S("modulo_id") == "str_palo" ? "input" : "sezione"]!.DeepClone();
                    if (sheet.S("modulo_id") == PaloOrizzontale.Module)
                    { input["shape"] = "Circolare"; input["diameter_mm"] = sheet["dati"]!["generali"].D("diametro") * 1000; }
                    double diameter = new SezioneCA(input).Bars.Max(b => b.Diametro);
                    double required = Materiali.MaterialCover.Required(state, J.Number(fields["CLS · fck [MPa]"].Value)!.Value, diameter);
                    double adopted = J.Number(fields["cover_mm"].Value) ?? throw new ArgumentException("copriferro adottato non valido");
                    bool passed = adopted >= required;
                    result.Add(new($"{name}: copriferro adottato {adopted:0.##} mm · minimo da Materiali {required:0.##} mm — {(passed ? "RISPETTATO" : "NON RISPETTATO")} ({material.S("nome")}, Ø massimo effettivo delle barre {diameter:0.##} mm).", passed));
                }
                catch (ArgumentException ex) { result.Add(new(name + ": copriferro non verificabile — " + ex.Message + ".", null)); }
            }
        }
        return result;
    }

    private void RefreshSharedStatus()
    {
        sharedStatus.Visibility = Visibility.Collapsed;
        if (document.S("tipo") != "progetti" || currentSheet?.Parent?.Parent is not JsonObject section || editor is null) return;
        var (temporary, active) = ProjectSharedData.PreviewSection(section, currentSheet, editor.materials?.CaptureState() ?? editor.Data);
        var checks = CoverChecks(temporary, currentSheet.S("modulo_id") == "mat_calcestruzzo" ? null : active);
        var inheritedConflicts = ProjectSharedData.Differences(temporary).Where(d =>
            ReferenceEquals(d.Second, active) && !ReferenceEquals(d.First.Parent, active.Parent)).ToArray();
        var messages = checks.Select(c => c.Text).ToList();
        var properties = inheritedConflicts.Select(d => SharedFieldLabel(d.Key)).Distinct().ToArray();
        if (properties.Length > 0)
            messages.Insert(0, "⚠ Dati diversi dal riferimento superiore: " + string.Join(", ", properties.Take(5)) +
                (properties.Length > 5 ? $" e altre {properties.Length - 5} proprietà" : "") + ". Apri il confronto nella pagina del progetto per uniformare.");
        if (messages.Count == 0) return;
        sharedStatus.Text = string.Join("\n", messages);
        sharedStatus.ToolTip = string.Join("\n", properties.Concat(checks.Select(c => c.Text)));
        sharedStatus.Foreground = inheritedConflicts.Length == 0 && checks.All(c => c.Passed == true) ? Brushes.DarkGreen : Brushes.DarkOrange;
        sharedStatus.Margin = new Thickness(12, 5, 12, 5); sharedStatus.Visibility = Visibility.Visible;
    }
}
