using Anthea.Application.Materials;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.FoundationWorkspace;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.ShallowFoundation;

namespace X.Desktop;

internal sealed partial class FoundationWorkspace
{
    void RefreshSurveyChoices()
    {
        bool previous=building;building=true;
        survey.ItemsSource=ShallowFoundationPresentation.SurveyNames(data);
        survey.SelectedIndex=ShallowFoundationPresentation.SelectedSurvey(data);building=previous;
    }
    void AddSurvey()
    {
        Commit();var surveys=data["stratigrafie"]!.AsArray();surveys.Add(new JsonArray(FoundationMaterials.NewLayer()));data["generali"]!["sondaggio_fondazione"]=surveys.Count-1;
        RefreshSurveyChoices();BuildSoils();Changed();
    }
    // confirm=false only from the UiTests traces (F5.9b), which answer yes to the question, as GeoEditor.DeleteSurvey.
    void DeleteSurvey(bool confirm=true)
    {
        var surveys=data["stratigrafie"]!.AsArray();if(surveys.Count<=1){state.Text=ShallowFoundationPresentation.KeepOneSurvey;return;}
        if(confirm && MessageBox.Show(Window.GetWindow(this),ShallowFoundationPresentation.RemoveSurveyQuestion,ShallowFoundationPresentation.RemoveSurveyTitle,MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        Commit();surveys.RemoveAt(survey.SelectedIndex);data["generali"]!["sondaggio_fondazione"]=Math.Min(survey.SelectedIndex,surveys.Count-1);RefreshSurveyChoices();BuildSoils();Changed();
    }
    void BuildSoils()
    {
        var rows=FoundationMaterials.Layers(data);
        var soilFields=ShallowFoundationFields.LayerColumns;
        var grid=new JsonGrid(soilFields);soilGrid=grid;grid.FrozenColumnCount=1;
        JsonRow Bind(JsonObject row)
        {
            var display=row.DeepClone().AsObject();return new JsonRow(display,key=>{row[key]=display[key]?.DeepClone();Changed();});
        }
        void Labels(){for(int i=0;i<grid.Rows.Count;i++){grid.Rows[i].Output("__strato",ShallowFoundationPresentation.LayerLetter(i));grid.Rows[i].Output("__color",StratigraphyDrawing.LayerColors[i%StratigraphyDrawing.LayerColors.Length]);}}
        foreach(var row in rows.OfType<JsonObject>())grid.Rows.Add(Bind(row));Labels();
        void Add(){grid.Commit();var row=FoundationMaterials.NewLayer();rows.Add(row);grid.Rows.Add(Bind(row));Labels();Changed();}
        void Delete(JsonRow row){grid.Commit();int i=grid.Rows.IndexOf(row);if(i<0)return;rows.RemoveAt(i);grid.Rows.RemoveAt(i);Labels();Changed();}
        var table=StratigraphyTable.Build(grid,soilFields,Add,Delete,gammaFallback:true);
        grid.RowHeight=32;grid.ColumnHeaderHeight=54;grid.Height=grid.ColumnHeaderHeight+grid.Rows.Count*grid.RowHeight+SystemParameters.HorizontalScrollBarHeight+4;
        var halfSpace=new CheckBox { Content=ShallowFoundationPresentation.HalfSpaceLabel,IsChecked=FoundationMaterials.Flag(data["generali"]!,"strato_portante_semiinfinito"),Margin=new Thickness(6,3,6,3) };
        halfSpace.Click+=(_,_)=>{data["generali"]!["strato_portante_semiinfinito"]=halfSpace.IsChecked==true;Changed();};
        soilHost.Content=Ui.Dock(table,bottom:halfSpace);
    }
    FrameworkElement ConcreteEditor()
    {
        var section=data["sezione"]!.AsObject();var catalog=MaterialInputCatalog.Concrete();InputForm? form=null;
        form=new InputForm(section,ShallowFoundationFields.Concrete(section,catalog),key=>
        {
            ShallowFoundationPresentation.ConcreteChanged(section,key,catalog,(k,v)=>form!.Set(k,v,true));
            Changed();
        });
        return Ui.Stack(form,Ui.Text(ShallowFoundationPresentation.ConcreteNote,11,color:Ui.Muted));
    }
}
