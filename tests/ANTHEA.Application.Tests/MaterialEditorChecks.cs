using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Materials;
internal static class MaterialEditorChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        var data = ModuleCatalog.CreateData("str_palo"); var workspace = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        foreach (string standard in ConcreteStandards.OrdinaryNames)
        {
            workspace["normativa"] = standard; workspace["coefficienti"] = ConcreteStandards.Defaults(standard);
            var catalog = MaterialInputCatalog.Concrete(standard);
            Check(JsonNode.DeepEquals(J.Node(catalog), J.Node(ConcreteMaterialCatalog.Concrete(standard))), "Catalogo CLS modificato");
            foreach (var (type, material) in new[] { ("Calcestruzzo", catalog.First()), ("Acciaio", MaterialInputCatalog.Steel(false,standard).First()), ("Trefoli", MaterialInputCatalog.Steel(true,standard).First()) })
            {
                string before = material.ToJsonString(); var shown = MaterialEditorPreview.Read(type, material, input, workspace);
                Check(shown.Curves.Characteristic.Length == 241 && shown.Curves.Design.Length == 241 && material.ToJsonString() == before, "Campionamento o input del materiale modificato");
                Check(shown.Curves.Characteristic.Concat(shown.Curves.Design).SelectMany(p => p).All(double.IsFinite), "Curva contiene valori non finiti");
                double saved = shown.Curves.Characteristic[0][1]; shown.Curves.Characteristic[0][1] = 12345;
                Check(MaterialEditorPreview.Read(type,material,input,workspace).Curves.Characteristic[0][1] == saved, "Curve condivise tra finestre");
            }
            catalog[0]["fck_mpa"] = -1;
            Check(MaterialInputCatalog.Concrete(standard)[0].D("fck_mpa") > 0, "Catalogo contaminato da materiale personalizzato");
        }
        var bad = MaterialInputCatalog.Concrete()[0]; bad["fck_mpa"] = -1; bool refused = false;
        try { MaterialEditorPreview.Read("Calcestruzzo",bad,input,workspace); } catch (ArgumentException) { refused = true; }
        Check(refused, "Editor presenta un calcestruzzo invalido");
        return checks;
    }
}
