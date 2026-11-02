using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Anthea.Application.Actions;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.Climate;

public sealed class ActionRowViewModel(JsonObject data):ObservableObject
{
    internal JsonObject Data{get;}=data;
    public string Name=>Data["Name"]?.ToString()??(Data["PositionM"] is { } x?"x = "+ActionFieldPresentation.Text(x)+" m · p = "+ActionFieldPresentation.Text(Data["PressureKpa"])+" kPa"
        :Data["ElevationM"] is { } z?"z = "+ActionFieldPresentation.Text(z)+" m · v = "+ActionFieldPresentation.Text(Data["VelocityMs"])+" m/s":"Riga");
    internal void Refresh()=>OnPropertyChanged(nameof(Name));
}
public sealed class ActionTableViewModel:ObservableObject
{
    private readonly string module;
    private readonly ActionTableDescriptor table;
    private readonly Action changed;
    private readonly Func<bool> editable;
    private readonly IWikiNavigator wiki;
    private ActionRowViewModel? selected;
    private FormViewModel? editor;
    public ActionTableViewModel(string module,ActionTableDescriptor table,Action changed,Func<bool> editable,CommandGuard guard,IWikiNavigator wiki)
    {
        this.module=module;this.table=table;this.changed=changed;this.editable=editable;this.wiki=wiki;
        Rows=new(table.Rows.Select(r=>new ActionRowViewModel(r!.AsObject())));
        AddCommand=GuardedCommands.Create(guard,Add,editable);RemoveCommand=GuardedCommands.Create(guard,Remove,()=>editable()&&Selected is not null);
        Selected=Rows.FirstOrDefault();
    }
    public string Title=>table.Title;
    public string Key=>table.Key;
    public ObservableCollection<ActionRowViewModel> Rows{get;}
    public ActionRowViewModel? Selected
    {
        get=>selected;
        set
        {
            if(!SetProperty(ref selected,value))return;
            BuildEditor();
            RemoveCommand.NotifyCanExecuteChanged();
        }
    }
    /// <param name="focusKey">Field whose editor takes the focus again in the rebuilt editor (the choice that rebuilt it).</param>
    private void BuildEditor(string? focusKey=null)
    {
            var value=selected;
            if(value is null)editor=null;
            else
            {
                var form=ClimateDocument.RowForm(module,table.Key,value.Data,table.Context);
                var fields=form.Fields.Select(f=>ActionFieldPresentation.Spec(f,module)).ToArray();
                editor=new(ActionFieldPresentation.Localize(form.Values,form.Fields),fields,key=>
                {
                    ClimateDocument.ApplyRowField(value.Data,form,key);value.Refresh();changed();
                    var field=form.Fields.First(f=>f.Key==key);
                    if(field.Boolean||field.Choices is not null)BuildEditor(key);
                },compact:true,validator:new ActionFieldPresentation.NumberValidator(form.Fields)){IsReadOnly=!editable(),WikiNavigator=wiki,PendingFocusKey=focusKey};
            }
            OnPropertyChanged(nameof(Editor));
    }
    public FormViewModel? Editor=>editor;
    public IRelayCommand AddCommand{get;}
    public IRelayCommand RemoveCommand{get;}
    private void Add(){if(!editable())return;var row=ClimateDocument.NewTableRow(table.Key,table.Rows);table.Rows.Add(row);var vm=new ActionRowViewModel(row);Rows.Add(vm);Selected=vm;changed();}
    private void Remove(){if(!editable()||Selected is null)return;int i=Rows.IndexOf(Selected);table.Rows.RemoveAt(i);Rows.RemoveAt(i);Selected=Rows.ElementAtOrDefault(Math.Min(i,Rows.Count-1));changed();}
    internal void Commit()=>Editor?.Commit();
    internal void RefreshAccess(){if(Editor is not null)Editor.IsReadOnly=!editable();AddCommand.NotifyCanExecuteChanged();RemoveCommand.NotifyCanExecuteChanged();}
}
