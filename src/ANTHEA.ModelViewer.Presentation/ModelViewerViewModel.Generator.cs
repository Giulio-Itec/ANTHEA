using System.Collections.ObjectModel;
using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

/// <summary>A static load case in the panel of the generator: nature, category of a variable case (none until chosen), group and direction of a
/// seismic case, as indexes of the lists of the view model.</summary>
public sealed partial class GeneratorCaseEditor:ObservableObject
{
    readonly Action changed;
    internal GeneratorCaseEditor(ModelCaseAssignment assignment,Action changed)
    {
        Case=assignment.Case;nature=(int)assignment.Nature;
        category=assignment.CategoryIndex is {} c?c+1:0;group=assignment.Group??"";direction=assignment.DirectionIndex;this.changed=changed;
    }
    public string Case {get;}
    [ObservableProperty] int nature;
    [ObservableProperty] int category;
    [ObservableProperty] string group;
    [ObservableProperty] int direction;
    public bool IsVariable=>Nature==(int)ModelCaseNature.Variable;
    public bool IsSeismic=>Nature==(int)ModelCaseNature.Seismic;
    partial void OnNatureChanged(int value)
    {
        if(!IsSeismic)Direction=0;
        OnPropertyChanged(nameof(IsVariable));OnPropertyChanged(nameof(IsSeismic));changed();
    }
    partial void OnCategoryChanged(int value)=>changed();
    partial void OnGroupChanged(string value)=>changed();
    partial void OnDirectionChanged(int value)=>changed();
    internal ModelCaseAssignment Assignment=>ModelCaseAssignment.FromIndexes(Case,(ModelCaseNature)Nature,IsVariable&&Category>0?Category-1:null,
        string.IsNullOrWhiteSpace(Group)?null:Group.Trim(),IsSeismic?Direction:0);
}

/// <summary>
/// The panel of the generator of the combinations (decisions of the user of 1/11/2026, census D9 a §5.4): the static cases with nature, category,
/// group and seismic direction; the category of a variable case is always chosen by the user; the preview gives the rows before the generation,
/// or the limit (4096 rows, 12 groups) with the number and the remedy. The generation replaces the generated combinations, keeps those of the
/// user and the choices in the copy, and is written off the thread of the interface like a saved definition.
/// </summary>
public sealed partial class ModelViewerViewModel
{
    [ObservableProperty] bool showCombinationGenerator;
    [ObservableProperty] string generatorPreview="";
    bool generatorReady;
    public ObservableCollection<GeneratorCaseEditor> GeneratorCases {get;}=[];
    public IReadOnlyList<string> GeneratorNatureLabels {get;}=["Esclusa","Permanente G1","Permanente G2","Variabile","Eccezionale","Sismica"];
    /// <summary>«Da scegliere» then the categories of NTC 2018 Tab. 2.5.I in the order of VariableActionCategory.</summary>
    public IReadOnlyList<string> GeneratorCategoryLabels {get;}=["— da scegliere —","A · ambienti residenziali","B · uffici","C · ambienti suscettibili di affollamento",
        "D · ambienti a uso commerciale","E · aree per immagazzinamento, uso industriale","F · rimesse, veicoli ≤ 30 kN","G · rimesse, veicoli 30–160 kN",
        "H · coperture","Vento","Neve (fino a 1000 m s.l.m.)","Neve (oltre 1000 m s.l.m.)","Variazioni termiche"];
    public IReadOnlyList<string> GeneratorDirectionLabels {get;}=["— (±1)","X","Y","Z"];

    void NotifyGeneratorCommands()
    {
        OpenCombinationGeneratorCommand.NotifyCanExecuteChanged();GenerateCombinationsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute=nameof(CanStartCombination))] void OpenCombinationGenerator()
    {
        GeneratorCases.Clear();
        foreach(var choice in ModelCombinationGenerator.Choices(Snapshot!))GeneratorCases.Add(new GeneratorCaseEditor(choice,UpdateGeneratorPreview));
        ShowCombinationGenerator=true;UpdateGeneratorPreview();
    }
    [RelayCommand] void CloseCombinationGenerator()=>ShowCombinationGenerator=false;
    partial void OnShowCombinationGeneratorChanged(bool value)=>NotifyGeneratorCommands();

    void UpdateGeneratorPreview()
    {
        if(Snapshot==null){generatorReady=false;GeneratorPreview="";NotifyGeneratorCommands();return;}
        var (ready,_,message)=ModelCombinationGenerator.Preview(Snapshot,GeneratorCases.Select(c=>c.Assignment).ToArray());
        generatorReady=ready;GeneratorPreview=message;NotifyGeneratorCommands();
    }

    bool CanGenerateCombinations()=>generatorReady&&ShowCombinationGenerator&&CanStartCombination()&&!IsCombinationBusy;
    [RelayCommand(CanExecute=nameof(CanGenerateCombinations))] async Task GenerateCombinationsAsync()
    {
        var choices=GeneratorCases.Select(c=>c.Assignment).ToArray();int count=0;
        try
        {
            if(!await PublishCombinationChangeAsync(s=>
            {
                var generated=ModelCombinationGenerator.Generate(s,choices);count=generated.Length;
                return ModelCombinationGenerator.Apply(s,generated,choices);
            },"Generazione delle combinazioni NTC 2018…"))return;
            ShowCombinationGenerator=false;RefreshCombinations();
            CombinationMessage=$"Generate {count} combinazioni NTC 2018 (§2.5.3). Le combinazioni dell’utente e i loro risultati restano; le scelte dei casi sono salvate nel modello.";
        }
        catch(Exception ex){CombinationMessage="Generazione non completata: "+ex.Message;}
    }
}
