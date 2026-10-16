using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.FoundationWorkspace;

/// <summary>
/// Presentation of the shallow foundation sheet moved from the historic view in F5.9b (ShallowFoundationPresentation): rules of the
/// fields, texts, survey and layer readings, results and the shared concrete, with values written by hand. The identity of the
/// historic view with these functions is proved by ui/check-fondazioni-historic (traces frozen before the move).
/// </summary>
internal static class ShallowFoundationPresentationChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var data = FoundationDocument.Defaults();
            var input = data["input"]!.AsObject();
            bool Visible(string key) => ShallowFoundationPresentation.FieldVisible(input, ShallowFoundationPresentation.VisibilityInput(data), key);
            Check(Visible("Length") && Visible("MomentX") && Visible("RoughBase") && Visible("azioni_confermate") && Visible("Compression")
                && !Visible("IncludeDepthFactors") && !Visible("RelativeDensity") && !Visible("WaterUnitWeight"), "Campi visibili del foglio predefinito");
            Check(Visible("Method") && Visible("Surcharge") && Visible("profondita_falda"), "Campi senza regola sempre visibili");
            data["generali"]!["presenza_falda"] = true;
            Check(Visible("WaterUnitWeight") && input["WaterEnabled"]!.GetValue<bool>() == false, "Falda dei dati comuni, non dell'input storico");
            input["Shape"] = 1;
            Check(!Visible("Length") && !Visible("MomentX") && !Visible("HorizontalY") && Visible("MomentY"), "Nastriforme: lunghezza, Mx e Hy nascosti");
            input["MomentX"] = "12"; input["HorizontalY"] = "0";
            Check(Visible("MomentX") && !Visible("HorizontalY"), "Mx diverso da «0» resta visibile, Hy «0» no");
            input["Shape"] = 2; input["MomentY"] = "0,0";
            Check(Visible("MomentY") && !Visible("Length"), "My «0,0» è diverso da «0»: resta visibile come nella vista storica");
            input["Condition"] = 1; input["Compression"] = 2;
            Check(Visible("Compression") && !Visible("RelativeDensity") && !Visible("RoughBase"), "Non drenato: compressibilità diversa da zero visibile, Dr nascosta");
            input["Profile"] = 1; input["Method"] = 3;
            Check(!Visible("azioni_confermate") && Visible("IncludeDepthFactors"), "Confronto caratteristico senza conferma; fattori di profondità per Hansen");

            var view = new JsonObject();
            Check(ShallowFoundationPresentation.ViewFlag(view, "metodo", true) && !ShallowFoundationPresentation.ViewFlag(view, "input_raccolti", false), "Stato dei gruppi predefinito");
            ShallowFoundationPresentation.SaveViewFlag(view, "metodo", false);
            view["vista_fondazioni"]!["Azioni"] = "no";
            Check(!ShallowFoundationPresentation.ViewFlag(view, "metodo", true) && ShallowFoundationPresentation.ViewFlag(view, "Azioni", true)
                && view.ToJsonString() == "{\"vista_fondazioni\":{\"metodo\":false,\"Azioni\":\"no\"}}", "Stato dei gruppi salvato; valore non booleano ignorato");

            var surveys = FoundationDocument.Defaults();
            surveys["stratigrafie"]!.AsArray().Add(new JsonArray(FoundationMaterials.NewLayer()));
            surveys["generali"]!["sondaggio_fondazione"] = 1;
            Check(ShallowFoundationPresentation.SurveyNames(surveys).SequenceEqual(["Sondaggio 1", "Sondaggio 2"]) && ShallowFoundationPresentation.SelectedSurvey(surveys) == 1,
                "Nomi e sondaggio esaminato");
            surveys["generali"]!["sondaggio_fondazione"] = 2;
            bool refused = false;
            try { ShallowFoundationPresentation.SelectedSurvey(surveys); } catch (ArgumentException) { refused = true; }
            Check(refused, "Sondaggio fuori dall'elenco rifiutato");
            Check(ShallowFoundationPresentation.LayerLetter(0) == "A" && ShallowFoundationPresentation.LayerLetter(25) == "Z" && ShallowFoundationPresentation.LayerLetter(26) == "A",
                "Lettere degli strati");

            var result = J.Obj(("Status", "Verificata"), ("Resistance", 1906871.374), ("Utilization", 0.3146), ("EffectiveArea", 6e6));
            var calculated = J.Obj(("Normal", "600"), ("Condition", 0));
            Check(ShallowFoundationPresentation.Headline(calculated, result) == "Verificata     N = 600 kN     R = 1906,871 kN     N/R = 0,315     A′ = 6 m²", "Titolo del risultato");
            Check(ShallowFoundationPresentation.Headline(calculated, J.Obj(("Status", "Non verificata"), ("Resistance", 0), ("EffectiveArea", 0)))
                == "Non verificata     N = 600 kN     R = 0 kN     N/R = —     A′ = 0 m²", "Indice indefinito con il trattino");
            Check(ShallowFoundationPresentation.SummaryLine(("Esito", "Verificata")) == "Esito   Verificata", "Riga del riepilogo");
            Check(ShallowFoundationPresentation.CalculatedState(1, J.Obj(("nome_combinazione", "SLU · A1"))) == "Calcolato · sondaggio 2 · unica combinazione: SLU · A1", "Stato dopo il calcolo");
            var graphics = J.Obj(("MinimumPressure", 0.0723), ("MaximumPressure", 0.136), ("CompressedArea", 8.64e6));
            Check(ShallowFoundationPresentation.PressureSummary(calculated, graphics) == "Efficaci · pmin = 72,3 kPa · pmax = 136 kPa · area compressa = 8,64 m²"
                && ShallowFoundationPresentation.PressureSummary(J.Obj(("Condition", 1)), graphics).StartsWith("Totali · ", StringComparison.Ordinal), "Testo delle pressioni");
            Check(ShallowFoundationPresentation.MechanismKey(0) == "MechanismX" && ShallowFoundationPresentation.MechanismKey(1) == "MechanismY"
                && ShallowFoundationPresentation.MechanismAxes.SequenceEqual(["Sezione X · larghezza B", "Sezione Y · lunghezza L"]), "Sezioni del meccanismo");
            Check(ShallowFoundationPresentation.ResultsPlaceholder(true).StartsWith("Verifica da calcolare", StringComparison.Ordinal)
                && ShallowFoundationPresentation.ResultsPlaceholder(false).StartsWith("Calcolo in attesa del rilascio approvato GPC", StringComparison.Ordinal), "Risultati senza calcolo");

            var catalog = new[] { J.Obj(("nome", "C25/30"), ("classe_cls", "C25/30"), ("materiale_cls_nome", "C25/30"), ("fck_mpa", 25.0), ("cls_diagramma", "Parabola-rettangolo")) };
            var section = J.Obj(("classe_cls", "C12/15"), ("materiale_cls_nome", "C12/15"), ("fck_mpa", 12), ("cls_diagramma", "Bilineare"));
            Check(ShallowFoundationPresentation.ConcreteChoices(section, catalog).SequenceEqual(["C25/30", "C12/15", "Personalizzato"]), "Voci del calcestruzzo con la classe del foglio");
            var shown = new List<string>();
            section["classe_cls"] = "C25/30";
            ShallowFoundationPresentation.ConcreteChanged(section, "classe_cls", catalog, (k, v) => shown.Add(k + "=" + v));
            Check(section.ToJsonString() == "{\"classe_cls\":\"C25/30\",\"materiale_cls_nome\":\"C25/30\",\"fck_mpa\":25,\"cls_diagramma\":\"Parabola-rettangolo\"}"
                && shown.SequenceEqual(["classe_cls=C25/30", "materiale_cls_nome=C25/30", "fck_mpa=25", "cls_diagramma=Parabola-rettangolo"]), "Classe del catalogo copiata");
            shown.Clear(); section["fck_mpa"] = "28";
            ShallowFoundationPresentation.ConcreteChanged(section, "fck_mpa", catalog, (k, v) => shown.Add(k + "=" + v));
            Check(section["classe_cls"]!.ToString() == "Personalizzato" && section["materiale_cls_nome"]!.ToString() == "Personalizzato" && shown.SequenceEqual(["classe_cls=Personalizzato"]),
                "fck digitato fuori dal catalogo");
            shown.Clear(); section["fck_mpa"] = "25";
            ShallowFoundationPresentation.ConcreteChanged(section, "fck_mpa", catalog, (k, v) => shown.Add(k + "=" + v));
            Check(section["classe_cls"]!.ToString() == "C25/30" && shown.SequenceEqual(["classe_cls=C25/30"]), "fck del catalogo scritto come testo");
            shown.Clear(); section["cls_diagramma"] = "Stress block";
            ShallowFoundationPresentation.ConcreteChanged(section, "cls_diagramma", catalog, (k, v) => shown.Add(k + "=" + v));
            Check(shown.Count == 0 && section["cls_diagramma"]!.ToString() == "Stress block", "Diagramma senza effetti sulla classe");
            Check(ShallowFoundationPresentation.CoefficientColumns.Count == 6 && ShallowFoundationPresentation.AdditiveColumns.Count == 5, "Colonne delle tabelle");
            return checks;
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
}
