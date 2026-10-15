// Spostato da X.Desktop/Wpf/ProjectSharing.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: metodo usato solo dalle prove
// WPF, compilato solo nella configurazione UiTests, fuori dall'installer.
using System.Text.Json.Nodes;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    // Da ProjectSharing.cs: ereditarietà dei dati di sezione su un foglio creato dalle prove.
    private static void InheritSectionData(JsonObject sheet, JsonObject section)
    {
        ProjectSharedData.InheritHierarchy(sheet, section);
    }
}
