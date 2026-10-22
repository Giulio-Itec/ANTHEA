using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Application.Examples;

namespace Anthea.Presentation.Shell;

/// <summary>
/// Files of the documents of the shell (F5.13 a, proposal 3.3 and 3.5): open, save and the installed examples. The sessions never
/// touch the disk directly; the checks pass their own implementation.
/// </summary>
public interface IDocumentFiles
{
    /// <summary>Reads and validates a document (DocumentService.Open).</summary>
    JsonObject Open(string path);

    /// <summary>Validates and writes a document (DocumentService.Save).</summary>
    void Save(string path, JsonObject document);

    /// <summary>True when <paramref name="path"/> lies in the folder of the installed examples (MainWindow.IsInExamplesFolder).</summary>
    bool IsInExamples(string path);

    /// <summary>
    /// A working copy of the installed example at <paramref name="path"/>, checked against the catalogue (ExampleCatalog.OpenCopy);
    /// null when the path is not an example of the catalogue, and the file then opens as any other (MainWindow.LoadFile).
    /// </summary>
    JsonObject? OpenExampleCopy(string path);
}

/// <summary>The files of the documents on disk, through DocumentService and the catalogue of the examples, as MainWindow reads them.</summary>
/// <param name="examplesFolder">Folder of the installed examples; <see cref="Installed"/> uses the «Esempi» folder of the program.</param>
public sealed class DocumentFiles(string examplesFolder) : IDocumentFiles
{
    private readonly string examples = System.IO.Path.GetFullPath(examplesFolder ?? throw new ArgumentNullException(nameof(examplesFolder)));

    /// <summary>The files of the installed program: the examples in «Esempi» beside the executable, as MainWindow.InstalledExamples.</summary>
    public static DocumentFiles Installed() => new(System.IO.Path.Combine(AppContext.BaseDirectory, "Esempi"));

    public JsonObject Open(string path) => DocumentService.Open(path);

    public void Save(string path, JsonObject document) => DocumentService.Save(path, document);

    public bool IsInExamples(string path) => System.IO.Path.GetFullPath(path).StartsWith(examples + System.IO.Path.DirectorySeparatorChar,
        StringComparison.OrdinalIgnoreCase);

    public JsonObject? OpenExampleCopy(string path)
    {
        if (!IsInExamples(path)) return null;
        var catalog = ExampleCatalog.Load(examples);
        return catalog.FindFile(path) is { } example ? catalog.OpenCopy(example) : null;
    }
}
