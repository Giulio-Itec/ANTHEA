using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Concrete;

internal static class ConcreteDomainContractChecks
{
    internal static int Run(string root)
    {
        int checks = 0, states = 0, cracks = 0, limits = 0, details = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        var comparison = new OwnedProjectionComparison("Raster", "IsRasterCreated", "LimitState", "Mesh", "IsMeshCreated");
        foreach (string file in Directory.GetFiles(Path.Combine(root, "tests/fixtures/f3/inputs"), "str_palo--*.json"))
        {
            var data = JsonNode.Parse(File.ReadAllText(file))!["data"]!.AsObject();
            ConcreteSheetPreparation.PrepareReport(data);
            var input = data["input"]!.AsObject(); var workspace = data["workspace_ca"]!.AsObject();
            var options = workspace["dominio2d"]!.AsObject();
            var session = new ConcreteAnalysisSession();
            var rows = data["combinazioni"]!.Array("SLU").OfType<JsonObject>().Select(r => J.Obj(("id", r.S("id")), ("N", r["azioni"]![0]), ("Mx", r["azioni"]![1]), ("My", r["azioni"]![2]))).ToArray();
            var domain = session.Domain(input, workspace, options, "SLU", false, rows);
            comparison.Compare(domain.Two!.Source, domain.Two, "Domain2D");
            Check(ReferenceEquals(domain.Two, session.Domain(input, workspace, options, "SLU", false, rows).Two), "Proiezione del dominio non riusata");
            foreach (var check in domain.Checks.Values)
            {
                comparison.Compare(check.Source, check, "Check");
                if (check.LimitState is null) continue;
                Check(!check.LimitState.IsValueCreated && !check.Source.LimitState!.IsValueCreated, "Copia anticipa il calcolo del piano limite");
                var state = check.LimitState.Value;
                comparison.Compare(check.Source.LimitState!.Value, state, "LimitState");
                Check(ReferenceEquals(state, check.LimitState.Value), "Piano limite proiettato due volte"); limits++;
            }
            // Use the same solver as the existing view; test the boundary rather than a second formula.
            var native = new CheckerSection(input, workspace, options, "SLU").Stress(new ActionPoint(-100, 10, 5), "ED");
            Check(!native.IsRasterCreated, "Fixture ha già campionato il raster");
            var projection = new ConcreteProjection(); var owned = projection.Read(native)!;
            Check(!native.IsRasterCreated && !owned.IsRasterCreated, "La lettura campiona prematuramente il raster");
            Check(ReferenceEquals(owned, projection.Read(native)) && !ReferenceEquals(owned, new ConcreteProjection().Read(native)), "Identità cache non limitata alla sessione");
            comparison.Compare(native, owned, "StressState");
            var raster = owned.Raster;
            Check(native.IsRasterCreated && raster is not null && ReferenceEquals(raster, owned.Raster), "Raster assente o non memorizzato");
            comparison.Compare(native.Raster, raster, "Raster");
            double first = native.Raster!.Stresses[0]; raster!.Stresses[0] = 123456;
            Check(BitConverter.DoubleToInt64Bits(native.Raster.Stresses[0]) == BitConverter.DoubleToInt64Bits(first), "Raster mutabile condiviso col motore");
            states++;
            var sle = session.Stress(input, workspace, workspace["sle"]!["SLE"]!.AsObject(), "SLE", [J.Obj(("id", "sle"), ("N", "-100"), ("Mx", "10"), ("My", "5"))]);
            foreach (var result in sle.Values)
                if (result.CrackResult is { } crack) { comparison.Compare(crack.Source, crack, "Crack"); cracks++; }
            var detail = ConcreteDetailingPresentation.Calculate(input, workspace, rows);
            foreach (var value in detail.Checks) { comparison.Compare(value.Source, value, "Detailing"); details++; }
            if (detail.Anchorage is { } anchorage) comparison.Compare(anchorage.Source, anchorage, "Anchorage");
        }
        var sample = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(sample);
        sample["input"]!["cover_mm"] = "40"; sample["input"]!["width_mm"] = "400"; sample["input"]!["height_mm"] = "600";
        settings["dettagli_costruttivi"]!["elemento"] = "Trave"; settings["sle_comuni"]!["esposizione"] = "XC1"; settings["ancoraggi"]!["lunghezza"] = "2000";
        var detailed = ConcreteDetailingPresentation.Calculate(sample["input"]!.AsObject(), settings, []);
        foreach (var value in detailed.Checks) { comparison.Compare(value.Source, value, "Detailing"); details++; }
        if (detailed.Anchorage is { } anchored) comparison.Compare(anchored.Source, anchored, "Anchorage");
        Check(details > 0, "Fixture dettagli incompleta: " + detailed.DetailsText);
        var three = new CheckerSection(sample["input"]!.AsObject(), settings, settings["dominio3d"]!.AsObject(), "SLU").Domain3D();
        var nativeCheck = three.Check(new ActionPoint(-100, 10, 5));
        Check(nativeCheck.LimitState is not null, "La verifica 3D non ha un piano limite: " + nativeCheck.Status);
        var ownedCheck = new ConcreteResults.Check(nativeCheck);
        Check(!nativeCheck.LimitState!.IsValueCreated && !ownedCheck.LimitState!.IsValueCreated, "Il contratto anticipa il piano limite 3D");
        var limitState = ownedCheck.LimitState.Value;
        comparison.Compare(nativeCheck.LimitState.Value, limitState, "3D.LimitState"); limits++;
        Check(ReferenceEquals(limitState, ownedCheck.LimitState.Value), "Piano limite 3D duplicato");
        var view = new ConcreteResults.Domain3D(three);
        Check(!three.IsMeshCreated && !view.IsMeshCreated, "Proiezione anticipa la mesh 3D");
        var mesh = view.DisplayMesh(settings["dominio3d"]!.AsObject());
        Check(view.IsMeshCreated && ReferenceEquals(mesh, view.DisplayMesh(settings["dominio3d"]!.AsObject())), "Cache mesh non preservata");
        comparison.Compare(mesh.Source, mesh, "Mesh");
        Check(mesh.Vertices.Count > 0 && mesh.Triangles.Count > 0, "Mesh di prova vuota");
        var vertex = mesh.Source.Vertices[0]; mesh.Vertices[0] = new ConcreteResults.Action(99, 88, 77);
        Check(mesh.Source.Vertices[0] == vertex, "Vertici condivisi con il motore");
        // 4 fixtures str_palo before F5.10; 11 with the seven added in F5.10 (section fixture_aggiunte_f5_10 of the F3 manifest).
        Check(states == 11 && limits > 0 && cracks > 0 && details > 0 && comparison.Values > 10000, $"Copertura domini/stati insufficiente: {states}, {limits}, {cracks}, {details}");
        Console.WriteLine($"Contratti domini e tensioni: {comparison.Values} valori esatti, {states} raster, {limits} piani limite, {cracks} fessurazioni e {details} dettagli");
        return checks;
    }
}
