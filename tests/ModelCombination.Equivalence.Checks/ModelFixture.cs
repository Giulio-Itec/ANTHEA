using System.Text.Json;
using ANTHEA.ModelWorkspace;
static partial class Program
{
    static ModelSnapshot CombinationModel()
    {
        var model=new ModelSnapshot
        {
            Name="Prova combinazioni",Source="MIDAS Civil NX",
            Nodes=[new(1,0,0,0),new(2,2,0,0),new(3,2,2,0),new(4,0,2,0)],
            Elements=[new(10,"PLATE",1,[1,2,3,4]),new(20,"BEAM",1,[1,2])],
            Plates=[new(1,"Soletta",.2,0)],Sections=[new(1,"Trave","SB",.3,.5,true)],
            SourceTables=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>("""
            {"UNIT":{"1":{"FORCE":"KN","DIST":"M"}},
             "NODE":{"1":{"X":0,"Y":0,"Z":0},"2":{"X":2,"Y":0,"Z":0},"3":{"X":2,"Y":2,"Z":0},"4":{"X":0,"Y":2,"Z":0}},
             "ELEM":{"10":{"TYPE":"PLATE","MATL":1,"SECT":1,"NODE":[1,2,3,4],"ANGLE":0,"STYPE":1},"20":{"TYPE":"BEAM","MATL":1,"SECT":1,"NODE":[1,2,0,0],"ANGLE":0,"STYPE":0}},
             "MATL":{"1":{"TYPE":"CONC","NAME":"C30/37","PARAM":[{"P_TYPE":1,"STANDARD":"EN04(RC)","DB":"C30/37","ELAST":32836000}]}},
             "THIK":{"1":{"NAME":"Soletta","TYPE":"VALUE","T_IN":0.2,"O_VALUE":0}},
             "SECT":{"1":{"SECTTYPE":"DBUSER","SECT_NAME":"Trave","SECT_BEFORE":{"OFFSET_PT":"CC","OFFSET_CENTER":0,"SHAPE":"SB","DATATYPE":2,"SECT_I":{"vSIZE":[0.5,0.3,0,0]}}}},
             "STLD":{"1":{"NAME":"G","TYPE":"D"},"2":{"NAME":"Q","TYPE":"L"}}}
            """)
        };
        string[] beamComponents=["Fx","Fy","Fz","Mx","My","Mz"];
        ModelResultSet Result(string name,double[] p,double[] b)=>new(name,false,model.Nodes.Select(n=>new CornerResult(10,n.Id,p.ToArray())).ToArray())
        {Fields=beamComponents.Select((c,i)=>new ResultField("BEAM","LOCAL",c,[new(20,1,b[i],0),new(20,2,b[i],1)])).ToArray()};
        model.Results=[Result("G",[10,20,30,40,50,60,70,80],[10,20,30,40,50,60]),
            Result("Q",[-4,8,-12,16,-20,24,-28,32],[-4,8,-12,16,-20,24])];
        return model;
    }

}
