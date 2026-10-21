using Anthea.Application.Concrete;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;
namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private readonly Dictionary<string,ConcreteResults.TorsionResult> torsionResults=new();
    private InputForm torsionForm=null!;
    private UIElement BuildTorsionOptions()
    {
        var o=ShearOptions;

        torsionForm=new InputForm(o,[new("cot_torsione","cot θ comune (T ≠ 0)"),new("as_torsione","As longitudinale disponibile per torsione","mm²"),new("chiusura_torsione","Staffe chiuse e barre sul profilo",Choices:["Da confermare","Confermato"]),new("modello_circolare","Taglio circolare",Choices:["Da scegliere","Pile · NTC §7.9.5.2","Parametri assegnati"]),new("z_d","z/d assegnato · circolare")],_=>{InvalidateActions("Taglio");Modified?.Invoke();},true,true);
        return Ui.Stack(torsionForm,Ui.Text("Torsione: As è la quota disponibile dopo pressoflessione. Staffe chiuse ortogonali all’asse; area di un ramo per torsione. Per Vx+Vy si adotta la somma conservativa dei contributi sul calcestruzzo, non una formula normativa di interazione biassiale. Modello delle pile: z/d = 0,75 piena, 0,60 cava.",11));
    }
    // The outputs of the torsion of a row and its text in the detail are in ANTHEA.Application (ConcreteShearPresentation, F5.10 S5).
}
