using System.Text.Json;
using ANTHEA.ModelWorkspace;

internal static class SectionInspectionSample
{
    internal static ModelSnapshot Model()
    {
        static object Property(string shape,double[] size,string reference="CC",int custom=0)=>new {SECTTYPE="DBUSER",SECT_NAME=shape,SECT_BEFORE=new {SHAPE=shape,DATATYPE=2,OFFSET_PT=reference,OFFSET_CENTER=0,HORZ_OFFSET_OPT=0,VERT_OFFSET_OPT=custom,USERDEF_OFFSET_ZI=custom*.1,SECT_I=new {vSIZE=size}}};
        var model=Sample.Model();model.Source="MIDAS synthetic section inspection";
        model.Sections=[new(2,"Rettangolo","SB",.2,.3,false),new(3,"Scatolare","B",.2,.3,true),new(4,"Profilo I","H",.2,.2,true),new(5,"Offset non risolto","SB",.2,.3,false),new(6,"Sorgente non supportata","PSC",0,0,false),new(100,"Campata · concio 1","I",0,0,false)];
        model.Elements=[..model.Elements.Where(e=>e.Type=="PLATE"),new(12,"BEAM",2,[1,4]),new(13,"BEAM",3,[2,3]),new(14,"BEAM",4,[5,6]),new(15,"BEAM",5,[1,2]),new(16,"BEAM",6,[4,3]),new(1000,"BEAM",100,[2,5])];
        model.SourceTables=new() {
            ["UNIT"]=JsonSerializer.SerializeToElement(new Dictionary<string,object>{["1"]=new {FORCE="KN",DIST="M",TEMPER="C"}}),
            ["SECT"]=JsonSerializer.SerializeToElement(new Dictionary<string,object>{
                ["2"]=Property("SB",[.3,.2],"CT"),["3"]=Property("B",[.3,.2,.01,.014,.19,.014]),["4"]=Property("H",[.2,.2,.008,.01,.2,.01,0]),
                ["5"]=Property("SB",[.3,.2],"CT",1),["6"]=new{SECTTYPE="PSC",SECT_NAME="PSC",SECT_BEFORE=new{SHAPE="X",OFFSET_PT="CC"}},
                ["100"]=new{SECTTYPE="COMPOSITE",SECT_NAME="Campata · concio 1",SECT_BEFORE=new{SHAPE="I",OFFSET_PT="CT",OFFSET_CENTER=0,SECT_I=new{vSIZE=new[]{1.96,.015,.6,.02,.8,.02}}},SECT_AFTER=new{SLAB=new[]{3.6,.28,0d},MATL_ELAST=6.2}}}),
            ["OFFS"]=JsonSerializer.SerializeToElement(new Dictionary<string,object>{["12"]=new{TYPE="ELEMENT",EZ=.1}})
        };return model;
    }
}
