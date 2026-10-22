using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Services;

namespace Anthea.Presentation.Shell;

/// <summary>
/// Opens the ViewModel of a sheet for the shell (F5.13 a, proposal 5.1): the new shell hosts only <see cref="ISheetViewModel"/>. The
/// composition root gives <see cref="RegistrySheetFactory"/>; the checks give their own. A module still on its historic view would need
/// an adapter of the desktop here (proposal 5.2, risk R5), not part of this step.
/// </summary>
public interface ISheetFactory
{
    /// <summary>A sheet of <paramref name="module"/> on a copy of <paramref name="data"/>, read-only for an archived revision.</summary>
    ISheetViewModel Open(string module, JsonObject data, bool readOnly, ISheetHost host);
}

/// <summary>
/// Sheets of the modules migrated to MVVM, from the <see cref="ModuleRegistry"/> of the composition root (as the MVVM branch of
/// SheetEditorFactory): a copy of the data in the <see cref="SheetContext"/>, the services and the limiter of the application, the
/// calculation service of the module (ModuleServices, or the one given by the checks), read-only as InspectRevision sets it.
/// </summary>
public sealed class RegistrySheetFactory : ISheetFactory
{
    private readonly ModuleRegistry modules;
    private readonly PresentationServices services;
    private readonly CalculationLimiter limiter;
    private readonly Func<string, IModuleService> calculations;

    /// <param name="calculations">Calculation service of a module; null uses ModuleServices.Get.</param>
    public RegistrySheetFactory(ModuleRegistry modules, PresentationServices services, CalculationLimiter limiter, Func<string, IModuleService>? calculations = null)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(limiter);
        this.modules = modules; this.services = services; this.limiter = limiter;
        this.calculations = calculations ?? ModuleServices.Get;
    }

    /// <exception cref="InvalidOperationException">The module has no MVVM implementation (ModuleRegistry.SheetModule).</exception>
    public ISheetViewModel Open(string module, JsonObject data, bool readOnly, ISheetHost host)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(host);
        var sheetModule = modules.SheetModule(module);
        var sheet = sheetModule.CreateSheet(new SheetContext((JsonObject)data.DeepClone(), readOnly, host, services, calculations(module), limiter));
        if (sheet.ModuleId != module)
        {
            sheet.Dispose();
            throw new InvalidOperationException($"Il modulo {module} ha creato un foglio del modulo {sheet.ModuleId}.");
        }
        if (readOnly) sheet.IsReadOnly = true;
        return sheet;
    }
}
