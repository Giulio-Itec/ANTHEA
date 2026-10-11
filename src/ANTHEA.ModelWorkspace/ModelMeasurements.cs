using GPC.Geometry;

namespace ANTHEA.ModelWorkspace;

public sealed record ModelMeasurement(int Kind,int[] Nodes);
public sealed record ModelMeasurementSettings(bool Visible,int Basis,string Unit,ModelMeasurement[] Items);
public sealed record ModelMeasurementValue(double? Value,ModelDirection? Delta,string Message)
{
    public bool Available=>Value.HasValue;
}

/// <summary>Adapts imported node coordinates to the existing GPC geometry operations. No solver or recovered result.</summary>
public static class ModelMeasurements
{
    public static void Validate(ModelMeasurementSettings? settings)
    {
        if(settings==null)return;
        if(settings.Basis is <0 or >1 || settings.Unit is not ("m" or "cm" or "mm") || settings.Items==null || settings.Items.Length>20 ||
            settings.Items.Any(m=>m==null || m.Kind is <0 or >1 || m.Nodes==null || m.Nodes.Length!=(m.Kind==0?2:3) || m.Nodes.Any(n=>n<=0) || m.Nodes.Distinct().Count()!=m.Nodes.Length))
            throw new InvalidDataException("Misure non valide: usa due nodi distinti per una distanza, tre per un angolo e al massimo venti misure.");
    }
    public static ModelMeasurementValue Read(ModelSnapshot snapshot,ModelMeasurement measurement,int basis,string? caseName)
    {
        Validate(new(true,basis,"m",[measurement]));
        var nodes=measurement.Nodes.Select(id=>snapshot.Nodes.FirstOrDefault(n=>n.Id==id)).ToArray();
        if(nodes.Any(n=>n==null))return new(null,null,"Uno dei nodi non è più disponibile.");
        var displacements=new Dictionary<int,ModelDisplacement>();
        if(basis==1)
        {
            var result=snapshot.Results.FirstOrDefault(r=>r.Name==caseName);
            if(result==null || result.IsEnvelope)return new(null,null,"Serve un caso con traslazioni simultanee: gli inviluppi non definiscono una geometria.");
            displacements=(result.Displacements??[]).ToDictionary(d=>d.Node);
            if(measurement.Nodes.Any(id=>!displacements.ContainsKey(id)))return new(null,null,"Spostamenti mancanti per uno o più nodi della misura.");
        }
        Vector3d Difference(int to,int from)
        {
            var a=nodes[from]!;var b=nodes[to]!;
            // Apply relative translations after subtracting the original coordinates, retaining small displacements at large origins.
            var vector=new Vector3d(b.X-a.X,b.Y-a.Y,b.Z-a.Z);
            return basis==0?vector:vector+new Vector3d(displacements[b.Id].X-displacements[a.Id].X,displacements[b.Id].Y-displacements[a.Id].Y,displacements[b.Id].Z-displacements[a.Id].Z);
        }
        var first=Difference(measurement.Kind==0?1:0,measurement.Kind==0?0:1);
        if(!double.IsFinite(first.Length))return new(null,null,"Estensione della misura non rappresentabile.");
        if(measurement.Kind==0)return new(first.Length,new(first.X,first.Y,first.Z),"");
        var second=Difference(2,1);
        if(first.Length==0 || second.Length==0 || !double.IsFinite(second.Length))return new(null,null,"Angolo non definito: due punti coincidono o non sono rappresentabili.");
        first.Unitize();second.Unitize();
        return new(first.AngleTo(second)*180/Math.PI,null,"");
    }
}
