using System.IO;
using System.Text.Json.Nodes;
using Anthea.Application.Examples;
using Anthea.Presentation.Examples;
using X.Core;
using X.Desktop.Views.Examples;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private static ExampleCatalog InstalledExamples() => ExampleCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Esempi"));
    private static bool IsInExamplesFolder(string filename) => Path.GetFullPath(filename).StartsWith(
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Esempi")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private void ShowExamples()
    {
        Commit();
        var catalog = InstalledExamples();
        SelectNavigation("Esempi");
        dashboardBody.Content = new ExampleLibraryView
        {
            DataContext = new ExampleLibraryViewModel(catalog, entry => OpenExampleCopy(catalog.OpenCopy(entry)), services.Messages)
        };
    }

    private void OpenExampleCopy(JsonObject loaded)
    {
        if (!ConfirmDiscard()) return;
        ExitRevisionPreview();
        editor?.Dispose();
        editor = null;
        currentSheet = null;
        document = loaded;
        path = null;
        dirty = true;
        sheetContent.Content = null;
        if (document.S("tipo") == "calcolo") ShowSheet(document); else ShowProjects();
        RefreshTree();
        UpdateTitle();
    }
}
