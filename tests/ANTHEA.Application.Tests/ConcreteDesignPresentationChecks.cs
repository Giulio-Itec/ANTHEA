using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Modules;
using Anthea.Calculations;

/// <summary>
/// Derivations of the reinforcement design of the RC section moved from the historic view in sub-step S8 of F5.10 (ConcreteDesignPresentation):
/// the fields of the search shown, the signature of a search, the fraction of the progress, the texts written by hand from ConcreteDesign.cs
/// of the historic view; then a short search on the slab of the trace «armature/ricerca-armature» (fixture soletta-dettagli-ricerca with the
/// same changes) for the rows of the alternatives, the scope of a slab and «Applica configurazione» on the data. The texts of a whole search
/// are compared with the frozen historic traces by the ViewModel (Presentation.Tests), the identity of the historic view by
/// ui/check-sezione-historic.
/// </summary>
internal static class ConcreteDesignPresentationChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            static JsonObject Workspace(string element, string objective, string links, bool complete) => J.Obj(("dettagli_costruttivi", J.Obj(("elemento", element))),
                ("calcola_armature", J.Obj(("obiettivo", objective), ("staffe", links), ("ricerca_completa", complete))));
            string Shown(string shape, JsonObject workspace) => string.Join(" ", ConcreteDesignPresentation.FieldsShown(J.Obj(("shape", shape)), workspace)
                .Select(f => f.Key + "=" + (f.Shown ? "1" : "0")));
            Check(Shown("Rettangolare", Workspace("Trave", "Peso", "Ottimizza", true)) == "n_superiori=1 n_inferiori=1 n_laterali=1 simmetrica=1 n_circolari=0 diametri_ortogonali=0 passi_ortogonali=0 "
                + "peso_peso=0 peso_barre=0 peso_diametri=0 diametri_staffe=1 passi_staffe=1 max_tentativi=0", "Campi della ricerca: sezione rettangolare, travi, peso, staffe ottimizzate");
            Check(Shown("Circolare", Workspace("Parete", "Compromesso", "Mantieni", false)) == "n_superiori=0 n_inferiori=0 n_laterali=0 simmetrica=0 n_circolari=1 diametri_ortogonali=1 passi_ortogonali=1 "
                + "peso_peso=1 peso_barre=1 peso_diametri=1 diametri_staffe=0 passi_staffe=0 max_tentativi=1", "Campi della ricerca: sezione circolare, parete, compromesso, staffe mantenute, campione");
            var data = J.Obj(("input", J.Obj(("shape", "Rettangolare"))), ("combinazioni", J.Obj()), ("workspace_ca", J.Obj(("tab", 7), ("layout", J.Obj()), ("resistenze_rapide", J.Obj()),
                ("proposta_armature_applicata", J.Obj()), ("calcola_armature", J.Obj(("mostra", "5"), ("diametri", "16"))))));
            Check(ConcreteDesignPresentation.Fingerprint(data) == "{\"shape\":\"Rettangolare\"}{}{\"calcola_armature\":{\"diametri\":\"16\"}}", "Firma della ricerca: senza lo stato dell'interfaccia");
            Check(ConcreteDesignPresentation.Fraction(0, 0) == 0 && ConcreteDesignPresentation.Fraction(1, 4) == 0.25, "Frazione delle configurazioni esaminate");
            Check(ConcreteDesignPresentation.NotStarted("x") == "Ricerca non avviata: x" && ConcreteDesignPresentation.Applied("") == "Configurazione applicata. Verifiche del foglio in aggiornamento; riscontri esecutivi da confermare sulla nuova disposizione."
                && ConcreteDesignPresentation.Applied("Avviso.").EndsWith("disposizione. Avviso.", StringComparison.Ordinal) && string.Join("|", ConcreteDesignPresentation.ShownChoices) == "1|5|10|25|50|Tutte",
                "Testi della ricerca e scelte di «Mostra le prime»");

            // A short search on the slab of the trace.
            var sheet = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/str_palo--soletta-dettagli-ricerca.json")))!["data"]!.AsObject();
            ConcreteSheetPreparation.PrepareReport(sheet);
            var input = sheet["input"]!.AsObject(); var workspace = sheet["workspace_ca"]!.AsObject();
            ConcreteEditor.PrepareDesign(workspace);
            input["cover_mm"] = "60"; workspace["dettagli_costruttivi"]!["zona_sovrapposizione"] = "No"; workspace["ancoraggi"]!["lunghezza"] = "900";
            var options = workspace["calcola_armature"]!.AsObject();
            foreach (var (key, value) in new[] { ("riserva_torsione", "25"), ("diametri", "16"), ("n_superiori", "10"), ("n_inferiori", "6;7"), ("n_laterali", "0"), ("diametri_ortogonali", "10"),
                ("passi_ortogonali", "150"), ("parallelismo", "1") }) options[key] = value;
            options["ricerca_completa"] = true;
            var result = StructuralQueries.ConcreteReinforcement((JsonObject)sheet.DeepClone());
            Check(result.Candidates.Count > 0 && ConcreteDesignPresentation.Status(result).StartsWith($"Ricerca completata · {result.Evaluated}/{result.Planned} configurazioni · {result.Candidates.Count} ammissibili", StringComparison.Ordinal)
                && ConcreteDesignPresentation.Diagnostics(result).StartsWith($"Esaminate tutte le {result.Planned} configurazioni ammesse.", StringComparison.Ordinal), "Ricerca della soletta: stato e copertura");
            options["mostra"] = "1";
            var top = ConcreteDesignPresentation.RankingRows(result, options);
            options["mostra"] = "Tutte";
            var all = ConcreteDesignPresentation.RankingRows(result, options);
            var best = result.Candidates[0];
            Check(top.Count == 1 && all.Count == result.Candidates.Count && top[0].S("id") == best.Id.ToString(CultureInfo.InvariantCulture) && top[0].S("rank") == "1"
                && top[0].S("config") == best.Description && top[0].S("esito") is "Da completare" or "Completati", "Alternative: «Mostra le prime» 1 e «Tutte»");
            string scope = ConcreteDesignPresentation.Scope(best);
            Check(scope.Contains("Rete ortogonale: As totale ", StringComparison.Ordinal) && scope.EndsWith("Le schede dei dettagli conservano le verifiche da completare.", StringComparison.Ordinal)
                && ConcreteDesignPresentation.Metrics(best, result)[0].Title == "Peso stimato" && ConcreteDesignPresentation.Checks(best).Count == best.Checks.Count,
                "Proposta della soletta: rete ortogonale, metriche e verifiche");
            var inputBefore = input;
            string notice = ConcreteDesignPresentation.Apply(input, workspace, best, result.Completed);
            Check(ReferenceEquals(inputBefore, sheet["input"]) && JsonNode.DeepEquals(input, best.Input) && workspace["proposta_armature_applicata"].S("descrizione") == best.Description
                && workspace["dettagli_costruttivi"].S("barre_trattenute") == "Da confermare" && workspace["ancoraggi"].S("sigma") == "" && workspace["ancoraggi"].S("cautele") == "Da verificare"
                && workspace["proposta_armature_applicata"]!["riserva_torsione_percento"]!.ToString() == "25" && notice is not null, "Applica configurazione sui dati: ingressi della proposta, conferme da rifare");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        return checks;
    }
}
