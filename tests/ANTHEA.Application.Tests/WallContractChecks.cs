using System.Reflection;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Modules;
using Anthea.Application.Walls;
using X.Core;

internal static class WallContractChecks
{
    internal static int Run(string root)
    {
        int checks = 0, walls = 0, globalCases = 0, schedules = 0;
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
        var comparison = new OwnedProjectionComparison();
        static bool Exact(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
        var inputs = Directory.GetFiles(Path.Combine(root, "tests/fixtures/f3/inputs"), "*.json")
            .Select(path => (Path: path, Fixture: JsonNode.Parse(File.ReadAllText(path))!.AsObject()))
            .Where(p => p.Fixture.S("module") == RetainingWall.Module)
            .Select(p => (Name: Path.GetFileName(p.Path), Data: p.Fixture["data"]!.AsObject())).ToList();
        var globalInput = RetainingWall.Defaults(); globalInput["layers"]![0]!["thickness"] = 25;
        RetainingWall.PrepareGlobalProfile(globalInput);
        var globalSettings = globalInput["global_stability"]!;
        globalSettings["enabled"] = true; globalSettings["profile_confirmed"] = true;
        globalSettings["search_mode"] = "Assegnata"; globalSettings["depth_min"] = .1;
        globalInput["seismic"]!["source"] = RetainingWall.SeismicManual;
        inputs.Add(("globale", globalInput));
        Check(WallModel.DeriveSeismic(globalInput) is null && RetainingWall.DeriveSeismic(globalInput) is null, "Coefficienti sismici assegnati: derivazione opzionale persa");
        foreach (var (name, data) in inputs)
        {
            string before = data.ToJsonString();
            var calculation = ModuleServices.Get(RetainingWall.Module).Calculate(data);
            var native = calculation.NativeAs<RetainingWall.Result>(); var view = WallModel.Read(calculation); walls++;
            if (name == "globale") Check(native.GlobalError is null, "Fixture globale non valida: " + native.GlobalError);
            comparison.Compare(native, view, name);
            Check(ReferenceEquals(view, WallModel.Read(calculation)) && data.ToJsonString() == before, "Vista ricreata o input mutato");
            Check(JsonNode.DeepEquals(native.Json(), view.Json()), "Risultato JSON cambiato");
            Check(ReportRetainingWall.Csv(native) == WallExports.Csv(view), "CSV verifiche diverso");
            comparison.Compare(ReportRetainingWall.Envelope(native.Checks.Concat(native.Structural)).ToArray(), WallExports.Envelope(view.Checks.Concat(view.Structural)).ToArray(), "Inviluppo verifiche");
            for (int i = 0; i < native.Cases.Count; i++)
            {
                var a = native.Cases[i]; var b = view.Cases[i];
                double height = native.Input["geometry"].D("height") + native.Input["geometry"].D("slab");
                var original = RetainingWall.Integrate(a.Pressures, 0, height, height); var shown = WallModel.Integrate(b.Pressures, 0, height, height);
                Check(Exact(original.Force, shown.Force) && Exact(original.Moment, shown.Moment), "Lettura delle spinte cambiata");
                Check(new[] { 0d, native.Width / 2, native.Width }.All(x => Exact(RetainingWall.Pressure(a.Contact, x), WallModel.Pressure(b.Contact, x))), "Legge di contatto diversa");
            }
            if (native.GlobalStability is { } global)
            {
                globalCases += global.Cases.Length;
                Check(ReportRetainingWall.GlobalCsv(global) == WallExports.GlobalCsv(view.GlobalStability!), "CSV globale diverso");
                foreach (var pair in global.Cases.Zip(view.GlobalStability!.Cases).Where(p => p.First.Critical is not null))
                {
                    var a = pair.First.Critical!.Circle; var b = pair.Second.Critical!.Circle;
                    Check(Exact(a.Base((a.Left + a.Right) / 2), b.Base((b.Left + b.Right) / 2)), "Profilo circolare modificato");
                }
                double x = global.Section.Surface[0].X;
                Check(Exact(Anthea.Calculations.Geotechnics.Slope.Height(global.Section.Surface, x), Slope.Height(view.GlobalStability.Section.Surface, x)), "Interrogazione profilo diversa");
            }
            if (native.Input.S("family") == "cantilever")
            {
                var expected = RetainingWall.CalculateBarSchedule(native); var actual = WallModel.CalculateBarSchedule(view);
                comparison.Compare(expected, actual, "Distinta ferri"); schedules++;
                Check(ReportRetainingWall.BarScheduleCsv(expected) == WallExports.BarScheduleCsv(actual), "CSV ferri diverso");
            }
            var other = WallModel.Read(new(calculation.ModuleId, calculation.Data, calculation.Values, native));
            view.Input["only_view"] = true; view.Cases[0].Factors["only_view"] = true; view.Cases.Clear();
            Check(native.Input["only_view"] is null && native.Cases.Count > 0 && native.Cases[0].Factors["only_view"] is null
                && other.Input["only_view"] is null && other.Cases.Count == native.Cases.Count, "Copie condivise con il motore");
        }
        Check(walls >= 5 && globalCases > 0 && schedules > 0 && comparison.Values > 10000, $"Copertura muri insufficiente: {walls} casi, {globalCases} combinazioni globali, {schedules} distinte, {comparison.Values} valori");
        bool NativeType(Type t) => t.Assembly == typeof(RetainingWall).Assembly || t.Assembly.GetName().Name!.StartsWith("GPC")
            || t.HasElementType && NativeType(t.GetElementType()!) || t.IsGenericType && t.GenericTypeArguments.Any(NativeType);
        foreach (var t in typeof(WallModel).Assembly.GetExportedTypes().Where(t => t.Namespace == "Anthea.Application.Walls"))
            Check(t.GetProperties().All(p => !NativeType(p.PropertyType))
                && t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .All(m => !NativeType(m.ReturnType) && m.GetParameters().All(p => !NativeType(p.ParameterType))), "API muri espone il motore: " + t.Name);
        Console.WriteLine($"Muri: {comparison.Values} valori esatti, {walls} fixture, {globalCases} combinazioni globali e {schedules} distinte");
        return checks;
    }
}
