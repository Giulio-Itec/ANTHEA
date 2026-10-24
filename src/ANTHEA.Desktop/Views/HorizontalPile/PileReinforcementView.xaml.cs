using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.HorizontalPile.Elastic;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>
/// View of <see cref="PileReinforcementViewModel"/> (F5.8). The code-behind does only what decision Q3 allows and what is state of the view:
/// <list type="bullet">
/// <item>the section of the panel «Sezione e verifiche», the historic <see cref="SectionDrawing"/> (205 × 175) of the selected segment;</item>
/// <item>the dialogs of the view, modal as the historic ones: «NTC §7.2.5» (<see cref="ElasticNtcView"/>), «Info sezioni», «Proponi suddivisione…»
/// (<see cref="ElasticProposalView"/>), «Dividi…» (<see cref="ElasticSplitView"/>) and «Dimensiona…» (<see cref="PileSegmentDesignView"/>, closed
/// through <see cref="PileSegmentDesignViewModel.Close"/>).</item>
/// </list>
/// No calculation and no write of the document: the dialogs call the ViewModel.
/// </summary>
public partial class PileReinforcementView : UserControl
{
    /// <summary>The «Perimetro delle verifiche» of «Info sezioni», as the historic editor (NTC §7.2.5 and the scope of the checks).</summary>
    internal const string SectionsInfo = "NTC 2018 §7.2.5 · G.U. 20/02/2018, PDF 217, pagina stampata 213: per i pali As≥0,3% Ac, φst≥8 mm e s≤8φL per tutta la lunghezza. Se non si esclude il raggiungimento della capacità, il testo richiede zone dissipative (10D presso testa / 5D ai contatti profondi), As≥1%, staffe singole s≤6φL e condizioni ulteriori su duttilità e azioni. I controlli di testa sono attivabili nelle ipotesi Sisma e leggibili nel riepilogo. Le azioni sismiche devono essere assegnate e confermate; le zone profonde restano da valutare. Regole Pilastro: default modificabile, aggiuntivo ai minimi pali.\n\nN–M tramite il dominio resistente Checker; taglio tramite lo stesso motore del modulo c.a. Staffe chiuse a 90°, due bracci. La proposta dei tratti conserva inizialmente l'armatura principale; personalizzare i tratti e controllare gli esiti. Ancoraggi rettilinei a fyd e sovrapposizioni sono proposte da completare con disposizione, confinamento e requisiti esecutivi/sismici pertinenti. MRd tratteggiato = capacità nominale, senza certificare i dettagli costruttivi.";

    private PileReinforcementViewModel? editor;

    /// <summary>The section of the selected segment in «Sezione e verifiche».</summary>
    internal SectionDrawing SummarySection { get; } = new() { Width = 205, Height = 175 };

    /// <summary>Shows a window of the view (the dialogs): modal, as the historic ones; the checks show it off screen without waiting.</summary>
    internal Func<Window, bool?> ShowWindow { get; set; } = window => window.ShowDialog();

    public PileReinforcementView()
    {
        InitializeComponent();
        SummarySectionHost.Content = SummarySection;
        DataContextChanged += (_, _) => Attach(DataContext as PileReinforcementViewModel);
    }

    private void Attach(PileReinforcementViewModel? next)
    {
        if (ReferenceEquals(editor, next)) return;
        if (editor is not null) editor.PropertyChanged -= OnEditorChanged;
        editor = next;
        if (editor is null) return;
        editor.PropertyChanged += OnEditorChanged;
        DrawSection();
    }

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PileReinforcementViewModel.Summary)) DrawSection();
    }

    private void DrawSection()
    {
        if (editor?.Summary?.Segment?.Section is not { } s) return;
        SummarySection.Caption = s.Caption; SummarySection.CircularLinkRadius = s.LinkRadius; SummarySection.LinkDiameter = s.LinkDiameter; SummarySection.LinkCaption = s.LinkCaption;
        SummarySection.ShowAxes = s.ShowAxes; SummarySection.Outline = [.. s.Outline]; SummarySection.Bars = [.. s.Bars];
        SummarySection.InvalidateVisual();
    }

    /// <summary>«NTC §7.2.5» and «NTC §7.2.5 · finestra interna».</summary>
    private void Ntc_Click(object sender, RoutedEventArgs e) => ShowWindow(Ui.Dialog(this, ElasticNtcView.Title, new ElasticNtcView(), 1040, 820));

    /// <summary>«Info sezioni»: «Perimetro delle verifiche».</summary>
    private void SectionsInfo_Click(object sender, RoutedEventArgs e)
    {
        var text = new TextBlock { Text = SectionsInfo, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        text.SetResourceReference(TextBlock.FontSizeProperty, "Font.Subheading");
        text.SetResourceReference(TextBlock.ForegroundProperty, "Token.Ink");
        var page = new Border { Child = text };
        page.SetResourceReference(Border.BackgroundProperty, "Token.Surface");
        ShowWindow(Ui.Dialog(this, "Perimetro delle verifiche", page, 900, 520));
    }

    /// <summary>«Proponi suddivisione…»: the proposal in its dialog, «Applica e archivia i tratti precedenti»; without a result the message.</summary>
    private void Propose_Click(object sender, RoutedEventArgs e)
    {
        if (editor?.Propose() is not { } proposal) return;
        Window? window = null;
        var view = new ElasticProposalView(PileReinforcementViewModel.ProposalText(proposal), () => { if (editor.ApplyProposal(proposal)) window?.Close(); });
        window = Ui.Dialog(this, ElasticProposalView.Title, view, 700, 500);
        ShowWindow(window);
    }

    /// <summary>«Dividi…» of a card: the depth of the cut in its dialog.</summary>
    private void Split_Click(object sender, RoutedEventArgs e)
    {
        if (editor is null || sender is not FrameworkElement { DataContext: ElasticSegmentViewModel segment }) return;
        Window? window = null;
        var view = new ElasticSplitView();
        view.SplitRequested += text =>
        {
            if (editor.Split(segment.Index, text) is { } error) view.ShowError(error);
            else window?.Close();
        };
        window = Ui.Dialog(this, ElasticSplitView.Title, view, 420, 220);
        ShowWindow(window);
    }

    /// <summary>«Dimensiona…» of a card: the design of the segment in its dialog; closing it ends the search.</summary>
    private void Design_Click(object sender, RoutedEventArgs e)
    {
        if (editor is null || sender is not FrameworkElement { DataContext: ElasticSegmentViewModel segment } || editor.Design(segment.Index) is not { } design) return;
        var window = Ui.Dialog(this, PileSegmentDesignView.Title, new PileSegmentDesignView { DataContext = design }, 740, 650);
        window.Closed += (_, _) => design.Close();
        ShowWindow(window);
    }
}
