using System.Text;
namespace Anthea.Application.Concrete;

public static class ConcreteResponsePresentation
{
    public static string InspectionText(ConcreteResults.StressState state, ConcreteResults.Action force, string title, ConcreteGeometry section)
    {
        var p = state.Source.Native.StrainPlane;
        var b = new StringBuilder(title);
        b.AppendLine($"\nN = {force.N:G8} kN · Mx = {force.Mx:G8} kNm · My = {force.My:G8} kNm");
        b.AppendLine($"\nPIANO DI DEFORMAZIONE · coordinate geometriche in mm\nε(x,y) = ε0 + gx (x − x0) + gy (y − y0)\nε0 = {p.StrainReferencePoint:G9}\nx0 = {p.ReferencePoint.X:G9} mm · y0 = {p.ReferencePoint.Y:G9} mm\ngx = {p.ChiX:G9} mm⁻¹ · gy = {p.ChiY:G9} mm⁻¹\nAsse neutro: ε(x,y) = 0. Deformazioni positive a trazione.");
        b.AppendLine(ResponseSummary(state.Response, "ESTREMI DELLA SOLUZIONE"));
        b.AppendLine("\nBARRE · coordinate geometriche · tensioni MPa · deformazioni ‰");
        for (int i = 0; i < section.Bars.Count; i++) { var bar = section.Bars[i]; b.AppendLine($"B{i+1:00}: ({bar.X:0.###}; {bar.Y:0.###}) mm · Ø{bar.Diametro:0.###}\n  σ = {state.tensioni_barre[i]:0.######} MPa · ε = {state.BarStrains[i]:0.######} ‰"); }
        foreach (var v in state.ConcreteVertices) b.AppendLine($"{v.Id}: ({v.X:0.###}; {v.Y:0.###}) mm · σ = {v.Stress:0.######} MPa · ε = {v.Strain:0.######} ‰");
        return b.ToString();
    }
    public static string ResponseSummary(ConcreteResults.SectionState? r, string title)
    {
        if (r is null) return title + "\nRiepilogo nativo non disponibile";
        static string V(double? v) => EngineeringFormat.Number(v);
        string s = $"\n{title}\nσc min / max: {V(r.CMin)} / {V(r.CMax)} MPa\nσs min / max: {V(r.SMin)} / {V(r.SMax)} MPa\nεc min / max: {V(r.EcMin)} / {V(r.EcMax)} ‰\nεs min / max: {V(r.EsMin)} / {V(r.EsMax)} ‰";
        if (r.PMin is not null) s += $"\nσp min / max: {V(r.PMin)} / {V(r.PMax)} MPa\nεp min / max: {V(r.EpMin)} / {V(r.EpMax)} ‰";
        return s + $"\n\nd utile: {V(r.UsefulDepth)} mm\nDistanza asse neutro dal lembo (GPC Engine): {V(r.NeutralDistance)} mm\nInclinazione asse neutro: {V(r.NeutralAngle)}°\n— indica dato non disponibile (es. deformazione uniforme).";
    }
}
