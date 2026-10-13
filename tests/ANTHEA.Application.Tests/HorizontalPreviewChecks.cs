using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
using Anthea.Calculations;

internal static class HorizontalPreviewChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        JsonObject Fixture(string module) => JsonNode.Parse(File.ReadAllText(Path.Combine(root,
            "tests/fixtures/f3/inputs/" + module + "--default.json")))!["data"]!.AsObject();
        var data = Fixture("geo_palo_orizzontale"); HorizontalSheetPresentation.PrepareReport(data);
        string original = data.ToJsonString();
        var stiffness = ElasticHorizontalPile.SectionStiffness(data);
        string text = $"EJ={stiffness.EI:G7} kNm² · E={stiffness.ModulusMpa:G7} MPa · J={stiffness.InertiaMm4:G7} mm⁴\n{stiffness.Assumption}"
            + (stiffness.OverrideReason == "" ? "" : " · OVERRIDE: " + stiffness.OverrideReason);
        Check(HorizontalPreview.StiffnessDescription(data) == text, "Descrizione della rigidezza modificata");
        var section = HorizontalPreview.ConcreteSection(data);
        var materials = HorizontalPreview.ConcreteStrengths(data["sezione"]!.AsObject());
        var model = HorizontalPreview.ReadModel(data);
        Check(data.ToJsonString() == original, "Anteprime del palo modificano il documento");
        Check(section.Geometry.Width == 1000 && section.Geometry.Bars.Count == 16 && section.LinkRadius == 425 && section.LinkDiameter == 10,
            "Quote indipendenti della sezione circolare da un metro: 16 barre e staffa con copriferro 70 mm");
        section.Geometry.Input["diameter_mm"] = 123;
        Check(data.ToJsonString() == original, "Anteprima espone l'input della sezione condiviso");
        Check(materials.Values.All(v => v != "—") && model.Efficiency == 1d.ToString("F3"), "Errore della stratigrafia contamina proprietà indipendenti");
        data["sezione"]!["longitudinal_bar_count"] = 513;
        bool refused = false;
        try { HorizontalPreview.ConcreteSection(data); } catch (ArgumentException) { refused = true; }
        Check(refused, "Anteprima non limita la geometria con troppe barre");
        data["sezione"]!["fck_mpa"] = -1;
        Check(HorizontalPreview.ConcreteStrengths(data["sezione"]!.AsObject()).Values.All(v => v == "—"), "Materiale invalido visualizzato con numeri precedenti");
        Check(HorizontalPreview.PassivePressure(J.Obj(("tipologia", "Granulare"), ("angolo_attrito", 30))) == 3d.ToString("F1"), "Kp del terreno a 30 gradi");
        Check(new[] { -1d, 90d }.All(phi => HorizontalPreview.PassivePressure(J.Obj(("tipologia", "Granulare"), ("angolo_attrito", phi))) == "—"), "Kp ai limiti non validi");
        var chs = Fixture("geo_micropalo_orizzontale"); HorizontalSheetPresentation.PrepareReport(chs);
        string chsOriginal = chs.ToJsonString();
        var valid = HorizontalPreview.ChsSection(chs);
        Check(valid.Geometry is not null && valid.Fields.Values.All(v => v != "—") && chs.ToJsonString() == chsOriginal, "CHS valido o dati del documento alterati");
        valid.Geometry!["diametro_mm"] = 123;
        Check(chs.ToJsonString() == chsOriginal, "CHS espone dati condivisi");
        chs["sezione"]!["modo_chs"] = "Non valido";
        var invalid = HorizontalPreview.ChsSection(chs);
        Check(invalid.Geometry is null && invalid.Fields.Values.All(v => v == "—"), "CHS invalido conserva anteprima precedente");
        chs["sezione"]!["modo_chs"] = "Catalogo";
        Check(HorizontalPreview.ChsSection(chs).Geometry?.D("diametro_mm") == 139.7, "CHS non recupera dopo correzione");
        return checks;
    }
}
