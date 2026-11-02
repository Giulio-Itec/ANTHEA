using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ANTHEA.ModelWorkspace;
using Anthea.Application.Documents;
using Anthea.Calculations;
using X.Core;

/// <summary>
/// --archive-timing [--models 500x100,1000x100] [--runs N]: results archive beside the project (2/11/2026), before and after on the synthetic
/// MIDAS models of the timing of the combinations, with the generated definitions and three user combinations calculated (a sum, the envelopes
/// of the SLU and SLV rows). Before is the code path of 1.17.0, still in the program (the copy in the document); after is the archive.
/// Sizes of the files, preparation of the entry (off the thread of the interface), «Salva», opening of the project, first access to the
/// results, a second «Salva» after one more combination; the values read back must be bit-identical (SHA-256 of their JSON).
/// </summary>
static partial class Program
{
    static async Task<int> ResultArchiveTiming(string output, string[] args)
    {
        string Option(string name, string fallback) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        int runs = int.Parse(Option("--runs", "3"), CultureInfo.InvariantCulture);
        var markdown = new StringBuilder($"# Archivio dei risultati · {DateTime.Now:yyyy-MM-dd HH:mm}\n\n");
        markdown.AppendLine($"Build {(IsRelease() ? "Release" : "Debug")}, {Environment.ProcessorCount} processori; {runs} corse, mediana [minimo–massimo] in ms.").AppendLine();
        string folder = Path.Combine(output, "file"); Directory.CreateDirectory(folder);
        foreach (string size in Option("--models", "500x100,1000x100").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = size.Split('x');
            var source = ModelOfSize(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
            var model = AddUserCombinations(ModelCombinationGenerator.Apply(source.Snapshot, ModelCombinationGenerator.Generate(source.Snapshot, source.Natures)), source);
            var service = new GpcModelCombinationService();
            foreach (var id in new[] { "ANTHEA/utente-somma", "ANTHEA/utente-inviluppo-slu", "ANTHEA/utente-inviluppo-slv" })
                model = ModelCombinations.Attach(model, id, await service.CalculateAsync(model, id));
            var next = ModelCombinations.Attach(model, "ANTHEA/utente-annidata", await service.CalculateAsync(model, "ANTHEA/utente-annidata"));
            string values = ValuesDigest(model);
            var times = new TimingRecorder(); long inlineBytes = 0, documentBytes = 0, archiveBytes = 0, archiveAfter = 0;
            for (int run = 0; run < runs; run++)
            {
                // Before (1.17.0): the whole copy in the document.
                var (doc, section) = ArchiveDocument();
                var inline = await times.TimeAsync("prima · preparazione della voce (fuori dall'interfaccia)", () => Task.Run(() => ModelDocumentEntry.Prepare(model, long.MaxValue)));
                string id = ProjectModelStore.Set(section, inline);
                string before = Path.Combine(folder, $"prima-{source.Label}.programma");
                times.Time("prima · Salva", () => { DocumentService.Save(before, doc); return 0; });
                inlineBytes = new FileInfo(before).Length;
                var reopened = times.Time("prima · apertura del progetto", () => DocumentService.Open(before));
                var container = ArchiveSection(reopened);
                var read = times.Time("prima · primo accesso ai risultati (apertura del modello)", () => ProjectModelStore.Read(container, id)!);
                if (ValuesDigest(read) != values) throw new InvalidOperationException("Valori diversi dopo la riapertura (prima).");
                // After: the archive beside the project.
                (doc, section) = ArchiveDocument(); var archive = new ModelResultArchive(null);
                var entry = await times.TimeAsync("dopo · preparazione della voce (fuori dall'interfaccia)", () => Task.Run(() => ModelDocumentEntry.Prepare(model)));
                id = ProjectModelStore.Set(section, entry, null, archive);
                string after = Path.Combine(folder, $"dopo-{source.Label}.programma");
                if (File.Exists(ModelResultArchive.PathFor(after))) File.Delete(ModelResultArchive.PathFor(after));
                times.Time("dopo · Salva (archivio + documento)", () => archive.Save(after, doc, () => DocumentService.Save(after, doc)));
                documentBytes = new FileInfo(after).Length; archiveBytes = new FileInfo(ModelResultArchive.PathFor(after)).Length;
                var second = await Task.Run(() => ModelDocumentEntry.Prepare(next));
                ProjectModelStore.Set(section, second, id, archive);
                times.Time("dopo · secondo Salva (una combinazione in più)", () => archive.Save(after, doc, () => DocumentService.Save(after, doc)));
                archiveAfter = new FileInfo(ModelResultArchive.PathFor(after)).Length;
                reopened = times.Time("dopo · apertura del progetto", () => DocumentService.Open(after));
                container = ArchiveSection(reopened);
                var opened = new ModelResultArchive(after, reopened);
                read = times.Time("dopo · primo accesso ai risultati (apertura del modello)", () => ProjectModelStore.Read(container, id, opened)!);
                if (!entry.IsArchived || read.UnavailableResults.Count > 0 || ValuesDigest(read) != ValuesDigest(next) || read.Fingerprint() != next.Fingerprint())
                    throw new InvalidOperationException("Valori diversi dopo la riapertura (dopo).");
                Console.WriteLine($"{source.Label} corsa {run + 1}: prima {inlineBytes / 1e6:F1} MB, dopo {documentBytes / 1e6:F1} + {archiveBytes / 1e6:F1} MB");
            }
            markdown.AppendLine($"## {source.Label}: {model.Results.Length} insiemi di risultati ({model.Results.Count(r => r.Combination != null)} derivati)").AppendLine();
            markdown.AppendLine($"Impronta dei valori `{values}`, uguale prima e dopo la riapertura.").AppendLine();
            markdown.AppendLine($"File: prima {inlineBytes / 1e6:F1} MB; dopo documento {documentBytes / 1e6:F2} MB + archivio {archiveBytes / 1e6:F1} MB ({archiveAfter / 1e6:F1} MB con una combinazione in più).").AppendLine();
            markdown.AppendLine("| Fase | Mediana | Min | Max |").AppendLine("|---|---:|---:|---:|");
            foreach (string phase in times.Order)
            {
                var samples = times.Phases[phase].Order().ToArray();
                markdown.AppendLine($"| {phase} | {samples[samples.Length / 2]:F0} | {samples[0]:F0} | {samples[^1]:F0} |");
            }
            markdown.AppendLine();
            foreach (var file in Directory.EnumerateFiles(folder)) File.Delete(file);
        }
        File.WriteAllText(Path.Combine(output, "archivio.md"), markdown.ToString());
        Console.WriteLine(markdown);
        File.WriteAllText(Path.Combine(output, "esito.txt"), "PASS tempi dell'archivio dei risultati");
        return 0;
    }

    /// <summary>SHA-256 of the JSON of the result sets of a copy: the values, bit for bit, with their names, fields and governing states.</summary>
    static string ValuesDigest(ModelSnapshot model) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(model.Results)));
}
