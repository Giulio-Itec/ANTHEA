using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The part «[taglio]» of the state (F5.10, sub-step S5) written from <see cref="ConcreteShearViewModel"/> in the format of the historic trace
/// (tests/Desktop/ConcreteSectionHistoricState.cs, TraceShear): the forms of the shear and of the torsion, the four forms of the links shown
/// read-only (tab 05, then tab 06), the note of the automatic parameters, the table with its commands, the detail, the summary, the summary of
/// the checks and the section drawn. The fields go through <see cref="Side.Form"/>, the table through the steps of the actions (S2).
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private sealed partial class Side
    {
        private void TraceShear(ConcreteSectionBlock block)
        {
            var shear = Sheet.Shear;
            block.Form(FormState("taglio", shear.Form)); block.Form(FormState("torsione", shear.Torsion));
            var stirrups = new[] { shear.Stirrups.Values, shear.Stirrups.Scheme, Sheet.DetailingStirrups.Values, Sheet.DetailingStirrups.Scheme };
            for (int i = 0; i < stirrups.Length; i++) block.Form(FormState("staffe-" + (i + 2).ToString(System.Globalization.CultureInfo.InvariantCulture), stirrups[i]));
            block.Text("nota-automatici", shear.AutomaticNote);
            foreach (string line in TableState("taglio", shear.Table.Table).Lines()) block.Add(line);
            block.Add("taglio comandi " + string.Join(" ", ShearCommands(shear.Table)));
            block.Text("taglio dettaglio", shear.Detail);
            block.Text("taglio sintesi", shear.Summary);
            block.Add(Cards("taglio-peggiori", shear.Worst).Line());
            block.Add("taglio sezione " + Viewport(shear.Drawing));
        }

        // The buttons of the panel of the table, in the order of the historic panel (the bar of the table, then «×» of the filter bar, which the
        // read-only revision leaves enabled); «(spento)» when the command is not available.
        private static IEnumerable<string> ShearCommands(ConcreteActionTableViewModel table)
        {
            static string Button(string text, bool enabled) => text + (enabled ? "" : "(spento)");
            if (table.Unified) yield return Button("Gestisci sollecitazioni…", table.ImportCommand.CanExecute(null));
            else
            {
                yield return Button("+ Combinazione", table.AddCommand.CanExecute(null));
                yield return Button("−", table.RemoveCommand.CanExecute(null));
                yield return Button("Copia", table.CopyCommand.CanExecute(null));
                yield return Button("Incolla", table.PasteCommand.CanExecute(null));
                yield return Button("Template Excel", table.TemplateCommand.CanExecute(null));
                yield return Button("Importa Excel", table.ImportCommand.CanExecute(null));
                yield return Button("Esporta Excel", table.ExportCommand.CanExecute(null));
                yield return Button("Sfoglia…", table.BrowseCommand.CanExecute(null));
            }
            yield return "×";
        }
    }
}
