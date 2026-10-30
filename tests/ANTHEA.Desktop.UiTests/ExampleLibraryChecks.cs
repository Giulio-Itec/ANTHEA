using System.IO;
using System.Windows.Controls;
using Anthea.Application.Modules;
using Anthea.Presentation.Examples;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell.Navigation;
using X.Desktop.Views.Examples;

namespace X.Desktop;

/// <summary>
/// ui/check-examples: the page «Esempi» and the copies of the installed examples. Since F5.13 g on the new shell, off screen (ShellSmokeBench of part e),
/// with the checks it had through MainWindow until 67eafba5 (MainWindow.CheckExamples): the page of the navigation in place of ShowExamples, the active
/// document of the shell in place of the fields path, dirty and editor of the window, Workspace.OpenFile (the opening from Start or Explorer, as
/// App.OnStartup passes it) in place of LoadFile.
/// </summary>
internal static class ExampleLibraryChecks
{
    internal static async Task Run(string output)
    {
        Directory.CreateDirectory(output);
        using var b = await ShellSmokeBench.OpenAsync(output);
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        b.Shell.Navigation.Select(ShellPage.Examples);
        await b.Idle();
        var view = ShellSmokeBench.Descendants<ExampleLibraryView>(b.Window).Single();
        var vm = (ExampleLibraryViewModel)view.DataContext;
        var modules = (ListBox)view.FindName("ModuleList");
        var examples = (ListBox)view.FindName("ExampleList");
        Check(modules.Items.Count == ModuleDirectory.All.Count &&
            vm.Groups.Select(g => g.Module).Order().SequenceEqual(ModuleDirectory.All.Select(m => m.Id).Order()), "Moduli del catalogo");
        foreach (var group in vm.Groups)
        {
            modules.SelectedItem = group;
            await b.Idle();
            Check(examples.Items.Count >= 2 && vm.Examples.All(e => e.Module == group.Module), "Filtro: " + group.Module);
        }
        modules.SelectedItem = vm.Groups.Single(g => g.Module == "str_palo");
        await b.Idle();
        Check(view.ActualWidth > 700 && examples.ActualHeight > 100, "Layout non disponibile");
        await b.Capture("catalogo.png");
        // Real command, then a direct file open as from Start/Explorer: both leave an unsaved copy.
        vm.SelectedGroup = vm.Groups.Single(g => g.Module == "mat_acciaio_armatura");
        string original = Path.Combine(AppContext.BaseDirectory, "Esempi", vm.SelectedExample!.Path);
        byte[] bytes = File.ReadAllBytes(original);
        vm.OpenCommand.Execute(null);
        await b.Idle();
        Check(b.Workspace.CurrentDocument is { Path: null, IsDirty: true } && b.Workspace.Active?.Module == "mat_acciaio_armatura", "Comando non apre una copia");
        var opened = b.Workspace.OpenFile(original);
        await b.Idle();
        Check(opened is { Path: null, IsDirty: true } && ReferenceEquals(b.Workspace.CurrentDocument, opened), "Doppio clic non apre una copia");
        Check(bytes.SequenceEqual(File.ReadAllBytes(original)), "Originale modificato");
        // The copies are never saved: the window closes without questions.
        b.Services.Confirmations.SaveChanges = () => SaveChangesAnswer.Discard;
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli WPF esempi (shell nuova)\n");
    }
}
