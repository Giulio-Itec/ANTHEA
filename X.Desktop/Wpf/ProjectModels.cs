using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Wpf;
using ANTHEA.ModelViewer.Presentation;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private readonly ContentControl modelReferenceNotice = new() { Visibility = Visibility.Collapsed };

    internal void ShowModelsHub()
    {
        Commit();
        SelectNavigation("Modelli");
        var panel = Ui.Stack(Ui.Text("Modelli di calcolo", 30, true),
            Ui.Text("Mesh, volumi e risultati, accanto ai fogli di verifica della stessa fase o sottofase.", 15, color: Ui.Muted));
        panel.Children.Add(Ui.Text("BOZZA · copie importate e riferimenti versionati", 12, true, Ui.Muted));
        if (document.S("tipo") != "progetti")
        {
            panel.Children.Add(Ui.Text("Crea o apri un archivio progetti per organizzare i modelli.", 16));
            panel.Children.Add(Ui.Bar(Ui.Button("Apri progetto…", () => Safe(Open)),
                Ui.Button("Nuovo archivio progetti", () => Safe(NewProjects), true)));
        }
        else
        {
            foreach (var root in document.Array("progetti").OfType<JsonObject>())
                foreach (var container in ProjectModelStore.Containers(root))
                {
                    var models = ProjectModelStore.Models(container).ToArray();
                    var commands = new WrapPanel();
                    foreach (var model in models)
                    {
                        string id = model.S("id");
                        commands.Children.Add(Ui.Button(model.S("nome"), () => Safe(() => ShowContainerModel(container, id))));
                    }
                    commands.Children.Add(Ui.Button("+ Modello", () => Safe(() => ShowContainerModel(container)), true));
                    panel.Children.Add(ProjectCard(ContainerPath(container), Ui.Text($"{models.Length} modelli · {container.Array("fogli").Count} fogli", color: Ui.Muted), commands));
                }
            panel.Children.Add(Ui.Button("Organizza progetti e sottofasi", () => Safe(ShowProjects)));
        }
        dashboardBody.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static string ContainerPath(JsonObject container)
    {
        var names = new List<string>();
        for (JsonObject? node = container; node != null; node = node.Parent?.Parent as JsonObject)
            if (node["nome"] != null) names.Add(node.S("nome"));
        names.Reverse(); return string.Join(" / ", names);
    }

    private void AddModelsOverview(StackPanel panel, JsonObject container)
    {
        var models = ProjectModelStore.Models(container).ToArray();
        var content = new StackPanel();
        content.Children.Add(Ui.Text($"{models.Length} modelli in questa sezione. I fogli possono riferirsi a una revisione del modello.", 13, color: Ui.Muted));
        foreach (var model in models)
        {
            string id = model.S("id");
            content.Children.Add(ProjectButton("▧  " + model.S("nome"), () => Safe(() => ShowContainerModel(container, id))));
        }
        if (!projectReadOnly) content.Children.Add(ProjectButton("+ Importa un modello", () => Safe(() => ShowContainerModel(container))));
        panel.Children.Add(ProjectCard("Modelli di calcolo", content));
    }

    private void AddModelTreeItems(TreeViewItem parent, JsonObject container)
    {
        foreach (var model in ProjectModelStore.Models(container))
        {
            string id = model.S("id");
            var button = ProjectButton("▧  " + model.S("nome"), () => Safe(() => ShowContainerModel(container, id)));
            button.ToolTip = "Apri mesh, volumi e risultati";
            parent.Items.Add(new TreeViewItem { Header = button, Tag = id, IsTabStop = false });
        }
    }

    internal void ShowContainerModel(JsonObject container, string? modelId = null)
    {
        // Read and validate before removing the currently displayed page.
        var snapshot = modelId == null ? null : ProjectModelStore.Read(container, modelId);
        Commit(); editor?.Dispose(); editor = null; currentSheet = null; sheetContent.Content = null;
        EnsureProjectWorkspace(); ArrangeProjectWorkspace(sheetOpen: true); SetProjectSheetTreeVisible(true);
        body.Content = null; projectContent.Content = null;
        var page = new DockPanel();
        var title = Ui.Text(modelId == null ? "Nuovo modello" : ProjectModelStore.Models(container).Single(m => m.S("id") == modelId).S("nome"), 20, true);
        var toolbar = Ui.Bar(ProjectButton("← Progetto", () => Safe(() => ShowProjectOverview(container))),
            ProjectButton("Tutti i modelli", () => Safe(ShowModelsHub)),
            ProjectButton("+ Modello", () => Safe(() => ShowContainerModel(container))),
            ProjectButton("Salva progetto", () => Safe(() => Save(false))));
        toolbar.Children[2].IsEnabled = !projectReadOnly;
        var top = Ui.Stack(toolbar, title, Ui.Text(ContainerPath(container) + (projectReadOnly ? " · sola lettura" : ""), 12, color: Ui.Muted));
        if (!projectReadOnly)
        {
            var name = new TextBox { Text = title.Text, MinWidth = 260, Margin = new Thickness(4) };
            top.Children.Add(Ui.Bar(name, ProjectButton("Rinomina", () => Safe(() =>
            {
                if (modelId == null) throw new InvalidOperationException("Importare prima il modello.");
                ProjectModelStore.Rename(container, modelId, name.Text); title.Text = name.Text.Trim(); MarkDirty(); RefreshTree(container);
            }))));
        }
        top.Margin = new Thickness(14, 8, 14, 8); DockPanel.SetDock(top, Dock.Top); page.Children.Add(top);
        var targets = ProjectModelStore.DescendantSheets(container).Select(s => new SheetTarget(s.S("id"), s.S("nome"),
            s[ProjectModelStore.Binding] == null ? "Non collegato" : ProjectModelStore.FindOwner(s) is { } owner ? ProjectModelStore.LinkStatus(owner, s) : "Riferimento da aggiornare")).ToArray();
        var viewer = new ModelViewerControl(snapshot, targets, projectReadOnly,
            imported =>
            {
                modelId = ProjectModelStore.Set(container, imported, modelId);
                title.Text = ProjectModelStore.Models(container).Single(m => m.S("id") == modelId).S("nome");
                MarkDirty(); RefreshTree(container);
            },
            (sheetId, element, result, component) =>
            {
                var sheet = ProjectModelStore.DescendantSheets(container).Single(s => s.S("id") == sheetId);
                ProjectModelStore.Link(container, sheet, modelId ?? throw new InvalidOperationException("Importare prima il modello."), element, result, component);
                MarkDirty();
            });
        page.Children.Add(viewer); projectContent.Content = page; body.Content = projectWorkspace;
        selectedProjectId = container.S("id"); RefreshTree(container); UpdateProjectRevisionBar(container);
    }

    private void RefreshModelReference(JsonObject sheet)
    {
        modelReferenceNotice.Content = null; modelReferenceNotice.Visibility = Visibility.Collapsed;
        if (sheet[ProjectModelStore.Binding] is not JsonObject link) return;
        var owner = ProjectModelStore.FindOwner(sheet);
        string status = owner == null ? "Riferimento da aggiornare: modello non disponibile in questa sezione" : ProjectModelStore.LinkStatus(owner, sheet);
        var row = Ui.Bar(Ui.Text($"{status} · elemento {link["elemento"]} · {link.S("caso")} · {link.S("componente")}", 13, true));
        if (owner != null) row.Children.Add(Ui.Button("Apri modello", () => Safe(() => ShowContainerModel(owner, link.S("modello_id")))));
        row.Margin = new Thickness(14, 5, 14, 5); modelReferenceNotice.Content = row; modelReferenceNotice.Visibility = Visibility.Visible;
    }
}
