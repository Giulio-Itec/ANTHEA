using System.Globalization;
using System.Text.Json;
using GPC.Converter.CivilNx;
using GPC.Model.Results.Locations;

namespace ANTHEA.ModelWorkspace;

/// <summary>Shared source binding and display conventions for static combinations and construction results.</summary>
internal static class MidasGpcDisplay
{
    internal static CivilNxSnapshot Source(ModelSnapshot snapshot)
    {
        if(snapshot.SourceTables==null||new[]{"UNIT","NODE","ELEM","STLD"}.Any(t=>!snapshot.SourceTables.ContainsKey(t)))
            throw new InvalidDataException("Per questo collegamento GPC servono le tabelle sorgente MIDAS complete. Reimporta geometria, attributi e risultati.");
        return new(snapshot.SourceTables.ToDictionary(p=>p.Key,p=>new CivilNxResponse("db/"+p.Key,
            JsonSerializer.Serialize(new Dictionary<string,JsonElement>{{p.Key,p.Value}}))));
    }

    internal static void ValidateGeometry(ModelSnapshot snapshot,GPC.Model.Models.Model model)
    {
        var nodes=model.NodesElements.Values.Where(n=>n.Source!=null).ToDictionary(n=>int.Parse(n.Source.OriginalId,CultureInfo.InvariantCulture));
        if(nodes.Count!=snapshot.Nodes.Length)throw new InvalidDataException("Numero di nodi diverso dalle tabelle sorgente MIDAS.");
        foreach(var n in snapshot.Nodes)
            if(!nodes.TryGetValue(n.Id,out var native)||Math.Abs(native.Position.X-n.X*1000)>1e-5||Math.Abs(native.Position.Y-n.Y*1000)>1e-5||Math.Abs(native.Position.Z-n.Z*1000)>1e-5)
                throw new InvalidDataException("La geometria salvata non coincide con le tabelle sorgente MIDAS. Aggiorna il modello prima di calcolare le combinazioni.");
        var source=snapshot.SourceTables!["ELEM"];
        foreach(var element in snapshot.Elements)
        {
            if(!source.TryGetProperty(element.Id.ToString(CultureInfo.InvariantCulture),out var e)||
                e.GetProperty("TYPE").GetString()!=element.Type||e.GetProperty("SECT").GetInt32()!=element.Property||
                !e.GetProperty("NODE").EnumerateArray().Select(n=>n.GetInt32()).Where(n=>n>0).SequenceEqual(element.Nodes)||
                (e.TryGetProperty("ANGLE",out var angle)?angle.GetDouble():0)!=element.Angle)
                throw new InvalidDataException("Connettività, assegnazioni o assi diversi dalle tabelle sorgente MIDAS. Aggiorna il modello prima del calcolo.");
        }
    }

    internal static double[] DisplayVector(ResultLocation sample)=>sample switch
    {
        PointResultPlateForces p=>[-p.Forces.Mxx*.001,-p.Forces.Myy*.001,-p.Forces.Mxy*.001,p.Forces.Fxx,p.Forces.Fyy,p.Forces.Fxy,p.Forces.Fxz,p.Forces.Fyz],
        StationResultBeamForces b=>[b.ResultBeamForces.N*.001,b.ResultBeamForces.V1*.001,b.ResultBeamForces.V2*.001,b.ResultBeamForces.T*1e-6,-b.ResultBeamForces.M1*1e-6,b.ResultBeamForces.M2*1e-6],
        NodeResultDisplacement d=>[d.ResultDisplacement.D1*.001,d.ResultDisplacement.D2*.001,d.ResultDisplacement.D3*.001,d.ResultDisplacement.R1,d.ResultDisplacement.R2,d.ResultDisplacement.R3],
        _=>throw new InvalidDataException("Famiglia dei risultati GPC non gestita dalla vista.")
    };
}
