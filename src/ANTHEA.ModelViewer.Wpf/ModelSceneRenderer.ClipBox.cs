using System.Numerics;
using System.Windows.Media;
using ANTHEA.ModelWorkspace;

namespace ANTHEA.ModelViewer.Wpf;

internal sealed partial class ModelSceneRenderer
{
    void DrawClipBox(ModelClipBox? box)
    {
        if(box is not {Outline:true,IsValid:true})return;
        var corners=(from x in new[]{box.MinX,box.MaxX} from y in new[]{box.MinY,box.MaxY} from z in new[]{box.MinZ,box.MaxZ}
            select new Vector3((float)(x-modelOrigin.X),(float)(y-modelOrigin.Y),(float)(z-modelOrigin.Z))).ToArray();
        var edges=new EdgeBuilder();
        for(int i=0;i<8;i++)foreach(int step in new[]{1,2,4})if((i&step)==0)edges.Line(corners[i],corners[i|step]);
        AddLines(edges,Color.FromArgb(170,104,203,220),1.1,bypassClip:true);
    }
}
