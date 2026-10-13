using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Native = Anthea.Calculations.RebarMaterial;
namespace Anthea.Application.Materials;

public static class RebarEditor
{
    public const string Module = Native.Module;
    public sealed record Preset(string Name, string Surface, double? A5, JsonObject Input);
    public sealed class Values
    {
        public double Fy { get; }
        public double Fu { get; }
        public double E { get; }
        public double Gamma { get; }
        public double EpsilonU { get; }
        public double Fyd { get; }
        public double EpsilonY { get; }
        public double EpsilonYd { get; }
        public double Ratio { get; }
        internal Values(Native.Values value)
        {
            Fy = value.Fy;
            Fu = value.Fu;
            E = value.E;
            Gamma = value.Gamma;
            EpsilonU = value.EpsilonU;
            Fyd = value.Fyd;
            EpsilonY = value.EpsilonY;
            EpsilonYd = value.EpsilonYd;
            Ratio = value.Ratio;
        }
    }
    private static Preset Copy(Native.Preset value) => new(value.Name, value.Surface, value.A5, (JsonObject)value.Input.DeepClone());
    public static Preset[] Catalog() => Native.Catalog().Select(Copy).ToArray();
    public static Preset? Match(JsonObject input) => Native.Match(input) is { } match ? Copy(match) : null;
    public static void Select(JsonObject input, string name) => Native.Select(input, name);
    public static Values Read(ModuleCalculation calculation) => new(calculation.NativeAs<Native.Values>());
}
