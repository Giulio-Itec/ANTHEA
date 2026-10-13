using System.Reflection;
using Anthea.Application.Concrete;
using Anthea.Calculations;
namespace X.Desktop;

/// <summary>Test-only access to retained originals: numerical and cache assertions keep their old oracle.</summary>
internal static class ConcreteContractProbe
{
    private static T Original<T>(object view) => (T)view.GetType().GetProperty("Source", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
    internal static CheckerDomain3D GetNativeForTest(this ConcreteResults.Domain3D view) => Original<CheckerDomain3D>(view);
    internal static CheckerDomain2D GetNativeForTest(this ConcreteResults.Domain2D view) => Original<CheckerDomain2D>(view);
    internal static CheckerStressState GetNativeForTest(this ConcreteResults.StressState view) => Original<CheckerStressState>(view);
    internal static ActionPoint GetNativeForTest(this ConcreteResults.Action view) => new(view.N, view.Mx, view.My);
    internal static ConcreteResults.StressState ViewForTest(this CheckerStressState state)
        => (ConcreteResults.StressState)Activator.CreateInstance(typeof(ConcreteResults.StressState), BindingFlags.Instance | BindingFlags.NonPublic, null, [state], null)!;
}
