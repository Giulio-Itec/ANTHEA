using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Presentation;

internal static class VerticalPilePreviewChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        foreach (bool micro in new[] { false, true })
        {
            var data = ModuleCatalog.CreateData(micro ? "geo_micropalo_verticale" : "geo_palo_verticale");
            var general = data["generali"]!; string before = data.ToJsonString();
            var display = VerticalPilePreview.Read(data, micro);
            Check(display.Correlation.Count == 2 && data.ToJsonString() == before, "Anteprima modifica dati o perde correlazioni");
            Check(VerticalPilePreview.NormativeBase(general, micro) == Calcolo.SicurezzaBaseNormativa(general, micro).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), "Formato del coefficiente di base alterato");
            general["verticali_indagate"] = "non valido";
            Check(VerticalPilePreview.Read(data, micro).Correlation.Count == 0, "Correlazioni inventate per scelta invalida");
        }
        var g = ModuleCatalog.CreateData("geo_palo_verticale")["generali"]!;
        var valid = J.Obj(("tipologia", "Granulare"), ("spessore", "2"), ("angolo_attrito", "30"));
        var broken = (JsonObject)valid.DeepClone(); broken["spessore"] = "?";
        var rows = VerticalPilePreview.LayerFields(g, new[] { valid, broken, valid }, false);
        Check(rows[0]["__nq"] != "—" && rows[1]["__nq"] == "—" && rows[2]["__nq"] == "—", "Spessore invalido non invalida le quote successive");
        Check(rows.Select(r => r["__strato"]).SequenceEqual(new[] { "A", "B", "C" }), "Etichette degli strati cambiate");
        var inactive = J.Obj(("laterale_attiva", false), ("terreno", "sconosciuto"));
        var incomplete = J.Obj(("terreno", "sconosciuto"));
        var tau = VerticalPilePreview.LayerFields(g, new[] { inactive, incomplete }, true);
        Check(tau[0]["__tau"] == "0,0" && tau[1]["__tau"] == "—", "Aderenza inattiva confusa con dato incompleto");
        Check(VerticalPilePreview.InitialAlpha("sconosciuto", "IGU") is null && VerticalPilePreview.InitialAlpha(PileInputCatalog.MicropileSoils[0], "?") is null, "Alpha inventato per dati invalidi");
        foreach (string soil in PileInputCatalog.MicropileSoils)
        foreach (string injection in new[] { "IGU", "IRS" })
            Check(VerticalPilePreview.InitialAlpha(soil, injection) == BustamanteDoix.IntervalloAlpha(soil,injection)[0].ToString(System.Globalization.CultureInfo.InvariantCulture), "Alpha iniziale diverso da quello precedente");
        var catalog = PileInputCatalog.ChsProfiles; string first = catalog[0]; catalog[0] = "modificato";
        Check(PileInputCatalog.ChsProfiles[0] == first, "Catalogo CHS condiviso e modificabile dalla vista");
        return checks;
    }
}
