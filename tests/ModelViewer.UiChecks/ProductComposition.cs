using System.Reflection;
using System.Windows;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;

static partial class Program
{
    /// <summary>
    /// The composition root of the product (X.Desktop.Services.DesktopComposition, internal), as App.OnStartup creates it (F5.2):
    /// the checks open the window of the application and the sheets with its services and its editors. Dialogs without an owner window.
    /// </summary>
    static object ProductComposition()
    {
        var composition = typeof(X.Desktop.App).Assembly.GetType("X.Desktop.Services.DesktopComposition")!;
        return Activator.CreateInstance(composition, Flags, null, [(Func<Window?>)(() => null)], null)!;
    }

    /// <summary>The factory of the sheet editors of the composition root (internal X.Desktop.SheetEditorFactory).</summary>
    static object ProductSheetEditors(object composition) => composition.GetType().GetProperty("SheetEditors", Flags)!.GetValue(composition)!;

    /// <summary>
    /// F5.13 g3: the window of the application as App.StartShell creates it (MainWindow is archived): the shell from its composition root
    /// (X.Desktop.Services.ShellComposition, internal) with the services, the modules and the limiter of the product, the Wiki on the tab of the shell.
    /// Closing the window does not ask about unsaved documents (the checks never save).
    /// </summary>
    static (Window Window, ShellViewModel Shell) ProductShell()
    {
        var composition = ProductComposition();
        var type = composition.GetType();
        var shellComposition = typeof(X.Desktop.App).Assembly.GetType("X.Desktop.Services.ShellComposition")!;
        var product = (PresentationServices)type.GetProperty("Services", Flags)!.GetValue(composition)!;
        var services = (PresentationServices)shellComposition.GetMethod("WithShellWiki", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [product])!;
        services = new PresentationServices(services.Messages, new QuietClose(services.Confirmations), services.Files, services.Dialogs, services.Clipboard, services.Links, services.Wiki);
        var examples = (string)shellComposition.GetProperty("ExamplesFolder", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var parts = shellComposition.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
            [services, type.GetProperty("Modules", Flags)!.GetValue(composition), type.GetProperty("Limiter", Flags)!.GetValue(composition), examples, null, null])!;
        return ((Window)parts.GetType().GetProperty("Window")!.GetValue(parts)!, (ShellViewModel)parts.GetType().GetProperty("Shell")!.GetValue(parts)!);
    }

    /// <summary>The questions of the product, except the one on closing the window: the documents of the checks close without saving.</summary>
    sealed class QuietClose(IConfirmationService inner) : IConfirmationService
    {
        public bool ConfirmSharedUpdate(SharedUpdatePrompt prompt) => inner.ConfirmSharedUpdate(prompt);
        public bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes) => inner.ConfirmProjectMove(destination, changes);
        public bool ConfirmClose(Func<bool> saveOrDiscard) => true;
        public SaveChangesAnswer AskSaveChanges() => SaveChangesAnswer.Discard;
        public bool ConfirmDelete(string title, string question) => inner.ConfirmDelete(title, question);
    }
}
