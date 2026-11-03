using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using ANTHEA.ModelWorkspace;

namespace Anthea.Projects;

/// <summary>
/// The file of a project (docs/sviluppo/progetto-sistema.md, step 2a): the document («.programma») and, beside it, the archive of the results
/// of its models («.anthea-risultati», decision «Blocchi accanto» of 2/11/2026), opened and saved together in one place. Until 3/11/2026 the
/// order archive → document lived in the shell (WorkspaceSession.Save) and the result sets the archive could not find were not reported.
/// </summary>
public static class ProjectFile
{
    /// <summary>The project at <paramref name="path"/>, read and checked as DocumentService.Open does (a sheet that cannot be read is kept
    /// read-only with its problem, step 2b), with the archive of its results (nothing
    /// is read from the archive before a model opens).</summary>
    public static ProjectFileContent Open(string path)
    {
        var problems = new List<DocumentProblem>();
        var document = DocumentService.Open(path, problems);
        return new(document, new ModelResultArchive(path, document), problems);
    }

    /// <summary>
    /// «Salva» and «Salva con nome…»: the archive of the results beside <paramref name="path"/>, then the document through
    /// <paramref name="writeDocument"/> (DocumentService.Save when null), so that the document on disk never cites a block the archive does not
    /// have. The thread of the caller: since step 2c also a thread of the pool, on a document thawed from a <see cref="FrozenDocument"/>;
    /// <paramref name="progress"/> gets the share of the result sets written.
    /// </summary>
    /// <returns>What the user should know after the save: the result sets the archive could not find.</returns>
    public static ProjectSaveReport Save(string path, JsonObject document, ModelResultArchive results, Action<string, JsonObject>? writeDocument = null,
        IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(results);
        var write = writeDocument ?? DocumentService.Save;
        return new(results.Save(path, document, () => write(path, document), progress));
    }
}

/// <summary>A project read from its file: the document and the archive of the results of its models.</summary>
/// <param name="Problems">The sheets and revisions that could not be read (step 2b): kept as they were, read-only.</param>
public sealed record ProjectFileContent(JsonObject Document, ModelResultArchive Results, IReadOnlyList<DocumentProblem> Problems);

/// <summary>The outcome of a save of a project.</summary>
/// <param name="MissingBlocks">Result sets the document cites and the archive could not find (archive missing or damaged when the project was
/// opened): the document is saved, those results are not and their combinations must be calculated again.</param>
public sealed record ProjectSaveReport(int MissingBlocks)
{
    /// <summary>The warning for the user, null when nothing is missing.</summary>
    public string? Warning => MissingBlocks == 0 ? null :
        $"Progetto salvato, ma {MissingBlocks} insiemi di risultati dei modelli non erano nell'archivio dei risultati (archivio mancante o danneggiato all'apertura): " +
        "quei risultati non sono stati salvati. Le combinazioni interessate risultano da calcolare.";
}
