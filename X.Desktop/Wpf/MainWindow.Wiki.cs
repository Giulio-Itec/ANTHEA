using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private WikiView? wiki;
    private Button wikiHelp = null!;
    internal void ShowContextualWikiTheory()
    {
        if (editor is null) return;
        var guide = WikiCatalog.ForModule(editor.Module);
        var target = guide?.Related.Select(WikiCatalog.Resolve).FirstOrDefault(a => a is not null && a.Type == "theory" && a.Modules.Contains(editor.Module)) ?? guide;
        if (target is not null) ShowWiki(target.Id);
    }
    internal void ShowWiki(string? uri = null)
    {
        Commit(); SelectNavigation("Wiki");
        wiki ??= new WikiView((module, example) => Safe(() => OpenWikiModule(module, example)), returnToWork: () => Safe(() =>
        { if (editor is not null) ResumeCalculation(); else if (document.S("tipo") == "progetti") ShowProjects(); else ShowHome(); }));
        // Unlike the calculation dashboard, Wiki owns a responsive scroll layout.
        dashboardViewport.Content = null;
        body.Content = dashboard;
        dashboard.Width = double.NaN; dashboard.Height = double.NaN;
        dashboardBody.Margin = new Thickness(20, 14, 20, 18);
        dashboardBody.Content = wiki;
        if (uri is not null) wiki.Navigate(uri);
    }
    private void OpenWikiModule(string module, string? example)
    {
        if (example is null) { OpenModule(module); return; }
        var data = WikiExamples.Create(module, example);
        ModuleCatalog.ValidateData(module, data);
        if (!ConfirmDiscard()) return;
        ExitRevisionPreview(); document = Archivio.Documento(module); document["dati"] = data;
        path = null; dirty = true; currentSheet = null; ShowSheet(document); RefreshTree(); UpdateTitle();
    }
    internal static class WikiExamples
    {
        internal static JsonObject Definition(string example)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(example, "^[a-z0-9-]+$")) throw new ArgumentException("Identificativo esempio non valido.");
            using var stream = WikiCatalog.Resource("examples." + example + ".json");
            return JsonNode.Parse(stream)!.AsObject();
        }
        internal static JsonObject Create(string module, string example)
        {
            var definition = Definition(example);
            if (definition.S("moduleId") != module) throw new ArgumentException("Esempio non compatibile con il modulo richiesto.");
            var data = ModuleCatalog.CreateData(module);
            void Apply(JsonObject target, JsonObject values)
            {
                foreach (var (key, value) in values)
                {
                    if (key != "combinazioni" && value is JsonObject child && target[key] is JsonObject existing) Apply(existing, child);
                    else target[key] = value?.DeepClone();
                }
            }
            // Replace combination families as a whole, avoiding residual factory load cases.
            Apply(data, definition["overrides"]!.AsObject());
            ModuleCatalog.ValidateData(module, data); return data;
        }
    }
}
