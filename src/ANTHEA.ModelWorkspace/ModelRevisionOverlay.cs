namespace ANTHEA.ModelWorkspace;

public sealed record ModelRevisionMarker(int Element,string Change);
public sealed record ModelRevisionGhost(int Element,ModelDirection[] Points);
public sealed record ModelRevisionOverlay(string PreviousRevision,string CurrentRevision,string PreviousName,
    ModelRevisionMarker[] Markers,ModelRevisionGhost[] Removed,bool ShowRemoved=true)
{
    public static ModelRevisionOverlay Create(ModelRevisionReview review,string previousRevision,string currentRevision)
    {
        var nodes=review.Previous.Nodes.ToDictionary(n=>n.Id);
        var removed=review.Elements.Where(e=>e.Removed).Select(e=>e.Id).ToHashSet();
        return new(previousRevision,currentRevision,review.Previous.Name,
            review.Elements.Where(e=>!e.Removed).Select(e=>new ModelRevisionMarker(e.Id,e.Added?"Nuovo":e.Geometry?"Geometria":"Attributi")).ToArray(),
            review.Previous.Elements.Where(e=>removed.Contains(e.Id)).Select(e=>new ModelRevisionGhost(e.Id,e.Nodes.Select(id=>new ModelDirection(nodes[id].X,nodes[id].Y,nodes[id].Z)).ToArray())).ToArray());
    }
    public static void Validate(ModelRevisionOverlay? overlay)
    {
        if(overlay==null)return;
        static bool Hash(string? key)=>key?.Length==64 && key.All(Uri.IsHexDigit);
        if(!Hash(overlay.PreviousRevision)||!Hash(overlay.CurrentRevision)||overlay.PreviousName==null||overlay.PreviousName.Length>1000||
            overlay.Markers==null||overlay.Removed==null||overlay.Markers.Length>500000||overlay.Removed.Length>500000||
            overlay.Markers.Any(m=>m==null||m.Element<=0||m.Change is not("Nuovo" or "Geometria" or "Attributi"))||
            overlay.Removed.Any(m=>m==null||m.Element<=0||m.Points==null||m.Points.Length is <2 or >4||m.Points.Any(p=>p==null||!double.IsFinite(p.X)||!double.IsFinite(p.Y)||!double.IsFinite(p.Z)))||
            overlay.Markers.Select(m=>m.Element).Distinct().Count()!=overlay.Markers.Length||overlay.Removed.Select(m=>m.Element).Distinct().Count()!=overlay.Removed.Length||
            overlay.Markers.Select(m=>m.Element).Intersect(overlay.Removed.Select(m=>m.Element)).Any())
            throw new InvalidDataException("Annotazioni della revisione non valide.");
    }
}
