using System.Text.Json.Nodes;
using Anthea.Calculations;

namespace Anthea.Application.Concrete;

/// <summary>Editor preparation and catalogs. Existing adapters remain the sole source of calculation rules.</summary>
public static class ConcreteEditor
{
    public static string[] ExposureChoices => ConcreteLibraryMapping.ExposureChoices.ToArray();
    public static string[] Sets => SectionWorkspace.Sets.ToArray();
    public static string[] SharedSleFields => SectionWorkspace.SharedSleFields.ToArray();
    public static string[] Standards => ConcreteStandards.OrdinaryNames.ToArray();
    public static string[] ResistanceCriteria => CheckerSection.Criteria.ToArray();
    public static (string Key, string Label)[] Coefficients => ConcreteStandards.Coefficients.ToArray();
    public static (string Input, string Standard)[] CommonCoefficients => ConcreteCalculationSettings.CommonCoefficients.ToArray();
    public static (string Key, string Name)[] DesignTargets => ConcreteReinforcementDesign.Targets.ToArray();
    public static string Label(string key) => SectionWorkspace.Label(key);
    public static double Number(string text, string label) => SectionWorkspace.Number(text, label);
    public static JsonObject PrepareWorkspace(JsonObject data) => SectionWorkspace.Prepare(data);
    public static JsonObject StandardDefaults(string name) => ConcreteStandards.Defaults(name);
    public static string StandardNote(string name) => ConcreteStandards.Note(name);
    public static void PrepareParameters(JsonObject input, JsonObject settings) => ConcreteCalculationSettings.Prepare(input, settings);
    public static void ValidateStirrups(JsonObject input, JsonObject options) => ConcreteCalculationSettings.ValidateStirrups(input, options);
    public static JsonObject PrepareDesign(JsonObject settings) => ConcreteReinforcementDesign.Prepare(settings);
    public static bool HasStressCheck(string set) => SleCheckScope.Stress(set);
    public static bool HasCrackCheck(string set, JsonNode workspace) => SleCheckScope.Cracking(set, workspace);
    public static double ReinforcementArea(double diameter, int count = 1) => ReinforcementGeometry.Area(diameter, count);
    public static double ReinforcementDiameter(double area) => ReinforcementGeometry.EquivalentDiameter(area);
}
