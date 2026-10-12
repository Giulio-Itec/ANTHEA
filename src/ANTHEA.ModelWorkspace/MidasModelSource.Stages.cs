using System.Text.Json;
using GPC.Converter.CivilNx;

namespace ANTHEA.ModelWorkspace;

public sealed partial class MidasModelSource
{
    static async Task<IReadOnlyList<CivilNxResponse>> ReadStageTablesAsync(MidasReadOnlyClient client, ModelSnapshot geometry,
        MidasImportRequest request, IProgress<ModelImportProgress>? progress, CancellationToken token)
    {
        var selection = request.Stages!.Selection();
        var families = new List<(CivilNxResultTable Table, int[] Ids)>();
        if (request.Forces)
        {
            families.Add((CivilNxResultTable.BeamForce, geometry.Elements.Where(e => e.Type == "BEAM").Select(e => e.Id).ToArray()));
            families.Add((CivilNxResultTable.PlateForcePerUnitLength, geometry.Elements.Where(e => e.Type == "PLATE").Select(e => e.Id).ToArray()));
        }
        if (request.Displacements) families.Add((CivilNxResultTable.DisplacementGlobal, geometry.Nodes.Select(n => n.Id).ToArray()));
        var responses = new List<CivilNxResponse>(); long bytes = 0;
        int total = families.Sum(f => (f.Ids.Length + 1999) / 2000) * selection.Steps.Count * selection.SourceCases.Count;
        if (total == 0 || total > 10000) throw new InvalidDataException("Selezione stage vuota o troppo ampia: scegli meno stati.");
        foreach (var step in selection.Steps) foreach (var name in selection.SourceCases) foreach (var family in families)
        foreach (var chunk in family.Ids.Chunk(2000))
        {
            token.ThrowIfCancellationRequested();
            progress?.Report(new($"Stage · {step.Phase} · {step.Step} · {name} · {family.Table}", responses.Count, total));
            var table = new CivilNxTableRequest { Name = "ANTHEAStage", Table = family.Table, ElementIds = chunk,
                LoadCases = [name + "(CS)"], StageSteps = [step.RequestName], PlateNodes = true,
                DisplacementReference = family.Table == CivilNxResultTable.DisplacementGlobal ? selection.DisplacementReference : null };
            var result = await client.ResultsAsync(table, token).ConfigureAwait(false);
            string json = result.GetRawText(); bytes += System.Text.Encoding.UTF8.GetByteCount(json);
            if (bytes > ModelSnapshotFiles.MaximumPackageBytes) throw new InvalidDataException("Risultati stage troppo grandi: riduci fasi, passi o casi.");
            responses.Add(new("post/TABLE", json));
        }
        return responses;
    }
}
