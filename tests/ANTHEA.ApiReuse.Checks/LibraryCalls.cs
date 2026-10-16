using System.Reflection;
using System.Reflection.Emit;
using Anthea.Calculations;

internal static class LibraryCalls
{
    // Read real IL calls: merely mentioning/importing a GPC type does not satisfy the check.
    public static void Check()
    {
        Require(typeof(ConcreteBond), "Calculate", "GPC.Checkers.Concrete.Detailing.AnchorageCalculator", "Bond");
        Require(typeof(ConcreteBond), "Strength", "GPC.Checkers.Concrete.Detailing.AnchorageCalculator", "BondStrength");
        Require(typeof(ConcreteAnchorageCalculator), "Calculate", "GPC.Checkers.Concrete.Detailing.AnchorageCalculator", "Calculate");
        Require(typeof(ConcreteDetailingCalculator), "Calculate", "GPC.Checkers.Concrete.Detailing.MemberDetailingCalculator", "Calculate");
        Require(typeof(MomentCurvatureCalculator), "Calculate", "GPC.Checkers.Concrete.Response.MomentCurvatureAnalysis", "Calculate");
        Require(typeof(TensionBarSpacing), "Maximum", "GPC.Checkers.Concrete.Cracking.CrackSectionGeometry", "MaximumSpacing");
        Require(typeof(LegacySectionRegions), "Clip", "GPC.Checkers.Concrete.Cracking.CrackSectionGeometry", "Clip");
        Require(typeof(LegacySectionRegions), "Region", "GPC.Checkers.Concrete.Cracking.CrackSectionGeometry", "Region");
        Require(typeof(RebarMaterial.Values), "get_Fyd", "GPC.Model.Materials.SteelMaterial", "CalculateFyd");
        Require(typeof(RebarMaterial.Values), "get_EpsilonYd", "GPC.Model.Materials.SteelMaterial", "CalculateDesignYieldingStrainTension");
        // CreepFromModularRatio è uscita dal prodotto (decisione D3, voce T7): la usavano solo le prove, non c'è più facciata da controllare.
        foreach (string name in new[] { "Resolve", "ModularRatio" })
            Require(typeof(Homogenization), name, "GPC.Checkers.Concrete.Serviceability.Homogenization", name);
        if (ConcreteServiceabilityAdapter.EngineName != "Library") throw new Exception("SLE production engine is not Checker");
    }

    private static void Require(Type facade, string methodName, string libraryType, string libraryMethod)
    {
        var method = facade.GetMethod(methodName) ?? throw new Exception(facade.Name + "." + methodName + " missing");
        if (!Calls(method).Any(c => c.DeclaringType?.FullName == libraryType && c.Name == libraryMethod
            && c.DeclaringType.Assembly.GetName().Name!.StartsWith("GPC", StringComparison.Ordinal)))
            throw new Exception(facade.Name + "." + methodName + " does not call " + libraryType + "." + libraryMethod);
    }

    private static IEnumerable<MethodBase> Calls(MethodInfo method)
    {
        var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode))
            .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(c => unchecked((ushort)c.Value));
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? throw new Exception("Missing method body");
        for (int i = 0; i < il.Length;)
        {
            ushort code = il[i++]; if (code == 0xfe) code = (ushort)(0xfe00 | il[i++]);
            var op = codes[code];
            if (op.OperandType == OperandType.InlineMethod)
            {
                var called = method.Module.ResolveMethod(BitConverter.ToInt32(il, i));
                if (called is not null) yield return called;
            }
            i += op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                _ => 4
            };
        }
    }
}
