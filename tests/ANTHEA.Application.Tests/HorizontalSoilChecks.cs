using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Presentation;
using GPC.Checkers.Geotechnics.Piles;

internal static class HorizontalSoilChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        foreach (string type in new[] { "Granulare", "Coesivo" })
        {
            var layer = J.Obj(("tipologia", type));
            var state = HorizontalSoilPresentation.Prepare(layer);
            Check(state.SelectedChoice >= 0 && state.Choices.Length > 0, "Scelta iniziale del terreno non riconosciuta");
            foreach (var choice in state.Choices)
            {
                var candidate = (JsonObject)layer.DeepClone();
                var expected = (JsonObject)layer.DeepClone(); var soil = expected["reazione_orizzontale"]!.AsObject();
                soil["legge"] = ElasticHorizontalPile.Laws[choice.Mode];
                if (choice.Mode == 4) ElasticHorizontalPile.SelectSoil(expected, "riga_146", choice.Row);
                else ElasticHorizontalPile.InitializeMean(soil);
                HorizontalSoilPresentation.SelectLaw(candidate, choice);
                Check(JsonNode.DeepEquals(candidate, expected), "Scelta della legge diversa dall'editor precedente");
                var prepared = HorizontalSoilPresentation.Prepare(candidate);
                Check(prepared.Choices[prepared.SelectedChoice] == choice, "Scelta non ripristinata nella lista");
            }
            var selected = state.Soil; var valueKey = state.ValueField;
            selected[valueKey] = "123.75"; HorizontalSoilPresentation.RecordManualChoice(selected, valueKey);
            HorizontalSoilPresentation.Prepare(layer);
            Check(selected.S(valueKey) == "123.75", "Preparazione sovrascrive il valore manuale");
            HorizontalSoilPresentation.KeepValue(selected);
            Check(selected.S(valueKey) == "123.75" && selected.S("scelta_per") == ElasticHorizontalPile.SelectionKey(selected), "Conferma della fonte perde il valore");
            HorizontalSoilPresentation.AdoptMean(selected);
            Check(selected.S(valueKey) != "123.75" && !HorizontalSoilPresentation.Description(selected).Contains("Fonte cambiata"), "Adozione della media non risolve la fonte");
        }
        var sand = HorizontalSoilPresentation.SandTable(); var clay = HorizontalSoilPresentation.CohesiveTable();
        Check(sand.Length == ViggianiHorizontalSoil.SandTable.Count && clay.Length == ViggianiHorizontalSoil.CohesiveTable.Count, "Catalogo incompleto");
        foreach (var pair in sand.Zip(ViggianiHorizontalSoil.SandTable))
            Check(pair.First.Addensamento == pair.Second.Density && pair.First.Media_software == ViggianiHorizontalSoil.MeanA(pair.Second.Density)
                && pair.First.Nh_immerso_N_cm3 == pair.Second.SubmergedNhNPerCm3, "Proiezione della tabella sabbie");
        foreach (var pair in clay.Zip(ViggianiHorizontalSoil.CohesiveTable))
            Check(pair.First.Terreno == pair.Second.Category && pair.First.Autori == pair.Second.Author
                && pair.First.Media_software_N_cm3 == ViggianiHorizontalSoil.MeanNh(pair.Second.Id), "Proiezione della tabella argille");
        sand[0] = sand[0] with { Media_software = -1 };
        Check(HorizontalSoilPresentation.SandTable()[0].Media_software > 0, "Tabella dell'editor modifica il catalogo");
        var invalid = J.Obj(("tipologia", "Coesivo")); var initial = HorizontalSoilPresentation.Prepare(invalid);
        initial.Soil["riga_146"] = "riga inesistente";
        var missing = HorizontalSoilPresentation.Prepare(invalid);
        Check(missing.SelectedChoice == -1 && missing.Minimum == "—" && missing.Maximum == "—", "Fonte assente mantiene un intervallo numerico");
        return checks;
    }
}
