using System.Text.Json.Nodes;
namespace Anthea.Application.Modules;

public sealed record HorizontalPileAnalysis(JsonObject Capacity, JsonObject? Section, string SectionError)
{
    public static HorizontalPileAnalysis Calculate(JsonObject source, CancellationToken cancellation = default) => Calculate(source, null, cancellation);

    /// <summary>
    /// The capacity of the horizontal pile or micropile and, when the capacity does not give it, the section of the moment.
    /// </summary>
    /// <param name="capacity">Calculation service of the capacity; null uses the module service of the type of section (the
    /// historic view in production). A sheet passes the service of its context (F5.7), which the checks replace with a barrier.</param>
    public static HorizontalPileAnalysis Calculate(JsonObject source, IModuleService? capacity, CancellationToken cancellation = default)
    {
        var snapshot = (JsonObject)source.DeepClone();
        snapshot.Remove("vista_orizzontale");
        var module = snapshot.S("tipo_sezione") == "CHS" ? MicropaloOrizzontale.Module : PaloOrizzontale.Module;
        var values = (capacity ?? ModuleServices.Get(module)).Calculate(snapshot, cancellation).Values;
        JsonObject? section = values["sezione"] as JsonObject;
        string error = "";
        // A valid section can be shown even while the stratigraphy is still incomplete.
        if (section is null && snapshot["generali"].S("origine_momento") is "Sezione c.a." or "Sezione CHS")
        {
            cancellation.ThrowIfCancellationRequested();
            try { section = PaloOrizzontale.Section(snapshot); }
            catch (ArgumentException ex) { error = ex.Message; }
        }
        cancellation.ThrowIfCancellationRequested();
        return new(values, section, error);
    }
}
