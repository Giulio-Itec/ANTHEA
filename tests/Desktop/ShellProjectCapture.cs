using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Anthea.Application.Documents;
using Anthea.Presentation.Shell.Projects;
using X.Core;

namespace X.Desktop;

/// <summary>
/// --check-shell-capture &lt;cartella&gt; (UiTests, F5.13 e): the capture of a project driven through the new shell, compared with the same project driven
/// through MainWindow, as the module benches compare the historic and the MVVM capture (F5.13 g3: MainWindow is archived, its capture is the one recorded
/// in tests/fixtures/f5-shell/cattura-mainwindow). The same file is opened in each shell (a copy with the same name),
/// then the same steps: a sheet opened, the concrete changed and its shared data propagated, the diameter of the piles changed and propagated, a new revision,
/// a local difference, «Uniforma a questo» in the comparison, the archived revision shown read-only and the current one again, undo, redo, save. After each
/// step the state is written as canonical JSON: the working and the displayed document, the title of the window, «not saved», read-only, the revision shown,
/// the sheet with the focus and its data, the status line of the shared data; at the end the saved file. Identifiers and the dates of the revisions are
/// numbered or blanked in order of appearance, the same way on both sides.
/// Writes mainwindow/ and shell/ (each with esito.txt «PASS …», for tools/qa/f5_confronto_diretto.py), confronto.txt and esito.txt: PASS when the two captures
/// are equal byte for byte.
/// </summary>
internal static class ShellProjectCapture
{
    internal const string Flag = "--check-shell-capture";

    /// <summary>The steps of the scenario, in order: the names of the files of the capture.</summary>
    internal static readonly string[] Steps = ["apertura", "foglio-calcestruzzo", "calcestruzzo-condiviso", "pali-condivisi", "nuova-revisione", "differenza-locale",
        "uniforma", "revisione-archiviata", "versione-corrente", "annulla", "ripristina", "salvataggio"];

    internal const string SectionId = "cattura-spalla", ConcreteId = "cattura-cls", SectionSheetId = "cattura-ca", PilesId = "cattura-pali";

    /// <summary>The project of the capture: a work with an abutment, its concrete, the RC section of its piles and the piles.</summary>
    internal static JsonObject Project()
    {
        var document = ProjectDocuments.CreateArchive();
        var root = J.Obj(("id", "cattura-progetto"), ("nome", "Viadotto della cattura"), ("fogli", new JsonArray()), ("strutture", new JsonArray()));
        document.Array("progetti").Add(root);
        var section = J.Obj(("id", SectionId), ("nome", "Spalla A"), ("fogli", new JsonArray()), ("strutture", new JsonArray()));
        root.Array("strutture").Add(section);
        JsonObject Sheet(string id, string module, string name) => J.Obj(("id", id), ("nome", name), ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module)));
        section.Array("fogli").Add(Sheet(ConcreteId, "mat_calcestruzzo", "Calcestruzzo della spalla"));
        var rc = Sheet(SectionSheetId, "str_palo", "Sezione dei pali"); rc["dati"]!["input"]!["shape"] = "Circolare"; rc["dati"]!["input"]!["diameter_mm"] = "1200";
        section.Array("fogli").Add(rc);
        var piles = Sheet(PilesId, "geo_palo_verticale", "Pali della spalla"); piles["dati"]!["generali"]!["diametro"] = "1.2";
        section.Array("fogli").Add(piles);
        return document;
    }

    internal static async Task Run(string output)
    {
        Directory.CreateDirectory(output);
        string shellFile = Path.Combine(output, "file-shell", "progetto.programma");
        Directory.CreateDirectory(Path.GetDirectoryName(shellFile)!);
        DocumentService.Save(shellFile, Project());
        // F5.13 g3: MainWindow is archived; mainwindow/ is the copy of its capture recorded before the archiving (tests/fixtures/f5-shell/cattura-mainwindow),
        // so that ui/shell-capture-confronto compares the same folders as before.
        string historic = Path.Combine(output, "mainwindow"), shell = Path.Combine(output, "shell");
        Directory.CreateDirectory(historic);
        foreach (var file in Directory.GetFiles(ShellReferences.Capture)) File.Copy(file, Path.Combine(historic, Path.GetFileName(file)), true);
        await ShellCapture(shellFile, shell);
        var lines = new List<string> { "Cattura del progetto: valori registrati della finestra storica (tests/fixtures/f5-shell/cattura-mainwindow, copia in mainwindow/) contro la shell nuova (shell/)" };
        bool same = ShellReferences.SameFolders(historic, shell, lines);
        int files = lines.Count - 1;
        File.WriteAllLines(Path.Combine(output, "confronto.txt"), lines, new UTF8Encoding(false));
        if (!same) throw new InvalidOperationException("Catture del progetto diverse tra i valori registrati della finestra storica e la shell nuova: " + string.Join(", ", lines.Where(l => l.StartsWith("DIVERSO", StringComparison.Ordinal))));
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS cattura del progetto uguale tra i valori registrati della finestra storica e la shell nuova ({files} file)\n", new UTF8Encoding(false));
    }

    /// <summary>The state after a step, as both drivers read it.</summary>
    internal sealed record State(JsonObject Working, JsonObject Displayed, string Title, bool Dirty, bool ReadOnly, int? Revision, string? SheetId, JsonObject? SheetData, string Status);

    /// <summary>Writes the state of step <paramref name="index"/> into <paramref name="folder"/>, with the identifiers numbered and the dates blanked.</summary>
    internal static void Write(string folder, int index, State state)
    {
        Directory.CreateDirectory(folder);
        // From undo on, the sheet is left out on both sides: MainWindow closes it at every undo and redo and shows the overview, the shell keeps the tab open,
        // reloaded on the node with the same identifier (proposal 3.7, question SH5, approved at the checkpoint of 22/10). The documents are compared.
        if (index >= Array.IndexOf(Steps, "annulla")) state = state with { SheetId = null, SheetData = null, Status = "(foglio escluso: SH5, proposta 3.7)" };
        var value = J.Obj(("passo", Steps[index]), ("titolo", state.Title), ("non_salvato", state.Dirty), ("sola_lettura", state.ReadOnly), ("revisione_mostrata", state.Revision),
            ("foglio", state.SheetId), ("dati_del_foglio", state.SheetData?.DeepClone()), ("riga_di_stato", state.Status),
            ("documento_di_lavoro", state.Working.DeepClone()), ("documento_mostrato", ReferenceEquals(state.Working, state.Displayed) ? null : state.Displayed.DeepClone()));
        File.WriteAllText(Path.Combine(folder, $"{index + 1:00}-{Steps[index]}.json"), Normalize(value.ToJsonString(new JsonSerializerOptions { WriteIndented = true })), new UTF8Encoding(false));
    }

    /// <summary>The saved file, read back, and the outcome of the capture of one side.</summary>
    internal static void Finish(string folder, string file)
    {
        var saved = JsonNode.Parse(File.ReadAllText(file))!;
        File.WriteAllText(Path.Combine(folder, $"{Steps.Length + 1:00}-file-salvato.json"), Normalize(saved.ToJsonString(new JsonSerializerOptions { WriteIndented = true })), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(folder, "esito.txt"), $"PASS cattura del progetto: {Steps.Length} passi e il file salvato\n", new UTF8Encoding(false));
    }

    /// <summary>
    /// Identifiers numbered by first appearance, the same way on both sides: the GUIDs of the nodes (32 hexadecimal digits) and of the models of the RC
    /// section (with dashes: every save writes the model again with new ones), the SHA-256 that depend on them (the archive of the model, «dati_ref» of the
    /// revisions); timestamps and dates of the revisions blanked.
    /// </summary>
    private static string Normalize(string text)
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        string Number(Match m, string kind) => ids.TryGetValue(m.Value, out var id) ? id : ids[m.Value] = "<" + kind + " " + (ids.Count + 1) + ">";
        text = Regex.Replace(text, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", m => Number(m, "guid"));
        text = Regex.Replace(text, "[0-9A-F]{64}", m => Number(m, "sha"));
        text = Regex.Replace(text, "[0-9a-f]{32}", m => Number(m, "id"));
        text = Regex.Replace(text, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?", "<istante>");
        return Regex.Replace(text, @"\d{2}/\d{2}/\d{4}( \d{2}:\d{2})?", "<data>");
    }

    /// <summary>The scenario through the new shell: the tree, the sheets in their tabs, the revision bar, the comparison, the commands of the project bar.</summary>
    private static async Task ShellCapture(string file, string folder)
    {
        var b = await ShellSmokeBench.OpenAsync(folder);
        try
        {
            var shell = b.Shell; var workspace = b.Workspace;
            int step = 0;
            async Task Capture()
            {
                await b.Idle();
                var project = b.Project;
                var active = workspace.Active;
                Write(folder, step++, new State(project.WorkingDocument, project.Document, shell.Title, project.IsDirty, project.IsReadOnly, project.DisplayedRevision,
                    active?.Id, active?.ViewModel.Data, shell.SharedStatus.IsVisible ? shell.SharedStatus.Text : ""));
            }
            JsonObject Find(string id) => b.Project.Find(id)!;
            workspace.OpenFile(file);
            await Capture();
            workspace.OpenSheet(Find(ConcreteId));
            await Capture();
            var state = (JsonObject)b.Sheet.ViewModel.PreviewData.DeepClone(); state["classe"] = "C40/50"; b.Sheet.SetConcreteState(state);
            b.Services.Confirmations.SharedUpdate = () => true;
            workspace.CommitActive();
            await Capture();
            workspace.OpenSheet(Find(PilesId)); await b.Idle();
            b.Sheet.SetGeometry("1.25");
            workspace.CommitActive();
            b.Services.Confirmations.SharedUpdate = null;
            await Capture();
            b.Services.Dialog = d => { var dialog = (NewRevisionDialogViewModel)d; dialog.Note = "Prima emissione"; dialog.CreateCommand.Execute(null); return dialog.Result; };
            await b.Press(b.Button(shell.Revisions!.AddCommand));
            b.Services.Dialog = null;
            await Capture();
            b.Sheet.SetGeometry("1.4");
            b.Services.Confirmations.SharedUpdate = () => false;
            workspace.CommitActive();
            b.Services.Confirmations.SharedUpdate = null;
            await Capture();
            b.Services.Dialog = d =>
            {
                var coherence = (CoherenceViewModel)d;
                var align = coherence.Conflicts.SelectMany(c => c.Sheets).Single(s => s.Sheet.S("id") == PilesId && s.Key == "diameter_mm");
                align.AlignCommand.Execute(align);
                return coherence.Result;
            };
            await shell.CompareCommand.ExecuteAsync(Find(SectionId));
            b.Services.Dialog = null;
            await Capture();
            await b.Press(Revision(b, 0));
            await Capture();
            await b.Press(Revision(b, null));
            await Capture();
            shell.Commands.UndoCommand.Execute(null);
            await Capture();
            shell.Commands.RedoCommand.Execute(null);
            await Capture();
            shell.Commands.SaveCommand.Execute(null);
            await Capture();
            Finish(folder, file);
        }
        finally { b.Dispose(); }
    }

    private static Button Revision(ShellSmokeBench b, int? number) => ShellSmokeBench.Descendants<Button>(b.Window).Single(button => button.Name == "Revision"
        && button.DataContext is RevisionTab tab && (number is null ? tab.IsCurrent : !tab.IsCurrent && tab.Number == number));
}
