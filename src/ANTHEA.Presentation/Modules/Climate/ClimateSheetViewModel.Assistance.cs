using System.ComponentModel;
using Anthea.Application.Actions;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Modules.Climate;

public sealed partial class ClimateSheetViewModel
{
    private readonly List<FieldViewModel> assistanceFields=[];
    private readonly List<ActionTableViewModel> assistanceTables=[];
    private FieldViewModel? activeField;
    private ActionTableViewModel? activeTable;
    private bool synchronizingCase;
    private int linkedRow;

    public ActionSketch AssistanceSketch=>ActionAssistanceDocument.Sketch(module,Data,activeTable?.Key,ActiveRow);
    public string ActiveFieldKey=>activeField?.Key??activeTable?.Key??"";
    public int ActiveRow=>activeTable?.Selected is { } row?activeTable.Rows.IndexOf(row):-1;
    public int ActiveFaceIndex=>activeTable?.Key=="FireFaces"?ActiveRow:-1;
    public string ActiveComponentName=>activeTable?.Selected?.Name??"";
    public string ActiveTableKey=>activeTable?.Key??"";
    public string AssistanceTitle=>activeField?.Label??"Seleziona un dato o una riga per individuarlo nello schema";
    public string AssistanceValue=>activeField is null?"":(activeField.Kind switch
    { FieldKind.Choice=>activeField.SelectedChoice??"—",FieldKind.Bool=>activeField.IsChecked?"Sì":"No",_=>activeField.EditText })+(activeField.HasUnit?" "+activeField.Unit:"");
    public string AssistanceHelp=>activeField is null?"Seleziona un campo: lo schema evidenzia quote e componenti associate. Il pannello può essere richiuso."
        :AssistanceSketch.Parts.Any(p=>p.Keys.Split(' ').Contains(activeField.Key))?"Il contorno marcato individua la parte associata al campo. Il valore inserito è riportato qui; lo schema è qualitativo."
        :"Parametro generale del modello: non rappresenta una quota o un componente geometrico. Ipotesi e riferimenti sono nelle schede dedicate.";
    public string AssistanceContext=>"Dati: "+(activeTable is null?"parametri generali":activeTable.Title+" · "+(activeTable.Selected?.Name??"nessuna riga"))+
        "   |   Risultati: "+(SelectedCaseItem?.Name??"in attesa di un calcolo valido");
    public string CaseLinkNote=>IsCranes?"La posizione e il caso sono collegati. Cambiando posizione si mantiene il gruppo di azioni, se disponibile; altrimenti si mostra il primo gruppo della posizione.":ActionAssistanceDocument.CaseTable(module) is not null
        ?"La selezione dello scenario / dell’età e quella del caso sono collegate. Le altre righe descrivono componenti del caso."
        :"Il caso di risultato si sceglie nel selettore «Caso». Le righe di input descrivono componenti o posizioni e non identificano un unico caso.";

    private void DetachAssistance()
    {
        foreach(var field in assistanceFields)field.PropertyChanged-=AssistanceFieldChanged;
        foreach(var table in assistanceTables)table.PropertyChanged-=AssistanceTableChanged;
        assistanceFields.Clear();assistanceTables.Clear();
    }
    private void ObserveAssistance(string? tableKey=null,string? fieldKey=null)
    {
        tableKey??=activeTable?.Key;fieldKey??=activeField?.Key;
        DetachAssistance();
        activeTable=Tables.FirstOrDefault(t=>t.Key==tableKey);
        foreach(var table in Tables){table.PropertyChanged+=AssistanceTableChanged;assistanceTables.Add(table);}
        foreach(var field in Forms.SelectMany(p=>p.Form.Fields).Concat(Tables.Where(t=>t.Editor is not null).SelectMany(t=>t.Editor!.Fields)))
        {field.PropertyChanged+=AssistanceFieldChanged;assistanceFields.Add(field);}
        activeField=(activeTable?.Editor?.Fields??Forms.SelectMany(p=>p.Form.Fields)).FirstOrDefault(f=>f.Key==fieldKey);
        RefreshAssistance();
    }
    private void AssistanceFieldChanged(object? sender,PropertyChangedEventArgs e)
    {
        if(sender is not FieldViewModel field||IsDisposed)return;
        if(e.PropertyName==nameof(FieldViewModel.IsFocused)&&field.IsFocused)
        {
            activeField=field;activeTable=Tables.FirstOrDefault(t=>ReferenceEquals(t.Editor,field.Form));RefreshAssistance();
        }
        else if(ReferenceEquals(field,activeField)&&e.PropertyName is nameof(FieldViewModel.EditText) or nameof(FieldViewModel.SelectedChoice) or nameof(FieldViewModel.IsChecked))RefreshAssistance();
    }
    private void AssistanceTableChanged(object? sender,PropertyChangedEventArgs e)
    {
        if(rebuilding||IsDisposed||sender is not ActionTableViewModel table)return;
        if(e.PropertyName==nameof(ActionTableViewModel.Editor))ObserveAssistance();
        if(e.PropertyName!=nameof(ActionTableViewModel.Selected)||synchronizingCase)return;
        activeTable=table;activeField=null;
        if(table.Key==ActionAssistanceDocument.CaseTable(module)&&table.Selected is not null)
        {
            linkedRow=table.Rows.IndexOf(table.Selected);
            int next=ActionAssistanceDocument.CaseForRow(module,Result,linkedRow,SelectedCase);
            if(next>=0&&next<Cases.Count)SelectedCase=next;
        }
        RefreshAssistance();
    }
    private void SynchronizeCaseInput()
    {
        if(synchronizingCase)return;
        linkedRow=ActionAssistanceDocument.RowForCase(module,Result,SelectedCase);
        var table=Tables.FirstOrDefault(t=>t.Key==ActionAssistanceDocument.CaseTable(module));
        if(table is not null&&linkedRow>=0&&linkedRow<table.Rows.Count)
        {
            synchronizingCase=true;
            try{table.Selected=table.Rows[linkedRow];}
            finally{synchronizingCase=false;}
        }
        RefreshAssistance();
    }
    private void RefreshAssistance()
    {
        foreach(string p in new[]{nameof(AssistanceSketch),nameof(ActiveFieldKey),nameof(ActiveTableKey),nameof(ActiveRow),nameof(ActiveFaceIndex),nameof(ActiveComponentName),nameof(AssistanceTitle),nameof(AssistanceValue),nameof(AssistanceHelp),nameof(AssistanceContext),nameof(CaseLinkNote)})OnPropertyChanged(p);
    }
    protected override void OnDisposed(){DetachAssistance();base.OnDisposed();}
}
