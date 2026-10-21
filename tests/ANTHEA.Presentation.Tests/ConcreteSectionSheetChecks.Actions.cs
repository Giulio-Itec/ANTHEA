using System.Globalization;
using Anthea.Application.Concrete;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The steps of the actions (F5.10, sub-step S2) driven on <see cref="ConcreteActionsViewModel"/> as the historic trace drives the tables of the
/// panels and the window «Sollecitazioni della sezione» (tests/Desktop/ConcreteSectionHistoricState.cs), and the part «[azioni]» of the state.
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private static readonly string[] ActionTables = ["3D", "2D", "SLE", "SLE_FREQ", "SLE_QP", "taglio"];

    private sealed partial class Side
    {
        /// <summary>A step on a table of the actions, on the limit state of a domain panel, or on the window of the actions.</summary>
        private static bool IsActionStep(ConcreteSectionStep step) => step.Kind switch
        {
            ConcreteSectionStep.Cell or ConcreteSectionStep.Select or ConcreteSectionStep.RowCommand or ConcreteSectionStep.Paste => ActionTables.Contains(step.Target),
            ConcreteSectionStep.Open => step.Target == "sollecitazioni",
            _ => false
        };

        private async Task<(string? Message, bool Own)> ApplyActionAsync(ConcreteSectionStep step)
        {
            if (step.Kind == ConcreteSectionStep.Open) return (await OpenActionsAsync(step), true);
            var actions = Sheet.Actions.Table(step.Target!);
            var table = actions.Table;
            switch (step.Kind)
            {
                case ConcreteSectionStep.Cell:
                {
                    // The binding of the cell writes the row (shown order), then the table refreshes its view at the end of the edit.
                    if (step.Row >= table.Shown.Count) return (null, true);
                    var row = table.Shown[step.Row];
                    var column = table.Columns.FirstOrDefault(c => c.Key == step.Key);
                    if (table.IsReadOnly || column is null || column.IsReadOnly) return (null, true);
                    row[step.Key!] = column.Kind == ConcreteColumnKind.Bool ? step.Text == "true" : step.Text;
                    SynchronizationContext.Current!.Post(_ => { if (!Sheet.IsDisposed) table.Refresh(); }, null);
                    return (null, true);
                }
                case ConcreteSectionStep.Select:
                    if (step.Index < table.Shown.Count) table.SelectedIndex = step.Index;
                    return (null, true);
                case ConcreteSectionStep.RowCommand:
                {
                    if (step.Index >= 0 && step.Index < table.Shown.Count) table.SelectedIndex = step.Index;
                    var command = (actions.IsShear, step.Key) switch
                    {
                        (false, "+ Riga") or (true, "+ Combinazione") => actions.AddCommand,
                        (false, "Duplica") => actions.DuplicateCommand,
                        (false, "− Riga") or (true, "−") => actions.RemoveCommand,
                        _ => throw new InvalidOperationException("Pulsante della tabella non trovato: " + step.Key)
                    };
                    if (command.CanExecute(null)) command.Execute(null);
                    return (null, true);
                }
                case ConcreteSectionStep.Paste:
                {
                    // «Incolla» with the text of the step on the clipboard; a refused paste shows its warning, the message of the step.
                    var command = step.Key == "aggiungi" ? actions.AppendCommand : actions.PasteCommand;
                    if (!command.CanExecute(null)) return (null, true);
                    if (step.Index >= 0 && step.Index < table.Shown.Count) table.SelectedIndex = step.Index;
                    clipboard.SetText(step.Text!);
                    int shown = messages.Shown.Count;
                    command.Execute(null);
                    return (messages.Shown.Count > shown ? messages.Shown[^1].Title + ": " + messages.Shown[^1].Message : null, true);
                }
                default: throw new ArgumentException("Passo della tabella sconosciuto: " + step);
            }
        }

        // «Sollecitazioni…»: the window driven inside the dialog service, as the historic trace drives the modal window; its state after every step.
        private async Task<string> OpenActionsAsync(ConcreteSectionStep step)
        {
            var command = Sheet.Actions.EditCommand;
            if (!command.CanExecute(null)) return "finestra non aperta: comando non disponibile";
            var log = new List<string>();
            bool opened = false;
            dialogs.Next = dialog =>
            {
                opened = true;
                var editor = (SectionActionsViewModel)dialog;
                bool closed = false;
                void Closed(object? sender, EventArgs e) => closed = true;
                editor.CloseRequested += Closed;
                try
                {
                    log.Add("aperta · " + EditorState(editor));
                    foreach (var inner in step.Dialog ?? [])
                    {
                        if (closed) { log.Add("chiusa: passo " + inner + " non eseguito"); continue; }
                        EditorStep(editor, inner);
                        log.Add(inner + " · " + (closed ? "chiusa" : EditorState(editor)));
                    }
                    if (!closed) log.Add("chiusa dalla traccia");
                }
                finally { editor.CloseRequested -= Closed; }
                return closed;
            };
            try { await command.ExecuteAsync(null); }
            finally { dialogs.Next = null; }
            if (!opened) throw new InvalidOperationException("Finestra sollecitazioni non aperta.");
            return string.Join("\n", log);
        }

        private static void EditorStep(SectionActionsViewModel editor, ConcreteSectionStep step)
        {
            switch (step.Kind)
            {
                case ConcreteSectionStep.Button:
                {
                    var command = step.Key switch
                    {
                        "Applica" => editor.ApplyAndCloseCommand, "Annulla" => editor.CancelCommand, "+ Riga" => editor.AddCommand, "Duplica" => editor.DuplicateCommand,
                        "Elimina" => editor.RemoveCommand, "Vuoti a zero · riga selezionata" => editor.FillMissingWithZeroCommand,
                        _ => throw new ArgumentException("Pulsante della finestra sconosciuto: " + step.Key)
                    };
                    if (command.CanExecute(null)) command.Execute(null);
                    break;
                }
                case ConcreteSectionStep.Select:
                    if (step.Index < editor.Rows.Count) editor.Selected = editor.Rows[step.Index];
                    break;
                case ConcreteSectionStep.Cell:
                    // The two-way binding of the cell writes the property of the row.
                    if (step.Row < editor.Rows.Count) typeof(SectionActionRow).GetProperty(step.Key!)!.SetValue(editor.Rows[step.Row], step.Text);
                    break;
                default: throw new ArgumentException("Passo della finestra sconosciuto: " + step.Kind);
            }
        }

        /// <summary>What the window shows: rows, selection, status, origins, notice (shown with the origins), previous actions, result, rows.</summary>
        private static string EditorState(SectionActionsViewModel editor) =>
            "righe " + editor.Rows.Count.ToString(CultureInfo.InvariantCulture) + " · selezionata "
            + (editor.Selected is null ? -1 : editor.Rows.IndexOf(editor.Selected)).ToString(CultureInfo.InvariantCulture)
            + " · stato " + ConcreteSectionTrace.Quote(editor.Status) + " · origini " + ConcreteSectionTrace.YesNo(editor.HasOrigins)
            + " · avviso " + ConcreteSectionTrace.YesNo(editor.HasOrigins) + " · precedente " + ConcreteSectionTrace.YesNo(editor.Legacy)
            + " · risultato " + (editor.Result is null ? "-" : ConcreteSectionTrace.StableHash(editor.Result))
            + "\n" + string.Join("\n", editor.Rows.Select(r => "  " + string.Join(" | ", new[] { r.Id, r.Element, r.Name, r.State, r.N, r.Vx, r.Vy, r.T, r.Mx, r.My, r.Origin }
                .Select(ConcreteSectionTrace.Quote)) + (r.Visible ? "" : " · nascosta")));

        /// <summary>The part «[azioni]»: unified or historic actions, the file of the actions shown by the tables, the rows of every limit state
        /// and of the shear with every value (inputs and outputs).</summary>
        private void TraceActions(ConcreteSectionBlock block)
        {
            var actions = Sheet.Actions;
            // The tables of the historic actions show the file next to their commands; the unified ones show no file.
            block.Add("sollecitazioni uniche " + ConcreteSectionTrace.YesNo(SectionActionInputs.Unified(Sheet.Data)) + " · file "
                + ConcreteSectionTrace.Quote(actions.Unified ? "" : actions.ExcelPath));
            foreach (var (key, rows) in Sheet.ActionRowSets)
            {
                block.Add("azioni " + key + " · " + rows.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var row in rows) block.Add("  " + ConcreteSectionTrace.Canonical(row.Values));
            }
            block.Add("azioni taglio · " + Sheet.ShearRowCollection.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var row in Sheet.ShearRowCollection) block.Add("  " + ConcreteSectionTrace.Canonical(row.Values));
        }
    }
}
