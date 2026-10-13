using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Scheduling;
namespace Anthea.Application.Concrete;

public sealed record ConcretePropertyPresentation(double Phi, double N, string Text);

/// <summary>Owns the section snapshot, reuses its native model and serializes property requests.
/// Only the current request may publish; disposal also invalidates work already in the engine.</summary>
public sealed class ConcretePropertySession : IDisposable
{
    private readonly JsonObject input, workspace;
    private readonly RecalculationScheduler scheduler = new();
    private CheckerSectionModel? model;

    public ConcretePropertySession(JsonObject input, JsonObject workspace)
    {
        this.input = (JsonObject)input.DeepClone();
        this.workspace = (JsonObject)workspace.DeepClone();
    }
    public Task<ScheduledCalculation<ConcretePropertyPresentation>> CalculateAsync(JsonObject options, CancellationToken cancellation = default)
    {
        var snapshot = (JsonObject)options.DeepClone();
        return scheduler.ScheduleAsync(token => Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            model ??= CheckerSection.PrepareModel(input, workspace);
            token.ThrowIfCancellationRequested();
            var properties = ConcreteSectionProperties.Calculate(model, input, workspace, snapshot);
            var text = new StringBuilder();
            foreach (var group in properties.Values.GroupBy(v => v.Group))
            {
                text.AppendLine(group.Key.ToUpperInvariant());
                foreach (var value in group) text.AppendLine($"{value.Name} = {EngineeringFormat.Number(value.Value)} {value.Unit}");
                text.AppendLine();
            }
            text.AppendLine("Sezione elastica non fessurata. n = Eacciaio(1 + φ)/Ecm, φ comune; Ep distinto per materiale. Queste opzioni non modificano le verifiche SLE del foglio.");
            return new ConcretePropertyPresentation(properties.Phi, properties.N, text.ToString());
        }, token), cancellation: cancellation);
    }
    public void Dispose() => scheduler.Dispose();
}
