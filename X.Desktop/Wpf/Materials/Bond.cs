using System.Windows;
using System.Windows.Controls;

namespace Materiali;
public sealed partial class MaterialView
{
    readonly TextBlock bondValue=Text("",22,true),bondDetails=Text("",12);
    UIElement BuildBond()
    {
        bondValue.TextAlignment=TextAlignment.Center;
        bondDetails.TextAlignment=TextAlignment.Center;
        var content=Stack(Text("Resistenza di aderenza",15,true),
            Select("bondCondition","Condizioni di aderenza"),
            Numeric("bondAlpha","Coefficiente di trazione · αct","—"),
            Numeric("bondGamma","Coefficiente parziale · γc","—"),bondValue,bondDetails,
            new Expander { Header="Formula e campo di applicazione",Content=Text("EN 1992-1-1:2004, §8.4.2, eq. (8.2): fbd = 2,25 η₁ η₂ fctd; fctd = αct fctk,0.05 / γc (§3.1.6). Diametro condiviso con il copriferro. η₂ = 1 per φ ≤32 mm, altrimenti (132−φ)/100. Per l'aderenza, fctk,0.05 è limitata al valore C60/75. Barre nervate, calcestruzzo normale, carichi prevalentemente statici. Le condizioni buone vanno accertate secondo figura 8.2; in altri casi η₁ = 0,7 (anche casseri scorrevoli salvo dimostrazione). αct = 1 e γc = 1,5 sono i valori iniziali per situazioni persistenti/transitorie; verificare annesso nazionale e situazione di progetto. Non è un calcolo della lunghezza di ancoraggio.",12) });
        return Paper(content);
    }
}
