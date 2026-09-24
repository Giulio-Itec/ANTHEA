using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private readonly TextBlock quickResistanceText = Ui.Text("Inserire N per calcolare le quattro resistenze.", 11);
    private readonly Grid quickResistanceResults = new();
    private int quickResistanceGeneration;
    private InputForm? quickResistanceForm;
    private UIElement BuildQuickResistance()
    {
        if (settings["resistenze_rapide"] is not JsonObject)
            settings["resistenze_rapide"] = J.Obj(("N", "0"), ("tipo", "Plastico"));
        quickResistanceForm = new(settings["resistenze_rapide"]!.AsObject(),
            [new("N", "N (− compressione)", "kN"), new("tipo", "Resistenza", Choices: ["Plastico", "Elastico"])],
            key => { _ = RefreshQuickResistance(); Modified?.Invoke(); }, true, true);
        for(int i=0;i<2;i++)
        {
            quickResistanceResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
            quickResistanceResults.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        return Panel("Resistenze a N assegnato", Ui.Stack(quickResistanceForm, quickResistanceText, quickResistanceResults), "Mx± / My± · kNm · assi locali · iterativo");
    }
    private async Task RefreshQuickResistance()
    {
        if (initializing || disposed || quickResistanceForm is null) return;
        int generation = ++quickResistanceGeneration;
        quickResistanceResults.Children.Clear(); quickResistanceText.Visibility = Visibility.Visible;
        quickResistanceText.Text = "Calcolo delle quattro direzioni…";
        try
        {
            var o = settings["resistenze_rapide"]!;
            double n = SectionWorkspace.Number(o.S("N"), "N"); bool elastic = o.S("tipo") == "Elastico";
            var input = (JsonObject)Input.DeepClone(); var workspace = (JsonObject)settings.DeepClone();
            var result = await Task.Run(() => SectionMomentResistance.Calculate(input, workspace, n, elastic));
            if (disposed || generation != quickResistanceGeneration) return;
            quickResistanceText.Text = string.Join("\n", result.Select(r => $"{r.Direction}   {(r.Moment is double m ? EngineeringFormat.Number(m) + " kNm" : "— · " + r.Status)}"));
            quickResistanceText.Visibility = Visibility.Collapsed;
            for(int i=0;i<result.Length;i++)
            {
                var r=result[i];
                var card=Ui.Paper(Ui.Stack(Ui.Text(r.Direction,11,true),Ui.Text(r.Moment is double m?EngineeringFormat.Number(m)+" kNm":"— · "+r.Status,12)),6);
                card.Margin=new Thickness(i%2==0?0:4,4,i%2==0?4:0,0);
                Grid.SetColumn(card,i%2);Grid.SetRow(card,i/2);quickResistanceResults.Children.Add(card);
            }
        }
        catch (Exception ex) { if (!disposed && generation == quickResistanceGeneration) quickResistanceText.Text = "Resistenze non disponibili: " + ex.Message; }
    }
}
