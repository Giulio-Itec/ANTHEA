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
        dirty = false; currentSheet = null; ShowSheet(document); await editor!.WaitForHorizontalAutomatic();
        if (editor.Result is null) throw new Exception("Palo orizzontale: calcolo WPF fallito");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "orizzontale.png"), Ui.Snapshot(this));
        await editor.VerifyHorizontal(directory);
        double width = Width, height = Height;
        foreach (var size in new[] { (1600, 990), (1280, 720), (960, 640), (760, 480) })
        {
            Width = size.Item1; Height = size.Item2;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
            await editor.VerifyHorizontalLayout(directory, size.Item1.ToString());
        }
        Width = width; Height = height;
        var retained = editor; var retainedResult = editor.Result; string retainedData = editor.Data.ToJsonString();
        Commit(); ShowHome(); ResumeCalculation();
        if (!ReferenceEquals(editor, retained) || !ReferenceEquals(editor.Result, retainedResult) || editor.Data.ToJsonString() != retainedData)
            throw new Exception("Navigazione Home perde il modulo orizzontale");
        Commit(); Archivio.Scrivi(Path.Combine(directory, "orizzontale.programma"), document);
        editor.ExportResult(Path.Combine(directory, "orizzontale.json"));
        editor.ExportReport(Path.Combine(directory, "orizzontale.docx"), "Palo orizzontale", []);
        File.WriteAllText(Path.Combine(directory, "orizzontale.csv"), PaloOrizzontale.Csv(editor.Result!));
        if (!JsonNode.DeepEquals(editor.Result, PaloOrizzontale.Calculate(editor.Data))) throw new Exception("WPF/Core orizzontale discordanti");
        dirty = false; LoadFile(Path.Combine(directory, "orizzontale.programma")); await editor.WaitForHorizontalAutomatic();
        if (editor.Result is null) throw new Exception("Riapertura orizzontale fallita");
        File.WriteAllText(Path.Combine(directory, "orizzontale_smoke.txt"), "Layout 1600/1280/960/760, espansione, scorrimento, modifica/copia/eliminazione strati e stratigrafie, colori/esiti, Home, calcolo automatico all'apertura e alle modifiche, focus continuo, risultati obsoleti scartati, dati incompleti, Dispose, momento automatico, diagrammi, salvataggio/riapertura, JSON, CSV, DOCX: OK");
        dirty = false;
        await SmokeMicroHorizontal(Path.Combine(directory, "micropalo"));
    }

    private async Task SmokeMicroHorizontal(string directory)
    {
        Directory.CreateDirectory(directory);
        var data = MicropaloOrizzontale.Defaults(); data.Array("stratigrafie")[0]!.AsArray().Add(PaloOrizzontale.Layer());
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", MicropaloOrizzontale.Module), ("dati", data));
        dirty = false; currentSheet = null; ShowSheet(document); await editor!.WaitForHorizontalAutomatic();
        await editor.VerifyChs();
        foreach (var size in new[] { (1600, 990), (960, 640), (760, 480) }) {
            Width = size.Item1; Height = size.Item2; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
            await editor.VerifyHorizontalLayout(directory, size.Item1.ToString());
        }
        var retained = editor; Commit(); ShowHome(); ResumeCalculation();
        if (!ReferenceEquals(retained, editor)) throw new Exception("Home perde micropalo CHS");
        Archivio.Scrivi(Path.Combine(directory, "micropalo.programma"), document);
        editor.ExportReport(Path.Combine(directory, "micropalo.docx"), "Micropalo orizzontale CHS", []);
        dirty = false; LoadFile(Path.Combine(directory, "micropalo.programma")); await editor.WaitForHorizontalAutomatic();
        if (editor.Result?["sezione"].S("tipo") != "CHS") throw new Exception("Riapertura CHS fallita");
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "Micropalo CHS: layout, catalogo/manuale, calcolo, Home, salvataggio e relazione OK"); dirty = false;
    }
}

internal sealed partial class SheetEditor
{
    internal Task VerifyHorizontal(string directory) => horizontal!.Smoke(directory);
    internal Task VerifyHorizontalLayout(string directory, string size) => horizontal!.VerifyLayout(directory, size);
    internal Task WaitForHorizontalAutomatic() => horizontal!.WaitForAutomatic();
    internal Task VerifyChs() => horizontal!.VerifyChs();
}
