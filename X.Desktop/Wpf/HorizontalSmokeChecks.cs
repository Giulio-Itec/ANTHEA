using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeHorizontal(string directory)
    {
        testing = true; Directory.CreateDirectory(directory);
        var data = PaloOrizzontale.Defaults(); data["stratigrafie"]![0]!.AsArray().Add(PaloOrizzontale.Layer());
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", PaloOrizzontale.Module), ("dati", data));
        dirty = false; currentSheet = null; ShowSheet(document); await editor!.CalculateAsync();
        if (editor.Result is null) throw new Exception("Palo orizzontale: calcolo WPF fallito");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "orizzontale.png"), Ui.Snapshot(this));
        await editor.VerifyHorizontal(directory);
        double width = Width, height = Height; Width = 1366; Height = 850;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "orizzontale_1366.png"), Ui.Snapshot(this)); Width = width; Height = height;
        Commit(); Archivio.Scrivi(Path.Combine(directory, "orizzontale.programma"), document);
        editor.ExportResult(Path.Combine(directory, "orizzontale.json"));
        editor.ExportReport(Path.Combine(directory, "orizzontale.docx"), "Palo orizzontale", []);
        File.WriteAllText(Path.Combine(directory, "orizzontale.csv"), PaloOrizzontale.Csv(editor.Result!));
        if (!JsonNode.DeepEquals(editor.Result, PaloOrizzontale.Calculate(editor.Data))) throw new Exception("WPF/Core orizzontale discordanti");
        dirty = false; LoadFile(Path.Combine(directory, "orizzontale.programma")); await editor.CalculateAsync();
        if (editor.Result is null) throw new Exception("Riapertura orizzontale fallita");
        File.WriteAllText(Path.Combine(directory, "orizzontale_smoke.txt"), "Layout 1600/1366, calcolo, invalidazione, momento, diagrammi, salvataggio/riapertura, JSON, CSV, DOCX: OK");
        dirty = false;
    }
}

internal sealed partial class SheetEditor
{
    internal Task VerifyHorizontal(string directory) => horizontal!.Smoke(directory);
}
