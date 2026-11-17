using System.IO;
using System.Text.Json.Nodes;
using System.Windows.Controls;
using Anthea.Application.Stratigraphy;
using Anthea.Presentation.Modules.Shared;
using Anthea.Reports;
using X.Core;

namespace X.Desktop;

internal static partial class StratigraphyViewChecks
{
    private static async Task EditingFeatures(string root, string output)
    {
        var services = new Services();
        var document = StratigraphyDocument.Create();
        document["description"] = "Ubicazione già archiviata";
        using var host = new Hosted(Editors(services).Open(Module, document));
        await host.Settle();
        var vm = host.Sheet.Editor;
        Check(vm.CompactGeneralFields.Select(f => f.Key).SequenceEqual(["name", "ground_elevation", "water_state", "water_depth"]), "Quattro campi generali compatti");
        Check(!Visual(host.View.GeneralForm).OfType<TextBlock>().Any(t => t.Text is "Parametri da usare nelle verifiche" or "Descrizione / ubicazione"), "Campi rimossi assenti dalla vista");
        var row = vm.TableRows[0];
        Check(row.Label == "A" && StratigraphyEditing.LayerLabel(25) == "Z" && StratigraphyEditing.LayerLabel(26) == "AA", "Lettere degli strati senza ripetizioni dopo Z");
        FieldBox(SelectedRow(host.View), "Peso naturale γ [kN/m³]").Text = "18,5";
        Check(row.Means.Get("gamma_sat") == "18,5" && row.Data["gamma_sat"]!.ToString() == "18,5", "Peso saturo collegato al naturale nei dati e nella cella");
        FieldBox(SelectedRow(host.View), "Peso naturale γ [kN/m³]").Text = "19";
        Check(row.Means.Get("gamma_sat") == "19", "Peso saturo segue le successive modifiche");
        FieldBox(SelectedRow(host.View), "Peso dell’unità di volume saturo γsat [kN/m³]").Text = "21";
        row.Means["gamma"].EditText = "20";
        Check(row.Means.Get("gamma_sat") == "21", "Peso saturo assegnato manualmente conservato");
        FieldBox(SelectedRow(host.View), "Peso dell’unità di volume saturo γsat [kN/m³]").Text = "";
        Check(row.Means.Get("gamma_sat") == "20", "Svuotare il saturo ripristina il collegamento");
        vm.AddLayerCommand.Execute(null); vm.DuplicateLayerCommand.Execute(null);
        await host.Settle();
        Check(vm.TableRows.Select(r => r.Label).SequenceEqual(["A", "B", "C"]) && vm.TableRows.Select(r => r.Color).Distinct().Count() == 3, "Nuovi strati e duplicati hanno lettere e colori distinti");
        var id = vm.TableRows[0].Data["id"]!.ToString();
        vm.MoveLayerDownCommand.Execute(null);
        Check(vm.TableRows[1].Data["id"]!.ToString() == id && vm.TableRows[1].Label == "B", "Riordino conserva il terreno e aggiorna le lettere");
        vm.RemoveLayerCommand.Execute(null);
        Check(vm.TableRows.Count == 2 && vm.TableRows[1].Label == "B", "Eliminazione aggiorna le lettere senza salti");

        vm.GeneralForm["name"].EditText = "Sondaggio originale";
        string original = StratigraphyProfiles.CopyProfile(vm.Data).ToJsonString();
        Click(Visual(host.View).OfType<Button>().Single(b => Equals(b.Content, "Duplica stratigrafia")));
        await host.Settle();
        Check(vm.Profiles.Count == 2 && host.View.ProfileTabs.SelectedIndex == 1 && vm.TableRows.Count == 2, "Duplicazione del profilo seleziona la nuova scheda con tutti gli strati");
        Check(vm.Data["id"]!.ToString() != vm.RootData["id"]!.ToString() && vm.TableRows[0].Data["id"]!.ToString() != StratigraphyDocument.Layers(vm.RootData)[0]!["id"]!.ToString(), "Duplicazione crea identità indipendenti");
        FieldBox(host.View.GeneralForm, "Nome della stratigrafia").Text = "Sondaggio copia";
        vm.TableRows[0].Means["gamma"].EditText = "22";
        Check(StratigraphyProfiles.CopyProfile(vm.RootData).ToJsonString() == original, "Modifiche della copia non alterano l'originale");
        vm.GeneralForm["water_state"].SelectedChoice = "Assegnata";
        vm.GeneralForm["water_depth"].EditText = "3";
        vm.SelectedProfileIndex = 0;
        Check(vm.GeneralForm.Get("water_state") == "Non nota", "Falda indipendente per ciascun profilo");
        vm.SelectedProfileIndex = 1;
        host.Editor.Commit();
        var reopened = JsonNode.Parse(host.Editor.Data.ToJsonString())!.AsObject();
        Check(StratigraphyProfiles.All(reopened).Count == 2 && reopened["description"]!.ToString() == "Ubicazione già archiviata", "Salvataggio conserva tutti i profili e i dati generali precedenti");
        using (var again = new Hosted(Editors(new Services()).Open(Module, reopened)))
        {
            await again.Settle();
            again.Sheet.Editor.SelectedProfileIndex = 1;
            again.Sheet.Editor.TableRows[0].Means["gamma"].EditText = "23";
            Check(again.Sheet.Editor.TableRows[0].Means.Get("gamma_sat") == "23", "Collegamento dei pesi conservato alla riapertura");
        }
        Check(SoilProfileTransfer.SurveyCount(Module, reopened) == 2, "Scambio del terreno vede tutti i profili");
        var packet = SoilProfileTransfer.Extract(Module, reopened, 1);
        Check(packet["stratigraphy"]!["name"]!.ToString() == "Sondaggio copia" && packet["stratigraphy"]![StratigraphyProfiles.Key] is null && packet["source_survey"]!.GetValue<int>() == 2, "Esportazione del solo profilo scelto");
        using (var transfer = new SoilTransferDialogViewModel(Module, reopened, [], services, services))
        {
            transfer.SelectedSurveyIndex = 1;
            Check(transfer.Surveys.SequenceEqual(["1 · Sondaggio originale", "2 · Sondaggio copia"]) && transfer.Profile!["source_survey"]!.GetValue<int>() == 2, "Dialogo terreno distingue i profili per nome e trasferisce quello scelto");
        }
        var complete = packet.DeepClone().AsObject();
        foreach (var layer in StratigraphyDocument.Layers(complete["stratigraphy"]!.AsObject()).OfType<JsonObject>())
        {
            layer["behavior"] = "Granulare";
            layer["gamma"] = 18; layer["gamma_sat"] = 20; layer["phi"] = 30;
        }
        Check(StratigraphyTransfer.ToLegacy(complete, "Medi")["source_survey"]!.GetValue<int>() == 2, "Conversione per i pali conserva la stratigrafia di provenienza");
        var replaced = SoilProfileTransfer.Apply(Module, reopened, packet, survey: 0);
        Check(StratigraphyProfiles.All(replaced).Count == 2 && StratigraphyProfiles.All(replaced)[1].ToJsonString() == StratigraphyProfiles.All(reopened)[1].ToJsonString(), "Importazione sostituisce solo il profilo di destinazione");
        StratigraphyProfiles.Remove(replaced, 0);
        Check(StratigraphyProfiles.All(replaced).Count == 1 && replaced["name"]!.ToString() == "Sondaggio copia" && replaced[StratigraphyProfiles.Key] is null, "Eliminazione del primo profilo promuove il successivo senza perdere i dati");
        string report = StratigraphyReport.Create("Prova profili", reopened).GetXml("word/document.xml").ToString();
        Check(report.Contains("Sondaggio originale") && report.Contains("Sondaggio copia"), "Report comprende tutti i profili");
        services.Answers.Enqueue(false); vm.RemoveProfileCommand.Execute(null);
        Check(vm.Profiles.Count == 2, "Eliminazione della stratigrafia annullabile");
        services.Answers.Enqueue(true); vm.RemoveProfileCommand.Execute(null);
        Check(vm.Profiles.Count == 1 && !vm.RemoveProfileCommand.CanExecute(null), "Conservata almeno una stratigrafia");
        vm.AddProfileCommand.Execute(null);
        Check(vm.Profiles.Count == 2 && vm.TableRows.Count == 1 && vm.TableRows[0].Means.Get("gamma") == "", "Nuova stratigrafia vuota indipendente");
        string beforeReadOnly = host.Editor.Data.ToJsonString();
        host.Editor.InspectRevision(); vm.SelectedProfileIndex = 0;
        vm.AddProfileCommand.Execute(null); vm.DuplicateProfileCommand.Execute(null); vm.TableRows[0].Means["gamma"].EditText = "99";
        Check(host.Editor.Data.ToJsonString() == beforeReadOnly && !vm.AddProfileCommand.CanExecute(null), "Sola lettura blocca profili e parametri anche cambiando scheda");
        File.WriteAllText(Path.Combine(output, "profili-riapertura.json"), reopened.ToJsonString());
    }
}
