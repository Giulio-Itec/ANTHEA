using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Application.Reporting;
using Anthea.Calculations;
using Anthea.ConcreteWorkspace;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Presentation.Modules;
using Anthea.Testing.Normalization;
using X.Core;

namespace Anthea.Presentation.Tests;

internal static partial class ConcreteSectionSheetChecks
{
    internal static int RunBarMaterials(string root, string output) => SingleThreadContext.Run(async _ =>
    {
        var expect = new Expect();
        await BarMaterialsAsync(root, output, expect);
        return expect.Count;
    });
    private static async Task BarMaterialsAsync(string root, string output, Expect expect)
    {
        using var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"));
        await IdleAsync(sheet);
        var bars = sheet.Inputs.Bars;
        expect.That(bars.Columns.Select(c => c.Key).SequenceEqual(["id", "x", "y", "materiale", "phi"])
            && bars.Columns[3].Kind == ConcreteColumnKind.Choice && !bars.Columns[3].IsReadOnly, "Barre: materiale modificabile accanto alle coordinate");
        expect.That(!sheet.Input.ContainsKey("barre_manuali"), "La sola apertura non trasforma la disposizione in manuale");
        bars.Rows[0]["materiale"] = "B450A";
        await IdleAsync(sheet);
        var first = CheckerSection.PrepareModel(sheet.Input, sheet.Settings).Section.Rebars.First().RebarMaterial;
        expect.That(first.Name == "B450A" && first.StrainUTension == .025
            && CheckerSection.PrepareModel(sheet.Input, sheet.Settings).Section.Rebars.Skip(1).First().RebarMaterial.StrainUTension == .075,
            "Il materiale B450A è applicato solo alla barra scelta, con la sua duttilità");
        var custom = (JsonObject)sheet.Input.DeepClone();
        custom["tipo"] = "Acciaio"; custom["nome"] = "Acciaio prova 500"; custom["id"] = "test-500";
        custom["fyk_mpa"] = 500; custom["steel_fu_mpa"] = 600; custom["steel_modulus_mpa"] = 190000;
        sheet.Settings.Array("materiali_custom").Add(custom);
        bars.Rows[0]["materiale"] = "Acciaio prova 500";
        await IdleAsync(sheet);
        var model = CheckerSection.PrepareModel(sheet.Input, sheet.Settings);
        var native = model.Section.Rebars.ToArray();
        expect.That(native[0].RebarMaterial.Name == "Acciaio prova 500" && native[0].RebarMaterial.Fyk == 500 && native[0].RebarMaterial.E == 190000
            && native.Skip(1).All(b => b.RebarMaterial.Fyk == 450 && b.RebarMaterial.E == 200000), "Materiale personalizzato: proprietà native per barra, le altre restano del foglio");
        var options = (JsonObject)sheet.Settings["sle"]!["SLE"]!.DeepClone();
        options["modello"] = "Lineare"; options["trazione_cls"] = "No"; options["phi"] = "0";
        var state = new CheckerSection(model, sheet.Input, sheet.Settings, options).Stress(new(100, 0, 0), "SLE");
        expect.That(native.Select((bar, i) => Math.Abs(state.tensioni_barre[i] - state.BarStrains[i] / 1000 * bar.RebarMaterial.E)).Max() < 1e-7,
            "Controllo indipendente σ=Es·ε: il solutore usa Es della singola barra (tolleranza 1e-7 MPa)");
        expect.That(Math.Abs(native.Select((bar, i) => bar.Area * state.tensioni_barre[i]).Sum() - 100000) < .1,
            "Trazione pura: equilibrio indipendente ΣAs·σs=100000 N, tolleranza 0,1 N");
        expect.That(state.OrdinarySteelStressLimits[0] == 400 && state.OrdinarySteelStressLimits[1] == 360,
            "Limiti NTC della rara per materiale: 0,8×500 e 0,8×450 MPa");
        expect.Throws<ArgumentException>(() => ConcreteBarMaterials.RequireSheetProperties(sheet.Input, "Dettagli", "fyk_mpa"),
            "Un modello a fyk unico non accetta silenziosamente un materiale diverso");
        var stored = sheet.Input["barre_manuali"]!.AsArray();
        expect.That(stored.All(b => !b!.AsObject().ContainsKey("materiale")), "L'etichetta della UI non viene serializzata");
        byte[] fullReport = sheet.Report.BuildReport("Materiali delle barre", new HashSet<string> { "geometria", "materiali" });
        byte[] shortReport = ((IModuleShortReport)sheet.Report).BuildShortReport("Materiali delle barre");
        File.WriteAllBytes(Path.Combine(output, "materiali-barre.docx"), fullReport);
        File.WriteAllBytes(Path.Combine(output, "materiali-barre-short.docx"), shortReport);
        string fullText = DocxText.Extract(fullReport, new TextNormalizer([])), shortText = DocxText.Extract(shortReport, new TextNormalizer([]));
        expect.That(fullText.Contains("Materiali assegnati alle barre") && fullText.Contains("Acciaio prova 500")
            && shortText.Contains("Acciai delle barre") && shortText.Contains("Acciaio prova 500"), "Relazioni completa e sintetica riportano i materiali assegnati");
        string file = Path.Combine(output, "materiali-barre-trefoli.anthea");
        sheet.Inputs.Cable.Set("x", "0", true); sheet.Inputs.Cable.Set("y", "0", true);
        sheet.Inputs.AddCableCommand.Execute(null);
        expect.That(sheet.Inputs.Tendons.Rows.Count == 1, "Inserimento trefolo di prova");
        var tendon = sheet.Inputs.Tendons.Rows[0];
        tendon["materiale"] = "Y1770"; tendon["sigma0"] = "900";
        await IdleAsync(sheet);
        var cable = CheckerSection.PrepareModel(sheet.Input, sheet.Settings).Section.Rebars.Last();
        expect.That(cable.RebarMaterial.Fu == 1770 && Math.Abs(cable.EpsilonP - 900 / cable.RebarMaterial.E) < 1e-12,
            "Trefolo: materiale e tensione iniziale diventano materiale e predeformazione GPC");
        sheet.Commit();
        DocumentService.Save(file, J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", "str_palo"), ("dati", sheet.Data.DeepClone())));
        var data = DocumentService.Open(file)["dati"]!.AsObject();
        using var reopened = OpenSheet(data);
        await IdleAsync(reopened);
        var restored = ConcreteSectionArchive.Restore(reopened.Input, reopened.Settings)!;
        expect.That(restored.Rebars.First().RebarMaterial.Fyk == 500 && Math.Abs(restored.Rebars.Last().EpsilonP - cable.EpsilonP) < 1e-12,
            "Salvataggio e riapertura delle classi GPC: materiali distinti e predeformazione conservati");
        reopened.Inputs.Bars.Rows[0]["x"] = (reopened.Inputs.Bars.Rows[0].Values.D("x") + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await IdleAsync(reopened);
        expect.That(ConcreteSectionArchive.Restore(reopened.Input, reopened.Settings) is null
            && CheckerSection.PrepareModel(reopened.Input, reopened.Settings).Section.Rebars.First().RebarMaterial.Fyk == 500,
            "Modifica delle coordinate: archivio invalidato, materiale conservato nella ricostruzione");
        reopened.Inputs.Bars.Rows[0]["materiale"] = ConcreteBarMaterialPresentation.DefaultLabel(reopened.Input);
        await IdleAsync(reopened);
        expect.That(CheckerSection.PrepareModel(reopened.Input, reopened.Settings).Section.Rebars.First().RebarMaterial.Fyk == 450,
            "Del foglio ripristina il materiale generale");
        reopened.Settings["trefoli"]![0]!["sigma0"] = "2000";
        expect.Throws<ArgumentException>(() => CheckerSection.PrepareModel(reopened.Input, reopened.Settings), "Tensione iniziale oltre fpk rifiutata");
        sheet.Input["barre_manuali"]![0]!["acciaio"] = null;
        expect.Throws<ArgumentException>(() => CheckerSection.PrepareModel(sheet.Input, sheet.Settings), "Materiale salvato malformato: nessun ripiego al materiale generale");
    }
}
