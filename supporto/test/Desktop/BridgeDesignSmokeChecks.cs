using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeBridgeDesign(string directory)
    {
        testing = true; Directory.CreateDirectory(directory); WindowState = WindowState.Normal; Width = 1600; Height = 990;
        document = Archivio.Documento(BridgeConcept.Module); dirty = false; currentSheet = null; ShowSheet(document);
        var workspace = editor!.bridgeDesign ?? throw new Exception("Bridge Design non aperto.");
        async Task Settle() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        void Check(bool ok, string error) { if (!ok) throw new Exception(error); }
        await Settle();
        Check(workspace.Calculation is not null, "Risultato iniziale assente.");
        File.WriteAllBytes(Path.Combine(directory, "bridge_design_1600.png"), Ui.Snapshot(this));
        double cost = workspace.Calculation!.TotalCost, co2 = workspace.Calculation.Carbon;
        workspace.Pin(); workspace.Outputs.SelectedIndex = 3; await Settle();
        var price = workspace.Editors["rates/concrete_deck"]; price.Text = "480"; await Settle();
        Check(workspace.Calculation!.TotalCost > cost && workspace.Calculation.Carbon == co2, "Il prezzo non si propaga correttamente.");
        workspace.Undo(); await Settle();
        Check(workspace.Editors["rates/concrete_deck"].Text == "240", "Annulla non ripristina il controllo prezzi.");
        Check(Math.Abs(workspace.Calculation!.TotalCost - cost) < 1e-6, "Annulla non ripristina il calcolo.");
        workspace.Editors["input/length"].Text = ""; Check(workspace.Calculation is null && editor.Result is null, "Risultati obsoleti con input vuoto.");
        workspace.Undo(); Check(workspace.Calculation is not null, "Ripristino da input invalido fallito.");
        for (int tab = 0; tab < 4; tab++)
        {
            workspace.Inputs.SelectedIndex = tab; workspace.Outputs.SelectedIndex = 0; await Settle();
            File.WriteAllBytes(Path.Combine(directory, $"input_{tab}.png"), Ui.Snapshot(this));
        }
        for (int tab = 0; tab < 5; tab++)
        {
            workspace.Outputs.SelectedIndex = tab; workspace.Outputs.BringIntoView(); await Settle();
            File.WriteAllBytes(Path.Combine(directory, $"risultati_{tab}.png"), Ui.Snapshot(this));
        }
        workspace.Inputs.SelectedIndex = 2; await Settle();
        foreach (var family in BridgeConcept.Families)
        {
            var button = Ui.Descendants<Button>(workspace).Single(b => (string)b.GetValue(System.Windows.Automation.AutomationProperties.NameProperty) == family.Name);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            workspace.Editors["input/length"].Text = ((family.MinSpan + family.MaxSpan) / 2 * 4.6).ToString(System.Globalization.CultureInfo.InvariantCulture);
            workspace.Editors["input/height"].Text = "40";
            await Settle(); Check(workspace.Calculation?.Family.Id == family.Id, "Cambio famiglia fallito: " + family.Id);
            File.WriteAllBytes(Path.Combine(directory, family.Id + "_prospetto.png"), workspace.Drawing.Png(false));
            File.WriteAllBytes(Path.Combine(directory, family.Id + "_sezione.png"), workspace.Drawing.Png(true));
        }
        workspace.AutoSize(); await Settle(); Check(workspace.Calculation is not null, "Dimensioni automatiche fallite.");
        workspace.Randomize(); await Settle(); Check(workspace.Calculation is not null, "Ponte casuale non calcolabile."); workspace.Undo();
        workspace.Editors["input/pile_count"].Text = "1"; await Settle(); _ = workspace.Drawing.Png(); workspace.Undo();
        // Save through the shell to exercise normal archive integration, then reopen an independent editor.
        Commit(); string archive = Path.Combine(directory, "BridgeDesign.anthea"); Archivio.Scrivi(archive, document);
        var reopened = Archivio.Leggi(archive); using var restored = new SheetEditor(BridgeConcept.Module, reopened["dati"]!.AsObject());
        Check(restored.HasResults && restored.Data["alternative_a"] is JsonObject, "Archivio incompleto al riavvio.");
        File.WriteAllBytes(Path.Combine(directory, "BridgeDesign.docx"), workspace.BuildReport("Predimensionamento del ponte"));
        var section = J.Obj(("nome", "Opera di prova"), ("fogli", new JsonArray(J.Obj(("nome", "Bridge Design"), ("modulo_id", BridgeConcept.Module), ("dati", workspace.Data)))), ("strutture", new JsonArray()));
        int unavailable = await GenerateSectionReport(section, Path.Combine(directory, "Progetto.docx"), _ => { }, CancellationToken.None);
        Check(unavailable == 0, "Bridge Design non incluso nel report di progetto.");
        // Home keeps the same live editor, just like the other ANTHEA modules.
        ShowHome(); OpenModule(BridgeConcept.Module); Check(ReferenceEquals(editor!.bridgeDesign, workspace), "Navigazione Home perde la configurazione.");
        workspace.Inputs.SelectedIndex = 0; workspace.Outputs.SelectedIndex = 0;
        foreach (double width in new[] { 1366d, 960d, 780d })
        {
            Width = width; Height = width < 1000 ? 720 : 900; await Settle();
            var scroller = (ScrollViewer)workspace.Content; scroller.ScrollToTop(); await Settle();
            Check(scroller.ScrollableWidth < 1, "Scorrimento orizzontale esterno indesiderato.");
            File.WriteAllBytes(Path.Combine(directory, $"bridge_design_{width:0}.png"), Ui.Snapshot(this));
        }
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "PASS: catalogo, quattro schede input, otto famiglie, cinque schede risultati, listino, invalidazione, undo, A/B, auto, random, PNG, archivio, report Word singolo e di progetto, Home, layout 1600/1366/960/780.");
    }
}
