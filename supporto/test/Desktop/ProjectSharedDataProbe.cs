// Spostato da src/ANTHEA.Application/Projects/ProjectSharedSoils.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: confronto dei
// dati comuni degli strati usato solo da ProjectSharingSmokeChecks. Codice conservato per le prove, fuori dall'installer (solo UiTests).
using System.Reflection;
using System.Text.Json.Nodes;
using X.Core;

namespace X.Desktop;

/// <summary>
/// Accesso di prova ai terreni condivisi di <see cref="ProjectSharedData"/>. Elenco delle proprietà comuni (SoilProperties), valore
/// confrontato (SoilValue) e falda attiva (WaterDataActive) restano privati nel prodotto, dove li usa il confronto dei fogli: si leggono per
/// riflessione, come in tests/Shared/ConcreteContractProbe.cs, così il confronto resta quello del prodotto e non una sua copia.
/// </summary>
internal static class ProjectSharedDataProbe
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;
    static readonly string[] SoilProperties = (string[])typeof(ProjectSharedData).GetField("SoilProperties", Private)!.GetValue(null)!;
    static readonly Func<JsonObject, string, JsonNode?> SoilValue = typeof(ProjectSharedData)
        .GetMethod("SoilValue", Private, [typeof(JsonObject), typeof(string)])!.CreateDelegate<Func<JsonObject, string, JsonNode?>>();
    static readonly Func<JsonObject, JsonObject, bool> WaterDataActive = typeof(ProjectSharedData)
        .GetMethod("WaterDataActive", Private, [typeof(JsonObject), typeof(JsonObject)])!.CreateDelegate<Func<JsonObject, JsonObject, bool>>();

    public static bool SameCommonSoilData(JsonObject first, JsonObject second)
    {
        if (!ProjectSharedData.SupportsSharedSoils(first) || !ProjectSharedData.SupportsSharedSoils(second)) return false;
        var a = first["dati"]!.AsObject().Array("stratigrafie");
        var b = second["dati"]!.AsObject().Array("stratigrafie");
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] is not JsonArray sa || b[i] is not JsonArray sb || sa.Count != sb.Count) return false;
            for (int j = 0; j < sa.Count; j++)
            {
                if (sa[j] is not JsonObject ra || sb[j] is not JsonObject rb) return false;
                foreach (string key in SoilProperties)
                    if ((key != "peso_specifico_saturo" || WaterDataActive(first, second)) && !ProjectSharedData.Equal(SoilValue(ra, key), SoilValue(rb, key))) return false;
            }
        }
        return true;
    }
}
