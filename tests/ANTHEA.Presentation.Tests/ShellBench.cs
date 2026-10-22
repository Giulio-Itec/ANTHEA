using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The workspace of the shell checks (F5.13): scripted services, the probe module (tests/Shared/ProbeSheetModule.cs) registered for
/// real modules of the directory and opened through <see cref="RegistrySheetFactory"/>, the files of the documents with an examples
/// folder of the check; it records the closed sheets and the reloads.
/// </summary>
internal sealed class ShellBench
{
    internal static IReadOnlyList<string> Modules { get; } = ["mat_calcestruzzo", "str_palo", "geo_palo_verticale", "geo_stratigrafia"];

    internal ShellBench(ScriptedConfirmations? confirmations = null, ScriptedFileDialogs? dialogs = null, string? examples = null, ScriptedDialogs? windows = null)
    {
        Confirmations = confirmations ?? new ScriptedConfirmations();
        Dialogs = dialogs ?? new ScriptedFileDialogs();
        Windows = windows ?? new ScriptedDialogs();
        Services = Scripted.Services(Messages, Confirmations, Dialogs, Windows);
        var registry = new ModuleRegistry(Modules.Select(m => (ISheetModule)new ProbeSheetModule(ModuleDirectory.Get(m))));
        Factory = new RegistrySheetFactory(registry, Services, new CalculationLimiter(1), m => new EchoService(m));
        Workspace = new WorkspaceSession(Services, Factory, new DocumentFiles(examples ?? Path.Combine(Path.GetTempPath(), "anthea-esempi-assenti")));
        Workspace.SheetClosed += (_, e) => Closed.Add((e.Sheet, e.Reason));
        Workspace.SheetsReloaded += (_, e) => Reloads.Add(e);
    }

    internal ScriptedMessages Messages { get; } = new();
    internal ScriptedConfirmations Confirmations { get; }
    internal ScriptedFileDialogs Dialogs { get; }
    internal ScriptedDialogs Windows { get; }
    internal PresentationServices Services { get; }
    internal RegistrySheetFactory Factory { get; }
    internal WorkspaceSession Workspace { get; }
    internal List<(SheetSession Sheet, SheetCloseReason Reason)> Closed { get; } = [];
    internal List<SheetsReloadedEventArgs> Reloads { get; } = [];

    /// <summary>The probe ViewModel of an open sheet.</summary>
    internal static ProbeSheetViewModel Probe(SheetSession sheet) => (ProbeSheetViewModel)sheet.ViewModel;

    /// <summary>A calculation service that echoes the data, of the module it is asked for (the probe checks the module).</summary>
    internal sealed class EchoService(string module) : IModuleService
    {
        public string ModuleId => module;
        public JsonObject Prepare(JsonObject source) => (JsonObject)source.DeepClone();
        public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default) => new(module, source, new JsonObject { ["eco"] = true });
    }

    /// <summary>A host that does nothing, for sheets opened outside the workspace.</summary>
    internal sealed class NullHost : ISheetHost
    {
        public void OpenModuleCopy(string module, JsonObject data, string name) { }
        public void ReplaceSoil(JsonObject data) { }
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() { }
    }
}
