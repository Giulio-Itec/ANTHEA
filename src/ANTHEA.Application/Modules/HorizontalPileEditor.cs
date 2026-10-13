using System.Text.Json.Nodes;
using Native = Anthea.Calculations.PaloOrizzontale;
using Group = Anthea.Calculations.HorizontalPileGroup;
namespace Anthea.Application.Modules;

public static class HorizontalPileEditor
{
    public const string Module = Native.Module;
    public const string BromsMethod = Native.BromsMethod;
    public const string StratifiedMethod = Native.StratifiedMethod;
    public static string Csv(JsonObject result) => Native.Csv(result);
    public static JsonArray References() => Native.References();
}

public static class HorizontalPileGroupEditor
{
    public const string Module = Group.Module;
    public static string[] MethodNames => Group.MethodNames.ToArray();
    public static string[] LayoutNames => Group.LayoutNames.ToArray();
    public static void Upgrade(JsonObject data) => Group.Upgrade(data);
    public static void Regenerate(JsonObject data) => Group.Regenerate(data);
    public static void Select(JsonObject result, int method, string direction) => Group.Select(result, method, direction);
}
