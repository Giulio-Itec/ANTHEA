using System.Windows;
using System.Windows.Controls;

namespace Materiali;

public sealed partial class MaterialView
{
    readonly TextBox minimumConcreteClass=new(){IsReadOnly=true,IsReadOnlyCaretVisible=false,Background=Brush("#EDF5FF"),FontWeight=FontWeights.SemiBold};
    readonly TextBlock minimumConcreteNote=Text("",12);
    void RefreshMinimumConcrete(Exposure[] active)
    {
        try
        {
            minimumConcreteClass.Text=MinimumConcrete.Label(MinimumConcrete.Required(active));
            minimumConcreteNote.Text="UNI 11104:2025, prospetto 6. Per più esposizioni si adotta la classe minima più elevata.";
            if(active.Any(e=>e.Code=="XF1"))minimumConcreteNote.Text+=" XF1: si adotta C32/40 dell'edizione 2016 (prospetto 5); il prospetto 6 della 2025 indica C30/37.";
            if(active.Any(e=>e.Code.StartsWith("XF")))minimumConcreteNote.Text+=" XF2/XF3/XF4: valori del prospetto con aria inglobata; per l’alternativa senza aria occorrono prove prestazionali (nota a). XF1: valore ordinario senza aria aggiunta; l’alternativa aerata della nota b richiede una specifica dedicata.";
        }
        catch(ArgumentException ex){minimumConcreteClass.Text="Da definire";minimumConcreteNote.Text=ex.Message;}
    }
}
