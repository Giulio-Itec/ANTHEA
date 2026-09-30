using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

internal sealed partial class HorizontalWorkspace
{
    private async Task VerifyStratified(string directory)
    {
        var backup = Data.Array("stratigrafie").DeepClone();
        string method = model.Get("metodo_calcolo");
        var upper = PaloOrizzontale.Layer(); upper["tipologia"] = "Coesivo"; upper["spessore"] = 2;
        var lower = PaloOrizzontale.Layer(); lower["spessore"] = Data["generali"].D("lunghezza") - 2;
        Data["stratigrafie"] = new JsonArray(new JsonArray(upper, lower)); RebuildSurveys(); Changed();
        await WaitForAutomatic();
        if (Result is not null) throw new Exception("Il metodo Broms ha accettato implicitamente un profilo misto");
        advanced.IsExpanded = true;
        ((ComboBox)model.Editors["metodo_calcolo"]).SelectedItem = PaloOrizzontale.StratifiedMethod;
        await WaitForAutomatic();
        if (Result is null || Result.S("metodo_calcolo") != PaloOrizzontale.StratifiedMethod || !Result["sondaggi"]![0].B("misto"))
            throw new Exception("Selezione del metodo stratificato: " + status.Text);
        if (!warnings.Text.Contains("MODELLO STRATIFICATO SPERIMENTALE")) throw new Exception("Ipotesi del nuovo modello non esposte");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); Window.GetWindow(this).UpdateLayout();
        var selector = model.Editors["metodo_calcolo"];
        var bounds = selector.TransformToAncestor(cards[0]).TransformBounds(new Rect(0, 0, selector.ActualWidth, selector.ActualHeight));
        if (bounds.Width < 180 || bounds.Right > cards[0].ActualWidth - 8)
            throw new Exception("Selettore del metodo stratificato tagliato");
        File.WriteAllBytes(Path.Combine(directory, "stratificato-opzioni.png"), Ui.Snapshot(Window.GetWindow(this)));
        File.WriteAllText(Path.Combine(directory, "stratificato-ui.json"), Result.ToJsonString(J.Options));
        var diagnosticsTabs = ResultsTabs();
        var diagnosticsDialog = Ui.Dialog(this, "Stratificato · tensioni e reazioni", diagnosticsTabs, 1280, 900);
        diagnosticsDialog.Show(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        File.WriteAllBytes(Path.Combine(directory, "stratificato-terreno-ui.png"), Ui.Snapshot(diagnosticsDialog));
        var childTabs = Ui.Descendants<TabControl>(diagnosticsTabs).First(); childTabs.SelectedIndex = 1;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        File.WriteAllBytes(Path.Combine(directory, "stratificato-equilibrio-ui.png"), Ui.Snapshot(diagnosticsDialog));
        diagnosticsTabs.SelectedIndex = diagnosticsTabs.Items.Count - 2;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        File.WriteAllBytes(Path.Combine(directory, "stratificato-riferimenti-ui.png"), Ui.Snapshot(diagnosticsDialog)); diagnosticsDialog.Close();
        File.WriteAllBytes(Path.Combine(directory, "stratificato-relazione.docx"), ReportOrizzontale.Create("Palo stratificato esempio di lettura", Result, true, ReportFigures()));
        foreach (bool soil in new[] { true, false }) File.WriteAllBytes(Path.Combine(directory, soil ? "stratificato-terreno.png" : "stratificato-equilibrio.png"), new HorizontalDiagrams(Result, 0, soil).Png(1320, 880));
        // Three alternating layers and water above the granular layer: inspect
        // continuous vertical stress, discontinuous local limit and both closures.
        var example = (JsonObject)Result["input"]!.DeepClone();
        example["generali"]!["presenza_falda"] = true; example["generali"]!["profondita_falda"] = 1;
        example["generali"]!["origine_momento"] = "Manuale"; example["generali"]!["momento_resistente"] = 900;
        example["generali"]!["provenienza_momento"] = "Esempio illustrativo dei diagrammi";
        example["generali"]!["passo"] = .25;
        var c1 = PaloOrizzontale.Layer(); c1["tipologia"] = "Coesivo"; c1["spessore"] = 2; c1["coesione_non_drenata"] = 40;
        var sand = PaloOrizzontale.Layer(); sand["spessore"] = 3;
        var c2 = PaloOrizzontale.Layer(); c2["tipologia"] = "Coesivo"; c2["spessore"] = 5; c2["coesione_non_drenata"] = 65;
        example["generali"]!["lunghezza"] = 10;
        example["stratigrafie"] = new JsonArray(new JsonArray(c1, sand, c2));
        var illustrated = PaloOrizzontale.Calculate(example);
        if (illustrated.S("errore") != "") throw new Exception(illustrated.S("errore"));
        File.WriteAllText(Path.Combine(directory, "esempio-falda.json"), illustrated.ToJsonString(J.Options));
        var illustratedFigures = new List<ReportOrizzontale.Figure>();
        foreach (bool soil in new[] { true, false })
        {
            var drawing = new HorizontalDiagrams(illustrated, 0, soil);
            File.WriteAllBytes(Path.Combine(directory, soil ? "esempio-falda-terreno.png" : "esempio-falda-equilibrio.png"), drawing.Png(1320, 880));
            illustratedFigures.Add(new(1, soil ? "Tensioni del terreno stratificato con falda" : "Reazioni e sollecitazioni nel palo", drawing.Png(1000, 660), 1000d / 660));
        }
        byte[] exampleReport = ReportOrizzontale.Create("Palo in tre strati con falda", illustrated, true, illustratedFigures);
        File.WriteAllBytes(Path.Combine(directory, "esempio-falda.docx"), exampleReport);
        using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(exampleReport)))
        {
            if (zip.Entries.Count(e => e.FullName.StartsWith("word/media/")) != 2) throw new Exception("Diagrammi mancanti dalla relazione");
            using var sr = new StreamReader(zip.GetEntry("word/document.xml")!.Open()); string xml = sr.ReadToEnd();
            if (!xml.Contains("landscape") || !xml.Contains("nzgs.org") || !xml.Contains("FHWA")) throw new Exception("Riferimenti o impaginazione relazione incompleti");
        }
        string path = Path.Combine(directory, "stratificato.programma");
        var doc = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", PaloOrizzontale.Module), ("dati", Data));
        Archivio.Scrivi(path, doc);
        var reopened = Archivio.Leggi(path)["dati"]!.AsObject();
        if (reopened["generali"].S("metodo_calcolo") != PaloOrizzontale.StratifiedMethod || PaloOrizzontale.Calculate(reopened).S("errore") != "")
            throw new Exception("Metodo stratificato non conservato nel salvataggio");
        Data["stratigrafie"] = backup; RebuildSurveys();
        ((ComboBox)model.Editors["metodo_calcolo"]).SelectedItem = method;
        advanced.IsExpanded = false; Changed(); await WaitForAutomatic();
        if (Result is null) throw new Exception("Ripristino metodo Broms fallito");
    }
}
