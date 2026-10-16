using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Materials;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Reports;
using Materiali;
using X.Core;

/// <summary>
/// The derivations of the material views moved to Application in F5.5 (MaterialExposurePresentation, the deviation and cover rules
/// of MaterialSheetInput and MaterialSheetPresentation, RebarPresentation, MaterialReportChapter.Document), with values written by
/// hand from the rules of the historic views. The traces of tests/fixtures/f5-materiali prove that the views give the same output.
/// </summary>
internal static class MaterialViewPresentationChecks
{
    internal static int Run()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
            int checks = 0;
            void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
            MaterialExposureChoice Choice(string code) => MaterialSheetInput.ExposureChoices.Single(e => e.Code == code);

            // Exposure menu: one active class, the shown one.
            var single = MaterialExposurePresentation.Texts("XS3", [Choice("XS3")], null);
            Check(single.Shown == "XS3" && single.Details == "Corrosione da cloruri marini: zone di marea, di onda e di spruzzi."
                && single.Aggressiveness == "Aggressività: molto aggressiva" && single.Combined == "" && !single.CombinedVisible, "Esposizione singola");
            Check(MaterialExposurePresentation.Descriptions.Count == 18 && MaterialExposurePresentation.Examples.Count == 18
                && MaterialSheetInput.ExposureChoices.All(e => MaterialExposurePresentation.Descriptions.ContainsKey(e.Code) && MaterialExposurePresentation.Examples.ContainsKey(e.Code)),
                "Testi delle esposizioni incompleti");
            // A selection that is not active gives the first active class; two classes give the combined line.
            var combined = MaterialExposurePresentation.Texts("XC1", [Choice("XC4"), Choice("XS1")], null);
            Check(combined.Shown == "XC4" && combined.Combined == "Classi attive: XC4 + XS1\nAggressività complessiva: aggressiva" && combined.CombinedVisible
                && combined.Aggressiveness == "Aggressività: aggressiva", "Esposizioni combinate");
            var invalid = MaterialExposurePresentation.Texts("X0", [Choice("X0"), Choice("XC1")], null);
            Check(invalid.Combined == "Classi attive: X0 + XC1 · combinazione non valida: X0 deve essere isolata.", "X0 combinata");
            var none = MaterialExposurePresentation.Texts("XC1", [], null);
            Check(none.Shown is null && none.Details == "Selezionare una classe di esposizione." && none.Examples == "" && none.Aggressiveness == "Aggressività: da definire"
                && !none.CombinedVisible, "Nessuna esposizione attiva");
            var legacy = MaterialExposurePresentation.Texts("XD2", [Choice("XD2")], new JsonArray("XF1", "XC4", "XD2"));
            Check(legacy.Combined == "Archivio precedente con più esposizioni: XF1 + XC4 + XD2. Classe attiva unica: XD2." && legacy.CombinedVisible, "Archivio con più esposizioni");
            Check(MaterialExposurePresentation.Aggressiveness(0) == "ordinaria", "Aggressività ordinaria");
            try { MaterialExposurePresentation.Aggressiveness(3); throw new Exception("Gravità sconosciuta accettata"); }
            catch (ArgumentOutOfRangeException) { checks++; }

            // Deviation, cover criterion, key orders.
            Check(!MaterialSheetInput.DeviationChoosable(0) && MaterialSheetInput.DeviationChoosable(1) && MaterialSheetInput.DeviationChoosable(2), "Tolleranza selezionabile");
            Check(MaterialSheetPresentation.DeviationNote(0) == "Posa ordinaria: Δcdev = 10 mm (Circolare 2019 §C4.1.6.1.3)."
                && MaterialSheetPresentation.DeviationNote(1).StartsWith("Δcdev da 5 a 10 mm", StringComparison.Ordinal)
                && MaterialSheetPresentation.DeviationNote(2).StartsWith("Δcdev da 0 a 10 mm", StringComparison.Ordinal), "Note della tolleranza");
            Check(MaterialSheetInput.IsNtcCover("NTC + Circ. 2019") && !MaterialSheetInput.IsNtcCover("EC2 2004") && !MaterialSheetInput.IsNtcCover(null), "Criterio del copriferro");
            Check(MaterialSheetInput.NumberKeys.SequenceEqual(["diameter", "aggregate", "cementName", "bondAlpha", "bondGamma"])
                && MaterialSheetInput.ChoiceKeys.SequenceEqual(["ntcElement", "coverMethod", "life", "deviationControl", "deviationValue", "ground", "abrasion", "consistency", "cement", "cementClass", "cementEarly", "bondCondition"])
                && MaterialSheetInput.FlagKeys.SequenceEqual(["highStrength", "slab", "quality", "rough", "ntcQuality"]), "Ordine delle chiavi del calcestruzzo");

            // Reinforcing steel.
            Check(RebarPresentation.Types().SequenceEqual(["B450C", "B450A", "FeB22k", "FeB32k", "FeB38k", "FeB44k", "Personalizzato"]), "Tipi di acciaio");
            JsonObject Input(string name) { var input = RebarMaterial.Defaults()["input"]!.AsObject(); RebarEditor.Select(input, name); return input; }
            var b450c = RebarPresentation.Read(Input("B450C"));
            Check(b450c.Selected == "B450C" && !b450c.Custom && !b450c.Historical && RebarPresentation.PropertyKeys.All(b450c.Locked) && b450c.NameLocked && !b450c.DiagramEnabled
                && b450c.Description == "Aderenza migliorata · classe C\nAcciaio B450C da catalogo." && b450c.Source.StartsWith("Proprietà dal catalogo", StringComparison.Ordinal), "B450C");
            Check(RebarPresentation.Read(Input("B450A")).Description.StartsWith("Aderenza migliorata · classe A", StringComparison.Ordinal), "B450A");
            var historic = RebarPresentation.Read(Input("FeB38k"));
            Check(historic.Selected == "FeB38k" && historic.Historical && !historic.Custom && historic.Locked("fyk_mpa") && !historic.Locked("steel_eps_u") && historic.DiagramEnabled
                && historic.NameLocked && historic.Description == "Aderenza migliorata\nAcciaio storico · A5 minimo 14%\nA5 non coincide con εu. Definire εu per le verifiche."
                && historic.Source.StartsWith("D.M. 09/01/1996", StringComparison.Ordinal), "Acciaio storico");
            var custom = Input("Personalizzato");
            var state = RebarPresentation.Read(custom);
            Check(state.Selected == "Personalizzato" && state.Custom && !RebarPresentation.PropertyKeys.Any(state.Locked) && !state.NameLocked && state.DiagramEnabled
                && state.Description == "Dati personalizzati. Specificare la provenienza dei valori adottati.", "Acciaio personalizzato");
            Check(!RebarPresentation.MarkCustom(custom) && custom.S("materiale_acciaio_nome") == "Acciaio personalizzato", "Personalizzato già marcato");
            var previous = Input("B450A"); previous["steel_eps_u"] = 30;
            var det3 = RebarPresentation.Read(previous);
            Check(det3.Custom && det3.Selected == "Personalizzato" && det3.Description.StartsWith("Valori salvati diversi dal catalogo B450A", StringComparison.Ordinal)
                && previous.S("classe_acciaio") == "B450A", "Catalogo precedente (DET-3)");
            Check(RebarPresentation.MarkCustom(previous) && previous.S("classe_acciaio") == "Personalizzato" && previous.S("materiale_acciaio_nome") == "Acciaio personalizzato",
                "Passaggio a personalizzato dopo una modifica");
            Check(!RebarPresentation.MarkCustom(Input("B450C")), "Preset del catalogo marcato personalizzato");
            var data = RebarMaterial.Defaults();
            var results = RebarPresentation.Results(ModuleServices.Get(RebarMaterial.Module).Calculate(data), historical: false);
            Check(results.Text == "fyd     391,3 MPa\nεyd     1,96 ‰\nfu / fyk     1,2" && results.Status == "✓ Proprietà coerenti" && results.Coherent && results.Diagram.Count > 1,
                "Valori del B450C: " + results.Text);
            Check(!RebarPresentation.Results(ModuleServices.Get(RebarMaterial.Module).Calculate(data), historical: true).Coherent, "Stato dell'acciaio storico");
            var failed = RebarPresentation.Failed(new ArgumentException("fu deve essere maggiore o uguale a fyk."));
            Check(failed.Text == "fyd     —\nεyd     —\nfu / fyk     —" && failed.Status == "⚠ fu deve essere maggiore o uguale a fyk." && !failed.Coherent && failed.Diagram.Count == 0,
                "Valori di un acciaio incompleto");

            // Report of one material sheet: the composition of the historic SheetEditor.ExportReport, the same bytes.
            var sheetData = ModuleCatalog.CreateData("mat_calcestruzzo");
            var sheet = J.Obj(("nome", "Titolo"), ("modulo_id", "mat_calcestruzzo"), ("dati", sheetData.DeepClone()));
            var section = J.Obj(("nome", "Titolo"), ("fogli", new JsonArray()), ("strutture", new JsonArray()));
            section.Array("fogli").Add(sheet);
            var expected = ReportProject.Compose(new ProjectReportPlan(section), new Dictionary<JsonObject, ReportProject.SheetContent> { [sheet] = MaterialReportChapter.Create("mat_calcestruzzo", sheetData) },
                [], materialSheet: true);
            Check(DocxWriter.Write(MaterialReportChapter.Document("mat_calcestruzzo", "Titolo", sheetData)).SequenceEqual(DocxWriter.Write(expected)), "Report del foglio materiale");
            return checks;
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
}
