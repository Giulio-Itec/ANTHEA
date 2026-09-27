using System.Text.Json.Nodes;
using X.Core;

internal static class ProjectAuditChecks
{
    public static void Run()
    {
        int count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
        void Reject(Action action, string message)
        { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception(message); }

        // Compatibility oracle: the previous clone-and-normalize comparison, retained only in tests.
        JsonNode? OldNormalize(JsonNode? value)
        {
            if (value is JsonObject obj)
            { var result = new JsonObject(); foreach (var (key, item) in obj.OrderBy(p => p.Key)) result[key] = OldNormalize(item); return result; }
            if (value is JsonArray array) return new JsonArray(array.Select(OldNormalize).ToArray());
            return J.Number(value) is double number ? JsonValue.Create(number) : value?.DeepClone();
        }
        string[] samples = ["null", "true", "false", "0", "-0.0", "1", "1.0", "\"1,0\"", "\" 1e0 \"", "\"NaN\"", "\"Infinity\"", "\"\"", "\"nome\"", "[]", "{}", "[1,2]", "[2,1]", "[\"1\",\"2,0\"]", "{\"a\":1,\"b\":[2,null]}", "{\"b\":[\"2,0\",null],\"a\":\"1\"}", "{\"a\":null}", "{\"b\":null}"];
        foreach (string a in samples) foreach (string b in samples)
        {
            var left = JsonNode.Parse(a); var right = JsonNode.Parse(b);
            // Signed zero is intentionally equal: it cannot be a physical project conflict.
            bool expected = J.Number(left) == 0 && J.Number(right) == 0 || JsonNode.DeepEquals(OldNormalize(left), OldNormalize(right));
            // .NET 8 DeepEquals also accepts different keys if their values are null.
            if (left is JsonObject lo && right is JsonObject ro && !lo.Select(p => p.Key).ToHashSet().SetEquals(ro.Select(p => p.Key))) expected = false;
            Check(J.Equivalent(left, right) == expected, "Confronto JSON cambia semantica: " + a + " / " + b);
            Check(left?.ToJsonString() == JsonNode.Parse(a)?.ToJsonString(), "Confronto modifica l’input");
        }

        var project = ProjectDocuments.AddProject(ProjectDocuments.CreateArchive());
        var first = ProjectDocuments.AddSheet(project, "str_palo");
        var second = ProjectDocuments.AddSheet(project, "str_palo");
        first["dati"]!["input"]!["materiale_acciaio_nome"] = "Nome A";
        second["dati"]!["input"]!["materiale_acciaio_nome"] = "Nome B";
        Check(!new ProjectComparison(project).Differences.Any(d => d.Key == "materiale_acciaio_nome"), "Nome descrittivo causa falso conflitto");
        second["dati"]!["input"]!["fyk_mpa"] = 500;
        var comparison = new ProjectComparison(project);
        Check(comparison.Differences.Any(d => d.Key == "fyk_mpa"), "Resistenza discordante non segnalata");
        Check(new ProjectReportPlan(project).Conflicts.Count == comparison.Differences.Count, "Report e confronto divergono");
        second["dati"]!["input"]!["fyk_mpa"] = first["dati"]!["input"]!["fyk_mpa"]!.DeepClone();
        Check(!new ProjectComparison(project).Differences.Any(d => d.Key == "fyk_mpa"), "Confronto obsoleto dopo modifica");
        Check(comparison.Differences.Any(d => d.Key == "fyk_mpa"), "Lo snapshot cambia dopo la sua creazione");
        foreach (var sheet in new[] { first, second }) sheet["dati"]!["input"]!["shape"] = "Circolare";
        Check(!ProjectSharedData.ActiveField(first, "Staffe · rami_x") && !ProjectSharedData.ActiveField(first, "Staffe · rami_y"), "Rami rettangolari attivi in sezione circolare");
        Check(ProjectSharedData.ActiveField(first, "circular_sides"), "Contorno nativo non confrontabile");
        first["dati"]!["input"]!["barre_manuali"] = new JsonArray();
        Check(!ProjectSharedData.ActiveField(first, "longitudinal_bar_count"), "Array manuale vuoto riattiva valori automatici dormienti");

        var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        var options = settings["sle"]!["SLE"]!.AsObject(); var session = new ConcreteAnalysisSession();
        var rows = new[] { J.Obj(("id", "S1"), ("N", -200), ("Mx", 50), ("My", 0)) };
        var baseline = session.Stress(input, settings, options, "SLE", rows)["S1"];
        Check(baseline.State is not null, "Tensioni non disponibili");
        options["asse_neutro"] = !options.B("asse_neutro");
        Check(ReferenceEquals(baseline.State, session.Stress(input, settings, options, "SLE", rows)["S1"].State), "Asse neutro ricalcola le tensioni");
        input["fck_mpa"] = 40;
        var different = session.Stress(input, settings, options, "SLE", rows)["S1"];
        Check(different.State is not null && !ReferenceEquals(baseline.State, different.State), "Materiale cambiato conserva tensioni obsolete");

        var pile = PaloOrizzontale.Defaults(); pile["generali"]!["azione_assiale"] = 500;
        string unchanged = pile.ToJsonString();
        var result = HorizontalConcreteSection.Calculate(pile);
        Check(pile.ToJsonString() == unchanged, "La resistenza del palo modifica gli input");
        Check(result.D("n_checker_kn") == -500 && Math.Abs(result.D("residuo_n_kn")) <= result.D("tolleranza_n_kn"), "N del palo: segno o equilibrio errato");
        Check(result.Array("direzioni").Count == 2 && result.D("momento_knm") > 0, "Resistenze nei due versi assenti");
        var steel = pile["sezione"]!.AsObject();
        steel["steel_fu_mpa"] = ""; steel["steel_eps_u"] = " ";
        Reject(() => HorizontalConcreteSection.Calculate(pile), "Dati esplicitamente incompleti sostituiti senza errore");
        steel.Remove("steel_fu_mpa"); steel.Remove("steel_eps_u");
        Check(HorizontalConcreteSection.Calculate(pile).D("momento_knm") > 0, "Compatibilità dei vecchi fogli senza campi opzionali persa");
        steel["steel_fu_mpa"] = "NaN";
        Reject(() => HorizontalConcreteSection.Calculate(pile), "fu non finito accettato");
        steel["steel_fu_mpa"] = 600; steel["steel_eps_u"] = 75;
        steel["steel_diagramma"] = "Elastoplastico";
        double plastic = HorizontalConcreteSection.Calculate(pile).D("momento_knm");
        steel["steel_diagramma"] = "Incrudente";
        Check(HorizontalConcreteSection.Calculate(pile).D("momento_knm") > plastic, "Il palo ignora il legame incrudente");
        steel["steel_diagramma"] = "sconosciuto";
        Reject(() => HorizontalConcreteSection.Calculate(pile), "Legame sconosciuto sostituito senza errore");
        steel["steel_diagramma"] = "Elastoplastico"; steel["longitudinal_bar_count"] = 15;
        Reject(() => HorizontalConcreteSection.Calculate(pile), "Armatura incompatibile con il modello di Broms accettata");
        Console.WriteLine($"Audit dati/progetti/materiali: {count} controlli superati.");
    }
}
