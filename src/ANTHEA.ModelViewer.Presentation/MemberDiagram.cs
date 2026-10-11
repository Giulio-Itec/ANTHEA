using ANTHEA.ModelWorkspace;

namespace ANTHEA.ModelViewer.Presentation;

public sealed record MemberDiagramSample(double Station, double Value, int Node);
public sealed record MemberDiagramReading(string Component, string Unit, string Value, string Origin);
public sealed record MemberDiagramChoice(int Id, string Type, int Property)
{
    public override string ToString() => $"{Type} {Id} · proprietà {Property}";
}
/// <summary>A plot of imported samples. Lines between samples are graphical interpolation, never recovered analysis.</summary>
public sealed record MemberDiagramSeries(string Component, string Unit, MemberDiagramSample[] Samples)
{
    public bool HasAmbiguousStations => Samples.GroupBy(p=>p.Station).Any(g=>g.Select(p=>p.Value).Distinct().Count()>1);
    public double Minimum => Samples.Length==0 ? 0 : Samples.Min(s=>s.Value);
    public double Maximum => Samples.Length==0 ? 0 : Samples.Max(s=>s.Value);
    public string RangeDescription=>$"{Component} [{Unit}] · minimo {Minimum:G7} · massimo {Maximum:G7} · {Samples.Length} campioni importati";
    public (double? Value, bool Imported) At(double station)
    {
        var exact=Samples.Where(p=>Math.Abs(p.Station-station)<1e-10).ToArray();
        if(exact.Length>0) return exact.Select(p=>p.Value).Distinct().Count()==1 ? (exact[0].Value,true) : (null,false);
        if(HasAmbiguousStations || Samples.Length<2 || station<Samples[0].Station || station>Samples[^1].Station) return (null,false);
        for(int i=1;i<Samples.Length;i++)if(station<Samples[i].Station)
        {
            var a=Samples[i-1];var b=Samples[i];double fraction=(station-a.Station)/(b.Station-a.Station);
            return (a.Value*(1-fraction)+b.Value*fraction,false);
        }
        return (null,false);
    }
    public static MemberDiagramSeries[] Read(ModelSnapshot model, int elementId, string? resultCase, string forceUnit, string lengthUnit)
    {
        var element=model.Elements.FirstOrDefault(e=>e.Id==elementId && ElementFamilies.IsLine(e.Type));
        var result=model.Results.FirstOrDefault(r=>r.Name==resultCase);
        if(element==null || result==null)return [];
        double force=forceUnit switch {"N"=>1000,"MN"=>.001,_=>1};
        double length=lengthUnit switch {"mm"=>1000,"cm"=>100,_=>1};
        return ResultFields.Components(element.Type,"LOCAL").Select(component=>
        {
            bool moment=component.StartsWith('M');double factor=force*(moment?length:1);
            var points=ResultFields.Read(result,element.Type,"LOCAL",component).Where(v=>v.Element==elementId)
                .Select(v=>new MemberDiagramSample(v.Station ?? (v.Node==element.Nodes[0]?0:1),v.Value*factor,v.Node))
                .DistinctBy(v=>(v.Station,v.Value)).OrderBy(v=>v.Station).ToArray();
            return new MemberDiagramSeries(component,moment?$"{forceUnit}·{lengthUnit}":forceUnit,points);
        }).Where(s=>s.Samples.Length>0).ToArray();
    }
}
