using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Application.Examples;
using Anthea.Application.Modules;
using Anthea.Presentation.Examples;
using Anthea.Presentation.Services;

var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var root = Directory.GetCurrentDirectory();
var source = Path.Combine(root, "supporto/esempi/distribuzione");
var catalog = ExampleCatalog.Load(source);
int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
void Reject(Action action, string message)
{
    try { action(); } catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or System.Text.Json.JsonException) { checks++; return; }
    throw new Exception("Accettato: " + message);
}
Check(catalog.Entries.Count >= 30, "Catalogo incompleto");
foreach (var module in ModuleDirectory.All)
    Check(catalog.Entries.Count(e => e.Module == module.Id) >= 2, "Mancano esempi: " + module.Id);
var results = new JsonArray();
var errors = new List<string>();
foreach (var example in catalog.Entries)
{
    Console.WriteLine(example.Path);
    try
    {
        var full = Path.Combine(source, example.Path);
        var before = File.ReadAllBytes(full);
        var document = catalog.OpenCopy(example);
        var second = catalog.OpenCopy(example);
        Check(!ReferenceEquals(document, second), "Copia non indipendente");
        document["titolo_esempio_modificato"] = "Copia di lavoro";
        Check(second["titolo_esempio_modificato"] is null, "Modifica condivisa");
        var saved = Path.Combine(output, example.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
        DocumentService.Save(saved, document);
        DocumentService.Open(saved);
        Check(before.SequenceEqual(File.ReadAllBytes(full)), "Originale alterato");
        if (document["tipo"]?.ToString() != "calcolo") continue;
        var module = ModuleServices.Get(example.Module);
        var data = module.Prepare(document["dati"]!.AsObject());
        if (example.Kind is "dati" or "geometria") continue;
        var result = module.Calculate(data);
        // Results are evidence of an executable example, not independent numerical expectations.
        results.Add(new JsonObject { ["example"] = example.Path, ["values"] = result.Values.DeepClone() });
        Check(!result.HasError, example.Path + ": " + result.Values["errore"] + " " + result.Values["errori_calcolo"]?.ToJsonString());
    }
    catch (Exception ex) { errors.Add(example.Path + ": " + ex.Message); Console.Error.WriteLine(errors[^1]); }
}
File.WriteAllText(Path.Combine(output, "calcoli.json"), results.ToJsonString(new() { WriteIndented = true }));
Check(errors.Count == 0, string.Join(Environment.NewLine, errors));
var messages = new Messages();
ExampleEntry? opened = null;
using var vm = new ExampleLibraryViewModel(catalog, e => opened = e, messages);
Check(vm.Groups.Count == ModuleDirectory.All.Count, "Gruppi UI incompleti");
foreach (var group in vm.Groups)
{
    vm.SelectedGroup = group;
    Check(vm.Examples.All(e => e.Module == group.Module), "Filtro modulo");
    vm.SelectedExample = vm.Examples.Last();
    vm.OpenCommand.Execute(null);
    Check(opened == vm.SelectedExample, "Comando copia");
}
vm.SelectedExample = null;
Check(!vm.OpenCommand.CanExecute(null), "Comando senza selezione");
var absent = ExampleCatalog.Load(Path.Combine(output, "assente"));
using var empty = new ExampleLibraryViewModel(absent, _ => throw new Exception(), messages);
Check(empty.Groups.Count == 0 && !empty.OpenCommand.CanExecute(null), "Componente assente");
var invalid = Path.Combine(output, "invalid");
Directory.CreateDirectory(invalid);
var originalCatalog = JsonNode.Parse(File.ReadAllText(Path.Combine(source, "catalogo.json")))!.AsObject();
var first = catalog.Entries[0];
Directory.CreateDirectory(Path.Combine(invalid, first.Module));
File.Copy(Path.Combine(source, first.Path), Path.Combine(invalid, first.Path), true);
void Catalog(Action<JsonObject> edit)
{
    var json = originalCatalog.DeepClone().AsObject();
    json["entries"] = new JsonArray(json["entries"]![0]!.DeepClone());
    edit(json);
    File.WriteAllText(Path.Combine(invalid, "catalogo.json"), json.ToJsonString());
}
Catalog(j => j["entries"]![0]!["path"] = "../fuori.anthea");
Reject(() => ExampleCatalog.Load(invalid), "percorso esterno");
Catalog(j => j["entries"]!.AsArray().Add(j["entries"]![0]!.DeepClone()));
Reject(() => ExampleCatalog.Load(invalid), "duplicato");
Catalog(j => j["version"] = 999);
Reject(() => ExampleCatalog.Load(invalid), "versione");
Catalog(j => j["entries"]![0]!["path"] = first.Module + "/mancante.anthea");
Reject(() => ExampleCatalog.Load(invalid), "file mancante");
Catalog(_ => { });
File.AppendAllText(Path.Combine(invalid, first.Path), " ");
var corrupt = ExampleCatalog.Load(invalid);
Reject(() => corrupt.OpenCopy(corrupt.Entries[0]), "hash alterato");
Check(messages.Failures == 0, "Errori ViewModel");
File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli, {catalog.Entries.Count} esempi, {vm.Groups.Count} moduli\n");
Console.WriteLine($"PASS {checks}");

sealed class Messages : IMessageService
{
    public int Failures { get; private set; }
    public void ReportFailure(string title, Exception error) { Failures++; throw error; }
    public void Inform(string title, string message) { }
    public void Warn(string title, string message) { }
}
