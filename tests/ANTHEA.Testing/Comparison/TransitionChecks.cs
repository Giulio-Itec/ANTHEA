using System.Text.Json.Nodes;

namespace Anthea.Testing.Comparison;

/// <summary>
/// 'check-transitions': negative checks of the admitted transitions (decision D2 of 15/10/2026). A list built from a comparison admits
/// exactly that comparison; an unlisted difference, a listed transition that does not occur, a changed value or a duplicate entry fail.
/// </summary>
public static class TransitionChecks
{
    public static int Run()
    {
        int checks = 0; var errors = new List<string>();
        void Check(bool condition, string message) { checks++; if (!condition) errors.Add(message); }
        string root = Path.Combine(Path.GetTempPath(), "anthea-transizioni-" + Guid.NewGuid().ToString("N"));
        try
        {
            string a = Path.Combine(root, "a"), b = Path.Combine(root, "b");
            Directory.CreateDirectory(a); Directory.CreateDirectory(b);
            File.WriteAllText(Path.Combine(a, "x.json"), """{"valore":1.5,"testo":"uguale","elenco":[1],"norma":"NTC 2018"}""");
            File.WriteAllText(Path.Combine(b, "x.json"), """{"valore":2.5,"testo":"uguale","elenco":[1,2],"norma":"NTC 2018","nuova":true}""");
            File.WriteAllText(Path.Combine(a, "report.txt"), "Titolo\nNota con rimando di terzi\nFine");
            File.WriteAllText(Path.Combine(b, "report.txt"), "Titolo\nNota con rimando normativo\nRiga aggiunta\nFine");
            var tolerances = Tolerances.Load(null);

            var plain = new BaselineComparer(tolerances); var report = plain.Compare(a, b);
            var listed = report["differenze"]!.AsArray().OfType<JsonObject>().Select(d => new Transition(d["file"]!.GetValue<string>(),
                d["percorso"]!.GetValue<string>(), d["classe"]!.GetValue<string>(), d["a"]?.GetValue<string>(), d["b"]?.GetValue<string>(), "PROVA-1")).ToList();
            Check(plain.NotAdmitted == listed.Count && listed.Count >= 4, $"senza elenco: {plain.NotAdmitted} differenze non ammesse, {listed.Count} rilevate");

            int Run(IEnumerable<Transition> list) { var c = new BaselineComparer(tolerances); c.Admit(list); c.Compare(a, b); return c.NotAdmitted; }
            Check(Run(listed) == 0, "l'elenco completo non ammette esattamente il confronto");
            Check(Run(listed.Skip(1)) == 1, "una differenza non elencata non fa fallire il confronto");
            var changed = listed.Select((t, i) => i == 0 ? t with { B = (t.B ?? "") + "x" } : t).ToList();
            Check(Run(changed) == 2, "un valore dopo diverso da quello elencato viene ammesso");
            Check(Run(listed.Append(listed[0])) == 1, "una transizione elencata due volte e avvenuta una volta non fa fallire il confronto");
            Check(Run(listed.Append(listed[0] with { Path = "$.inesistente" })) == 1, "una transizione elencata e assente non fa fallire il confronto");
            Check(Run(listed.Select(t => t with { Class = "testo" }).Where(t => t.Path != "$.valore")) > 0, "la classe della differenza non conta");

            string file = Path.Combine(root, "senza-voce.json");
            File.WriteAllText(file, """{"voci":[{"file":"x.json","percorso":"$.valore","classe":"numero","a":"1.5","b":"2.5","voce":""}]}""");
            try { Transition.Load(file); Check(false, "transizione senza voce del registro accettata"); }
            catch (InvalidDataException) { Check(true, ""); }

            var historicCount = new Transition("esito.txt", "riga 1#2", "numero", "71", "75", "F4");
            var currentCount = historicCount with { B = "77", Entry = "F5" };
            var article = new Transition("wiki/new.md", "", "file-aggiunto", null, "wiki/new.md", "F5");
            var engineering = listed[0];
            var updated = Transition.WithEditorialUpdate([historicCount, engineering], [currentCount, article]);
            Check(updated.Contains(engineering) && updated.Contains(currentCount) && updated.Contains(article) && updated.Count == 3,
                "aggiornamento editoriale: conservare tutte le transizioni ingegneristiche");
            foreach (var invalid in new[] { article with { File = "sheets/result.json" }, article with { Class = "numero" },
                article with { Class = "file-rimosso" }, currentCount with { B = "78" } })
            {
                try { Transition.WithEditorialUpdate([historicCount, engineering], [currentCount, invalid]); Check(false, "aggiornamento editoriale non valido accettato"); }
                catch (InvalidDataException) { Check(true, ""); }
            }
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        foreach (string error in errors) Console.Error.WriteLine("FAIL " + error);
        Console.WriteLine(errors.Count == 0 ? $"PASS {checks} controlli delle transizioni ammesse" : $"FAIL {errors.Count} di {checks} controlli");
        return errors.Count;
    }
}
