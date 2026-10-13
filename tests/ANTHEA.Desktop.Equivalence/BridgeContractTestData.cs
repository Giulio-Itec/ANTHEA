using Anthea.Application.Bridge;
using Anthea.Application.Modules;
using System.Text.Json.Nodes;
namespace X.Desktop;

internal static class BridgeContractTestData
{
    internal static BridgeResultView Project(Anthea.Calculations.BridgeResult source) => BridgeResultView.Read(
        new ModuleCalculation(Anthea.Calculations.BridgeSection.Module, source.Input, new JsonObject(), source));
}
