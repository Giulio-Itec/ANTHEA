using System.Text.Json.Nodes;

namespace Anthea.Presentation.Modules;

/// <summary>
/// Requests of a sheet to the window that hosts it (proposal F5, sections 3.3 and 5.1), in place of the events of the
/// historic SheetEditor. In the current window MvvmSheetEditor turns each command into the ISheetEditor event that
/// MainWindow already handles: <see cref="OpenModuleCopy"/> → ModuleCopyRequested (also for the concrete section of a
/// wall, which MainWindow opens as a copy in "str_palo"), <see cref="ReplaceSoil"/> → SoilReplacementRequested,
/// <see cref="RequestReport"/> → ReportRequested; <see cref="SoilSources"/> reads ISheetEditor.SoilSources.
/// </summary>
public interface ISheetHost
{
    /// <summary>Opens a copy of <paramref name="data"/> in a sheet of <paramref name="module"/> named <paramref name="name"/>.</summary>
    void OpenModuleCopy(string module, JsonObject data, string name);

    /// <summary>Replaces the soil of this sheet with <paramref name="data"/> (data of the same module).</summary>
    void ReplaceSoil(JsonObject data);

    /// <summary>Other sheets of the project that can give or receive the soil; empty outside a project.</summary>
    IReadOnlyList<SoilSource> SoilSources();

    /// <summary>Asks the window to export the report of this sheet (the «Report Word» command of the window).</summary>
    void RequestReport();

    /// <summary>
    /// Closes one editing operation of the sheet as one step of the history of its document (organizzazione del modulo paratie, progetto del
    /// 16/11, contract R01 of the review G-208): the shell confirms the sheet as when it loses the focus (drafts of the fields, shared data,
    /// history), so that two commands give two steps and one command never gives more than one. A sheet calls it before a command, to close
    /// what was typed before, and after it. Without changes nothing is recorded. Hosts without a history of their own (the window of a single
    /// module, the checks) do nothing: additive member.
    /// </summary>
    void ConfirmEdit() { }
}

/// <summary>A sheet of the project that can give or receive the soil: label shown, module, data and identifier of the sheet.</summary>
public sealed record SoilSource(string Label, string Module, JsonObject Data, string Id);
