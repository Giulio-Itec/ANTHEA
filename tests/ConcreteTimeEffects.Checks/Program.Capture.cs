using System.IO;
using System.Globalization;
using System.Text.Json;
using Anthea.Presentation.Modules.Rheology;

internal static partial class Program
{
    private static void CaptureHistory(string output)
    {
        var cases = new List<object>();
        long[] Bits(ConcreteTimePointView p) => [BitConverter.DoubleToInt64Bits(p.AgeDays), BitConverter.DoubleToInt64Bits(p.CreepCoefficient), BitConverter.DoubleToInt64Bits(p.EcmMpa), BitConverter.DoubleToInt64Bits(p.ModulusFactor)];
        foreach (var item in new[] { (12d, 40d, 20d, .5, 20), (25d, 50d, 200d, 7d, 160), (35d, 70d, 560d, 10d, 37), (90d, 100d, 1500d, 28d, 1000) })
        for (int cement = 0; cement < 3; cement++)
        {
            string Text(double x) => x.ToString("G17", CultureInfo.InvariantCulture);
            var vm = new ConcreteTimeEffectsViewModel(item.Item1) { Humidity = Text(item.Item2), NotionalSize = Text(item.Item3), LoadingAge = Text(item.Item4), Samples = Text(item.Item5) };
            vm.Cement = vm.Cements[cement]; vm.CalculateCommand.Execute(null);
            Check(vm.HasResults, "historic capture valid case " + cases.Count);
            var readings = new List<long[]>();
            foreach (double age in new[] { .5, 7, 17, 28, 365, 10000 }) { vm.QueryAge = Text(age); readings.Add(Bits(vm.QueryPoint!)); }
            cases.Add(new { Fck = item.Item1, Humidity = item.Item2, Size = item.Item3, LoadingAge = item.Item4, Samples = item.Item5, Cement = cement, Infinity = BitConverter.DoubleToInt64Bits(vm.CreepInfinity!.Value), Points = vm.Points.Select(Bits).ToArray(), Readings = readings });
        }
        string text = JsonSerializer.Serialize(cases, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(output, "historic-capture.json"), text);
        string reference = Path.Combine(AppContext.BaseDirectory, "historic-capture.json");
        Check(File.Exists(reference), "frozen pre-relocation capture is present");
        Check(text == File.ReadAllText(reference), "all historic sampled values and point readings remain bit for bit identical");
    }
}
