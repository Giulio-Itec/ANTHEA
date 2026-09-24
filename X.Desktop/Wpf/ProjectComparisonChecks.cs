using System.Text.Json.Nodes;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private void CheckComparisonOrder()
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static JsonObject Sheet(string id, string module) => new()
        { ["id"] = id, ["nome"] = id, ["modulo_id"] = module, ["dati"] = Archivio.NuovoFoglio(module) };
        var material = Sheet("mat", "mat_calcestruzzo");
        var rc = Sheet("ca", "str_palo");
        var horizontal = Sheet("po", PaloOrizzontale.Module);
        var vertical = Sheet("pv", "geo_palo_verticale");
        var microV = Sheet("mv", "geo_micropalo_verticale");
        var microH = Sheet("mo", MicropaloOrizzontale.Module);
        vertical["dati"]!["stratigrafie"] = new JsonArray(new JsonArray(Archivio.NuovoStratoPalo()));
        horizontal["dati"]!["stratigrafie"] = new JsonArray(new JsonArray(PaloOrizzontale.Layer()));
        horizontal["dati"]!["generali"]!["diametro"] = "1.4";
        JsonObject[] all = [material, rc, horizontal, vertical, microV, microH, Sheet("steel", RebarMaterial.Module)];
        for (int i = 0; i < all.Length; i++) for (int j = i + 1; j < all.Length; j++)
        {
            var forward = ProjectSharedData.ComparableFields(all[i], all[j]).Select(p => p.Source.Key).Order().ToArray();
            var backward = ProjectSharedData.ComparableFields(all[j], all[i]).Select(p => p.Source.Key).Order().ToArray();
            Check(forward.SequenceEqual(backward), "Campi confrontati dipendenti dalla direzione");
        }
        Check(ProjectSharedData.ComparableFields(rc, material).Any(p => p.Source.Key == "esposizione"), "Esposizione mancante esclusa dal confronto");
        Check(!ProjectSharedData.Common(rc, material).Any(p => p.Source.Key == "esposizione"), "Confronto ha abilitato un trasferimento non valido");
        Check(ProjectSharedData.ComparableFields(microV, microH).Any(p => p.Source.Key == "CHS · profilo_chs"), "Profilo CHS mancante escluso dal confronto");
        Check(!ProjectSharedData.Common(microV, microH).Any(p => p.Source.Key == "CHS · profilo_chs"), "Profilo vuoto trasferibile al catalogo");

        var section = new JsonObject { ["nome"] = "Ordine indipendente", ["fogli"] = new JsonArray(material, rc, horizontal, vertical) };
        var sheets = section["fogli"]!.AsArray();
        string Signature() => string.Join("\n", ProjectSharedData.Differences(section).Select(d => $"{d.First.S("id")}|{d.Second.S("id")}|{d.Key}|{d.Left}|{d.Right}"));
        var expected = Signature();
        var expectedWarnings = ProjectSharedData.Limitations(section).ToArray();
        var expectedCover = CoverChecks(section).Select(c => c.Text).Order().ToArray();
        var savedInputs = all.ToDictionary(s => s.S("id"), s => s["dati"]!.DeepClone());
        int orders = 0;
        void Permute(JsonObject[] ordered, int start)
        {
            if (start < ordered.Length)
            {
                for (int i = start; i < ordered.Length; i++)
                { (ordered[start], ordered[i]) = (ordered[i], ordered[start]); Permute(ordered, start + 1); (ordered[start], ordered[i]) = (ordered[i], ordered[start]); }
                return;
            }
            sheets.Clear(); foreach (var sheet in ordered) sheets.Add(sheet);
            Check(Signature() == expected, "Conflitti diversi dopo riordino dei fogli");
            Check(ProjectSharedData.Limitations(section).SequenceEqual(expectedWarnings), "Avvisi diversi dopo riordino");
            Check(CoverChecks(section).Select(c => c.Text).Order().SequenceEqual(expectedCover), "Avvisi copriferro diversi dopo riordino");
            var header = new StackPanel(); AddCoherenceBadge(header, section);
            Check(header.Children.OfType<Button>().Single().Content.ToString()!.StartsWith("⚠ Differenze tra fogli"), "Indicatore di conflitto dipende dall'ordine");
            orders++;
        }
        Permute([material, rc, horizontal, vertical], 0);
        Check(orders == 24, "Permutazioni non complete");
        foreach (var sheet in all) Check(JsonNode.DeepEquals(savedInputs[sheet.S("id")], sheet["dati"]), "Il confronto ha alterato gli input");

        var single = new JsonObject { ["fogli"] = new JsonArray(rc.DeepClone()) };
        var singleHeader = new StackPanel(); AddCoherenceBadge(singleHeader, single);
        Check(singleHeader.Children.OfType<Button>().Single().Content.ToString() == "⚠ 1 avviso", "Avviso copriferro assente per singolo foglio");
        var consistent = new JsonObject { ["fogli"] = new JsonArray(rc.DeepClone(), rc.DeepClone()) };
        var consistentHeader = new StackPanel(); AddCoherenceBadge(consistentHeader, consistent);
        Check(consistentHeader.Children.OfType<Button>().Single().Content.ToString()!.Contains("Dati comuni coerenti · ⚠"), "Dati coerenti nascondono gli avvisi");

        // Applying a valid reference resolves the same conflict in either order and keeps other fields.
        foreach (bool reverse in new[] { false, true })
        {
            var m = (JsonObject)material.DeepClone(); var c = (JsonObject)rc.DeepClone();
            var pair = new JsonObject { ["fogli"] = reverse ? new JsonArray(c, m) : new JsonArray(m, c) };
            var before = c["dati"]!["input"]!.DeepClone();
            ProjectSharedData.Apply(m, pair, new HashSet<string> { "Materiali" }, keys: new HashSet<string> { "esposizione" });
            Check(!ProjectSharedData.Differences(pair).Any(d => d.Key == "esposizione"), "Esposizione non risolta dopo riordino");
            Check(JsonNode.DeepEquals(before, c["dati"]!["input"]), "Uniformazione esposizione altera altri input");
        }
    }
}
