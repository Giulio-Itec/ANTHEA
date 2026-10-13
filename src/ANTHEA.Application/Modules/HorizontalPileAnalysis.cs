using System.Text.Json.Nodes;
namespace Anthea.Application.Modules;

public sealed record HorizontalPileAnalysis(JsonObject Capacity, JsonObject? Section, string SectionError)
{
    public static HorizontalPileAnalysis Calculate(JsonObject source, CancellationToken cancellation = default)
    {
        var snapshot = (JsonObject)source.DeepClone();
        snapshot.Remove("vista_orizzontale");
        var module = snapshot.S("tipo_sezione") == "CHS" ? MicropaloOrizzontale.Module : PaloOrizzontale.Module;
        var capacity = ModuleServices.Get(module).Calculate(snapshot, cancellation).Values;
        JsonObject? section = capacity["sezione"] as JsonObject;
        string error = "";
        // A valid section can be shown even while the stratigraphy is still incomplete.
        if (section is null && snapshot["generali"].S("origine_momento") is "Sezione c.a." or "Sezione CHS")
        {
            cancellation.ThrowIfCancellationRequested();
            try { section = PaloOrizzontale.Section(snapshot); }
            catch (ArgumentException ex) { error = ex.Message; }
        }
        cancellation.ThrowIfCancellationRequested();
        return new(capacity, section, error);
    }
}
