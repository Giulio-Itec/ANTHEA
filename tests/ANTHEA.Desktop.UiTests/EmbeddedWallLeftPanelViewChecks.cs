using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.EmbeddedWalls;

namespace X.Desktop;

/// <summary>
/// Organizzazione del modulo paratie (progetto del 16/11), step 4, off screen, on a calculated sheet with two cases: the box Caso above the
/// panel chooses input and results together (D3), the tree shows the case chosen, the tab Analisi lists cases and phases with the state of
/// the calculation and brings the drawing to the phase. Captures pannello-caso and pannello-analisi; neither data nor results change.
/// </summary>
internal static partial class EmbeddedWallViewChecks
{
    private static async Task CheckLeftPanel(FrameworkElement view, EmbeddedWallSheetViewModel sheet,
        Func<Task> settle, Action<bool, string> check, Action<string> capture)
    {
        string before = sheet.Data.ToJsonString(); var result = sheet.Current;
        var box = Visual(view).OfType<ComboBox>().Single(c => c.Name == "CaseSelector");
        var panel = Visual(view).OfType<TabControl>().Single(t => t.Name == "LeftPanelTabs");
        check(box.Items.Count == 2 && box.SelectedItem as string == sheet.InputCase && sheet.SelectedResultCase?.CaseId == sheet.InputCase,
            "Passo 4: la casella del caso non guida insieme anteprima e risultati.");
        box.SelectedItem = "C2"; await settle();
        check(sheet.InputCase == "C2" && sheet.SelectedResultCase?.CaseId == "C2" && sheet.WorkTree.Single(n => n.Title.StartsWith("Fasi")).Children.All(n => n.CaseId == "C2"),
            "Passo 4: il caso scelto non arriva ai risultati o all'albero.");
        capture("pannello-caso");
        panel.SelectedIndex = 1; await settle();
        var list = Visual(view).OfType<ListBox>().Single(l => l.Name == "AnalysisList");
        check(list.Items.Count == sheet.AnalysisEntries.Count && sheet.AnalysisEntries.Count(a => a.IsCase) == 2
            && sheet.AnalysisEntries.Any(a => a.CaseId == "C1" && a.Stage is not null), "Passo 4: la scheda Analisi non elenca casi e fasi.");
        capture("pannello-analisi");
        var first = sheet.AnalysisEntries.First(a => a.CaseId == "C1" && a.Stage is not null);
        list.SelectedItem = first; await settle();
        check(sheet.InputCase == "C1" && sheet.InputStage?["id"] == first.Stage && sheet.SelectedStage?.Id == first.Stage
            && sheet.Documents.Active == EmbeddedWallDocuments.Section, "Passo 4: la riga dell'analisi non porta il disegno e i risultati alla fase.");
        panel.SelectedIndex = 0; await settle();
        check(sheet.Data.ToJsonString() == before && ReferenceEquals(sheet.Current, result), "Passo 4: il pannello modifica i dati o i risultati.");
    }
}
