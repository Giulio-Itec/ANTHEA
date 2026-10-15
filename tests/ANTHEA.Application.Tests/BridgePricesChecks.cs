using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.BridgeDesign;
using Anthea.Calculations;
using X.Core;

internal static class BridgePricesChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        void Reject(Action action) { try { action(); } catch (ArgumentException) { checks++; return; } throw new InvalidOperationException("Invalid catalogue accepted"); }
        var source = BridgeConcept.Defaults(); string before = source.ToJsonString();
        var vm = new PriceCatalogueViewModel(source);
        Check(vm.CanApply && vm.Estimate!.Complete, "Legacy rates not previewable");
        vm.ApplyCommand.Execute(null);
        Check(source.ToJsonString() == before && vm.Result is not null, "Draft modified caller");
        var adopted = (JsonObject)source.DeepClone(); adopted["pricing"] = vm.Result!.DeepClone();
        var old = BridgeConcept.Calculate(source); var updated = BridgeConcept.Calculate(adopted);
        Check(Math.Abs(old.TotalCost - updated.TotalCost) <= old.TotalCost * 1e-10, "Legacy prices changed when adopted");
        Check(old.Carbon == updated.Carbon && old.Inertia == updated.Inertia, "Price adoption changed physics");
        Check(adopted["pricing"]!["history"]!.AsArray().Count == 1, "Legacy rates not retained in history");
        var catalogue = JsonSerializer.Deserialize<PriceCatalogue>(vm.ExportCatalogue())!;
        var doubled = catalogue with { Name = "Secondo prezzario", Edition = "2027", Entries = catalogue.Entries.Select(e => e with { Price = e.Price * 2 }).ToArray() };
        var changed = new PriceCatalogueViewModel(adopted); changed.Import(JsonSerializer.SerializeToUtf8Bytes(doubled), "doubled.json");
        Check(changed.CanApply, changed.Status);
        changed.ApplyCommand.Execute(null); var next = (JsonObject)adopted.DeepClone(); next["pricing"] = changed.Result!.DeepClone();
        var r = BridgeConcept.Calculate(next);
        Check(Math.Abs(r.TotalCost - old.TotalCost * 2) <= r.TotalCost * 1e-10, "New prices not applied by engine");
        Check(r.Carbon == old.Carbon && r.Inertia == old.Inertia, "Repricing changed structural or carbon result");
        Check(BridgeConceptExport.Csv(r).Contains(r.PriceRevision!), "CSV misses calculation price revision");
        using (var report = new ZipArchive(new MemoryStream(BridgeConceptExport.Report("Price test", next, r))))
        using (var reader = new StreamReader(report.GetEntry("word/document.xml")!.Open()))
        {
            string xml = reader.ReadToEnd(); Check(xml.Contains("Secondo prezzario") && xml.Contains(r.PriceRevision!), "Report price provenance missing");
        }
        Check(changed.Result!["snapshot"]!.S("Revision") != vm.Result!["snapshot"]!.S("Revision"), "Price revision not changed");
        Check(Anthea.Serialization.BridgeEnvironmentData.Revision(next) == Anthea.Serialization.BridgeEnvironmentData.Revision(source), "Price change changed carbon revision");
        Anthea.Serialization.BridgeEnvironmentData.Synchronize(next);
        string environmentalRevision = next["environment"]!.S("revision");
        var environmentalBefore = BridgeConcept.Calculate(next);
        next["assumptions"]!["co2_concrete"] = 400;
        // Actual inputs, rather than stale metadata, identify the result even before the editor synchronizes.
        var environmentalAfter = BridgeConcept.Calculate(next);
        Check(environmentalAfter.EnvironmentRevision != environmentalRevision && environmentalBefore.EnvironmentRevision == environmentalRevision, "Environmental revision became stale");
        Check(environmentalAfter.TotalCost == environmentalBefore.TotalCost && environmentalAfter.Carbon != environmentalBefore.Carbon, "Environmental change contaminated prices");
        Anthea.Serialization.BridgeEnvironmentData.Synchronize(next);
        Check(next["environment"]!["history"]!.AsArray().Count == 1 && next["environment"]!["history"]![0]!.S("revision") == environmentalRevision, "Previous environmental factors were not archived");
        Anthea.Serialization.BridgeEnvironmentData.Synchronize(next);
        Check(next["environment"]!["history"]!.AsArray().Count == 1, "Unchanged factors create spurious revisions");
        Check(next["pricing"]!["snapshot"]!.S("Revision") == changed.Result!["snapshot"]!.S("Revision"), "CO2 edit changed price revision");
        Check(BridgeConceptExport.Csv(environmentalAfter).Contains(environmentalAfter.EnvironmentRevision!), "CSV misses environmental revision");
        var roundtrip = new PriceCatalogueViewModel(JsonNode.Parse(next.ToJsonString())!.AsObject());
        Check(roundtrip.CanApply && roundtrip.History.Count == 2, "Price archive round trip failed");
        roundtrip.SelectedRevision = roundtrip.History[0]; roundtrip.RestoreRevisionCommand.Execute(null);
        Check(roundtrip.CanApply && Math.Abs((double)roundtrip.Estimate!.Total!.Value - old.TotalCost) < old.TotalCost * 1e-10, "History restore did not recover original costs");
        var pinnedData = (JsonObject)source.DeepClone(); pinnedData["alternative_a"] = source.DeepClone();
        pinnedData["alternative_a"]!["rates"]!["rebar"] = 1111;
        var pin = new PriceCatalogueViewModel(pinnedData) { RepricePinnedAlternative = true };
        pin.Import(JsonSerializer.SerializeToUtf8Bytes(doubled), "second.json"); pin.ApplyCommand.Execute(null); pin.CommitTo(pinnedData);
        Check(pinnedData["alternative_a"]!["pricing"]!["snapshot"]!.S("Revision") == pinnedData["pricing"]!["snapshot"]!.S("Revision"), "Pinned alternative not repriced on request");
        Check(Math.Abs(BridgeConcept.Calculate(pinnedData["alternative_a"]!.AsObject()).TotalCost - old.TotalCost * 2) < old.TotalCost * 1e-10, "Pinned estimate still uses old rates");
        Check(pinnedData["alternative_a"]!["pricing"]!["history"]![0]!["snapshot"]!["Rates"]!.AsArray().Single(q => q!.S("WorkCode") == "rebar")!.S("Price") == "1111", "Pinned history replaced by main bridge prices");
        var badAlternative = (JsonObject)adopted.DeepClone(); badAlternative["alternative_a"] = source.DeepClone(); badAlternative["alternative_a"]!["input"]!["length"] = "invalid";
        var invalidPin = new PriceCatalogueViewModel(badAlternative); Check(invalidPin.CanApply, "Unselected bad alternative blocks main bridge pricing");
        invalidPin.RepricePinnedAlternative = true; Check(!invalidPin.CanApply, "Invalid pinned alternative did not disable apply");
        var overrideVm = new PriceCatalogueViewModel(adopted); var steel = overrideVm.Mappings.Single(m => m.WorkCode == "rebar");
        steel.OverridePrice = "1234"; Check(!overrideVm.CanApply, "Unexplained override accepted"); steel.Reason = "Offerta specifica";
        Check(overrideVm.CanApply && steel.Status.StartsWith("Personalizzato"), "Valid override unavailable");
        overrideVm.KeepOverrides = true; overrideVm.Import(JsonSerializer.SerializeToUtf8Bytes(doubled), "new.json");
        Check(steel.OverridePrice == "1234", "Requested override carryover lost");
        overrideVm.KeepOverrides = false; overrideVm.Import(JsonSerializer.SerializeToUtf8Bytes(catalogue), "old.json");
        Check(steel.OverridePrice.Length == 0, "Override silently carried across catalogue");
        var missing = new PriceCatalogueViewModel(adopted); missing.Mappings.Single(m => m.WorkCode == "concrete_deck").EntryCode = "does not exist";
        Check(!missing.CanApply && missing.Estimate?.Total is null, "Unpriced estimate accepted");
        var incompatible = new PriceCatalogueViewModel(adopted); incompatible.Entries.Single(e => e.Code == "concrete_deck").Unit = "m";
        Check(!incompatible.CanApply, "Incompatible unit accepted");
        var kg = new PriceCatalogueViewModel(adopted); var rebar = kg.Entries.Single(e => e.Code == "rebar"); rebar.Price = "1,66"; rebar.Unit = "kg";
        Check(kg.CanApply && kg.Mappings.Single(m => m.WorkCode == "rebar").ResolvedPrice == "1660", "kg/t conversion incorrect");
        var inclusions = new PriceCatalogueViewModel(adopted);
        foreach (var e in inclusions.Entries.Where(e => e.Code.StartsWith("pile_"))) e.Includes = "rebar";
        Check(inclusions.CanApply && inclusions.Estimate!.Total < vm.Estimate!.Total, "Pile inclusion did not reduce only assigned costs");
        Check(inclusions.Estimate!.Lines.Any(l => l.IncludedBy is not null) && inclusions.Estimate.Lines.Any(l => l.WorkCode == "rebar" && l.Cost > 0), "Inclusion hid all reinforcement");
        string csv = "Codice;Descrizione;Unita;Prezzo;Inclusioni\r\nA;\"Cls; speciale\";m³;200,5;\r\nB;Acciaio;t;;\r\n";
        var imported = PriceCatalogueImport.Csv(Encoding.UTF8.GetBytes(csv), "Test");
        Check(imported.Entries[0].Description == "Cls; speciale" && imported.Entries[0].Price == 200.5m && imported.Entries[1].Price is null, "CSV quotes, comma or null prices lost");
        Reject(() => PriceCatalogueImport.Csv(Encoding.UTF8.GetBytes(csv + "A;Duplicato;m;2;\r\n"), "Test"));
        Reject(() => PriceCatalogueImport.Number("1.234,5")); Reject(() => PriceCatalogueImport.Number("-1"));
        Reject(() => PriceCatalogueImport.Csv(Encoding.UTF8.GetBytes("Codice;Descrizione;Unita;Prezzo;Inclusioni\nA;\"unterminated"), "Bad"));
        var excel = PriceCatalogueImport.Excel(Workbook(), "Excel");
        Check(excel.Entries.Length == 1 && excel.Entries[0].Code == "A" && excel.Entries[0].Price == 200.5m, "Shared Excel reader price import failed");
        return checks;
    }
    private static byte[] Workbook()
    {
        using var data = new MemoryStream(); data.Write(SectionActionsExcel.Template());
        using (var zip = new ZipArchive(data, ZipArchiveMode.Update, true))
        {
            string Read(string name) { using var s = new StreamReader(zip.GetEntry(name)!.Open()); return s.ReadToEnd(); }
            void Write(string name, string content) { zip.GetEntry(name)?.Delete(); using var s = new StreamWriter(zip.CreateEntry(name).Open()); s.Write(content); }
            var book = Read("xl/workbook.xml").Replace("Azioni", "Prezzario"); Write("xl/workbook.xml", book);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            string[][] rows = [PriceCatalogueImport.Headers, ["A", "Calcestruzzo", "m³", "200.5", ""]];
            var sheet = new XElement(ns + "worksheet", new XElement(ns + "sheetData", rows.Select((row, k) => new XElement(ns + "row", new XAttribute("r", k + 1), row.Select((value, col) => new XElement(ns + "c", new XAttribute("r", ((char)('A' + col)).ToString() + (k + 1)), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", value))))))));
            Write("xl/worksheets/sheet1.xml", sheet.ToString());
        }
        return data.ToArray();
    }
}
