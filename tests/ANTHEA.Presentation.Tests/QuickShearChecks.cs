using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.Presentation.Modules.QuickSection;
using Anthea.Reports;
using X.Core;

namespace Anthea.Presentation.Tests;

internal static class QuickShearChecks
{
    internal static async Task Run(Expect expect, string folder, Func<JsonObject, QuickSectionSheetViewModel> open)
    {
        await QuickShearCombinationChecks.Run(expect, folder, open);
        var data = ConcreteQuickSection.Create();
        ConcreteQuickSection.ReplaceCombinations(data, [new("Momento A", -600, 150, 0), new("Esercizio", -250, 60, 0, "SLE"), new("Sismica", -400, 100, 0, "SLV")]);
        string original = data.ToJsonString();
        expect.That(!ConcreteQuickShear.Enabled(data) && ConcreteQuickShear.Calculate(data).Count == 0 && data.ToJsonString() == original,
            "Taglio: leggere un foglio preesistente non crea dati né risultati");
        using var sheet = open(data);
        await sheet.PendingCalculation;
        var flexure = sheet.Current!.Combinations.Select(r => (r.Id, r.Ratio, r.Passed)).ToArray();
        string bending = sheet.Data["combinazioni"]!.ToJsonString(), input = sheet.Data["input"]!.ToJsonString();
        sheet.ToggleShearCommand.Execute(null);
        expect.That(sheet.ShearExpanded && sheet.ShearCombinationRows.Count == 1 && sheet.ShearCombinationRows[0].Name == "Combo 1", "Taglio: apertura con una combinazione autonoma già pronta");
        sheet.CopyShearCombinationsCommand.Execute(null);
        expect.That(sheet.ShearCombinationRows.Select(r => (r.Name, r.N)).SequenceEqual(sheet.CombinationRows.Select(r => (r.Name, r.N)))
            && !sheet.ShearCombinationRows.Select(r => r.Id).Intersect(sheet.CombinationRows.Select(r => r.Id)).Any(), "Taglio: copia nomi e N con identità separate, senza classificazione SL");
        string id = sheet.ShearCombinationRows[0].Id;
        sheet.ShearCombinationRows[0].Vx = "75"; sheet.ShearCombinationRows[0].Vy = "40";
        sheet.CopyShearCombinationsCommand.Execute(null);
        expect.That(sheet.ShearCombinationRows.Count == 3 && sheet.ShearCombinationRows[0].Vx == "75" && sheet.ShearCombinationRows[0].Vy == "40", "Taglio: ricopia senza duplicati e senza perdere i tagli inseriti");
        sheet.AddShearCombinationCommand.Execute(null);
        sheet.ShearCombinationRows[^1].Name = "Solo taglio"; sheet.ShearCombinationRows[^1].N = "-120"; sheet.ShearCombinationRows[^1].Vy = "60";
        sheet.CopyShearCombinationsCommand.Execute(null);
        expect.That(sheet.ShearCombinationRows.Count == 4 && sheet.ShearCombinationRows[^1].Name == "Solo taglio", "Taglio: ricopia conserva anche le righe indipendenti");
        sheet.ShearCombinationRows[0].IsSelected = true;
        sheet.MoveShearDownCommand.Execute(null);
        expect.That(sheet.ShearCombinationRows[1].Id == id && sheet.SelectedShearCombinationId == id, "Taglio: ordine e selezione indipendenti");
        sheet.ShearCombinationRows[1].Name = "Taglio A"; sheet.ShearCombinationRows[1].N = "-500";
        await sheet.PendingCalculation;
        expect.That(sheet.Current!.Shear!.Count == 4 && sheet.Current.Shear.All(r => r.Error.Length == 0 && r.Directions.Count == 2),
            "Taglio: due assi per ogni combinazione, senza filtro SL: " + string.Join("; ", sheet.Current.Shear.Select(r => r.Error)));
        var linked = sheet.Current.Shear.Single(r => r.Id == id);
        var (prepared, settings) = ConcreteQuickShear.Prepare(sheet.Data);
        var native = ConcreteShearAnalysis.Calculate(prepared, settings, settings["taglio"]!.AsObject(), ConcreteQuickShear.Combinations(sheet.Data).Single(r => r.S("id") == id));
        expect.That(linked.Directions.Select(r => (r.VRsd, r.VRcd, r.VRd, r.Ratio)).SequenceEqual(native.Shear.Select(r => (r.VRsd, r.VRcd, r.VRd, r.Ratio))),
            "Taglio: risultati identici bit per bit al motore esistente");
        sheet.EffectiveStirrup = false; await sheet.PendingCalculation;
        expect.That(sheet.Current!.Shear!.All(r => r.Error.Contains("diametro")), "Taglio: staffa non effettiva richiede nuova armatura, nessun vecchio esito valido");
        sheet.ShearForm.Set(ConcreteQuickShear.Diameter, "8"); sheet.ShearForm.Set(ConcreteQuickShear.Spacing, "100"); await sheet.PendingCalculation;
        expect.That(sheet.Current!.Shear!.All(r => r.Error.Length == 0) && sheet.Current.Shear.Single(r => r.Id == id).Directions[0].VRsd != linked.Directions[0].VRsd,
            "Taglio: nuova armatura modifica la resistenza a taglio");
        expect.That(sheet.Data["input"]!.ToJsonString() == input && sheet.Data["combinazioni"]!.ToJsonString() == bending
            && sheet.Current.Combinations.Select(r => (r.Id, r.Ratio, r.Passed)).SequenceEqual(flexure), "Taglio: geometria, tabella momenti e risultati a flessione invariati esattamente");
        foreach (string shape in new[] { "Rettangolare", "Circolare", "A T" })
        {
            var alternate = (JsonObject)sheet.Data.DeepClone(); alternate["input"]!["shape"] = shape;
            ConcreteQuickSection.WriteLayer(alternate, 0, "n", "3"); ConcreteQuickSection.WriteLayer(alternate, 0, "phi", "16");
            ConcreteQuickSection.WriteLayer(alternate, 1, "n", "3"); ConcreteQuickSection.WriteLayer(alternate, 1, "phi", "16");
            if (shape == "Circolare") { alternate["input"]!["diameter_mm"] = "1000"; alternate["input"]!["longitudinal_bar_count"] = "12"; alternate["input"]!["longitudinal_bar_diameter_mm"] = "20"; alternate["input"]!["second_inner_enabled"] = false; }
            if (shape == "A T") { alternate["input"]!["web_width_mm"] = "300"; alternate["input"]!["flange_width_mm"] = "600"; alternate["input"]!["flange_thickness_mm"] = "150"; }
            ConcreteQuickSection.ApplyLayers(alternate);
            var before = ConcretePreview.PrepareGeometry(alternate["input"]!.AsObject());
            var actual = ConcretePreview.PrepareGeometry(ConcreteQuickShear.Prepare(alternate).Input);
            expect.That(before.Bars.Select(b => (b.X, b.Y, b.Diametro, b.Area)).SequenceEqual(actual.Bars.Select(b => (b.X, b.Y, b.Diametro, b.Area))), "Taglio: barre longitudinali identiche con armatura indipendente, " + shape);
            if (shape == "Circolare")
            {
                expect.That(ConcreteQuickShear.Calculate(alternate).All(r => r.Error.Contains("circolare")), "Taglio circolare: modello esplicitamente richiesto");
                ConcreteQuickShear.Write(alternate, "modello_circolare", JsonValue.Create("Pile · NTC §7.9.5.2"));
                expect.That(ConcreteQuickShear.Calculate(alternate).All(r => r.Error.Length == 0), "Taglio circolare: modello pile NTC disponibile");
            }
        }
        var pendingLayout = (JsonObject)sheet.Data.DeepClone(); pendingLayout["input"]!["width_mm"] = "400"; pendingLayout["input"]!["height_mm"] = "600";
        foreach (int layer in new[] { 0, 1 }) { ConcreteQuickSection.WriteLayer(pendingLayout, layer, "n", "4"); ConcreteQuickSection.WriteLayer(pendingLayout, layer, "phi", "20"); }
        string pendingSource = pendingLayout.ToJsonString();
        var expectedLayout = ConcreteQuickSection.Geometry(pendingLayout).Geometry!;
        var actualLayout = ConcretePreview.PrepareGeometry(ConcreteQuickShear.Prepare(pendingLayout).Input);
        expect.That(expectedLayout.Bars.Select(b => (b.X, b.Y, b.Diametro)).SequenceEqual(actualLayout.Bars.Select(b => (b.X, b.Y, b.Diametro)))
            && ConcreteQuickShear.Calculate(pendingLayout).All(r => r.Error.Length == 0) && pendingLayout.ToJsonString() == pendingSource,
            "Taglio: ricostruzione degli strati identica a flessione anche chiamando il report direttamente dopo una modifica delle dimensioni, sorgente invariata");
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ANTHEA.sln"))) root = root.Parent;
        var drawing = ConcreteDxfImport.Read(Path.Combine(root!.FullName, "tests/fixtures/dxf/sezione-forata-cm.dxf"));
        var dxf = (JsonObject)sheet.Data.DeepClone();
        var dxfDialog = new ConcreteDxfImportViewModel(dxf["input"]!.AsObject(), drawing); dxf["input"] = dxfDialog.Prepared!.Input.DeepClone();
        expect.That(ConcreteQuickShear.Calculate(dxf).All(r => r.Error.Contains("Manuali")), "Taglio DXF: parametri geometrici manuali richiesti esplicitamente");
        foreach (var (key, value) in new[] { ("parametri", "Manuali"), ("bw_x", "600"), ("d_x", "500"), ("asl_x", "500"), ("bw_y", "400"), ("d_y", "650"), ("asl_y", "500") })
            ConcreteQuickShear.Write(dxf, key, JsonValue.Create(value));
        var dxfBefore = ConcretePreview.PrepareGeometry(dxf["input"]!.AsObject());
        var dxfAfter = ConcretePreview.PrepareGeometry(ConcreteQuickShear.Prepare(dxf).Input);
        expect.That(dxfBefore.Bars.Select(b => (b.X, b.Y, b.Diametro)).SequenceEqual(dxfAfter.Bars.Select(b => (b.X, b.Y, b.Diametro)))
            && dxfBefore.AreaCls == dxfAfter.AreaCls, "Taglio DXF: armatura indipendente conserva barre, contorno e foro");
        expect.That(ConcreteQuickShear.Calculate(dxf).All(r => r.Error.Length == 0), "Taglio DXF: geometria importata verificabile con armatura indipendente");
        var noLinks = (JsonObject)sheet.Data.DeepClone();
        foreach (var row in ConcreteQuickShear.Combinations(noLinks)) ConcreteQuickShear.WriteCombination(noLinks, row.S("id"), ConcreteQuickShear.ConditionField, ConcreteQuickShear.WithoutLinks);
        ConcreteQuickSection.View(noLinks)[ConcreteQuickShear.Key]!["ancoraggio"] = "Da verificare";
        string beforeNoLinks = noLinks.ToJsonString();
        expect.That(ConcreteQuickShear.Calculate(noLinks).All(r => r.Error.Length == 0 && r.Directions.Count == 2)
            && noLinks.ToJsonString() == beforeNoLinks, "Taglio senza staffe: ipotesi di ancoraggio efficace anche nei file precedenti, senza modificare i dati in lettura");
        var (noLinksInput, noLinksSettings) = ConcreteQuickShear.Prepare(noLinks);
        var noLinksAction = ConcreteQuickShear.Combinations(noLinks).First(r => r.S("id") == ConcreteQuickShear.Selected(noLinks));
        var noLinksNative = ConcreteShearAnalysis.Calculate(noLinksInput, noLinksSettings, noLinksSettings["taglio"]!.AsObject(), noLinksAction);
        expect.That(ConcreteQuickShear.Calculate(noLinks).Single(r => r.Id == noLinksAction.S("id")).Directions.Select(r => (r.VRd, r.Ratio))
            .SequenceEqual(noLinksNative.Shear.Select(r => (r.VRd, r.Ratio))), "Taglio senza staffe: risultati identici al motore con barre ancorate");
        var changedLayers = (JsonObject)noLinks.DeepClone(); ConcreteQuickSection.WriteLayer(changedLayers, 0, "n", "3");
        expect.That(ConcreteQuickShear.Calculate(changedLayers).All(r => r.Error.Length == 0), "Taglio: modifica degli strati ricalcola senza nuova conferma di ancoraggio");
        noLinks["input"]!["width_mm"] = "800";
        expect.That(ConcreteQuickShear.Calculate(noLinks).All(r => r.Error.Length == 0), "Taglio: modifica della geometria ricalcola con l’ipotesi di ancoraggio del modulo rapido");
        sheet.ShearForm.Set("rami_x", "0"); await sheet.PendingCalculation;
        expect.That(sheet.Current!.Shear!.All(r => r.Error.Length > 0), "Taglio: rami non validi producono errori, nessun risultato residuo");
        sheet.ShearForm.Set("rami_x", "2"); await sheet.PendingCalculation;
        var report = ConcreteQuickReportAnalysis.Create(sheet.Data).Report;
        expect.That(report.Shear!.Rows.Any(r => r.Name == "Solo taglio") && report.Shear.Rows.Any(r => r.Name == "Taglio A" && r.N == "-500")
            && report.Cases.All(r => r.Name != "Solo taglio"), "Taglio: report usa la tabella autonoma e le sue azioni");
        foreach (bool brief in new[] { false, true })
        {
            var options = brief ? new HashSet<string> { QuickSectionReport.Version, QuickSectionReport.Brief } : QuickSectionReport.Defaults();
            var doc = QuickSectionReport.Create("Prova taglio indipendente", report, options);
            expect.That(doc.GetXml("word/document.xml").Root!.Value.Contains("armatura a taglio indipendente"), "Taglio: distinzione della staffa nel report " + (brief ? "breve" : "completo"));
            File.WriteAllBytes(Path.Combine(folder, brief ? "taglio-breve.docx" : "taglio-completo.docx"), DocxWriter.Write(doc));
        }
        sheet.ToggleShearCommand.Execute(null); await sheet.PendingCalculation;
        expect.That(!sheet.ShearExpanded && sheet.Current!.Shear!.Count == 4, "Taglio: chiusura del pannello conserva azioni e verifiche");
        sheet.Commit(); var document = DocumentService.CreateDocument(ConcreteQuickSection.Module); document["dati"] = sheet.Data.DeepClone();
        string path = Path.Combine(folder, "taglio-indipendente.anthea"); DocumentService.Save(path, document);
        using var reopened = open(DocumentService.Open(path)["dati"]!.AsObject());
        expect.That(JsonNode.DeepEquals(ConcreteQuickShear.Options(reopened.Data), ConcreteQuickShear.Options(sheet.Data)), "Taglio: salvataggio e riapertura conservano tutte le combinazioni e impostazioni");
        var collection = QuickSectionDocuments.Create(ConcreteQuickSection.Module);
        QuickSectionDocuments.Add(collection, "Prima")["dati"] = sheet.Data.DeepClone();
        QuickSectionDocuments.Add(collection, "Seconda")["dati"] = dxf.DeepClone();
        string collectionPath = Path.Combine(folder, "taglio-raccolta.programma"); DocumentService.Save(collectionPath, collection);
        var savedSections = QuickSectionDocuments.Sections(DocumentService.Open(collectionPath));
        expect.That(savedSections.Count == 2 && JsonNode.DeepEquals(ConcreteQuickShear.Options(savedSections[1]["dati"]!.AsObject()), ConcreteQuickShear.Options(dxf)), "Taglio: salvataggio della raccolta conserva anche la sezione non selezionata");
        using var copied = open(ConcreteQuickSection.Create()); copied.CopyFrom(sheet.Data);
        expect.That(JsonNode.DeepEquals(ConcreteQuickShear.Options(copied.Data), ConcreteQuickShear.Options(sheet.Data)), "Taglio: Copia in/da trasferisce dati e impostazioni del pannello");
        sheet.IsReadOnly = true; string locked = sheet.Data.ToJsonString();
        sheet.CopyShearCombinationsCommand.Execute(null); sheet.AddShearCombinationCommand.Execute(null); sheet.MoveShearUpCommand.Execute(null);
        sheet.ShearCombinationRows[0].Vx = "999"; sheet.ShearCombinationRows[0].RemoveCommand.Execute(null); sheet.EffectiveStirrup = true;
        sheet.ShearForm.Set(ConcreteQuickShear.Diameter, "20"); sheet.ToggleShearCommand.Execute(null);
        expect.That(sheet.Data.ToJsonString() == locked && !sheet.CopyShearCombinationsCommand.CanExecute(null), "Taglio: sola lettura protetta anche con invocazione diretta");
        await reopened.PendingCalculation; await copied.PendingCalculation;
    }
}
