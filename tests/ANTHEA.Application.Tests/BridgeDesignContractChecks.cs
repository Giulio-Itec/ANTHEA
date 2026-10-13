using System.Reflection;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Modules;
using View = Anthea.Application.BridgeDesign.BridgeDesignModel;

internal static class BridgeDesignContractChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        var comparison = new OwnedProjectionComparison();
        foreach (var family in BridgeConcept.Families)
        {
            var data = BridgeConcept.Defaults(); var input = data["input"]!;
            input["family"] = family.Id; input["length"] = Math.Min(1980, (family.MinSpan + family.MaxSpan) / 2 * (BridgeConcept.HasTowers(family.Id) ? 2 : 3));
            input["spans"] = 3; input["lanes"] = 2; input["height"] = 25;
            input["obstacle"] = "Nessuno"; input["soil"] = "Roccia"; input["foundation"] = "Plinto diretto";
            input["continuous"] = family.Id is not "tied_arch" and not "truss";
            if (family.Id == "suspension") input["deck_type"] = BridgeConcept.DeckTypes[1];
            var calculation = ModuleServices.Get(BridgeConcept.Module).Calculate(data);
            var native = calculation.NativeAs<BridgeConcept.Result>(); var view = View.Read(calculation);
            comparison.Compare(native, view, family.Id);
            comparison.Compare(BridgeConcept.TechnicalSchedule(data, native), View.TechnicalSchedule(data, view), "Distinta tecnica");
            comparison.Compare(BridgeConcept.SpanSchedule(native), View.SpanSchedule(view), "Campate");
            comparison.Compare(BridgeConcept.SupportSchedule(data, native), View.SupportSchedule(data, view), "Sostegni");
            Check(ReferenceEquals(view, View.Read(calculation)), "Proiezione ricreata per lo stesso calcolo");
            Check(JsonNode.DeepEquals(native.Json(), view.Json()), "Risultato esportato diverso");
            Check(View.Csv(view) == X.Core.BridgeConceptExport.Csv(native)
                && View.TechnicalCsv(data, view) == X.Core.BridgeConceptExport.TechnicalCsv(data, native), "CSV diverso");
            double first = native.Spans[0]; view.Spans[0] = -123;
            Check(native.Spans[0] == first && View.Read(new(calculation.ModuleId, calculation.Data, calculation.Values, native)).Spans[0] == first, "Campate condivise con il motore");
        }
        var choices = View.Site; choices[0] = choices.Last();
        Check(View.Site[0].Key == BridgeConcept.Site[0].Key, "Catalogo condiviso");
        comparison.Compare(new BridgeConcept.OptimizationOptions(), new View.OptimizationOptions(), "Opzioni predefinite");
        var originalProgress = new List<BridgeConcept.OptimizationProgress>();
        var shownProgress = new List<View.OptimizationProgress>();
        var source = BridgeConcept.Defaults();
        var originalOptions = new BridgeConcept.OptimizationOptions { KeepFamily = true, KeepSpans = true, KeepDepth = true, KeepSection = true, KeepPier = true, KeepFoundation = true };
        var options = new View.OptimizationOptions { KeepFamily = true, KeepSpans = true, KeepDepth = true, KeepSection = true, KeepPier = true, KeepFoundation = true };
        string before = source.ToJsonString();
        var expected = BridgeConcept.Optimize(source, originalOptions, default, new Capture<BridgeConcept.OptimizationProgress>(originalProgress));
        var actual = BridgeDesignAnalysis.Optimize(source, options, default, new Capture<View.OptimizationProgress>(shownProgress));
        comparison.Compare(expected, actual, "Ottimizzazione");
        comparison.Compare(originalProgress, shownProgress, "Avanzamento");
        Check(expected.Evaluated > 0 && originalProgress.Count > 0 && before == source.ToJsonString(), "Ricerca o progressi non provati, oppure input mutato");
        if (actual.Solutions.Length > 0)
        {
            actual.Solutions[0].Data["input"]!["length"] = -1;
            Check(expected.Solutions[0].Data["input"]!.D("length") > 0 && before == source.ToJsonString(), "Soluzione condivide dati modificabili");
        }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { BridgeDesignAnalysis.Optimize(source, options, cancelled.Token); throw new InvalidOperationException("Annullamento ignorato"); }
        catch (OperationCanceledException) { checks++; }
        bool NativeType(Type t) => t.Assembly == typeof(BridgeConcept).Assembly || t.Assembly.GetName().Name!.StartsWith("GPC")
            || t.HasElementType && NativeType(t.GetElementType()!) || t.IsGenericType && t.GenericTypeArguments.Any(NativeType);
        foreach (var t in typeof(View).GetNestedTypes().Append(typeof(View)))
            Check(t.GetProperties().All(p => !NativeType(p.PropertyType))
                && t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .All(m => !NativeType(m.ReturnType) && m.GetParameters().All(p => !NativeType(p.ParameterType))), "API pubblica espone motori: " + t.Name);
        Check(comparison.Values > 10000, "Copertura insufficiente");
        Console.WriteLine($"Bridge Design: {comparison.Values} valori confrontati esattamente in 14 famiglie e ottimizzazione");
        return checks;
    }
    private sealed class Capture<T>(List<T> values) : IProgress<T> { public void Report(T value) => values.Add(value); }
}
