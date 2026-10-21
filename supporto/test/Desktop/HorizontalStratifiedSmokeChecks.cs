using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;
using Anthea.Application.Documents;

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
        HorizontalStratifiedExample.Write(directory, Result);
        HorizontalStratifiedExample.SaveAndReopen(directory, Data);
        Data["stratigrafie"] = backup; RebuildSurveys();
        ((ComboBox)model.Editors["metodo_calcolo"]).SelectedItem = method;
        advanced.IsExpanded = false; Changed(); await WaitForAutomatic();
        if (Result is null) throw new Exception("Ripristino metodo Broms fallito");
    }
}
