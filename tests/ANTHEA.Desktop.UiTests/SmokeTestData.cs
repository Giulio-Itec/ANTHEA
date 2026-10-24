using System.IO;
using System.Text.Json.Nodes;

namespace X.Desktop;

internal static class SmokeTestData
{
    internal static JsonArray LoadCases()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
            {
                var path = Path.Combine(directory.FullName, "supporto", "test", "casi_confronto.json");
                if (File.Exists(path)) return JsonNode.Parse(File.ReadAllText(path))!.AsArray();
            }
        }
        throw new FileNotFoundException("Dati di test non trovati: supporto/test/casi_confronto.json.");
    }
}
