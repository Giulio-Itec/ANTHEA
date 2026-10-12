using Anthea.Application.Modules;
using Anthea.Reports;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using X.Core;

if (args.Length == 0 || args is ["--help"] or ["help"])
{
    Console.WriteLine("ANTHEA · servizi senza interfaccia");
    Console.WriteLine("Uso: ANTHEA.Headless validate <documento> | inspect <documento> | calculate <foglio> <risultati.json>");
    Console.WriteLine("validate controlla formato, fogli e revisioni; inspect mostra il contenuto sintetico.");
    Console.WriteLine("L’apertura conserva i dati e le decisioni pendenti; non esegue calcoli o migrazioni dei fogli.");
    return 0;
}
if (!(args.Length == 2 && args[0] is "validate" or "inspect") && !(args.Length == 3 && args[0] == "calculate"))
{
    Console.Error.WriteLine("Comando non disponibile. Usare --help.");
    return 2;
}
try
{
    var document = DocumentService.Open(args[1]);
    if (args[0] == "calculate")
    {
        if (Path.GetFullPath(args[1]).Equals(Path.GetFullPath(args[2]), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Il file dei risultati deve essere distinto dal documento di ingresso.");
        if (document.S("tipo") != "calcolo") throw new ArgumentException("Il comando calculate richiede un foglio singolo.");
        var outcome = ModuleServices.Get(document.S("modulo_id")).Calculate(document["dati"]!.AsObject());
        DocumentFile.Write(args[2], System.Text.Encoding.UTF8.GetBytes(outcome.Values.ToJsonString(J.Options)));
        Console.WriteLine(outcome.HasError ? "Calcolo non disponibile: " + outcome.Values.S("errore") : "Risultati esportati. Consultare esiti e limiti delle singole verifiche.");
        return outcome.HasError ? 1 : 0;
    }
    if (args[0] == "validate") Console.WriteLine("PASS Documento e revisioni validi.");
    else
    {
        var sheets = document.S("tipo") == "calcolo" ? new[] { document } : document.Array("progetti")
            .OfType<JsonObject>().SelectMany(ProjectSharedData.SubtreeSheets).ToArray();
        Console.WriteLine(JsonSerializer.Serialize(new { type = document.S("tipo"), sheets = sheets.Length,
            modules = sheets.GroupBy(s => s.S("modulo_id")).Select(g => new { id = g.Key, count = g.Count() }) },
            new JsonSerializerOptions { WriteIndented = true }));
    }
    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
