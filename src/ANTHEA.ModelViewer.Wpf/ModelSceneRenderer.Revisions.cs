using ANTHEA.ModelWorkspace;
using System.Numerics;
using System.Windows.Media;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed partial class ModelSceneRenderer
{
    private Vector3[] RevisionGhostPoints(ModelRevisionGhost item)=>item.Points.Select(n=>new Vector3((float)(n.X-modelOrigin.X),(float)(n.Y-modelOrigin.Y),(float)(n.Z-modelOrigin.Z))).ToArray();
    internal void FocusRemovedRevision(int id)
    {
        if(lastOptions?.Exploration?.Revision?.Removed.FirstOrDefault(g=>g.Element==id) is {} ghost)FocusPoints(RevisionGhostPoints(ghost));
    }
    private void DrawRevision(SceneOptions options,ModelRevisionOverlay? revision)
    {
        if(revision==null)return;
        var elements=model!.Elements.ToDictionary(e=>e.Id);
        foreach(var group in revision.Markers.GroupBy(m=>m.Change))
        {
            var edges=new EdgeBuilder();
            foreach(var marker in group)
            {
                if(!elements.TryGetValue(marker.Element,out var element)||options.Visible!=null&&!options.Visible.Contains(element.Id))continue;
                ElementOutline(edges,element,ElementPoints(element),options.Extruded);
            }
            var color=group.Key switch {"Nuovo"=>Color.FromRgb(71,214,141),"Geometria"=>Color.FromRgb(255,193,76),_=>Color.FromRgb(202,144,255)};
            AddLines(edges,color,2.5);
        }
        if(!revision.ShowRemoved)return;
        var removed=new EdgeBuilder();
        foreach(var item in revision.Removed)
        {
            var p=RevisionGhostPoints(item);
            for(int i=0;i<(p.Length==2?1:p.Length);i++)removed.Line(p[i],p[(i+1)%p.Length]);
        }
        // Original geometry of removed objects is explanatory context only, never selectable as current data.
        AddLines(removed,Color.FromRgb(255,110,117),2.5);
    }
}
