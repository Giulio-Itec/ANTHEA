using Anthea.Application.Materials;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.FoundationWorkspace;

namespace X.Desktop;

internal sealed partial class FoundationWorkspace
{
    void RefreshSurveyChoices()
    {
        bool previous=building;building=true;
        survey.ItemsSource=Enumerable.Range(1,data["stratigrafie"]!.AsArray().Count).Select(i=>"Sondaggio "+i).ToArray();
        survey.SelectedIndex=FoundationDocument.Choice(data["generali"]!.AsObject(),"sondaggio_fondazione",data["stratigrafie"]!.AsArray().Count);building=previous;
    }
    void AddSurvey()
    {
        Commit();var surveys=data["stratigrafie"]!.AsArray();surveys.Add(new JsonArray(FoundationMaterials.NewLayer()));data["generali"]!["sondaggio_fondazione"]=surveys.Count-1;
        RefreshSurveyChoices();BuildSoils();Changed();
    }
    void DeleteSurvey()
    {
        var surveys=data["stratigrafie"]!.AsArray();if(surveys.Count<=1){state.Text="Conservare almeno un sondaggio.";return;}
        if(MessageBox.Show(Window.GetWindow(this),"Eliminare il sondaggio selezionato e i suoi strati?","Stratigrafia",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        Commit();surveys.RemoveAt(survey.SelectedIndex);data["generali"]!["sondaggio_fondazione"]=Math.Min(survey.SelectedIndex,surveys.Count-1);RefreshSurveyChoices();BuildSoils();Changed();
    }
    void BuildSoils()
    {
        var rows=FoundationMaterials.Layers(data);
        Field[] soilFields=[new("__strato","Strato",ReadOnly:true),new("tipologia","Tipologia",Choices:["Granulare","Coesivo"]),new("spessore","Spessore S [m]"),new("peso_specifico","γ [kN/m³]"),new("peso_specifico_saturo","γsat [kN/m³]"),new("angolo_attrito","φ′ [°]"),new("coesione_efficace","c′ [kPa]"),new("coesione_non_drenata","Cu [kPa]")];
        var grid=new JsonGrid(soilFields);soilGrid=grid;grid.FrozenColumnCount=1;
        JsonRow Bind(JsonObject row)
        {
            var display=row.DeepClone().AsObject();return new JsonRow(display,key=>{row[key]=display[key]?.DeepClone();Changed();});
        }
        void Labels(){for(int i=0;i<grid.Rows.Count;i++){grid.Rows[i].Output("__strato",((char)('A'+i%26)).ToString());grid.Rows[i].Output("__color",StratigraphyDrawing.LayerColors[i%StratigraphyDrawing.LayerColors.Length]);}}
        foreach(var row in rows.OfType<JsonObject>())grid.Rows.Add(Bind(row));Labels();
        void Add(){grid.Commit();var row=FoundationMaterials.NewLayer();rows.Add(row);grid.Rows.Add(Bind(row));Labels();Changed();}
        void Delete(JsonRow row){grid.Commit();int i=grid.Rows.IndexOf(row);if(i<0)return;rows.RemoveAt(i);grid.Rows.RemoveAt(i);Labels();Changed();}
        var table=StratigraphyTable.Build(grid,soilFields,Add,Delete,gammaFallback:true);
        grid.RowHeight=32;grid.ColumnHeaderHeight=54;grid.Height=grid.ColumnHeaderHeight+grid.Rows.Count*grid.RowHeight+SystemParameters.HorizontalScrollBarHeight+4;
        var halfSpace=new CheckBox { Content="Strato portante omogeneo sotto la base, prolungato come semispazio",IsChecked=FoundationMaterials.Flag(data["generali"]!,"strato_portante_semiinfinito"),Margin=new Thickness(6,3,6,3) };
        halfSpace.Click+=(_,_)=>{data["generali"]!["strato_portante_semiinfinito"]=halfSpace.IsChecked==true;Changed();};
        soilHost.Content=Ui.Dock(table,bottom:halfSpace);
    }
    FrameworkElement ConcreteEditor()
    {
        var section=data["sezione"]!.AsObject();var catalog=MaterialInputCatalog.Concrete();InputForm? form=null;
        form=new InputForm(section,[new("classe_cls","Calcestruzzo",Choices:catalog.Select(m=>m["nome"]!.ToString()).Append(section["classe_cls"]!.ToString()).Append("Personalizzato").Distinct().ToArray()),new("fck_mpa","Resistenza fck","MPa"),new("cls_diagramma","Diagramma",Choices:MaterialInputCatalog.ConcreteDiagrams)],key=>
        {
            if(key=="classe_cls" && catalog.FirstOrDefault(m=>m["nome"]?.ToString()==section[key]?.ToString()) is {} selected)
                foreach(var (k,v) in selected.Where(p=>p.Key!="nome")){section[k]=v?.DeepClone();form!.Set(k,v?.ToString()??"",true);}
            if(key=="fck_mpa")
            {
                string name=catalog.FirstOrDefault(m=>m["fck_mpa"]?.ToString()==section[key]?.ToString())?["nome"]?.ToString()??"Personalizzato";
                section["classe_cls"]=name;section["materiale_cls_nome"]=name;form!.Set("classe_cls",name,true);
            }
            Changed();
        });
        return Ui.Stack(form,Ui.Text("Materiale condiviso nei Progetti. Non interviene nella portanza geotecnica; resistenza strutturale e peso proprio automatico non calcolati.",11,color:Ui.Muted));
    }
}
