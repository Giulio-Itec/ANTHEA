using System.Text.Json.Nodes;
namespace Anthea.Application.Presentation;
public static class TableValues
{
    public static int Compare(string a, string b) => J.Number(JsonValue.Create(a)) is double na && J.Number(JsonValue.Create(b)) is double nb
        ? na.CompareTo(nb) : StringComparer.CurrentCultureIgnoreCase.Compare(a, b);
}
