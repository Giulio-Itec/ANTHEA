using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Piles;
using GPC.Design.Piles;

namespace Anthea.Calculations;

public static partial class ElasticHorizontalPile
{
    // Document adaptation only. Search order, screening, resistance and seismic checks belong to GPC.Design.
    static JsonObject DesignSegmentWithLibrary(JsonObject root,JsonObject result,int index,int[] counts,double[] diameters,
        double[] linkDiameters,double[] spacings,CancellationToken cancellation,IProgress<PileCalculationProgress>? progress,int maximumParallelism)
    {
        cancellation.ThrowIfCancellationRequested();
        var source=root["elastico"]!.Array("tratti")[index]!.AsObject();var segment=ReadSegments(root)[index];
        var settings=root["elastico"]!["dettagli"]!;
        if(!settings.B("azioni_progetto")||!settings.B("taglio_confermato"))throw new ArgumentException("Confermare azioni di progetto e modello di taglio nelle ipotesi.");
        var seismic=SeismicSettings(root);seismic.Links=LinkKind(TransverseKind(root,source));
        if(TransverseKind(root,source)!="Staffe singole")throw new ArgumentException("Il dimensionamento a taglio richiede staffe singole: modello resistente della spirale non implementato.");
        if(seismic.Enabled&&(!seismic.SeismicActionsConfirmed||!seismic.ElasticMomentConfirmed||seismic.Links==PileSeismicLinks.Unspecified||!seismic.StandardNtc2018))
            throw new ArgumentException("Dimensionamento sismico: confermare azioni sismiche, momento elastico q=1, tipologia staffe e coefficienti ordinari NTC.");
        var workspace=ConcreteStandards.PileWorkspace(root["sezione"]!.AsObject());
        var options=J.Obj(("criterio","N costante"),("assi","Locali"),("strategia","Iterativo"),("modello","Non lineare"));
        var actions=result["risposta"]!.Array("Points").Select(p=>new PileAction
        {Depth=p.Required("Depth"),Side=p.S("Side"),N=p.Required("AxialForce",double.NegativeInfinity),V=p.Required("Shear",double.NegativeInfinity),M=p.Required("Moment",double.NegativeInfinity)}).ToArray();
        JsonObject Input(PileDesignLayout layout)
        {
            var input=SegmentSection(root,source);
            input["longitudinal_bar_count"]=layout.Count;input["longitudinal_bar_diameter_mm"]=layout.Diameter;
            input["transverse_bar_diameter_mm"]=layout.LinkDiameter;input["transverse_spacing_mm"]=layout.LinkSpacing;return input;
        }
        PileRcParameters Factory(PileDesignLayout layout)
        {
            var input=Input(layout);
            return RcParameters(input,settings,workspace,new CheckerSection(input,(JsonObject)workspace.DeepClone(),(JsonObject)options.DeepClone()));
        }
        var design=PileSegmentDesign.Search(counts,diameters,linkDiameters,spacings,actions,segment.Start,segment.End,
            result["input"]!.Required("lunghezza"),seismic,Factory,cancellation,progress==null?null:new PileDesignProgressAdapter(progress),maximumParallelism);
        if(design.Selected==null)throw new ArgumentException("Nessuna disposizione del catalogo soddisfa N–M–V, interferro e minimi selezionati. Ampliare il catalogo.");
        var selected=Input(design.Selected);var proposal=new JsonObject();foreach(string key in ReinforcementKeys)proposal[key]=selected[key]!.DeepClone();
        proposal["candidati_esaminati"]=design.Evaluated;
        proposal["esclusioni_geometriche"]=J.Node(design.GeometricExclusions.Select(e=>$"{e.Layout.Count}φ{e.Layout.Diameter}, staffe φ{e.Layout.LinkDiameter}/{e.Layout.LinkSpacing}: {e.Diagnostic}").ToArray());
        proposal["criterio"]="Ricerca discreta Checker: As crescente, poi quantità di staffe per metro crescente. N–M–V concomitanti a tutte le ascisse e controlli geometrici/minimi disponibili. Copriferro senza cmin,dur e dettagli di sviluppo restano da completare.";
        return proposal;
    }
    sealed class PileDesignProgressAdapter(IProgress<PileCalculationProgress> target):IProgress<PileDesignProgress>
    {
        public void Report(PileDesignProgress value)
        {
            if(value.Stage==PileDesignStage.Layout)
                target.Report(new($"Disposizione {value.Evaluated}/{value.Total}: {value.Layout.Count}φ{value.Layout.Diameter}, staffe φ{value.Layout.LinkDiameter}/{value.Layout.LinkSpacing}",value.Evaluated-1,value.Total));
            else target.Report(new($"Disposizione {value.Evaluated}/{value.Total} · MRd: {value.CompletedAxial}/{value.TotalAxial} valori N",value.CompletedAxial,value.TotalAxial));
        }
    }
}
