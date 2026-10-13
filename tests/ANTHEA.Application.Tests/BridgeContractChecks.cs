using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Modules;
using Anthea.Application.Bridge;
using NativeDerived = GPC.Checkers.CompositeBridge.BridgeDerivedResults;

internal static class BridgeContractChecks
{
    internal static int Run()
    {
        int checks = 0, values = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        static bool Exact(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
        void Compare(object? native, object? copy, string path)
        {
            if (native is null || copy is null) { if (native != copy) throw new Exception(path + ": null cambiato"); return; }
            if (native is double number) { if (!Exact(number, (double)copy)) throw new Exception(path + ": valore cambiato"); values++; return; }
            if (native is string || native is bool || native is int) { if (!native.Equals(copy)) throw new Exception(path + ": dato cambiato"); values++; return; }
            if (native.GetType().IsEnum) { if (native.ToString() != copy.ToString() || Convert.ToInt64(native) != Convert.ToInt64(copy)) throw new Exception(path + ": enumerazione cambiata"); return; }
            if (ReferenceEquals(native, copy)) throw new Exception(path + ": oggetto del motore esposto alla vista");
            if (native is IEnumerable sequence && copy is IEnumerable projection)
            {
                var a = sequence.Cast<object?>().ToArray(); var b = projection.Cast<object?>().ToArray();
                if (a.Length != b.Length) throw new Exception(path + ": conteggio cambiato");
                for (int i = 0; i < a.Length; i++) Compare(a[i], b[i], path + "[" + i + "]");
                return;
            }
            foreach (var p in copy.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.Name == "NeutralAxis") continue; // Compared separately with the original axis adapter.
                var sourceProperty = native.GetType().GetProperty(p.Name) ?? throw new Exception(path + ": campo inatteso " + p.Name);
                Compare(sourceProperty.GetValue(native), p.GetValue(copy), path + "." + p.Name);
            }
        }
        foreach (string method in BridgeSection.CalculationMethods)
        {
            var input = BridgeSection.Defaults(); input["metodo_analisi"] = method; input["classe4"] = false;
            var calculation = ModuleServices.Get(BridgeSection.Module).Calculate(input);
            var native = calculation.NativeAs<BridgeResult>(); var view = BridgeResultView.Read(calculation);
            Check(ReferenceEquals(view, BridgeResultView.Read(calculation)), "La vista ricalcola o ricrea lo stesso risultato");
            Check(JsonNode.DeepEquals(native.Json(), view.Json()), "Esportazione diversa dal risultato nativo");
            Compare(native.Geometry, view.Geometry, "Geometria"); Compare(native.Materials, view.Materials, "Materiali");
            Compare(native.Stages, view.Stages, "Fasi");
            Check(true, "Geometria, materiali, fasi e verifiche identici bit per bit");
            for (int i = 0; i < view.Stages.Count; i++)
            {
                var a = native.Stages[i]; var b = view.Stages[i];
                Compare(a.GetHistory(), b.GetHistory(), "Storico");
                var axis = SectionNeutralAxis.Bridge(a);
                Check(axis.Caption == b.NeutralAxis.Caption && axis.Explanation == b.NeutralAxis.Explanation &&
                    ((axis.Line is null && b.NeutralAxis.Line is null) || (axis.Line is { } line && b.NeutralAxis.Line is { } own &&
                        Exact(line.Start.X, own.Start.X) && Exact(line.Start.Y, own.Start.Y) && Exact(line.End.X, own.End.X) && Exact(line.End.Y, own.End.Y))), "Asse neutro dello storico cambiato");
                bool equal = a.Contributions.Zip(b.Contributions).All(pair =>
                    Exact(NativeDerived.Curvature(pair.First, native.Materials.Ea), pair.Second.Curvature(view.Materials.Ea)) &&
                    new[] { -native.Geometry.Height, 0d, native.Geometry.SlabHeight }.All(y =>
                        Exact(pair.First.SteelStress(y), pair.Second.SteelStress(y)) && new[] { "CLS", "Armatura" }.All(material =>
                            Exact(pair.First.Stress(material, y), pair.Second.Stress(material, y)))));
                Check(equal, "Letture dei diagrammi non identiche");
            }
            var second = BridgeResultView.Read(new ModuleCalculation(calculation.ModuleId, calculation.Data, calculation.Values, native));
            view.Input["classe_cls"] = "modificato"; view.Geometry.Bars[0] = view.Geometry.Bars.Last();
            view.Stages[0].Warnings.Add("solo vista"); view.Stages.Clear();
            Check(native.Input.S("classe_cls") != "modificato" && !native.Stages[0].Warnings.Contains("solo vista") && second.Stages.Count == native.Stages.Count
                && second.Input.S("classe_cls") != "modificato" && !second.Stages[0].Warnings.Contains("solo vista"), "Proiezioni o risultato nativo condividono dati modificabili");
        }
        var responseInput = BridgeSection.Defaults(); var request = BridgeSection.ResponseDefaults(); request["punti"] = 5;
        var response = StructuralQueries.BridgeResponse(responseInput, request);
        var reference = BridgeSection.CalculateResponse(responseInput, request);
        Compare(reference, response, "Curva");
        Check(BridgeEditor.ResponseCsv(response) == BridgeSection.ResponseCsv(reference), "CSV della curva modificato");
        var choices = BridgeEditor.CalculationMethods; choices[0] = "modificato";
        Check(BridgeEditor.CalculationMethods[0] == BridgeSection.CalculationMethods[0], "Catalogo condiviso modificabile");
        Check(values > 5000, "Copertura della proiezione insufficiente");
        bool NativeType(Type t) => t.Assembly.GetName().Name!.StartsWith("GPC") || t.Assembly == typeof(BridgeResult).Assembly
            || t.HasElementType && NativeType(t.GetElementType()!) || t.IsGenericType && t.GenericTypeArguments.Any(NativeType);
        foreach (var t in typeof(BridgeResultView).Assembly.GetExportedTypes().Where(t => t.Namespace == "Anthea.Application.Bridge"))
        {
            Check(t.GetProperties().All(p => !NativeType(p.PropertyType)) && t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .All(m => !NativeType(m.ReturnType) && m.GetParameters().All(p => !NativeType(p.ParameterType))), "API pubblica ponte espone il motore: " + t.Name);
        }
        Console.WriteLine($"Bridge: {values} valori originali confrontati esattamente");
        return checks;
    }
}
