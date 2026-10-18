using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Materials;
using Anthea.Application.Presentation;
using Anthea.Calculations;

internal static class EditorContractChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); checks++; }
        var comparison = new OwnedProjectionComparison();
        // New Application-owned modules have no counterpart in the frozen legacy catalog.
        comparison.Compare(ModuleCatalog.All, ModuleDirectory.All.Where(m => m.Id != Anthea.Application.Stratigraphy.StratigraphyDocument.Module && m.Id != Anthea.Application.Actions.SpectraDocument.Module).ToArray(), "Catalogo");
        foreach (var module in ModuleDirectory.All)
        {
            Check(ReferenceEquals(module, ModuleDirectory.Get(module.Id)), "Identità dei metadati del catalogo");
            var data = ModuleDirectory.CreateData(module.Id); ModuleDirectory.ValidateData(module.Id, data);
            data["only_view"] = true;
            Check(ModuleDirectory.CreateData(module.Id)["only_view"] is null, "Nuovo foglio condivide input precedenti");
        }
        var presets = RebarEditor.Catalog(); comparison.Compare(RebarMaterial.Catalog(), presets, "Acciai");
        foreach (var preset in presets)
        {
            var first = RebarMaterial.Defaults(); var second = (JsonObject)first.DeepClone();
            RebarMaterial.Select(first["input"]!.AsObject(), preset.Name); RebarEditor.Select(second["input"]!.AsObject(), preset.Name);
            Check(JsonNode.DeepEquals(first, second), "Selezione materiale cambiata");
            comparison.Compare(RebarMaterial.Match(first["input"]!.AsObject()), RebarEditor.Match(second["input"]!.AsObject()), "Riconoscimento");
            // Historical A5 is never a constitutive strain; supply the independent user input explicitly.
            if (string.IsNullOrWhiteSpace(second["input"].S("steel_eps_u"))) second["input"]!["steel_eps_u"] = 100;
            var calculation = ModuleServices.Get(RebarMaterial.Module).Calculate(second);
            comparison.Compare(calculation.NativeAs<RebarMaterial.Values>(), RebarEditor.Read(calculation), "Valori acciaio");
            preset.Input["only_view"] = true;
            Check(RebarEditor.Catalog().All(p => p.Input["only_view"] is null), "Catalogo acciai condiviso");
        }
        void Copy(string[] owned, string[] native, Func<string[]> again)
        { Check(owned.SequenceEqual(native), "Catalogo modificato"); string first = native[0]; owned[0] = "only_view"; Check(native[0] == first && again()[0] == first, "Catalogo mutabile condiviso"); }
        Copy(ElasticPileEditor.ReinforcementKeys, ElasticHorizontalPile.ReinforcementKeys, () => ElasticPileEditor.ReinforcementKeys);
        Copy(ElasticPileEditor.InnerRingKeys, ElasticHorizontalPile.InnerRingKeys, () => ElasticPileEditor.InnerRingKeys);
        Copy(HorizontalPileGroupEditor.MethodNames, HorizontalPileGroup.MethodNames, () => HorizontalPileGroupEditor.MethodNames);
        Copy(HorizontalPileGroupEditor.LayoutNames, HorizontalPileGroup.LayoutNames, () => HorizontalPileGroupEditor.LayoutNames);
        var input = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/geo_palo_orizzontale--single-soil-elastic.json")))!["data"]!.AsObject();
        HorizontalSheetPresentation.PrepareReport(input); var copy = (JsonObject)input.DeepClone();
        ElasticHorizontalPile.PrepareShared(input); ElasticPileEditor.PrepareShared(copy);
        ElasticHorizontalPile.PrepareReinforcement(input); ElasticPileEditor.PrepareReinforcement(copy);
        Check(JsonNode.DeepEquals(input, copy), "Preparazione palo cambiata");
        foreach (var segment in input["elastico"]!.Array("tratti").OfType<JsonObject>())
            Check(JsonNode.DeepEquals(ElasticHorizontalPile.SegmentSection(input, segment), ElasticPileEditor.SegmentSection(copy, segment)), "Sezione del tratto cambiata");
        Check(JsonNode.DeepEquals(PaloOrizzontale.References(), HorizontalPileEditor.References()), "Riferimenti del palo cambiati");
        Console.WriteLine($"Editor: {comparison.Values} valori esatti nei cataloghi e materiali");
        return checks;
    }
}
