namespace Anthea.Application.Presentation;
public static class UtilizationColors
{
    public const string Legend = "η · 0–0,50 blu · 0,50–0,70 verde · 0,70–0,90 giallo · 0,90–1,00 arancio · >1,00 rosso";
    public static string Color(double? value) => value is null || !double.IsFinite(value.Value) ? "#9AA4AF" : value <= .5 ? "#397BCC" : value <= .7 ? "#39A879" : value <= .9 ? "#E4C441" : value <= 1 ? "#EF8C32" : "#CB3939";
}
