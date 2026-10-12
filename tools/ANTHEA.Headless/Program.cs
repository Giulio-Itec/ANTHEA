using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using X.Core;

if (args.Length == 0 || args is ["--help"] or ["help"])
{
    Console.WriteLine("ANTHEA · servizi senza interfaccia");
    Console.WriteLine("Uso: ANTHEA.Headless validate <documento> | inspect <documento>");
    Console.WriteLine("validate controlla formato, fogli e revisioni; inspect mostra il contenuto sintetico.");
    Console.WriteLine("L’apertura conserva i dati e le decisioni pendenti; non esegue calcoli o migrazioni dei fogli.");
    return 0;
}
if (args.Length != 2 || args[0] is not ("validate" or "inspect"))
{
    Console.Error.WriteLine("Comando non disponibile. Usare --help.");
    return 2;
}
try
{
    var document = DocumentService.Open(args[1]);
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
