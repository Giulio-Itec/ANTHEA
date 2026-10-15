// Spostato da X.Calculations/ElasticHorizontalPile.Reinforcement.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: ramo legacy di
// DesignSegment (G1), ricerca dei tratti precedente usata solo dalle prove. Codice storico conservato per le prove, fuori dall'installer.
using GPC.Model.Checking.Contracts;
using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Piles;
namespace Anthea.Calculations;
public static partial class LegacyElasticHorizontalPile
{
    /// <summary>Ricerca dei tratti precedente al porting in GPC.Design (prima ElasticHorizontalPile.DesignSegment con legacy: true).</summary>
    public static JsonObject DesignSegment(JsonObject root,JsonObject result,int index,int[] counts,double[] diameters,double[] linkDiameters,double[] spacings,CancellationToken cancellation=default,IProgress<PileCalculationProgress>? progress=null)
    {
        var source=root["elastico"]!.Array("tratti")[index]!.AsObject();var segment=ElasticHorizontalPile.ReadSegments(root)[index];var settings=root["elastico"]!["dettagli"]!;
        if(!settings.B("azioni_progetto")||!settings.B("taglio_confermato"))throw new ArgumentException("Confermare azioni di progetto e modello di taglio nelle ipotesi.");
        var seismicSettings=ElasticHorizontalPile.SeismicSettings(root);seismicSettings.Links=ElasticHorizontalPile.LinkKind(ElasticHorizontalPile.TransverseKind(root,source));
        if(ElasticHorizontalPile.TransverseKind(root,source)!="Staffe singole")throw new ArgumentException("Il dimensionamento a taglio richiede staffe singole: modello resistente della spirale non implementato.");
        if(seismicSettings.Enabled&&(!seismicSettings.SeismicActionsConfirmed||!seismicSettings.ElasticMomentConfirmed||seismicSettings.Links==PileSeismicLinks.Unspecified||!seismicSettings.StandardNtc2018))throw new ArgumentException("Dimensionamento sismico: confermare azioni sismiche, momento elastico q=1, tipologia staffe e coefficienti ordinari NTC.");
        var workspace=ConcreteStandards.PileWorkspace(root["sezione"]!.AsObject());var options=J.Obj(("criterio","N costante"),("assi","Locali"),("strategia","Iterativo"),("modello","Non lineare"));
        var actions=result["risposta"]!.Array("Points").Where(p=>p.D("Depth")>=segment.Start-1e-9&&p.D("Depth")<=segment.End+1e-9).Select(p=>new PileAction{Depth=p.D("Depth"),Side=p.S("Side"),N=p.D("AxialForce"),V=p.D("Shear"),M=p.D("Moment")}).ToArray();
        if(actions.Length==0)throw new ArgumentException("Nessuna azione disponibile sul tratto.");
        var screen=new[]{actions.MaxBy(a=>Math.Abs(a.M))!,actions.MaxBy(a=>Math.Abs(a.V))!,actions.MinBy(a=>a.N)!,actions.MaxBy(a=>a.N)!}.Distinct().ToArray();
        var exclusions=new List<string>();var tables=new ElasticPileVerificationCache();int evaluated=0;
        var layouts=PileReinforcement.Layouts(counts,diameters,linkDiameters,spacings);
        foreach(var layout in layouts)
        {
            cancellation.ThrowIfCancellationRequested();evaluated++;progress?.Report(new($"Disposizione {evaluated}/{layouts.Count}: {layout.Count}φ{layout.Diameter}, staffe φ{layout.LinkDiameter}/{layout.LinkSpacing}",evaluated-1,layouts.Count));
            var input=ElasticHorizontalPile.SegmentSection(root,source);input["longitudinal_bar_count"]=layout.Count;input["longitudinal_bar_diameter_mm"]=layout.Diameter;input["transverse_bar_diameter_mm"]=layout.LinkDiameter;input["transverse_spacing_mm"]=layout.LinkSpacing;
            try
            {
                PileReinforcement Create(){var c=(JsonObject)input.DeepClone();return new PileReinforcement(ElasticHorizontalPile.RcParameters(c,settings,workspace,new CheckerSection(c,(JsonObject)workspace.DeepClone(),(JsonObject)options.DeepClone())));}
                var service=Create();if(service.ConstructionChecks(actions.Max(a=>a.N)).Any(c=>c.Passed==false))continue;
                if(seismicSettings.Enabled&&service.CheckHeadSeismic(seismicSettings,segment.Start,segment.End,result["input"].D("lunghezza"),[]).Checks.Any(c=>c.Passed==false))continue;
                if(screen.Any(a=>service.Check(a).Outcome!=EngineeringOutcome.Satisfied))continue;
                service.PrepareResistance(actions.Select(a=>a.N),Create,tables.For(input.ToJsonString()),cancellation,progress:(done,total)=>progress?.Report(new($"Disposizione {evaluated}/{layouts.Count} · MRd: {done}/{total} valori N",done,total)));
                var verified=actions.Select(service.Check).ToArray();if(verified.Any(c=>c.Outcome!=EngineeringOutcome.Satisfied))continue;
                if(seismicSettings.Enabled&&service.CheckHeadSeismic(seismicSettings,segment.Start,segment.End,result["input"].D("lunghezza"),verified).Checks.Any(c=>c.Passed!=true))continue;
                var proposal=new JsonObject();foreach(string key in ElasticHorizontalPile.ReinforcementKeys)proposal[key]=input[key]!.DeepClone();proposal["candidati_esaminati"]=evaluated;proposal["esclusioni_geometriche"]=J.Node(exclusions);proposal["criterio"]="Ricerca discreta Checker: As crescente, poi quantità di staffe per metro crescente. N–M–V concomitanti a tutte le ascisse e controlli geometrici/minimi disponibili. Copriferro senza cmin,dur e dettagli di sviluppo restano da completare.";return proposal;
            }
            catch(ArgumentException ex){exclusions.Add($"{layout.Count}φ{layout.Diameter}, staffe φ{layout.LinkDiameter}/{layout.LinkSpacing}: {ex.Message}");}
        }
        throw new ArgumentException("Nessuna disposizione del catalogo soddisfa N–M–V, interferro e minimi selezionati. Ampliare il catalogo.");
    }
}
