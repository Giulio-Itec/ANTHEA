using System.Windows;
using System.Windows.Controls;

namespace X.Desktop;

internal sealed partial class BridgeDesignWorkspace
{
    private FrameworkElement BuildOptimizationPanel()
    {
        optimizationObjective = Ui.Choice(["Costo minimo", "CO₂ minima", "Compromesso costo / CO₂ (50 / 50)"], "Costo minimo");
        var setup = Ui.Stack(Ui.Text("1 · Imposta la ricerca", 17, true), Ui.Text("Obiettivo", 12, true), optimizationObjective,
            Ui.Text("Mantieni costante", 13, true));
        foreach (var (key, label, value) in new[] {
            ("family", "Tipologia", false), ("spans", "Numero campate", false), ("depth", "Altezza in campata", false),
            ("section", "Dimensioni della sezione", false), ("continuous", "Continuità", true),
            ("pier", "Schema pila e quote imposte", false), ("foundation", "Fondazione e lunghezza pali", false) })
        {
            var check = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(4, 5, 4, 5) };
            optimizationLocks[key] = check; setup.Children.Add(check);
            check.Checked += (_, _) => { if (key == "section") optimizationLocks["family"].IsChecked = true; UpdateRangeAvailability(); ClearOptimizationChoice(); };
            check.Unchecked += (_, _) => { if (key == "family") optimizationLocks["section"].IsChecked = false; UpdateRangeAvailability(); ClearOptimizationChoice(); };
        }
        setup.Children.Add(Ui.Text("Intervalli · minimo / massimo", 13, true));
        setup.Children.Add(Ui.Bar(Ui.Text("Campate [n.]", 12), optimizationMin, optimizationMax));
        setup.Children.Add(Ui.Button("Suggerisci campate dalla lunghezza", SuggestOptimizationSpans, inspection: true));
        setup.Children.Add(Ui.Bar(Ui.Text("Altezza [m]", 12), optimizationMinDepth, optimizationDepth));
        setup.Children.Add(Ui.Text("0 = nessun limite. Minimo in campata; massimo anche sulle pile.", 11, color: Ui.Muted));
        setup.Children.Add(Ui.Text("Griglie · minimo / massimo / passo [%]", 13, true));
        setup.Children.Add(Ui.Text("Altezza rispetto al predimensionamento", 12)); setup.Children.Add(Ui.Bar(depthFrom, depthTo, depthStep));
        setup.Children.Add(Ui.Text("Lunghezza pali rispetto alla classe di terreno", 12)); setup.Children.Add(Ui.Bar(pileFrom, pileTo, pileStep));
        setup.Children.Add(Ui.Text("100–200%; massimo 11 valori per griglia. Spessori e interassi liberi seguono lo standard della tipologia.", 11, color: Ui.Muted));
        optimizationRun = Ui.Button("Avvia ottimizzazione", async () => await RunOptimization(), true);
        setup.Children.Add(Ui.Bar(optimizationRun, Ui.Button("Interrompi", () => optimizationCancellation?.Cancel(), inspection: true)));
        setup.Children.Add(new Expander { Header = "Costanti e significato dei blocchi", Content = Ui.Text(
            "Sito, lunghezza, larghezza, materiali, carichi e prezzi restano quelli del progetto. Altezza e sezione bloccate usano le quote adottate. Per pile e fondazioni, le quote imposte restano fisse; quelle a 0 restano automatiche. La configurazione corrente viene sempre confrontata, anche se esterna alle griglie percentuali, purché soddisfi i filtri.", 12) });

        optimizationApply = Ui.Button("Applica soluzione selezionata", () => ApplyOptimization(selectedSolution), true);
        optimizationApply.IsEnabled = false;
        var preview = Ui.Stack(optimizationBest, optimizationProgress, optimizationStatus, optimizationSelection,
            Ui.Bar(Ui.Button("Prospetto", () => { OptimizationPreview.Section = false; OptimizationPreview.InvalidateVisual(); }, inspection: true),
                Ui.Button("Sezione", () => { OptimizationPreview.Section = true; OptimizationPreview.InvalidateVisual(); }, inspection: true), optimizationApply),
            OptimizationPreview, optimizationChanges);
        var upperGrid = new Grid(); upperGrid.ColumnDefinitions.Add(new() { Width = new GridLength(335) }); upperGrid.ColumnDefinitions.Add(new());
        upperGrid.RowDefinitions.Add(new() { Height = GridLength.Auto }); upperGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var setupCard = Ui.Paper(setup); var previewCard = Ui.Paper(preview); upperGrid.Children.Add(setupCard); upperGrid.Children.Add(previewCard);

        var variable = Ui.Choice(["Costo [M€]", "CO₂ [t]", "Altezza in campata [m]", "Numero campate", "Lunghezza pali [m]", "Tipologia", "Schema pila", "Fondazione", "Continuità"], "Altezza in campata [m]");
        variable.SelectionChanged += (_, _) => { OptimizationTrace.Variable = variable.SelectedIndex; OptimizationTrace.InvalidateVisual(); }; OptimizationTrace.Variable = 2;
        var traceCard = Ui.Paper(Ui.Stack(Ui.Text("2 · Variabili esplorate", 17, true), variable, OptimizationTrace,
            Ui.Text("Ogni punto è un tentativo calcolato. Grigio = escluso. Nei grafici costo e CO₂ la linea scura è il minimo progressivo. Le quote non calcolabili sono omesse.", 11, color: Ui.Muted)));
        var filter = Ui.Choice(new[] { "Tutte le tipologie" }.Concat(BridgeConcept.Families.Select(f => f.Name)), "Tutte le tipologie");
        var pareto = new CheckBox { Content = "Solo frontiera Pareto", Margin = new Thickness(5) };
        void Filter() { OptimizationCloud.FamilyFilter = filter.SelectedIndex <= 0 ? null : BridgeConcept.Families[filter.SelectedIndex - 1].Id; OptimizationCloud.ParetoOnly = pareto.IsChecked == true; OptimizationCloud.InvalidateVisual(); }
        filter.SelectionChanged += (_, _) => Filter(); pareto.Checked += (_, _) => Filter(); pareto.Unchecked += (_, _) => Filter();
        OptimizationCloud.Selected += rank => { SelectOptimization(rank - 1); optimizationSelection.BringIntoView(); };
        var cloudCard = Ui.Paper(Ui.Stack(Ui.Text("3 · Famiglia di soluzioni plausibili", 17, true), Ui.Bar(filter, pareto), OptimizationCloud,
            Ui.Text("In basso a sinistra: meno costo e CO₂. Anello = Pareto; stella = ottimo; croce = progetto corrente. Clicca un punto per esaminarlo, anche fuori dalle prime N.", 11, color: Ui.Muted)));
        var legend = new WrapPanel();
        foreach (var family in BridgeConcept.Families) legend.Children.Add(Ui.Text("● " + family.Name + "   ", 11, color: BridgeOptimizationPlot.FamilyBrush(family.Id)));
        var chartGrid = new Grid(); chartGrid.ColumnDefinitions.Add(new()); chartGrid.ColumnDefinitions.Add(new());
        chartGrid.RowDefinitions.Add(new() { Height = GridLength.Auto }); chartGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        chartGrid.Children.Add(traceCard); chartGrid.Children.Add(cloudCard);
        var stack = Ui.Stack(Ui.Text("OTTIMIZZAZIONE · BRIDGE DESIGN", 24, true),
            Ui.Text("La preview segue il migliore provvisorio, al massimo due volte al secondo. Il progetto cambia solo con Applica.", 12, color: Ui.Muted),
            upperGrid, chartGrid, legend, Ui.Text("4 · Alternative ordinate per l’obiettivo scelto", 17, true),
            Ui.Bar(Ui.Text("Mostra le prime N", 12, true), OptimizationTop, Ui.Button("Aggiorna elenco", RefreshOptimizationRanking, inspection: true)),
            optimizationResults, Ui.Text("Pareto: nessun’altra soluzione esplorata migliora costo o CO₂ senza peggiorare l’altro indicatore. Compromesso: 0,5 C/Cmin + 0,5 CO₂/CO₂min.", 11, color: Ui.Muted),
            Ui.Text(BridgeConcept.OptimizationScope + " Il motore non è equivalente al sito di riferimento; la graduatoria è interna al modello ANTHEA.", 12, true));
        stack.Margin = new Thickness(20, 16, 20, 20); OptimizationScroll = Scroll(stack);
        void Layout()
        {
            bool compact = ActualWidth < 1120;
            upperGrid.ColumnDefinitions[0].Width = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(335);
            upperGrid.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(previewCard, compact ? 0 : 1); Grid.SetRow(previewCard, compact ? 1 : 0);
            setupCard.Margin = new Thickness(0, 0, compact ? 0 : 12, 12); previewCard.Margin = new Thickness(0, 0, 0, 12);
            chartGrid.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(cloudCard, compact ? 0 : 1); Grid.SetRow(cloudCard, compact ? 1 : 0);
            traceCard.Margin = new Thickness(0, 0, compact ? 0 : 12, 12); cloudCard.Margin = new Thickness(0, 0, 0, 12);
        }
        SizeChanged += (_, _) => Layout(); Layout();
        optimizationObjective.SelectionChanged += (_, _) => ClearOptimizationChoice();
        foreach (var edit in new[] { optimizationMin, optimizationMax, optimizationDepth, optimizationMinDepth, depthFrom, depthTo, depthStep, pileFrom, pileTo, pileStep })
            edit.TextChanged += (_, _) => ClearOptimizationChoice();
        OptimizationTop.LostKeyboardFocus += (_, _) => RefreshOptimizationRanking(); UpdateRangeAvailability(); return OptimizationScroll;
    }
}
