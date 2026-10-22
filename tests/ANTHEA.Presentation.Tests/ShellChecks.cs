namespace Anthea.Presentation.Tests;

/// <summary>F5.13 (a, b): the checks of the sessions and ViewModels of the new shell, in the order of the sub-steps (docs/refactoring/f5-13-shell.md).</summary>
internal static class ShellChecks
{
    internal static int Run(string output)
    {
        int checks = 0;
        checks += ShellDocumentChecks.Run(output);
        checks += ProjectSessionChecks.Run();
        checks += WorkspaceChecks.Run(output);
        checks += NavigationChecks.Run();
        checks += ProjectTreeChecks.Run();
        checks += RevisionBarChecks.Run();
        checks += CoherenceChecks.Run();
        checks += TabsChecks.Run();
        checks += CommandsChecks.Run(output);
        checks += ShellViewModelChecks.Run();
        // F5.13 c: the pieces of the shell that do not depend on the layout.
        checks += SheetExportsChecks.Run(output);
        checks += WindowTrackerChecks.Run();
        checks += ShellPartCChecks.Run(output);
        // F5.13 c, after the checkpoint of 22/10: a sheet of the tree in a new window or at full page.
        checks += ShellPlacementChecks.Run();
        checks += ShellFinalViewsChecks.Run();
        return checks;
    }
}
