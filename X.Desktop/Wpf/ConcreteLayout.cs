using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private readonly Dictionary<string,List<Grid>> workspaceSplits = new();
    private Grid ResultColumns(UIElement view, UIElement details)
    {
        var grid=Columns((view,6,350),(details,4,230));
        RegisterWorkspaceSplit(grid,"risultati",false,.6);
        return grid;
    }
    private void RegisterWorkspaceSplit(Grid grid,string group,bool rows,double initial)
    {
        if(settings["layout"] is not JsonObject)settings["layout"]=new JsonObject();
        if(!workspaceSplits.TryGetValue(group,out var peers))workspaceSplits[group]=peers=[];
        peers.Add(grid);
        double ratio=settings["layout"].D(group,initial);
        if(!double.IsFinite(ratio)||ratio<.15||ratio>.85)ratio=initial;
        ApplySplit(grid,rows,ratio);
        foreach(var splitter in grid.Children.OfType<GridSplitter>())
            splitter.DragCompleted+=(_,_)=>
            {
                double first=rows?grid.RowDefinitions[0].ActualHeight:grid.ColumnDefinitions[0].ActualWidth;
                double last=rows?grid.RowDefinitions[2].ActualHeight:grid.ColumnDefinitions[2].ActualWidth;
                if(first+last>0)SynchronizeWorkspaceSplit(group,rows,first/(first+last));
            };
    }
    private void SynchronizeWorkspaceSplit(string group,bool rows,double ratio)
    {
        ratio=Math.Clamp(ratio,.15,.85);
        foreach(var grid in workspaceSplits[group])ApplySplit(grid,rows,ratio);
        settings["layout"]![group]=ratio;
        Modified?.Invoke();
    }
    private static void ApplySplit(Grid grid,bool rows,double ratio)
    {
        if(rows){grid.RowDefinitions[0].Height=new(ratio,GridUnitType.Star);grid.RowDefinitions[2].Height=new(1-ratio,GridUnitType.Star);}
        else{grid.ColumnDefinitions[0].Width=new(ratio,GridUnitType.Star);grid.ColumnDefinitions[2].Width=new(1-ratio,GridUnitType.Star);}
    }
}
