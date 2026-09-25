using System.Text.Json.Nodes;
using X.Core;

internal static class ConcreteDataChecks
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
        JsonObject Sheet(string name) => J.Obj(("id", name), ("nome", name), ("modulo_id", "str_palo"), ("dati", SezioneCA.DefaultData()));
        var a = Sheet("A"); var b = Sheet("B");
        var section = J.Obj(("nome", "Controllo dati"), ("fogli", new JsonArray()));
        section.Array("fogli").Add(a); section.Array("fogli").Add(b);
        JsonObject Input(JsonObject s) => s["dati"]!["input"]!.AsObject();
        var ai = Input(a); var bi = Input(b);
        foreach (var (key, value) in new[] { ("diameter_mm", "1900"), ("longitudinal_bar_count", "99"), ("flange_width_mm", "9000"), ("second_top_gap", "777"), ("inner_width_mm", "99") }) bi[key] = value;
        bi["second_top_enabled"] = false;
        Check(ProjectSharedData.Differences(section).Count == 0, "Dimensioni inattive o falso/assente producono conflitti");
        bi["width_mm"] = "610";
        Check(ProjectSharedData.Differences(section).Select(d => d.Key).SequenceEqual(["width_mm"]), "Differenza geometrica attiva non isolata");
        bi["width_mm"] = "600";
        ai["foro_presente"] = true; bi["foro_presente"] = true;
        ai["inner_width_mm"] = "100"; ai["inner_height_mm"] = "200"; bi["inner_height_mm"] = "200";
        Check(ProjectSharedData.Differences(section).Any(d => d.Key == "inner_width_mm"), "Dimensione del foro assente nel confronto");
        ProjectSharedData.Apply(a, section, ["Geometria"], b, ["inner_width_mm"]);
        Check(bi.D("inner_width_mm") == 99 && Input(b).D("inner_width_mm") == 100, "Uniformazione foro non atomica o non applicata");
        bi = Input(b);
        ai["staffe_presenti"] = "No"; bi["staffe_presenti"] = "No";
        bi["transverse_bar_diameter_mm"] = "888";
        Check(!ProjectSharedData.Differences(section).Any(d => d.Key.StartsWith("transverse_")), "Staffe assenti producono conflitti dimensionali");
        bi["staffe_presenti"] = "Sì";
        Check(ProjectSharedData.Differences(section).Any(d => d.Key == "staffe_presenti"), "Presenza staffe non confrontata");
        bi["staffe_presenti"] = "No";
        ai["barre_manuali"] = new JsonArray(J.Obj(("x", "-100"), ("y", "-200"), ("phi", "20")), J.Obj(("x", "100"), ("y", "200"), ("phi", "20")));
        bi["barre_manuali"] = ai["barre_manuali"]!.DeepClone();
        ai["top_bar_count"] = "inattivo"; bi["top_bar_count"] = "2";
        ai["second_top_enabled"] = true; ai["second_top_gap"] = "inattivo";
        Check(!ProjectSharedData.Differences(section).Any(d => d.Key.StartsWith("top_bar_") || d.Key.StartsWith("second_")), "Wizard considerato con barre manuali");
        Check(new SezioneCA(ai).Bars.Count == 2, "Barre manuali bloccate da vecchi parametri automatici");
        var before = section.ToJsonString(); var plan = new ProjectReportPlan(section);
        Check(!plan.Fields[a].ContainsKey("top_bar_count") && !plan.Fields[a].ContainsKey("transverse_bar_diameter_mm"), "Report espone parametri inattivi");
        Check(section.ToJsonString() == before, "Confronto e report mutano i dati");
        foreach (string shape in new[] { "Circolare", "Rettangolare", "A T" })
        {
            var sample = SezioneCA.DefaultInput(); sample["shape"] = shape;
            sample["barre_manuali"] = ai["barre_manuali"]!.DeepClone();
            foreach (string key in new[] { "longitudinal_bar_count", "top_bar_count", "bottom_bar_count", "flange_bottom_count" }) sample[key] = "inattivo";
            Check(new SezioneCA(sample).Bars.Count == 2, "Validazione barre manuali " + shape);
        }
        var automatic = SezioneCA.DefaultInput(); automatic["side_bar_count_per_side"] = "0"; automatic["side_bar_diameter_mm"] = "inattivo";
        Check(new SezioneCA(automatic).Bars.Count == 10, "Diametro di barre laterali assenti blocca il calcolo");
        var summaries = new[] {
            VerificationSummary.Create("Completa", 2, [("A", .6, null), ("B", .9, null)]),
            VerificationSummary.Create("Incompleta", 2, [("A", .6, null)]),
            VerificationSummary.Create("NaN", 1, [("A", double.NaN, null)]),
            VerificationSummary.Create("Booleana", 1, [("A", null, true)]),
            VerificationSummary.Create("Fallita", 2, [("A", .6, false)]),
            VerificationSummary.Create("Nessuna", 0, []) };
        Check(summaries[0] is { Passed: true, Ratio: .9, Governing: "B", Completed: 2 }, "Combinazione governante riepilogo");
        Check(summaries[1] is { Passed: null, Missing: 1 }, "Esito incompleto");
        Check(summaries[2] is { Passed: null, Missing: 1, Ratio: null }, "NaN produce un falso verificato");
        Check(summaries[3] is { Passed: true, Completed: 1 }, "Verifica booleana persa");
        Check(summaries[4] is { Passed: false, Missing: 1 }, "Esito negativo nascosto dai mancanti");
        Check(summaries[5] is { Passed: null, Status: "NESSUNA AZIONE" }, "Nessuna azione produce falso verificato");
        var material = J.Obj(("id", "M"), ("modulo_id", "mat_calcestruzzo"), ("dati", J.Obj(("classe", "C35/45"), ("scelte", J.Obj(("life", "100 anni"))), ("numeri", J.Obj(("aggregate", "32"))), ("opzioni", J.Obj(("ntcQuality", true))))));
        section.Array("fogli").Add(material);
        Check(ProjectSharedData.ComparableFields(a, material).Count(p => p.Source.Key.StartsWith("Durabilità · ")) == 4, "Durabilità non confrontata fra Materiali e CA");
        Check(!ProjectSharedData.Fields(material).ContainsKey("Scheda CLS · scelte/life"), "Alias della vita utile duplicato nel confronto");
        ProjectSharedData.Apply(material, section, ["Materiali"], a, ["Durabilità · vita utile [anni]", "Durabilità · aggregato [mm]", "Durabilità · qualità copriferri"]);
        var adopted = a["dati"]!["workspace_ca"]!["dettagli_costruttivi"]!;
        Check(adopted.D("vita_durabilita") == 100 && adopted.D("aggregato") == 32 && adopted.S("qualita_copriferro") == "Sì", "Durabilità non trasferita con le unità e i tipi corretti");
        Check(ProjectSharedData.ComparableFields(a, material).Where(p => p.Source.Key.StartsWith("Durabilità · ")).All(p => ProjectSharedData.Equal(p.Source.Value, p.Target.Value)), "Differenze spurie dopo allineamento durabilità");
        material["dati"]!["scelte"]!["coverMethod"] = "EC2 2004";
        Check(!ProjectSharedData.Common(material, a).Any(p => p.Source.Key.StartsWith("Durabilità · ")), "Modelli diversi di durabilità uniformati implicitamente");
        Check(ProjectSharedData.ActiveField(material, "Durabilità · vita utile [anni]"), "Report Materiali perde la vita utile EC2");
        var otherMaterial = (JsonObject)material.DeepClone(); section.Array("fogli").Add(otherMaterial);
        material["dati"]!["scelte"]!["deviationControl"] = "Misura copriferri";
        material["dati"]!["scelte"]!["deviationValue"] = "5 mm";
        ProjectSharedData.Apply(material, section, ["Materiali"], otherMaterial, ["Scheda CLS · scelte/deviationControl", "Durabilità · tolleranza [mm]"]);
        Check(otherMaterial["dati"]!["scelte"].S("deviationControl") == "Misura copriferri" && otherMaterial["dati"]!["scelte"].S("deviationValue") == "5 mm", "Controllo di posa e tolleranza non aggiornati insieme");
        Console.WriteLine($"Dati CA e riepiloghi: {count} controlli superati."); return count;
    }
}
