using System.Windows;
using System.Windows.Controls;

namespace Materiali;

public sealed partial class MaterialView
{
    readonly ComboBox exposureSelector=new(){MinWidth=72,VerticalAlignment=VerticalAlignment.Top};
    readonly TextBlock exposureAggressiveness=Text("",14,true),combinedExposure=Text("",12);
    readonly TextBlock exposureExamples=Text("",12);
    bool updatingExposure;
    // Testi delle esposizioni (decisione U5 del 14/10/2026, voce F4-47) e aggressività: Materiali.MaterialExposurePresentation
    // in Application (F5.5).
    UIElement BuildExposureSelector(UIElement checks)
    {
        exposureSelector.ItemsSource=MaterialSheetInput.ExposureChoices.Select(e=>e.Code).ToArray();
        exposureSelector.SelectedItem="XC1";
        System.Windows.Automation.AutomationProperties.SetName(exposureSelector,"Classe di esposizione");
        var row=new Grid{Margin=new Thickness(0,5,0,8)};
        row.ColumnDefinitions.Add(new(){Width=new GridLength(80)});row.ColumnDefinitions.Add(new());
        var description=Stack(Text("Descrizione dell’ambiente",12,true),exposureDetails,Text("Esempi informativi",12,true),exposureExamples);
        description.Margin=new Thickness(12,0,0,0);
        row.Children.Add(exposureSelector);Grid.SetColumn(description,1);row.Children.Add(description);
        exposureSelector.SelectionChanged+=(_,_)=>
        {
            if(updatingExposure)return;
            updatingExposure=true;
            try{foreach(var (code,check) in exposureChecks)check.IsChecked=code==exposureSelector.SelectedItem?.ToString();}
            finally{updatingExposure=false;}
            RefreshDetails();
        };
        return Paper(Stack(Text("Classe di esposizione",15,true),row,exposureAggressiveness,combinedExposure,
            Text("Descrizioni ed esempi di ANTHEA secondo la classificazione di UNI EN 206, prospetto 1.\nAggressività: NTC 2018, tab. 4.1.III.",11)));
    }
    void RefreshExposureSelector(MaterialExposureChoice[] active)
    {
        updatingExposure=true;
        try
        {
            string? selected=exposureSelector.SelectedItem?.ToString();
            var texts=MaterialExposurePresentation.Texts(selected,active,legacyExposures);
            if(!active.Any(e=>e.Code==selected))exposureSelector.SelectedItem=texts.Shown;
            exposureDetails.Text=texts.Details;
            exposureExamples.Text=texts.Examples;
            exposureAggressiveness.Text=texts.Aggressiveness;
            combinedExposure.Text=texts.Combined;
            combinedExposure.Visibility=texts.CombinedVisible?Visibility.Visible:Visibility.Collapsed;
        }
        finally{updatingExposure=false;}
    }
}

