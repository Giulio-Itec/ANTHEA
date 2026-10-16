using System.Diagnostics.CodeAnalysis;
using Anthea.Application.Modules;

namespace Anthea.Presentation.Modules;

/// <summary>
/// The modules of the interface (proposal F5, section 5.1): one descriptor for every module of <see cref="ModuleDirectory"/>,
/// in its order, and the <see cref="ISheetModule"/> of the modules migrated to MVVM. It reads the directory and adds no module:
/// a sheet module must be a module of the directory, with the same <see cref="ModuleInfo"/>. Built by hand in the composition
/// root of the desktop (decision Q2) with the migrated modules; which implementation the window opens is decided by
/// ModuleImplementations in X.Desktop.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly Dictionary<string, IModuleDescriptor> descriptors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ISheetModule> sheets = new(StringComparer.Ordinal);

    /// <param name="sheetModules">Modules migrated to MVVM; empty while every module uses its historic view.</param>
    public ModuleRegistry(IEnumerable<ISheetModule> sheetModules)
    {
        ArgumentNullException.ThrowIfNull(sheetModules);
        foreach (var module in sheetModules)
        {
            if (module?.Info is not { } info) throw new ArgumentException("Modulo MVVM nullo o senza metadati.", nameof(sheetModules));
            var listed = ModuleDirectory.All.FirstOrDefault(m => m.Id == info.Id)
                ?? throw new ArgumentException("Modulo MVVM assente da ModuleDirectory: " + info.Id + ". Il registro non è un secondo catalogo.", nameof(sheetModules));
            if (listed != info) throw new ArgumentException("Metadati del modulo MVVM diversi da ModuleDirectory: " + info.Id, nameof(sheetModules));
            if (!sheets.TryAdd(info.Id, module)) throw new ArgumentException("Modulo MVVM registrato due volte: " + info.Id, nameof(sheetModules));
        }
        var all = new List<IModuleDescriptor>();
        foreach (var info in ModuleDirectory.All)
        {
            IModuleDescriptor descriptor = sheets.TryGetValue(info.Id, out var sheet) ? sheet : new HistoricModule(info);
            descriptors.Add(info.Id, descriptor); all.Add(descriptor);
        }
        All = all.AsReadOnly();
        SheetModuleIds = all.Where(d => d is ISheetModule).Select(d => d.Info.Id).ToArray().AsReadOnly();
    }

    /// <summary>Every module of <see cref="ModuleDirectory"/>, in its order: the migrated ones as their <see cref="ISheetModule"/>.</summary>
    public IReadOnlyList<IModuleDescriptor> All { get; }

    /// <summary>Identifiers of the modules with an MVVM implementation, in the order of the directory.</summary>
    public IReadOnlyList<string> SheetModuleIds { get; }

    /// <summary>Descriptor of <paramref name="id"/>; an unknown module fails as <see cref="ModuleDirectory.Get"/>.</summary>
    public IModuleDescriptor Get(string id) => descriptors.TryGetValue(id, out var descriptor) ? descriptor
        : throw new ArgumentException("Modulo del foglio non disponibile: " + id, nameof(id));

    /// <summary>The MVVM implementation of <paramref name="id"/>, when the module has one.</summary>
    public bool TryGetSheetModule(string id, [NotNullWhen(true)] out ISheetModule? module) => sheets.TryGetValue(id, out module);

    /// <summary>The MVVM implementation of <paramref name="id"/>; a module without one fails with its name.</summary>
    public ISheetModule SheetModule(string id) => sheets.TryGetValue(id, out var module) ? module
        : throw new InvalidOperationException("Nessuna implementazione MVVM per il modulo " + id + ": il modulo usa ancora la vista storica.");

    /// <summary>A module still opened by its historic view: identity and guide; persistence and reports not declared yet.</summary>
    private sealed class HistoricModule(ModuleInfo info) : IModuleDescriptor
    {
        public ModuleInfo Info { get; } = info;
        public ModuleHelp Help { get; } = ModuleHelp.For(info.Id);
        public ModulePersistence Persistence => ModulePersistence.Undeclared;
        public ModuleReportCapabilities Reports => ModuleReportCapabilities.Undeclared;
    }
}
