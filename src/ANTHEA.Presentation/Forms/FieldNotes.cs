using Anthea.Calculations;
using System.Text.Json.Nodes;

namespace Anthea.Presentation.Forms;

/// <summary>Notices shown under the fields (<see cref="FieldSpec.Note"/>), shared by the modules; moved from X.Desktop Ui.cs in F5.2.</summary>
public static class FieldNotes
{
    /// <summary>Stress block: the library accepts a wider tolerance on N for the stepped resultant (registro F2-15, decision of the user of 8/10).</summary>
    public static string? StressBlock(JsonObject values) => values.S("cls_diagramma") == "Stress block"
        ? "Stress block: risultati SLU meno precisi. La risultante a gradini richiede una tolleranza su N più ampia; MRd può scostarsi fino a circa l'1-2 % vicino agli estremi del dominio."
        : null;
}
